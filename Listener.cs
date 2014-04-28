using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Reflection;
using System.Text;
using System.Web;
#if DEBUG
using log4net;
#endif

namespace hostasp
{
    internal class Listener : IDisposable
    {
        private HttpListener _listener = null;
#if DEBUG
        private ILog _log = LogManager.GetLogger(typeof (Listener));
#endif
        public IObservable<HttpListenerContext> IncomingRequests { get; private set; }
 
        internal Listener(int port)
        {
#if DEBUG
            _log.Debug("Starting http listener");
#endif
            _listener = new HttpListener();
            _listener.Prefixes.Add(string.Format("http://localhost:{0}/", port));
            _listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;

            _listener.Start();

            IncomingRequests = _listener.GetContextsAsObservable().ObserveOn(NewThreadScheduler.Default);                       
        }

#if DEBUG
        ~Listener()
        {
            _log.Info("Finalizing listener");
        }
#endif

        public void Dispose()
        {
#if DEBUG
            _log.Debug("Disposing httplistener");
#endif
            try
            {
                if (_listener == null) return;

                _listener.Stop();
                _listener = null;
            }
            catch (ObjectDisposedException)
            {                
            }            
        }      
    }

    internal static class ListenerExtensions
    {
#if DEBUG
        private static ILog _log = LogManager.GetLogger(typeof (ListenerExtensions));
#endif

        private static IEnumerable<IObservable<HttpListenerContext>> Listen(this HttpListener listener)
        {
            IObservable<HttpListenerContext> temp;

            while (true)
            {
                try
                {
                    temp = listener.GetContextAsync().ToObservable();
                } 
                catch (Exception ex)
                {
#if DEBUG
                    _log.Error("Exception in getting listenercontext", ex);
#endif
                    temp = null;
                }
                if (temp != null) yield return temp;
            }
        }

        internal static IObservable<HttpListenerContext> GetContextsAsObservable(this HttpListener listener)
        {
            try
            {
                return listener.Listen().Concat();
            }
            catch (Exception ex)
            {
#if DEBUG
                _log.Error("Exception concatenating listenercontext into an observable", ex);
#endif
                throw;
            }
        }
    }
}