using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ViewApplication.Models;
using ViewApplication.Utils;

namespace ViewApplication.Controllers
{
    public class AccountController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        private readonly ILogger _logger;
        private IDistributedCache _cache;

        public AccountController(IHttpClientFactory httpClient, ILogger<AccountController> logger)
        {
            _httpClientFactory = httpClient;
            _logger = logger;
        }
        [AllowAnonymous]
        public IActionResult Login()
        {
            return View();
        }
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        public IActionResult Logoff()
        {
            if (User.Identity.IsAuthenticated)
            {
                Request.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Redirect(Constants.URL.LoginPath);
            }

            return Redirect(Constants.URL.LoginPath);
        }
        private async Task<ClaimsPrincipal> GetUserClaims(TokenModel tokenModel)
        {
            var httpClient = _httpClientFactory.CreateClient(Constants.ClientNoToken);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenModel.Token);

            var responseMessage = await httpClient.GetAsync(Constants.Routes.AccountGetClaims);

            responseMessage.EnsureSuccessStatusCode();

            var responseText = await responseMessage.Content.ReadAsStringAsync();

            var response = JsonConvert.DeserializeObject<ServiceResponse<List<Claim>>>(responseText, new ClaimConverter());

            var claims = response.Object;

            claims.Add(new Claim("access_token", tokenModel.Token));
            claims.Add(new Claim("refresh_token", tokenModel.RefreshToken));

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var principal = new ClaimsPrincipal(identity);

            return principal;
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginModel model, string location)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var httpClient = _httpClientFactory.CreateClient(Constants.ClientNoToken);

                    var url = Constants.Routes.Token;

                    var response = await httpClient.PostAsJsonAsync<LoginModel, TokenModel>(Constants.Routes.Token, model);

                    if (response != null && response.Object != null)
                    {
                        var principal = await GetUserClaims(response.Object);

                        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
                            new AuthenticationProperties
                            {
                                IsPersistent = true,
                                AllowRefresh = true,
                                ExpiresUtc = response.Object.Expires,
                                IssuedUtc = DateTime.UtcNow
                            });

                        return RedirectOrHome(location);
                    }
                    else
                    {
                        var desc = response.ShortDescription != null ? response.ShortDescription : "";
                        ModelState.AddModelError("", desc);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message);

                    ModelState.AddModelError("", "An error occured during login progress.");
                }
            }
            else
            {
                ModelState.AddModelError("", "Please check your inputs");
            }

            return View(model);
        }
        public IActionResult Index()
        {
            return View();
        }
        protected IActionResult RedirectOrHome(string location = null)
        {
            if (string.IsNullOrWhiteSpace(location) || !Url.IsLocalUrl(location))
                return Redirect(Constants.URL.Default);

            return Redirect(location);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {

            if (User.Identity.IsAuthenticated)
            {
                Request.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Redirect(Constants.URL.LoginPath);
            }

            return Redirect(Constants.URL.LoginPath);
        }
        public async Task<IActionResult> Profile()
        {
            var client = _httpClientFactory.CreateClient(Constants.ClientWithToken);
            var profile = await client.GetAsync<UserProfileModel>(Constants.Routes.AccountGetProfile);

            return View(profile.Object);
        }
        [HttpPost]
        public async Task<IActionResult> UpdateProfile(UserProfileModel model)
        {
            if (ModelState.IsValid)
            {
                var client = _httpClientFactory.CreateClient(Constants.ClientWithToken);
                var result = await client.PostAsJsonAsync<UserProfileModel, bool>(Constants.Routes.AccountUpdateProfile, model);

                if (result.IsValid && result.Object)
                {
                    model.StatusMessage = "Your profile has been updated";
                    return RedirectToAction(nameof(Profile));
                }
                else
                {
                    foreach (var item in result.ValidationErrors)
                    {
                        ModelState.AddModelError(item.Key, string.Join(",", item.Value));
                    }
                    
                    ModelState.AddModelError("", result.ShortDescription);
                }
            }
            return StatusCode(Microsoft.AspNetCore.Http.StatusCodes.Status304NotModified);
            // View(model);
        }
    }
}