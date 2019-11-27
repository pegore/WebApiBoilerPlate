using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using RabbitMQ.Client;
using System;
using System.Linq;
using System.Net.Mime;
using System.Text.Json;

namespace Hbsis.Icebev.Api
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
            services.AddControllers().AddJsonOptions(options =>
            {
                var serializerOptions = options.JsonSerializerOptions;
                serializerOptions.IgnoreNullValues = true;
                serializerOptions.IgnoreReadOnlyProperties = true;
                serializerOptions.WriteIndented = true;
            });
            var factory = new ConnectionFactory()
            {
                AutomaticRecoveryEnabled = true,
                HostName = Environment.GetEnvironmentVariable("RMQ_HOST"),
                Port = int.TryParse(Environment.GetEnvironmentVariable("RMQ_PORT"), out int port) ? port : 5672,
                UserName = Environment.GetEnvironmentVariable("RMQ_USER"),
                Password = Environment.GetEnvironmentVariable("RMQ_PASS")
            };
            var connection = factory.CreateConnection();
            services.AddHealthChecks().AddRabbitMQ(sp => connection);
            services.AddHealthChecksUI();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "WebApiBoilerPlate",
                    Version = "v1"
                });
            });

        }


        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }


            app.UseHealthChecks("/healthcheck", new HealthCheckOptions()
            {
                ResponseWriter = async (context, report) =>
                {
                    var result = JsonSerializer.Serialize(
                        new
                        {
                            statusApplication = report.Status.ToString(),
                            healthChecks = report.Entries.Select(e => new
                            {
                                check = e.Key,
                                ErrorMessage = e.Value.Exception?.Message,
                                status = Enum.GetName(typeof(HealthStatus), e.Value.Status)
                            })
                        });
                    context.Response.ContentType = MediaTypeNames.Application.Json;
                    await context.Response.WriteAsync(result);
                }
            });

            // Gera o endpoint que retornará os dados utilizados no dashboard
            app.UseHealthChecks("/healthchecks-data-ui", new HealthCheckOptions()
            {
                Predicate = _ => true,
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });

            // Ativa o dashboard para a visualização da situação de cada Health Check
            app.UseHealthChecksUI();
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                // Linha adicionada para resolver seguinte erro: Fetch error Not Found /swagger/v1/swagger.json
                string swaggerJsonBasePath = string.IsNullOrWhiteSpace("swagger") ? "." : "..";
                c.SwaggerEndpoint($"{swaggerJsonBasePath}/swagger/v1/swagger.json", "WebApiBoilerPlate");
            });



            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseAuthorization();
        }
    }
}
