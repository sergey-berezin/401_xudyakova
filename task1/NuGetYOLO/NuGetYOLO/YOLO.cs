using System;
using SixLabors.ImageSharp; // Из одноимённого пакета NuGet
using SixLabors.ImageSharp.PixelFormats;
using System.Linq;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.OnnxRuntime;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Reflection.Emit;
using System.IO;
using System.Net;
using System.Net.Http;
using Polly;
namespace NuGetYOLO
{
    public class DataTemplate
    {
        public DataTemplate(string filename, string label, float x, float y, float w, float h)
        {
            Filename = filename;
            Label = label;
            X = x;
            Y = y;
            W = w;
            H = h;
        }
        public string Filename { get; set; }
        public string Label { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float W { get; set; }
        public float H { get; set; }

    }
    public static class YOLO
    {
        static SemaphoreSlim hasMessages = new SemaphoreSlim(0, 1);
        static SemaphoreSlim boxLock = new SemaphoreSlim(1, 1);
        static Queue<((Image<Rgb24>, string) Input, TaskCompletionSource<(Image<Rgb24>, DataTemplate)> Result)> mailbox = new();

        static CancellationTokenSource cts = new CancellationTokenSource();
        static async Task<(Image<Rgb24>, DataTemplate)> Enqueue(Image<Rgb24> m, string f)
        {
            await boxLock.WaitAsync();
            if (mailbox.Count == 0)
                hasMessages.Release();
            var r = new TaskCompletionSource<(Image<Rgb24>, DataTemplate)>();
            mailbox.Enqueue(((m, f), r));
            boxLock.Release();
            return await r.Task;
        }
        static async Task Process()
        {
            while (!cts.Token.IsCancellationRequested)
            {
                await DownloadNetwork();
                await hasMessages.WaitAsync();
                await boxLock.WaitAsync();
                var input = new Queue<((Image<Rgb24>, string) Input, TaskCompletionSource<(Image<Rgb24>, DataTemplate)> Result)>();
                while (mailbox.Count > 0)
                    input.Enqueue(mailbox.Dequeue());
                boxLock.Release();
                while (input.Count > 0)
                {
                    var item = input.Dequeue();
                    // Выполняем полезные вычисления
                    var result = Net_Predict(item.Input);
                    item.Result.SetResult(result);
                }
            }
        }

        public static async Task<(List<Image<Rgb24>>, List<DataTemplate>)> Run(List<Image<Rgb24>> images, string[] filenames)
        {
            (List<Image<Rgb24>>, List<DataTemplate>) results = (new List<Image<Rgb24>>(), new List<DataTemplate>());
            List<Task> tasks = new List<Task>();
            Queue<int> nums = new Queue<int>();
            for (var i = 0; i < images.Count(); i++)
            {
                nums.Enqueue(i);
                var task = Task.Run(async () =>
                {
                    int j = 0;
                    lock (nums)
                    {
                        j = nums.Dequeue();
                    }
                    (Image<Rgb24>, DataTemplate) result = await Enqueue(images[j], filenames[j]);
                    lock (results.Item1)
                    {
                        results.Item1.Add(result.Item1);
                    }
                    lock (results.Item2)
                    {
                        results.Item2.Add(result.Item2);
                    }

                });
                tasks.Add(task);
            }
            var processTask = Task.Run(Process);
            foreach (var task in tasks)
            {
                await Task.WhenAll(task);
            }
            cts.Cancel();
            await processTask;
            return results;
        }

        static async Task DownloadNetwork()
        {
            if (!System.IO.File.Exists("tinyyolov2-8.onnx"))
            {
                var jitterer = new Random();
                var retryPolicy = Policy
                    .Handle<HttpRequestException>()
                    .WaitAndRetryAsync(5,
                        retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))  // exponential back-off: 2, 4, 8 etc
                                      + TimeSpan.FromMilliseconds(jitterer.Next(0, 1000)));  // plus some jitter: up to 1 second
                using (var httpClient = new HttpClient())
                {
                    var buffer = await retryPolicy.ExecuteAsync(async () =>
                    {
                        return await httpClient.GetByteArrayAsync("https://storage.yandexcloud.net/dotnet4/tinyyolov2-8.onnx", cts.Token);
                    });
                    await File.WriteAllBytesAsync("tinyyolov2-8.onnx", buffer, cts.Token);
                }
            }
        }

