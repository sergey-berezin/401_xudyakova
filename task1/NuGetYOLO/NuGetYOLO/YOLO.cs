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
using System.Reflection;
using System.Runtime.InteropServices;

namespace NuGetYOLO
{
    public class ObjectTemplate
    {
        public ObjectTemplate(string label, int labelNum, float conf, float x, float y, float w, float h)
        {
            Label = label;
            LabelNum = labelNum;
            Conf = conf;
            X = x;
            Y = y;
            W = w;
            H = h;
        }
        public string Label { get; set; }
        public int LabelNum { get; set; }
        public float Conf { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float W { get; set; }
        public float H { get; set; }
        public double IoU(ObjectTemplate obj2)
        {
            if ((X + W < obj2.X) || (obj2.X + obj2.W < X) || (Y + H < obj2.Y) || (obj2.Y + obj2.H < Y))
                return 0.0;
            double intersection = (Math.Min(X + W, obj2.X + obj2.W) - Math.Max(X, obj2.X)) * (Math.Min(Y + H, obj2.Y + obj2.H) - Math.Max(Y, obj2.Y));
            double union = (Math.Max(X + W, obj2.X + obj2.W) - Math.Min(X, obj2.X)) * (Math.Max(Y + H, obj2.Y + obj2.H) - Math.Min(Y, obj2.Y));
            
            return intersection / union;
        }
    }
    public class DataTemplate
    {
        public string Filename { get; set; }
        public Image<Rgb24> LabelledImage { get; set; }
        public List<ObjectTemplate> ObjectTemplates { get; set; }
        public DataTemplate(string filename, Image<Rgb24> labelledImage, List<ObjectTemplate> objectTemplates)
        {
            Filename = filename;
            LabelledImage = labelledImage;
            ObjectTemplates = objectTemplates;
        }
    }
    public static class YOLO
    {
        static SemaphoreSlim hasMessages = new SemaphoreSlim(0, 1);
        static SemaphoreSlim boxLock = new SemaphoreSlim(1, 1);
        static Queue<((Image<Rgb24>, string) Input, TaskCompletionSource<DataTemplate> Result)> mailbox = new Queue<((Image<Rgb24>, string) Input, TaskCompletionSource<DataTemplate> Result)>();
        static async Task<DataTemplate> EnqueueAsync(Image<Rgb24> m, string f)
        {
            await boxLock.WaitAsync();
            if (mailbox.Count == 0)
                hasMessages.Release();
            var r = new TaskCompletionSource<DataTemplate>();
            mailbox.Enqueue(((m, f), r));
            boxLock.Release();
            return await r.Task;
        }
        static async Task ProcessAsync(CancellationTokenSource cts)
        {
            while (!cts.Token.IsCancellationRequested)
            {
                await DownloadNetworkAsync(cts);
                await hasMessages.WaitAsync();
                await boxLock.WaitAsync();
                var input = new Queue<((Image<Rgb24>, string) Input, TaskCompletionSource<DataTemplate> Result)>();
                while (mailbox.Count > 0)
                    input.Enqueue(mailbox.Dequeue());
                boxLock.Release();
                while (input.Count > 0 && !cts.IsCancellationRequested)
                {
                    var item = input.Dequeue();
                    // Выполняем полезные вычисления
                    using var session = new InferenceSession("tinyyolov2-8.onnx");
                        var result = Net_Predict(item.Input, session);
                    item.Result.SetResult(result);
                }
            }
        }

        const int CellCount = 13; // 13x13 ячеек
        const int BoxCount = 5; // 5 прямоугольников в каждой ячейке
        const int ClassCount = 20; // 20 классов
        const int CellWidth = 32; // 416 / 13
        const int CellHeight = 32; // 416 / 13

        // Размер изображения
        const int TargetWidth = 416;
        const int TargetHeight = 416;
        const double treshholdConf = 0.3;

        static string[] labels = new string[]
        {
                "aeroplane", "bicycle", "bird", "boat", "bottle",
                "bus", "car", "cat", "chair", "cow",
                "diningtable", "dog", "horse", "motorbike", "person",
                "pottedplant", "sheep", "sofa", "train", "tvmonitor"
        };

        public static Color[] colors = new Color[]
        {
                Color.AliceBlue, Color.AntiqueWhite, Color.Aqua, Color.Aquamarine, Color.Azure, Color.Beige,
                Color.Bisque, Color.Black, Color.BlanchedAlmond, Color.Blue, Color.BlueViolet, Color.Brown, Color.BurlyWood,
                Color.CadetBlue, Color.Chartreuse, Color.Chocolate, Color.Coral, Color.CornflowerBlue, Color.Cornsilk,
                Color.Crimson, Color.Cyan, Color.DarkBlue, Color.DarkCyan, Color.DarkGoldenrod, Color.DarkGray, Color.DarkGreen,
                Color.DarkKhaki, Color.DarkMagenta, Color.DarkOliveGreen, Color.DarkOrange, Color.DarkOrchid, Color.DarkRed
        };

