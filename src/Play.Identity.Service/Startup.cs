using System;
using System.IO;
using System.Reflection;
using GreenPipes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Play.Common.MassTransit;
using Play.Common.Settings;
using Play.Identity.Service.Entities;
using Play.Identity.Service.Exceptions;
using Play.Identity.Service.HealthChecks;
using Play.Identity.Service.HostedServices;
using Play.Identity.Service.Settings;

namespace Play.Identity.Service
{
    public class Startup
    {
        public const string AllowedOriginSetting = "AllowedOrigin";
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            BsonSerializer.RegisterSerializer(new GuidSerializer(MongoDB.Bson.BsonType.String));
            var serviceSettings = Configuration.GetSection(nameof(ServiceSettings)).Get<ServiceSettings>();
            var mongoDbSettings = Configuration.GetSection(nameof(MongoDbSettings)).Get<MongoDbSettings>();
            var identityServerSettings = Configuration.GetSection(nameof(IdentityServerSettings)).Get<IdentityServerSettings>();
            services.Configure<IdentitySettings>(Configuration.GetSection(nameof(IdentitySettings)))
                .AddDefaultIdentity<ApplicationUser>()
                .AddRoles<ApplicationRole>()
                .AddMongoDbStores<ApplicationUser, ApplicationRole, Guid>
                (
                    mongoDbSettings.ConnectionString,
                    serviceSettings.ServiceName
                );

            services.AddMassTransitWithMessageBroker(Configuration, retryConfigurator => {
                retryConfigurator.Interval(3, TimeSpan.FromSeconds(5));
                retryConfigurator.Ignore(typeof(UnknownUserException), typeof(InSufficientFundsException));
            });

            services.AddIdentityServer(options => {
                        options.Events.RaiseErrorEvents = true;
                        options.Events.RaiseFailureEvents = true;
                        options.Events.RaiseSuccessEvents = true;
                        options.IssuerUri = identityServerSettings.IssuerURI;
                        options.KeyManagement.KeyPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                    })
                    .AddAspNetIdentity<ApplicationUser>()
                    .AddInMemoryApiScopes(identityServerSettings.ApiScopes)
                    .AddInMemoryApiResources(identityServerSettings.ApiResources)
                    .AddInMemoryClients(identityServerSettings.Clients)
                    .AddInMemoryIdentityResources(identityServerSettings.IdentityResources);

            services.AddLocalApiAuthentication();

            services.AddControllers();
            services.AddHostedService<IdentitySeedHostedServices>();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Play.Identity.Service", Version = "v1" });
            });

            services.AddHealthChecks()
                    .Add(new HealthCheckRegistration(
                        "mongodb",
                        serviceProvider => {
                            var mongoClient = new MongoClient(mongoDbSettings.ConnectionString);
                            return new MongoDBHealthCheck(mongoClient);
                        },
                        HealthStatus.Unhealthy,
                        ["ready"],
                        TimeSpan.FromSeconds(5)
                    ));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Play.Identity.Service v1"));

                app.UseCors(builder => {
                builder
                    .WithOrigins(Configuration[AllowedOriginSetting])
                    .AllowAnyHeader()
                    .AllowAnyMethod();
                });
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseIdentityServer();

            app.UseAuthorization();

            app.UseStaticFiles();

            app.UseCookiePolicy(new CookiePolicyOptions {
                MinimumSameSitePolicy = SameSiteMode.Lax
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapRazorPages();
                endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions() {
                    Predicate = (check) => check.Tags.Contains("ready")
                });
                endpoints.MapHealthChecks("/health/live", new HealthCheckOptions() {
                    Predicate = (check) => false
                });
            });
        }
    }
}