        static (Image<Rgb24>, DataTemplate) Net_Predict((Image<Rgb24>, string) data)
        {
            var image = data.Item1;
            var filename = data.Item2;
            int imageWidth = image.Width;
            int imageHeight = image.Height;

            // Размер изображения
            const int TargetWidth = 416;
            const int TargetHeight = 416;

            // Изменяем размер изображения до 416 x 416
            var resized = image.Clone(x =>
            {
                x.Resize(new ResizeOptions
                {
                    Size = new Size(TargetWidth, TargetHeight),
                    Mode = ResizeMode.Pad // Дополнить изображение до указанного размера
                });
            });
            // Перевод пикселов в тензор и нормализация
            var input = new DenseTensor<float>(new[] { 1, 3, TargetHeight, TargetWidth });
            for (int y = 0; y < TargetHeight; y++)
            {
                Span<Rgb24> pixelSpan = resized.GetPixelRowSpan(y);
                for (int x = 0; x < TargetWidth; x++)
                {
                    input[0, 0, y, x] = pixelSpan[x].R;
                    input[0, 1, y, x] = pixelSpan[x].G;
                    input[0, 2, y, x] = pixelSpan[x].B;
                }
            }
            // Подготавливаем входные данные нейросети. Имя input задано в файле модели
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("image", input),
            };
            // Вычисляем предсказание нейросетью
            using var session = new InferenceSession("tinyyolov2-8.onnx");
            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = session.Run(inputs);
            // Получаем результаты
            var outputs = results.First().AsTensor<float>();
            const int CellCount = 13; // 13x13 ячеек
            const int BoxCount = 5; // 5 прямоугольников в каждой ячейке
            const int ClassCount = 20; // 20 классов

            string[] labels = new string[]
            {
                "aeroplane", "bicycle", "bird", "boat", "bottle",
                "bus", "car", "cat", "chair", "cow",
                "diningtable", "dog", "horse", "motorbike", "person",
                "pottedplant", "sheep", "sofa", "train", "tvmonitor"
            };

            float Sigmoid(float value)
            {
                var e = (float)Math.Exp(value);
                return e / (1.0f + e);
            }

            List<float> confs = new List<float>();
            int i = 0;
            var bestConf = Double.MinValue;
            int bestRow = -1, bestCol = -1, bestBbox = -1;
            for (var row = 0; row < CellCount; row++)
                for (var col = 0; col < CellCount; col++)
                    for (var bbox = 0; bbox < BoxCount; bbox++)
                    {
                        var conf = Sigmoid(outputs[0, (5 + ClassCount) * bbox + 4, row, col]);
                        confs.Add(conf);
                        var _classes =
                        Enumerable.Range(0, ClassCount)
                        .Select(i => outputs[0, (5 + ClassCount) * bbox + 5 + i, row, col])
                        .ToArray();
                        int _bestClass = 0;
                        for (var cls = 1; cls < ClassCount; cls++)
                            if (_classes[_bestClass] < _classes[cls])
                                _bestClass = cls;
                        if (conf > bestConf)
                        {
                            bestConf = conf;
                            bestRow = row;
                            bestCol = col;
                            bestBbox = bbox;
                        }
                    }

            var classes =
                Enumerable.Range(0, ClassCount)
                .Select(i => outputs[0, (5 + ClassCount) * bestBbox + 5 + i, bestRow, bestCol])
                .ToArray();

            int bestClass = 0;
            for (var cls = 1; cls < ClassCount; cls++)
                if (classes[bestClass] < classes[cls])
                    bestClass = cls;

            var outX = outputs[0, (5 + ClassCount) * bestBbox, bestRow, bestCol];
            var outY = outputs[0, (5 + ClassCount) * bestBbox + 1, bestRow, bestCol];
            var outW = outputs[0, (5 + ClassCount) * bestBbox + 2, bestRow, bestCol];
            var outH = outputs[0, (5 + ClassCount) * bestBbox + 3, bestRow, bestCol];

            double[] anchors = new double[]
            {
                1.08, 1.19, 3.42, 4.41, 6.63, 11.38, 9.42, 5.11, 16.62, 10.52
            };

            const int CellWidth = 32; // 416 / 13
            const int CellHeight = 32; // 416 / 13

            var X = ((float)bestCol + Sigmoid(outX)) * CellWidth;
            var Y = ((float)bestRow + Sigmoid(outY)) * CellHeight;
            var width = (float)(Math.Exp(outW) * CellWidth * anchors[bestBbox * 2]);
            var height = (float)(Math.Exp(outH) * CellHeight * anchors[bestBbox * 2 + 1]);
            X -= width / 2;
            Y -= height / 2;


            resized.Mutate(
                ctx => ctx.DrawPolygon(
                    Pens.Dash(Color.Red, 2),
                    new PointF[] {
                        new PointF(X, Y),
                        new PointF(X + width, Y),
                        new PointF(X + width, Y + height),
                        new PointF(X, Y + height)
                    }));
            return (resized, new DataTemplate($"{filename.Split('\\').Last().Split('/').Last().Split('.').First()}_result.jpg", labels[bestClass], X, Y, width, height));
        }
    }
}