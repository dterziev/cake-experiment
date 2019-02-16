using System;
using System.Net;
using System.Web.Http;
using Microsoft.Owin.FileSystems;
using Microsoft.Owin.Hosting;
using Microsoft.Owin.StaticFiles;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NLog;
using Owin;
using Swashbuckle.Application;

namespace XPlat.WebApiNewCsProj
{
    public class ActionDisposable : IDisposable
    {
        private Action _action;

        public ActionDisposable(Action action)
        {
            _action = action;
        }
        public void Dispose()
        {
            _action?.Invoke();
            _action = null;
        }
    }

    public static class WebApplication
    {
        private static readonly Logger Logger = LogManager.GetLogger("WebApplication");

        private static void Startup(IAppBuilder app)
        {
            HttpListener listener = (HttpListener)app.Properties["System.Net.HttpListener"];
            listener.AuthenticationSchemes = AuthenticationSchemes.Ntlm;

            HttpConfiguration config = new HttpConfiguration
            {
                IncludeErrorDetailPolicy = IncludeErrorDetailPolicy.Always
            };
            config.EnableCors();
            config.EnableSystemDiagnosticsTracing();
            config.AddApiVersioning();
            config.AddVersionedApiExplorer();


            config.Formatters.JsonFormatter.SerializerSettings.ContractResolver = new DefaultContractResolver();
            config.Formatters.JsonFormatter.SerializerSettings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;

            config.MapHttpAttributeRoutes();
            config
                .EnableSwagger(c =>
                {
                    c.SingleApiVersion("v1", "A title for your API");
                    c.PrettyPrint();
                })
                .EnableSwaggerUi();

            PhysicalFileSystem physicalFileSystem = new PhysicalFileSystem("wwwroot");
            FileServerOptions fileServerOptions = new FileServerOptions { EnableDefaultFiles = true, FileSystem = physicalFileSystem };

            app.UseWebApi(config);
            app.UseFileServer(fileServerOptions);

        }

        public static IDisposable Start()
        {
            try
            {
                string url = "http://localhost:9000";

                IDisposable app = WebApp.Start(url, Startup);

                Logger.Info($"Web application started at: {url}");
                return new ActionDisposable(
                    () =>
                    {
                        Logger.Info("Stopping web application");
                        app.Dispose();
                        Logger.Info("Web application stopped");
                    });
            }
            catch (Exception ex)
            {
                throw new Exception("Error starting web application", ex);
            }
        }
    }
}
