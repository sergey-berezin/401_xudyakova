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

namespace Viewer.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {

        [ObservableProperty]
        private List<ItemModel>? items;

        [ObservableProperty]
        private Bitmap? currentImage = null;

        [ObservableProperty]
        private List<ObjectType>? currentObjectTypeList = null;

        private CancellationTokenSource? previous = null;
        private CancellationTokenSource? previousYOLO = null;


        async public Task<bool> FileProcessing(string[] filenames)
        {
            List<Image<Rgb24>> images = new List<Image<Rgb24>>();
            foreach (string imagename in filenames)
                images.Add(Image.Load<Rgb24>(imagename));

            CancellationTokenSource ctsYOLO = new CancellationTokenSource();
            CancellationTokenSource cts = new CancellationTokenSource();
            if (previous == null)
            {
                previous = cts;
                previousYOLO = ctsYOLO;
            }
            else
                lock (previous)
                {
                    if (previous != null)
                    {
                        previous.Cancel();
                        previousYOLO.Cancel();
                    }
                        
                    previousYOLO = ctsYOLO;
                    previous = cts;
                }

            var results = await YOLO.RunAsync(images, filenames, ctsYOLO);

            if (cts.IsCancellationRequested)
                return false;

            lock (this)
            {
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
                lock(previous)
                {
                    if (cts.IsCancellationRequested)
                        return false;
                    Items = itemsTemp;
                    CurrentImage = Items[0].Image;
                    CurrentObjectTypeList = Items[0].ObjectTypeList;
                }
            }
            
            return true;
        }

        public void ChangeCurrent(ItemModel itemModel)
        {
            CurrentImage = itemModel.Image;
            CurrentObjectTypeList = itemModel.ObjectTypeList;
        }

    }
}