using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ViewApplication.Models;
using Newtonsoft.Json;
using ViewApplication.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.FileProviders;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using System.Net;

namespace ViewApplication.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IWebHostEnvironment _env;

        public HomeController(ILogger<HomeController> logger, IHttpClientFactory httpClientFactory, IWebHostEnvironment env)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _env = env;
        }

        public async Task <IActionResult> Index()
        {
            IEnumerable<CarsDTO> cars = null;
            var client = _httpClientFactory.CreateClient(Constants.ClientWithToken);
            client.BaseAddress = new Uri("http://localhost:5746");
            //HTTP GET
            //var responseTask = client.GetAsync("/api/Cars/GetAll");
            var responseTask = await client.GetAsync(Constants.Routes.GetCarsList);
            

            var result = responseTask;
            if (result.IsSuccessStatusCode)
            {
                var readTask = result.Content.ReadAsAsync<IList<CarsDTO>>();
                readTask.Wait();

                cars = readTask.Result;
            }
            else
            {
                cars = Enumerable.Empty<CarsDTO>();
                ModelState.AddModelError(string.Empty, "Server error. Please contact administrator.");
            }
            return View(cars);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
        public IActionResult PDFReader()
        {
            var provider = new PhysicalFileProvider(_env.ContentRootPath);
            var contents = provider.GetDirectoryContents(string.Empty);
            var filePath = Path.Combine("wwwroot", "Create a Web App and RESTful API Server Using the MEAN Stack _ Heroku Dev Center.pdf");
            var fileInfo = provider.GetFileInfo(filePath);

            //string ReportURL = Url.Content("~/Create a Web App and RESTful API Server Using the MEAN Stack _ Heroku Dev Center.pdf");
            byte[] FileBytes = System.IO.File.ReadAllBytes(filePath);
            return File(FileBytes, "application/pdf");

            //Response.Headers.Add("Content-Disposition", $"inline; filename=\"{file.Name}\"");
            //return File(file.CreateReadStream(), "application/pdf", file.Name);
            //return View();
        }

      
    }
}
