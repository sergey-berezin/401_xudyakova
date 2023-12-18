using Microsoft.AspNetCore.Mvc.Testing;
using System.Text;
using System.Threading.Tasks;
using System;
using Xunit;
using System.IO;
using System.Net.Http;
using Newtonsoft.Json;

namespace UnitTests
{
    public class ServerControllerTests : IClassFixture<WebApplicationFactory<WebApplication.Startup>>
    {
        private readonly WebApplicationFactory<WebApplication.Startup> factory;
        const string url = "http://localhost:5000/Home";
        const string filePath = "..\\..\\..\\..\\WebTest\\cats.jpg";
        public ServerControllerTests(WebApplicationFactory<WebApplication.Startup> factory)
        {
            this.factory = factory;
        }
        public StringContent toJson(string str)
        {
            var jsonString = System.Text.Json.JsonSerializer.Serialize(str);
            return new StringContent(jsonString, Encoding.UTF8, "application/json");
        }

        [Fact]
        public async Task OkTest()
        {
            var client = factory.CreateClient();
            string img = Convert.ToBase64String(File.ReadAllBytes(filePath));
            var response = await client.PostAsync(url, toJson(img));
            await response.Content.ReadAsStringAsync();
            Assert.Equal(200, (int)response.StatusCode);
        }

        [Fact]
        public async Task ErrorTest()
        {
            var client = factory.CreateClient();
            string img = "image";
            var response = await client.PostAsync(url, toJson(img));
            await response.Content.ReadAsStringAsync();
            Assert.Equal(101, (int)response.StatusCode);
        }

        [Fact]
        public async Task ResultTest()
        {
            var client = factory.CreateClient();
            string img = Convert.ToBase64String(File.ReadAllBytes(filePath));
            var response = await client.PostAsync(url, toJson(img));
            var answer = await response.Content.ReadAsStringAsync();
            WebApplication.Controllers.HomeController.ObjectFrameInfo[] list =
                JsonConvert.DeserializeObject<WebApplication.Controllers.HomeController.ObjectFrameInfo[]>(answer);

            Assert.Equal(200, (int)response.StatusCode);
            Assert.Equal(2, list.Length);
            Assert.Equal("cat", list[0].Label);
            Assert.Equal("cat", list[1].Label);
        }
    }
}