        static float Sigmoid(float value)
        {
            var e = (float)Math.Exp(value);
            return e / (1.0f + e);
        }


        public static async Task<List<DataTemplate>> RunAsync(List<Image<Rgb24>> images, string[] filenames, CancellationTokenSource? cts = null)
        {
            if (cts == null) 
                cts = new CancellationTokenSource();
            List<DataTemplate> results = new List<DataTemplate>();
            List<Task> tasks = new List<Task>();
            Queue<int> nums = new Queue<int>();
            for (var i = 0; i < images.Count(); i++)
            {
                nums.Enqueue(i);
                var task = Task.Run(async () =>
                {
                    int j = 0;
                    lock (nums)
                        j = nums.Dequeue();
                    DataTemplate result = await EnqueueAsync(images[j], filenames[j]);
                    lock (results)
                        results.Add(result);
                });
                tasks.Add(task);
            }
            var processTask = Task.Run(async() => { return ProcessAsync(cts); });
            foreach (var task in tasks)
            {
                if (cts.IsCancellationRequested)
                    return new List<DataTemplate>();
                await Task.WhenAll(task);
            }
            cts.Cancel();
            await processTask;
            return results;
        }

        static async Task DownloadNetworkAsync(CancellationTokenSource cts)
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
                        return await httpClient.GetByteArrayAsync("https://storage.yandexcloud.net/dotnet4/tinyyolov2-8.onnx");
                    });
                    await File.WriteAllBytesAsync("tinyyolov2-8.onnx", buffer, cts.Token);
                }
            }
        }

        static DataTemplate Net_Predict((Image<Rgb24>, string) data, InferenceSession session)
        {
            var image = data.Item1;
            var filename = data.Item2;
            int imageWidth = image.Width;
            int imageHeight = image.Height;

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
            IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results;
            lock (session)
                results = session.Run(inputs);

            // Получаем результаты
            var outputs = results.First().AsTensor<float>();

            List<ObjectTemplate> objectTemplates = new List<ObjectTemplate>();
            for (var row = 0; row < CellCount; row++)
                for (var col = 0; col < CellCount; col++)
                    for (var bbox = 0; bbox < BoxCount; bbox++)
                    {
                        var conf = Sigmoid(outputs[0, (5 + ClassCount) * bbox + 4, row, col]);
                        if (conf >= treshholdConf)
                        {
                            var classes =
                                Enumerable.Range(0, ClassCount)
                                .Select(i => outputs[0, (5 + ClassCount) * bbox + 5 + i, row, col])
                                .ToArray();

                            int bestClass = 0;
                            for (var cls = 1; cls < ClassCount; cls++)
                                if (classes[bestClass] < classes[cls])
                                    bestClass = cls;

                            var outX = outputs[0, (5 + ClassCount) * bbox, row, col];
                            var outY = outputs[0, (5 + ClassCount) * bbox + 1, row, col];
                            var outW = outputs[0, (5 + ClassCount) * bbox + 2, row, col];
                            var outH = outputs[0, (5 + ClassCount) * bbox + 3, row, col];

                            double[] anchors = new double[]
                            {
                                1.08, 1.19, 3.42, 4.41, 6.63, 11.38, 9.42, 5.11, 16.62, 10.52
                            };

                            var X = ((float)col + Sigmoid(outX)) * CellWidth;
                            var Y = ((float)row + Sigmoid(outY)) * CellHeight;
                            var width = (float)(Math.Exp(outW) * CellWidth * anchors[bbox * 2]);
                            var height = (float)(Math.Exp(outH) * CellHeight * anchors[bbox * 2 + 1]);
                            X -= width / 2;
                            Y -= height / 2;

                            objectTemplates.Add(new ObjectTemplate(labels[bestClass], bestClass, conf, X, Y, width, height));
                        }
                    }
            for (int i = 0; i < objectTemplates.Count; i++)
            {
                var obj1 = objectTemplates[i];
                int j = i + 1;
                while (j < objectTemplates.Count)
                {
                    var obj2 = objectTemplates[j];
                    if ((obj1.Label == obj2.Label) && (obj1.IoU(obj2) > 0.5))
                    {
                        if (obj1.Conf < obj2.Conf)
                        {
                            objectTemplates[i] = objectTemplates[j];
                            obj1 = objectTemplates[j];
                        }
                        objectTemplates.RemoveAt(j);
                    }
                    else
                        j++;
                }
            }

            foreach (var obj in objectTemplates)
                resized.Mutate(
                    ctx => ctx.DrawPolygon(
                        Pens.Dash(colors[obj.LabelNum], 2),
                        new PointF[] {
                            new PointF(obj.X, obj.Y),
                            new PointF(obj.X + obj.W, obj.Y),
                            new PointF(obj.X + obj.W, obj.Y + obj.H),
                            new PointF(obj.X, obj.Y + obj.H)
                        }));

            return new DataTemplate(filename, resized, objectTemplates);
        }
    }
}