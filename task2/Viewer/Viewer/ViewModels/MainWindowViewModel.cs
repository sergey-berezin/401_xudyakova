using Avalonia.Media.Imaging;
using System.Collections.Generic;
using Viewer.Models;
using NuGetYOLO;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ReactiveUI;
using Avalonia;
using System.Xml.Linq;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using System.Collections;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Reactive;
using System.Windows.Input;

namespace Viewer.ViewModels
{
    public interface IFSServices
    {
        public Task<string[]?> Get_Load_Filenames();
    }
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private List<ItemModel>? items;

        [ObservableProperty]
        private Bitmap? currentImage = null;

        [ObservableProperty]
        private List<ObjectType>? currentObjectTypeList = null;

        private CancellationTokenSource? previous = null;
        public ReactiveCommand<Unit, Unit> LoadImages { get; }
        [ObservableProperty]
        private bool canLoad = true;
        public ReactiveCommand<Unit, Unit> CancelOperation { get; }
        [ObservableProperty]
        private bool canCancel = false;

        private readonly IFSServices fsServices;

        public MainWindowViewModel(IFSServices fsServices)
        {
            this.fsServices = fsServices;

            IObservable<bool> canExecuteLoad = this.WhenAnyValue(vm => vm.CanLoad);

            LoadImages = ReactiveCommand.Create(FileProcessing, canExecuteLoad);

            IObservable<bool> canExecuteCancel = this.WhenAnyValue(vm => vm.CanCancel);

            CancelOperation = ReactiveCommand.Create(CancelPrevious, canExecuteCancel);
        }

        private void CancelPrevious()
        {
            if (previous != null)
            {
                CanCancel = false;
                previous.Cancel();
                CanLoad = true;
            }
        }

        async public void FileProcessing()
        {
            string[]? filenames = await fsServices.Get_Load_Filenames();
            if (filenames == null)
                return;

            CanLoad = false;
            CancellationTokenSource cts = new CancellationTokenSource();
            previous = cts;
            CanCancel = true;
            List<Image<Rgb24>> images = new List<Image<Rgb24>>();
            foreach (string imagename in filenames)
                images.Add(Image.Load<Rgb24>(imagename));

            var results = await YOLO.RunAsync(images, filenames, cts);
            if (results.Count == 0)
                return;
            int i = 0;
            var itemsTemp = new List<ItemModel>();
            foreach (var result in results)
            {
                
                List<ObjectType> objectTypeList = new List<ObjectType>();
                var memoryStream = new System.IO.MemoryStream();
                result.LabelledImage.Save(memoryStream, new JpegEncoder());
                memoryStream.Position = 0;
                var LabelledImage = new Bitmap(memoryStream);
                memoryStream.Close();
                foreach (var obj in result.ObjectTemplates)
                    objectTypeList.Add(new ObjectType(YOLO.colors[obj.LabelNum], obj.Label));
                itemsTemp.Add(new ItemModel(result.Filename, objectTypeList, LabelledImage, i));
                i++;
            }
            itemsTemp.Sort();
            Items = itemsTemp;
            CurrentImage = Items[0].Image;
            CurrentObjectTypeList = Items[0].ObjectTypeList;
            CanCancel = false;
            CanLoad = true;
            

        }

        public void ChangeCurrent(ItemModel itemModel)
        {
            CurrentImage = itemModel.Image;
            CurrentObjectTypeList = itemModel.ObjectTypeList;
        }

    }
}