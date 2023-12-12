using NuGetYOLO;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Task3.Models
{
    public static class DataProcessing
    {
        static string[] labels = new string[]
            {
                "aeroplane", "bicycle", "bird", "boat", "bottle",
                "bus", "car", "cat", "chair", "cow",
                "diningtable", "dog", "horse", "motorbike", "person",
                "pottedplant", "sheep", "sofa", "train", "tvmonitor"
            };
        public static List<(string, List<ObjectTemplate> , Image<Rgb24>)> GetAllData()
        {
            List<(string, List<ObjectTemplate>, Image<Rgb24>)> values = new();
            using (var db = new LibraryContext())
            {
                foreach (var image in db.Images)
                    values.Add((image.Filename,
                                image.Items.Select(obj => new ObjectTemplate(labels[obj.LabelNum], obj.LabelNum, obj.Conf, obj.X, obj.Y, obj.W, obj.H)).ToList(),
                                ConvertByteArrayToImage(image.Content, image.Width, image.Height)));
            }
            return values;
        }
        public static string[]? GetKnownImages(string[]? filenames)
        {
            if (filenames == null)
                return null;
            List<string> resultList = new List<string>();
            foreach (string filename in filenames)
            {
                bool b = !Contains(filename);
                if (b)
                    resultList.Add(filename);
            }
            if (resultList.Count == 0)
                return null;
            var result = new string[resultList.Count];
            for (int i = 0; i < result.Count(); i++)
                result[i] = resultList[i];
            return result;
        }
        public static bool Contains(string filename)
        {
            using (var db = new LibraryContext())
                return (db.Images.FirstOrDefault(x => x.Filename == filename) != null);
        }

        private static byte[] ConvertImageToByteArray(Image<Rgb24> image)
        {
            byte[] pixels = new byte[image.Width * image.Height * Unsafe.SizeOf<Rgb24>()];
            for (int y = 0; y < image.Height; y++)
            {
                Span<Rgb24> pixelRowSpan = image.GetPixelRowSpan(y);

                for (int x = 0; x < image.Width; x++)
                {
                    int offset = (y * image.Width + x) * 3;
                    Rgb24 pixel = pixelRowSpan[x];
                    pixels[offset] = pixel.R;
                    pixels[offset + 1] = pixel.G;
                    pixels[offset + 2] = pixel.B;
                }
            }
            return pixels;
        }
        private static Image<Rgb24> ConvertByteArrayToImage(byte[] pixels, int width, int height)
        {

            Image<Rgb24> image = new Image<Rgb24>(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int offset = (y * width + x) * 3;
                    Rgb24 pixel = new Rgb24(pixels[offset], pixels[offset + 1], pixels[offset + 2]);
                    image[x, y] = pixel;
                }
            }

            return image;
        }
        public static void AddData(DataTemplate result)
        {
            using (var db = new LibraryContext())
            {
                lock (db)
                    if (!Contains(result.Filename))
                    {
                        var pixels = ConvertImageToByteArray(result.LabelledImage);
                        var objects = result.ObjectTemplates;

                        var dbImage = new DbImage() { Filename = result.Filename, Content = pixels };
                        dbImage.Items = new List<DbItem>();
                        dbImage.Height = result.LabelledImage.Height;
                        dbImage.Width = result.LabelledImage.Width;
                        foreach (ObjectTemplate obj in objects)
                        {
                            var newObject = new DbItem() { LabelNum = obj.LabelNum, Conf = obj.Conf, X = obj.X, Y = obj.Y, W = obj.W, H = obj.H };
                            dbImage.Items.Add(newObject);
                            db.Add(newObject);
                        }
                        db.Add(dbImage);
                        db.SaveChanges();
                    }

            }
        }

        public static void EmptyDatabase()
        {
            using (var db = new LibraryContext())
            {
                if (db.Images != null)
                    db.Images.RemoveRange(db.Images);
                if (db.Items != null)
                    db.Items.RemoveRange(db.Items);
                db.SaveChanges();
            }
        }
    }
}
