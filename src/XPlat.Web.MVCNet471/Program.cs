using System;
using System.Diagnostics;
using System.IO;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.HttpSys;
#if NETFRAMEWORK
using Topshelf;
#endif

namespace MyOutput
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string wwwRootPath = Directory.GetCurrentDirectory();
            if (!Directory.Exists(Path.Combine(wwwRootPath, "wwwroot")))
            {
                wwwRootPath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            }

#if NETFRAMEWORK
            HostFactory.Run(
                x =>
                {
                    x.Service<MainService>(
                        s =>
                        {
                            s.ConstructUsing(_ => new MainService(wwwRootPath));
                            s.WhenStarted(svc => svc.Start(args));
                            s.WhenStopped(svc => svc.Stop());
                        });
                    x.RunAsNetworkService();
                    x.SetServiceName("WebApiService");
                    x.SetDescription("WebApiService");
                    x.SetDisplayName("WebApiService");
                    x.UseNLog();
                    x.StartAutomatically();
                    x.EnableServiceRecovery(s =>
                    {
                        s.RestartService(1);
                        s.RestartService(5);
                    });
                });
#else
                            
#endif
            using (var svc = new MainService(wwwRootPath))
            {
                svc.Run(args);
            }
        }

    }

    public sealed class MainService : IDisposable
    {
        public MainService(string contentRoot)
        {
            _contentRoot = contentRoot;
        }

        private IWebHost _webHost;
        private readonly string _contentRoot;

        public void Start(string[] args)
        {
            BuildWebHost(args);
            _webHost.Start();
        }

        public void Run(string[] args)
        {
            BuildWebHost(args);
            _webHost.Run();
        }

        private void BuildWebHost(string[] args)
        {
            IWebHostBuilder builder = WebHost.CreateDefaultBuilder(args)
                .UseContentRoot(_contentRoot)
                .CaptureStartupErrors(true)
                .UseShutdownTimeout(TimeSpan.FromSeconds(15))
                .UseHttpSys(o =>
                {
                    o.Authentication.AllowAnonymous = true;
                    o.Authentication.Schemes =
                        AuthenticationSchemes.Negotiate |
                        AuthenticationSchemes.NTLM;
                    o.UrlPrefixes.Add(UrlPrefix.Create("http", "*", 5000, "/"));
                    o.UrlPrefixes.Add(UrlPrefix.Create("https", "*", 5001, "/"));
                })
                .UseStartup<Startup>();

            _webHost = builder.Build();
        }

        public void Stop()
        {
            this.Dispose();
        }

        public void Dispose()
        {
            _webHost?.Dispose();
            _webHost = null;
        }
    }
}
