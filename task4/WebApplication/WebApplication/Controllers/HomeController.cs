using Microsoft.AspNetCore.Mvc;
using NuGetYOLO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WebApplication.Controllers
{

    [ApiController]
    [Route("[controller]")]
    public class HomeController : Controller
    {
        public class ObjectFrameInfo
        {
            public string Label { get; set; }
            public float Conf { get; set; }
            public float X{ get; set; }
            public float Y{ get; set; }
            public float W{ get; set; }
            public float H{ get; set; }
            public ObjectFrameInfo()
            {

            }

            public ObjectFrameInfo(ObjectTemplate objectTemplate)
            {
                Label = objectTemplate.Label;
                Conf = objectTemplate.Conf;
                X = objectTemplate.X;
                Y = objectTemplate.Y;
                W = objectTemplate.W;
                H = objectTemplate.H;
            }
        }
        public static ObjectFrameInfo[] TempToFrameList(List<ObjectTemplate> list)
        {
            var result = new ObjectFrameInfo[list.Count];
            for (int i = 0; i < list.Count; i++)
                result[i] = new ObjectFrameInfo(list[i]);
            return result;
        }

        [HttpPost]
        public async Task<ActionResult<ObjectFrameInfo[]>> postImage([FromBody] string data)
        {
            try
            {
                CancellationTokenSource cts = new CancellationTokenSource();
                Image<Rgb24> img;
                byte[] image = Convert.FromBase64String(data);

                using (MemoryStream ms = new MemoryStream(image))
                {
                    img = Image.Load<Rgb24>(ms);
                }

                var imgs = new List<Image<Rgb24>> { img };

                var run_result = await YOLO.RunAsync(imgs, new string[] { "" }, cts);

                ObjectFrameInfo[] result;

                if (run_result.Count > 0)
                    result = TempToFrameList(run_result[0].ObjectTemplates);
                else
                    result = new ObjectFrameInfo[0];
                return Ok(result);
            }
            catch(Exception ex)
            {
                return StatusCode(101, ex.Message);
            }
        }
    }
}

