using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Text;

namespace SampleWeb
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureKestrel(options =>
                    {
                        #region Change header encoding

                        // We added the ability for to pick an encoding for specific headers (or all of them)
                        options.RequestHeaderEncodingSelector = (headerName) =>
                        {
                            // You can change the encoding here
                            return Encoding.UTF8;
                        };

                        #endregion

                        #region Change HTTP/2 Keep Alive settings

                        // Set the HTTP2 keep alive timeout
                        options.Limits.Http2.KeepAlivePingTimeout = TimeSpan.FromMinutes(1);
                        options.Limits.Http2.KeepAlivePingDelay = TimeSpan.MaxValue;

                        #endregion
                    });

                    // You can instantiate the startup class and pass state from Program to your
                    // Startup class.
                    webBuilder.UseStartup(context => new Startup
                    {
                        Configuration = context.Configuration
                    });
                })
                .Build().Run();
        }
    }
}