using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Asn1;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using System.IO.Enumeration;
using NuGetYOLO;
using static NuGetYOLO.YOLO;

namespace MainProgram
{
    class Program
    {
        static void WriteResults(string dir, string filename, (List<Image<Rgb24>>, List<DataTemplate>) results)
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
            for (int i = 0; i < results.Item1.Count(); i++)
                results.Item1[i].Save($"{dir}\\{results.Item2[i].Filename}.jpg");
            csvWriter.WriteRecords(results.Item2);
            streamWriter.Close();
        }
        static async Task Main(string[] args)
        {
            string dir = "results";
            string filename = "result.csv";
            string[] filenames = (string[])args.Clone();
            if (filenames.Length == 0)
                filenames = new string[2] { "..\\..\\..\\chair.jpg", "..\\..\\..\\bird.jpg" };
            List<Image<Rgb24>> images = new List<Image<Rgb24>>();
            foreach (string imagename in filenames) 
            {
                images.Add(Image.Load<Rgb24>(imagename));
            }
            (List<Image<Rgb24>>, List<DataTemplate>) results;

            results = await RunAsync(images, filenames);
            
            WriteResults(dir, filename, results);            
        }
    }
}

