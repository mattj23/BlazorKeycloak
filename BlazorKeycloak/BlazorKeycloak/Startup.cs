using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BlazorKeycloak.Data;
using BlazorKeycloak.Oidc;
using IdentityModel.Client;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;

namespace BlazorKeycloak
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRazorPages();
            services.AddServerSideBlazor();
            services.AddSingleton<WeatherForecastService>();
            
            // OpenID Connect Identity Stuff
            var oidcOptions = new OpenIdConnectOptions();
            this.Configuration.GetSection("Oidc").Bind(oidcOptions);
            services.AddSingleton(oidcOptions);

            services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.Cookie.SameSite = SameSiteMode.None;
                options.Cookie.Name = "AuthCookie";
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.SlidingExpiration = true;
                options.Events = new CookieAuthenticationEvents
                {
                    OnValidatePrincipal = async c =>
                    {
                        // this event is fired everytime the cookie has been validated by the cookie middleware, so basically during every authenticated request.
                        // the decryption of the cookie has already happened so we have access to the identity + user claims
                        // and cookie properties - expiration, etc..
                        // source: https://github.com/mderriey/aspnet-core-token-renewal/blob/2fd9abcc2abe92df2b6c4374ad3f2ce585b6f953/src/MvcClient/Startup.cs#L57
                        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        var exp = c.Properties.ExpiresUtc.GetValueOrDefault().ToUnixTimeSeconds();

                        if (now >= exp) // session cookie expired?
                        {
                            var response = await new HttpClient().RequestRefreshTokenAsync(new RefreshTokenRequest
                            {
                                Address = oidcOptions.Authority + "/protocol/openid-connect/token", // AAD="/oauth2/token",
                                ClientId = Configuration["Oidc:ClientId"],
                                ClientSecret = Configuration["Oidc:ClientSecret"],
                                RefreshToken = ((ClaimsIdentity)c.Principal.Identity).GetClaimValue(ClaimType.RefreshToken) //c.Properties.Items[".Token.refresh_token"] // check if present
                            }).ConfigureAwait(false);

                            if (!response.IsError)
                            {
                                ((ClaimsIdentity)c.Principal.Identity)
                                    .SetIdentityClaims(response.AccessToken, response.RefreshToken);

                                c.ShouldRenew = true; // renew session cookie
                            }
                        }
                    }
                };
            })
            .AddOpenIdConnect(options =>
            {
                options.Authority = oidcOptions.Authority;
                options.ClientId = oidcOptions.ClientId;
                options.ClientSecret = oidcOptions.ClientSecret;
                options.SaveTokens = true;
                options.ResponseType = oidcOptions.ResponseType;
                options.Resource = oidcOptions.Resource; // needed for proper jwt format access_token
                options.RequireHttpsMetadata = oidcOptions.RequireHttpsMetadata; // dev only
                options.GetClaimsFromUserInfoEndpoint = oidcOptions.GetClaimsFromUserInfoEndpoint; // does not work together with options.resource
                //options.CallbackPath = oidcOptions.CallbackPath; // "/signin-oidc/"
                //options.SignedOutCallbackPath = oidcOptions.SignedOutCallbackPath; // "/signout-oidc/"
                options.SaveTokens = oidcOptions.SaveTokens;

                options.Scope.Clear();
                foreach (var scope in oidcOptions.Scope)
                {
                    options.Scope.Add(scope);
                }

                options.Events = new OpenIdConnectEvents
                {
                    OnTokenValidated = t =>
                    {
                        // this event is called after the OIDC middleware received the auhorisation code, redeemed it for an access token + a refresh token
                        // and validated the identity token
                        ((ClaimsIdentity)t.Principal.Identity)
                            .SetIdentityClaims(t.TokenEndpointResponse.AccessToken, t.TokenEndpointResponse.RefreshToken);

                        t.Properties.ExpiresUtc = new JwtSecurityToken(t.TokenEndpointResponse.AccessToken).ValidTo; // align expiration of the cookie with expiration of the access token
                        t.Properties.IsPersistent = true; // so that we don't issue a session cookie but one with a fixed expiration

                        return Task.CompletedTask;
                    }
                };
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "name",
                    RoleClaimType = "groups",
                    ValidateIssuer = true
                };
            });

            services.AddAuthorization();
            services.AddHttpContextAccessor();

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (string.Equals(
                Environment.GetEnvironmentVariable("SSL_OFFLOAD"),
                "true", StringComparison.OrdinalIgnoreCase))
            {
                app.UseForwardedHeaders();
            }

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });
        }
    }
}