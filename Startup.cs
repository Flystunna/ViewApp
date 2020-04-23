using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using DataTables.AspNet.AspNetCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ViewApplication.Models;
using ViewApplication.Utils;
using System.Web;

namespace ViewApplication
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddTransient<TokenHttpHandler>();

            services.AddHttpClient(Constants.ClientNoToken, c => {

                c.BaseAddress = new Uri(Configuration[Constants.Keys.ApiBaseUrl]);
                c.DefaultRequestHeaders.Add("Accept", "application/json");

            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip
            });
            services.Configure<IISServerOptions>(options =>
            {
                options.AutomaticAuthentication = false;
            });
            services.AddHttpClient(Constants.ClientWithToken, c => {

                c.BaseAddress = new Uri(Configuration[Constants.Keys.ApiBaseUrl]);
                c.DefaultRequestHeaders.Add("Accept", "application/json");

            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip
            }).AddHttpMessageHandler<TokenHttpHandler>();

            services.AddMvc(Configuration => {
                var authorizationPolicy = new AuthorizationPolicyBuilder()
                                                         .RequireAuthenticatedUser()
                                                         .Build();

                Configuration.Filters.Add(new AuthorizeFilter(authorizationPolicy));
                //Configuration.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());

            }).SetCompatibilityVersion(CompatibilityVersion.Version_3_0);
            services.RegisterDataTables((ModelBindingContext modelBindingContext) => {
                var output = new Dictionary<string, object>();
                if (modelBindingContext.ValueProvider.GetValue("AdditionalParameters") != null)
                {

                    var additionalData = modelBindingContext.ValueProvider.GetValue("AdditionalParameters").FirstValue;

                    output["data"] = additionalData;
                }
                return output;
            }, false);

            services.AddSession();
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddAuthentication(x => {
                x.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                x.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
           .AddCookie(options => {
               options.LoginPath = new PathString(Constants.URL.LoginPath);
               options.ReturnUrlParameter = "location";
               options.AccessDeniedPath = new PathString(Constants.URL.AccessDeniedPath);

               options.SlidingExpiration = true;

               options.Events = new CookieAuthenticationEvents
               {
                   OnValidatePrincipal = async x => {
                       // since our cookie lifetime is based on the access token one,
                       // check if we're more than halfway of the cookie lifetime
                       var now = DateTimeOffset.UtcNow;
                       var timeElapsed = now.Subtract(x.Properties.IssuedUtc.Value);
                       var timeRemaining = x.Properties.ExpiresUtc.Value.Subtract(now);
                       if (timeElapsed > timeRemaining)
                       {
                           var identity = (ClaimsIdentity)x.Principal.Identity;
                           var accessTokenClaim = identity.FindFirst("access_token");
                           var refreshTokenClaim = identity.FindFirst("refresh_token");
                           using (var client = new HttpClient() { BaseAddress = new Uri(Configuration[Constants.Keys.ApiBaseUrl]) })
                           {
                               var response = await client.PostAsJsonAsync<RefreshTokenModel>(
                                   Constants.Routes.RefreshToken, new RefreshTokenModel
                                   {
                                       AccessToken = accessTokenClaim.Value,
                                       RefreshToken = refreshTokenClaim.Value
                                   });
                                   if (response.Content != null)
                                   {
                                       var token = await  response.Content.ReadAsAsync<TokenModel>();
                                       // everything went right, remove old tokens and add new ones
                                       identity.RemoveClaim(accessTokenClaim);
                                       identity.RemoveClaim(refreshTokenClaim);

                                       identity.AddClaims(new[]
                                       {
                                        new Claim("access_token", token.Token),
                                        new Claim("refresh_token", token.Token)
                                    });

                                       x.ShouldRenew = true;
                                   }
                           }
                       }
                   }
               };

           });
            services.AddMvc();
            services.AddControllersWithViews();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseStatusCodePagesWithReExecute("/error/{0}");

            app.UseStaticFiles();
            // app.UseHttpsRedirection();

            app.UseAuthentication();
            // app.UseStatusCodePages();
            //app.UseCookiePolicy();
            app.UseRouting();
            app.UseSession();
            //app.UseMvc(routes => {
            //    routes.MapRoute(
            //    name: "areas",
            //    template: "{area:exists}/{controller=home}/{action=index}/{id?}"
            //    );

            //    routes.MapRoute(
            //        name: "default",
            //        template: "{controller=home}/{action=index}/{id?}");
            //});
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
