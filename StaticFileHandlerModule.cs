using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Routing;

namespace hostasp
{
    //disguise the usual System.Web internal StaticFileHandler class as a HttpModule
    public class StaticFileHandlerModule : IHttpModule
    {
        //private readonly ILog _log = LogManager.GetLogger(typeof(StaticFileHandlerModule));
        private readonly IHttpHandler _handler;

        private static IHttpHandler GetHandler()
        {
            var assembly = Assembly.GetAssembly(typeof(IHttpHandler));
            var handlerType = assembly.GetType("System.Web.StaticFileHandler");
            var handlerConstructor = handlerType.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
            return (IHttpHandler)handlerConstructor.Invoke(null);
        }

        public StaticFileHandlerModule()
        {
            _handler = GetHandler();
        }

        public void Dispose() { }

        public void Init(HttpApplication context)
        {
            context.BeginRequest += context_BeginRequest;
        }

        public void context_BeginRequest(object sender, EventArgs e)
        {
            try
            {
                var app = sender as HttpApplication;

                _handler.ProcessRequest(app.Context);

                app.CompleteRequest();
            }
            catch (HttpException ex)
            {
                //file doesn't exist will be one of these exceptions, so eat
            }
            catch (Exception ex)
            {
                //_log.Error("Unexpected exception in staticfile module", ex);
            }                       
        }
    }    
}
