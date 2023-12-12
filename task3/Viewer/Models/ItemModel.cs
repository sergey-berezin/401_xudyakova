using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SixLabors.ImageSharp.PixelFormats;

namespace Task3.Models
{
    public class ObjectType
    {
        public IBrush Color { get; set; }
        public string Name { get; set; }
        public ObjectType(SixLabors.ImageSharp.Color color, string name)
        {
            var converted = color.ToPixel<Argb32>();
            var systemColor = System.Drawing.Color.FromArgb((int)converted.Argb);
            var avaloniaColor = new Avalonia.Media.Color(systemColor.B, systemColor.G, systemColor.R, systemColor.A);
            Color = new SolidColorBrush(avaloniaColor);
            Name = name;
        }
    }
    public class ItemModel : IComparable<ItemModel>
    {
        public string Filename { get; set; }
        public List<ObjectType> ObjectTypeList { get; set; }
        public Bitmap Image { get; set; }
        public int Size { get; set; }

        public int CompareTo(ItemModel compareItemModel)
        {
            if (compareItemModel == null)
                return 1;
            if (ObjectTypeList.Count == compareItemModel.ObjectTypeList.Count)
                return Filename.CompareTo(compareItemModel.Filename);
            return ObjectTypeList.Count.CompareTo(compareItemModel.ObjectTypeList.Count);
        }

        public ItemModel(string filename, List<ObjectType> objectTypeList, Bitmap image, int size = 128)
        {
            Filename = filename.Split('\\').Last();
            ObjectTypeList = objectTypeList;
            Image = image;
            Size = size;
        }
    }
}
