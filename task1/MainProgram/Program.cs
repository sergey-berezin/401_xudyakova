using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Globalization;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using NuGetYOLO;
using static NuGetYOLO.YOLO;

namespace MainProgram
{
    class Out
    {
        public string Filename { get; set; }
        public string Label { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float W { get; set; }
        public float H { get; set; }

        public Out(string filename, string label, float x, float y, float w, float h)
        {
            Filename = filename;
            Label = label;
            X = x;
            Y = y;
            W = w;
            H = h;
        }
    }
    class Program
    {
        static void WriteResults(string dir, string filename, List<DataTemplate> results)
        {
            string pathCsvFile = $"{dir}\\{filename}";
            
            var csvConfig = new CsvConfiguration(CultureInfo.CurrentCulture)
            {
                HasHeaderRecord = true,
                Delimiter = ";",
            };

            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
            Directory.CreateDirectory(dir);
            TextWriter streamWriter = new StreamWriter(pathCsvFile);
            CsvWriter csvWriter = new CsvWriter(streamWriter, csvConfig);
            List<Out> outList = new List<Out>();
            for (int i = 0; i < results.Count(); i++)
            {
                results[i].LabelledImage.Save($"{dir}\\" + $"{results[i].Filename.Split('\\').Last().Split('/').Last().Split('.').First()}_result.jpg");
                for (int j = 0; j < results[i].ObjectTemplates.Count(); j++)
                    outList.Add(new Out($"{results[i].Filename.Split('\\').Last().Split('/').Last()}", 
                        results[i].ObjectTemplates[j].Label, 
                        results[i].ObjectTemplates[j].X,
                        results[i].ObjectTemplates[j].Y,
                        results[i].ObjectTemplates[j].W,
                        results[i].ObjectTemplates[j].H));
            }
            csvWriter.WriteRecords(outList);
            streamWriter.Close();
        }
        static async Task Main(string[] args)
        {
            string dir = "results";
            string filename = "result.csv";
            string[] filenames = (string[])args.Clone();
            if (filenames.Length == 0)
                filenames = new string[2] { "..\\..\\..\\chair.jpg", "..\\..\\..\\cats.jpg" };
            List<Image<Rgb24>> images = new List<Image<Rgb24>>();
            foreach (string imagename in filenames) 
            {
                images.Add(Image.Load<Rgb24>(imagename));
            }
            List<DataTemplate> results;

            results = await RunAsync(images, filenames);
            
            WriteResults(dir, filename, results);            
        }
    }
}

