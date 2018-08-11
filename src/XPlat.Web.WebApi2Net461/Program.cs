using System;
using System.Net.Http;
using System.Web.Http;
using Microsoft.Owin.Hosting;
using Owin;
using Topshelf;

namespace XPlat.WebApiNewCsProj
{
    public class Program
    {
        class Service
        {
            private IDisposable webApp;

            public void Stop()
            {
                webApp?.Dispose();
                webApp = null;
            }

            public void Start()
            {
                webApp = WebApplication.Start();
            }
        }
        public static void Main(string[] args)
        {
            HostFactory.Run(
                x =>
                {
                    x.Service<Service>(
                        s =>
                        {
                            s.ConstructUsing(_ => new Service());
                            s.WhenStarted(svc => svc.Start());
                            s.WhenStopped(svc => svc.Stop());
                        });

                    x.RunAsNetworkService();
                    x.SetServiceName("WebApi Service");
                    x.EnableServiceRecovery(s =>
                    {
                        s.RestartService(1);
                        s.RestartService(5);
                    });
                });
        }
    }
}
