using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace hostasp
{
    internal class HttpListenerWorkerRequest : HttpWorkerRequest
    {
        private readonly HttpListenerContext _context;
        private readonly string _physicalDir;
        private readonly string _virtualDir;

        private IIdentity _clientIdentity { get { return _context.User == null ? null : _context.User.Identity; } }
        private WindowsIdentity _clientWindowsIdentity { get { return _clientIdentity == null ? null :_clientIdentity as WindowsIdentity; } }

        internal HttpListenerWorkerRequest(HttpListenerContext context, string vdir, string pdir) : base()
        {
            _context = context;
            _physicalDir = pdir;
            _virtualDir = vdir;
        }

        public override void EndOfRequest()
        {
            _context.Response.OutputStream.Close();
            _context.Response.Close();
        }

        public override void FlushResponse(bool finalFlush)
        {
            _context.Response.OutputStream.Flush();
        }

        public override string GetHttpVerbName()
        {
            return _context.Request.HttpMethod;
        }

        public override string GetHttpVersion()
        {
            return string.Format("HTTP/{0}.{1}", _context.Request.ProtocolVersion.Major, _context.Request.ProtocolVersion.Minor);
        }

        public override string GetLocalAddress()
        {
            return _context.Request.LocalEndPoint.Address.ToString();
        }

        public override int GetLocalPort()
        {
            return _context.Request.LocalEndPoint.Port;
        }

        public override string GetQueryString()
        {
            var queryString = string.Empty;
            var rawUrl = _context.Request.RawUrl;
            var index = rawUrl.IndexOf('?');
            if (index != -1)
                queryString = rawUrl.Substring(index + 1);
            return queryString;
        }

        public override string GetRawUrl()
        {
            return _context.Request.RawUrl;
        }

        public override string GetRemoteAddress()
        {
            return _context.Request.RemoteEndPoint.Address.ToString();
        }

        public override int GetRemotePort()
        {
            return _context.Request.RemoteEndPoint.Port;
        }

        public override string GetUriPath()
        {
            return _context.Request.Url.LocalPath;
        }

        public override void SendKnownResponseHeader(int index, string value)
        {
            _context.Response.Headers[HttpWorkerRequest.GetKnownResponseHeaderName(index)] = value;
        }

        public override void SendResponseFromFile(IntPtr handle, long offset, long length)
        {
            throw new NotImplementedException();
        }

        public override void SendResponseFromFile(string filename, long offset, long length)
        {
            using (var s = File.OpenRead(filename))
            {
                var buffer = new byte[length];
                var read = s.Read(buffer, (int)offset, buffer.Length);
                _context.Response.OutputStream.Write(buffer, 0, read);
            }
        }

        public override void SendResponseFromMemory(byte[] data, int length)
        {
            _context.Response.OutputStream.Write(data, 0, length);
        }

        public override void SendStatus(int statusCode, string statusDescription)
        {
            _context.Response.StatusCode = statusCode;
            _context.Response.StatusDescription = statusDescription;
        }

        public override void SendUnknownResponseHeader(string name, string value)
        {
            _context.Response.Headers[name] = value;
        }

        public override string GetAppPath()
        {
            return _virtualDir;
        }

        public override string GetAppPathTranslated()
        {
            return _physicalDir;
        }

        public override int ReadEntityBody(byte[] buffer, int size)
        {
            return _context.Request.InputStream.Read(buffer, 0, size);
        }

        public override string GetUnknownRequestHeader(string name)
        {
            return _context.Request.Headers[name];
        }

        public override string[][] GetUnknownRequestHeaders()
        {
            string[][] unknownRequestHeaders;
            NameValueCollection headers = _context.Request.Headers;
            var count = headers.Count;
            var headerPairs = new List<string[]>(count);
            for (int i = 0; i < count; i++)
            {
                var headerName = headers.GetKey(i);
                if (GetKnownRequestHeaderIndex(headerName) == -1)
                {
                    var headerValue = headers.Get(i);
                    headerPairs.Add(new string[] { headerName, headerValue });
                }
            }
            unknownRequestHeaders = headerPairs.ToArray();
            return unknownRequestHeaders;
        }

        public override string GetKnownRequestHeader(int index)
        {
            //todo extend
            switch (index)
            {
                case HeaderUserAgent:
                    return _context.Request.UserAgent;
                default:
                    return _context.Request.Headers[GetKnownRequestHeaderName(index)];
            }
        }

        public override string GetServerVariable(string name)
        {
            // TODO: vet this list
            switch (name)
            {
                case "HTTPS":
                    return _context.Request.IsSecureConnection ? "on" : "off";
                case "HTTP_USER_AGENT":
                    return _context.Request.Headers["UserAgent"];
                case "LOGON_USER":
                    return _clientWindowsIdentity == null ? null : _clientWindowsIdentity.Name;
                case "AUTH_TYPE":
                    return _clientIdentity == null ? null : _clientIdentity.AuthenticationType;
                default:
                    return null;
            }
        }

        public override string GetFilePath()
        {
            // TODO: this is a hack
            string s = _context.Request.Url.LocalPath;
            if (s.IndexOf(".aspx") != -1)
                s = s.Substring(0, s.IndexOf(".aspx") + 5);
            else if (s.IndexOf(".asmx") != -1)
                s = s.Substring(0, s.IndexOf(".asmx") + 5);
            return s;
        }

        public override string GetFilePathTranslated()
        {
            var s = GetFilePath();
            s = s.Substring(_virtualDir.Length);
            s = s.Replace('/', '\\');
            return _physicalDir + s;
        }

        public override string GetPathInfo()
        {
            var s1 = GetFilePath();
            var s2 = _context.Request.Url.LocalPath;
            return (s1.Length == s2.Length) ? string.Empty : s2.Substring(s1.Length);
        }

        //authenticated requests
        public override IntPtr GetUserToken()
        {            
            return _clientWindowsIdentity == null ? IntPtr.Zero : _clientWindowsIdentity.Token;
        }
    }
}
