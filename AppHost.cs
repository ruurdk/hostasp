using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Lifetime;
using System.Security.Principal;
using System.Web;
using System.Web.Hosting;
using System.Web.Routing;
using System.Web.Security;
using System.Web.SessionState;
#if DEBUG
using log4net;
using log4net.Config;
#endif

namespace hostasp
{   
    //this class will exist in the new appdomain to facilitate processing requests    
    public class AppHost : MarshalByRefObject
    {
#if DEBUG
        private static ILog _staticlog = LogManager.GetLogger(typeof (AppHost));        
        private ILog _log;        
#endif

        public string PPath { get; private set; }
        public string VPath { get; private set; }
        public AppDomain DefaultAppDomain { get; private set; }        

        public AppHost()
        {
            if (AppDomain.CurrentDomain.IsDefaultAppDomain()) throw new InvalidOperationException("Attempt to instance AppHost in the default AppDomain");
        }

#if DEBUG
        ~AppHost()
        {
            _log.Info("Finalizer called for AppHost");
        }
#endif

        public override object InitializeLifetimeService()
        {
            return null;
        }

        #region factory methods
        private static AppHost GetHost(string virtualDir, string physicalPath)
        {                 
            if (!(physicalPath.EndsWith("\\"))) physicalPath += "\\";           

#if DEBUG
            _staticlog.InfoFormat("Setting up app domain on virtual dir {0} - physical path {1}", virtualDir, physicalPath);
#endif
            var host = (AppHost)ApplicationHost.CreateApplicationHost(typeof(AppHost), virtualDir, physicalPath);
            host.PPath = physicalPath;
            host.VPath = virtualDir;
            host.DefaultAppDomain = AppDomain.CurrentDomain;

            return host;
        }
        
        private static AppHost GetHostRelativeToAssemblyPath(string virtualDir, string relativePath)
        {
#if DEBUG
            _staticlog.InfoFormat("Creating appdomain on relative address {0}", relativePath);
#endif
            var asmFilePath = new Uri(typeof(AppHost).Assembly.CodeBase).LocalPath;
            var asmPath = Path.GetDirectoryName(asmFilePath);
            var fullPath = Path.Combine(asmPath, relativePath);
            fullPath = Path.GetFullPath(fullPath);
#if DEBUG
            _staticlog.DebugFormat("Absolute path: {0}", fullPath);
            _staticlog.InfoFormat("Succesfully created new appdomain");
#endif
            return GetHost(virtualDir, fullPath);
        }

        public static AppHost HostAndSetup<T>(string virtualDir, string relativePath, int port) where T : HttpApplication
        {
            var host = GetHostRelativeToAssemblyPath(virtualDir, relativePath);

            //appdomain init
            host.SetupAllTheThings();

            host.SetupListener(port);

            host.HostMvcApp<T>();

            return host;
        }

        #endregion

        #region appdomain initialization
        private void SetupAssemblyResolvement()
        {
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler((e, args) =>
            {
#if DEBUG
                _log.DebugFormat("Resolving assembly in ASP.NET AppDomain: {0}", args.Name);
#endif
                try
                {
                    //first try to load directly
                    try
                    {
                        var directLoad = Assembly.LoadFrom(args.Name);
                        if (directLoad != null) return directLoad;
                    }
                    catch
                    {
#if DEBUG
                        _log.Debug("Failed to load from Web project, defaulting to default app domain codebase");
#endif
                    }

                    //try load from default app domain codebase                                
                    return Assembly.LoadFrom(Path.Combine(DefaultAppDomain.BaseDirectory, args.Name));
                }
                catch
                {
#if DEBUG
                    _log.DebugFormat("Failed to load from default AppDomain, returning not found");
#endif
                    return null;
                }
            });
        }

        private void SetupEventHandling()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            AppDomain.CurrentDomain.DomainUnload += CurrentDomain_DomainUnload;
        }

        private void CurrentDomain_DomainUnload(object sender, EventArgs e)
        {
#if DEBUG
            _log.Warn("Mvc AppDomain unloading.");
#endif
        }

        private void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
#if DEBUG
            _log.Info("Appdomain received process exit event");
#endif
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
#if DEBUG            
            var exception = e.ExceptionObject as Exception;
            if (exception != null)
            {
                _log.Error(string.Format("Unhandled exception in Mvc Appdomain. Terminating: {0}", e.IsTerminating), exception);
            }
            else
            {
                var rtexception = e.ExceptionObject as RuntimeWrappedException;
                _log.Error(string.Format("Unhandled exception in Mvc Appdomain. Terminating: {0}. Errormessage: {1}", e.IsTerminating, rtexception.Message));
            }
#endif             
        }

        private void SetupLoggingInNewAppDomain()
        {
#if DEBUG            
            using (var logconfig = Assembly.GetExecutingAssembly().GetManifestResourceStream("hostasp.log4net.config"))
            {
                XmlConfigurator.Configure(logconfig);
            }
            _log = LogManager.GetLogger(typeof(AppHost));
#endif             
        }

        private void SetupAllTheThings()
        {
            //init
            SetupAssemblyResolvement();
            SetupLoggingInNewAppDomain();
            SetupEventHandling();
        }
        #endregion

        #region HttpListener setup
        private Listener _listener;

        private void SetupListener(int port)
        {
#if DEBUG
            _log.InfoFormat("Setting up new httplistener on port {0}", port);
#endif
            _listener = new Listener(port);

#if DEBUG
            _log.InfoFormat("Start forwarding incoming requests to ASP.NET pipeline");
#endif
            _listener.IncomingRequests.Subscribe(
                (c) =>
                {
                    try
                    {
                        ProcessRequest(c);
                    }
                    catch (Exception ex)
                    {
#if DEBUG
                        _log.Error("Exception processing request", ex);
#endif
                    }
                }
#if DEBUG
                ,
                (ex) => _log.Error("Exception in request sequence", ex),
                () => _log.Info("HttpListener completed"));
#else
                );
#endif

#if DEBUG
            _log.Info("Completed httplistener setup");
#endif
        }

        #endregion

        #region ASP.NET hosting and request processing

        private HttpApplication _mvcApp;

        private void HostMvcApp<T>() where T: HttpApplication
        {
#if DEBUG
            _log.InfoFormat("Hosting Mvc application of type {0}", typeof(T));
#endif
            //usually IIS does this, but we need to serve static files ourselves
            HttpApplication.RegisterModule(typeof(StaticFileHandlerModule));
            
            _mvcApp = Activator.CreateInstance<T>();
#if DEBUG
            _log.InfoFormat("Successfully created mvc application");
#endif
        }        

        private void ProcessRequest(HttpListenerContext context)
        {
#if DEBUG
            _log.DebugFormat("Processing request");
#endif
           var wr = new HttpListenerWorkerRequest(context, VPath, PPath);
                   
           HttpContext.Current = new HttpContext(wr);           
           
           HttpRuntime.ProcessRequest(wr);
#if DEBUG
           _log.DebugFormat("Finished processing request");
#endif
        }
        #endregion        
    }
}