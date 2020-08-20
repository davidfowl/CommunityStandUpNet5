using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SampleWeb
{
    public class Startup
    {
        public IConfiguration Configuration { get; set; }

        public void ConfigureServices(IServiceCollection services)
        {
            // Configure JSON settings here or on the call itself
            services.Configure<JsonOptions>(o =>
            {
                o.SerializerOptions.PropertyNameCaseInsensitive = true;
            });
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    // Very fast way to check the protocol version
                    if (HttpProtocol.IsHttp2(context.Request.Protocol))
                    {
                        // Do something special
                    }

                    await context.Response.WriteAsJsonAsync(new { Name = "David", Age = 134 });
                });

                endpoints.MapPost("/", async context =>
                {
                    if (!context.Request.HasJsonContentType())
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        return;
                    }

                    var person = await context.Request.ReadFromJsonAsync<Person>();

                    await context.Response.WriteAsJsonAsync(person);
                });
            });
        }
    }
}
