using Avalonia.Media.Imaging;
using System.Collections.Generic;
using Task3.Models;
using NuGetYOLO;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using ReactiveUI;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Reactive;
using static Task3.Models.DataProcessing;
using System.Linq;
using System.Collections.ObjectModel;
using System.Reactive.Linq;

namespace Task3.ViewModels
{
    public interface IFSServices
    {
        public Task<string[]?> Get_Load_Filenames();
    }
    public partial class MainWindowViewModel : ObservableObject
    {
        private ObservableCollection<ItemModel> items;
        public ObservableCollection<ItemModel> Items 
        {
            get { return items; }
            set { SetProperty(ref items, value); }
        }

        [ObservableProperty]
        private Bitmap? currentImage = null;

        [ObservableProperty]
        private List<ObjectType>? currentObjectTypeList;

        private CancellationTokenSource? previous = null;
        public ReactiveCommand<Unit, Unit> LoadImages { get; }
        [ObservableProperty]
        private bool canLoad = true;
        public ReactiveCommand<Unit, Unit> CancelOperation { get; }
        [ObservableProperty]
        private bool canCancel = false;

        public ReactiveCommand<Unit, Unit> EmptyCacheOperation { get; }
        [ObservableProperty]
        private bool canEmptyCache = true;

        private readonly IFSServices fsServices;

        public MainWindowViewModel(IFSServices fsServices)
        {
            this.fsServices = fsServices;

            IObservable<bool> canExecuteLoad = this.WhenAnyValue(vm => vm.CanLoad);

            LoadImages = ReactiveCommand.Create(FileProcessing, canExecuteLoad);

            IObservable<bool> canExecuteCancel = this.WhenAnyValue(vm => vm.CanCancel);

            CancelOperation = ReactiveCommand.Create(CancelPrevious, canExecuteCancel);

            IObservable<bool> canExecuteEmptyCache = this.WhenAnyValue(vm => vm.CanEmptyCache);

            EmptyCacheOperation = ReactiveCommand.Create(EmptyCache, canExecuteEmptyCache);

            Items = new ObservableCollection<ItemModel>{ };

            foreach(ItemModel item in GetAllData().Select(item => new ItemModel(item.Item1, TemplateToTypeList(item.Item2), ImageToBitmap(item.Item3))))
                Items.Add(item);
            SortItems();
        }
        private void SortItems()
        {
            var temp = Items.ToList();
            temp.Sort();
            Items = new ObservableCollection<ItemModel>();
            foreach (ItemModel item in temp)
                Items.Add(item);
        }
        private void EmptyCache()
        {
            EmptyDatabase();
            Items.Clear();
            CurrentImage = null;
            CurrentObjectTypeList = null;
        }
        private void CancelPrevious()
        {
            if (previous != null)
            {
                CanCancel = false;
                previous.Cancel();
                CanLoad = true;
                CanEmptyCache = true;
            }
        }

        private static Bitmap ImageToBitmap(Image<Rgb24> image)
        {
            var memoryStream = new System.IO.MemoryStream();
            image.Save(memoryStream, new PngEncoder());
            memoryStream.Position = 0;
            var result = new Bitmap(memoryStream);
            memoryStream.Close();
            return result;
        }

        private static List<ObjectType> TemplateToTypeList(List<ObjectTemplate> objects)
        {
            List<ObjectType> result = new List<ObjectType>();
            foreach (var obj in objects)
                result.Add(new ObjectType(YOLO.colors[obj.LabelNum], obj.Label));
            return result;
        }

        async public void FileProcessing()
        {
            CanLoad = false;
            CanEmptyCache = false;
            string[]? filenamesInitial = await fsServices.Get_Load_Filenames();
            var filenames = GetKnownImages(filenamesInitial);
            if (filenames == null)
            {
                CanLoad = true;
                CanEmptyCache = true;
                return;
            }

            CancellationTokenSource cts = new CancellationTokenSource();
            previous = cts;

            CanCancel = true;

            List<Image<Rgb24>> images = new List<Image<Rgb24>>();
            foreach (string imagename in filenames)
                images.Add(Image.Load<Rgb24>(imagename));

            var results = await YOLO.RunAsync(images, filenames, cts);
            foreach (var result in results)
            {
                Items.Add(new ItemModel(result.Filename, TemplateToTypeList(result.ObjectTemplates), ImageToBitmap(result.LabelledImage)));
                AddData(result);
            }
            SortItems();

            CanCancel = false;
            CanLoad = true;
            CanEmptyCache = true;
        }

        public void ChangeCurrent(ItemModel itemModel)
        {
            CurrentImage = itemModel.Image;
            CurrentObjectTypeList = itemModel.ObjectTypeList;
        }
    }
}