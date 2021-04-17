using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DVDOrders.Services
{
    public class HTTPServer : IDisposable
    {
        private static readonly IDictionary<string, string> MimeTypeMappings =
            new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
            {
                #region extension to MIME type list

                {".asf", "video/x-ms-asf"},
                {".asx", "video/x-ms-asf"},
                {".avi", "video/x-msvideo"},
                {".bin", "application/octet-stream"},
                {".cco", "application/x-cocoa"},
                {".crt", "application/x-x509-ca-cert"},
                {".css", "text/css"},
                {".deb", "application/octet-stream"},
                {".der", "application/x-x509-ca-cert"},
                {".dll", "application/octet-stream"},
                {".dmg", "application/octet-stream"},
                {".ear", "application/java-archive"},
                {".eot", "application/octet-stream"},
                {".exe", "application/octet-stream"},
                {".flv", "video/x-flv"},
                {".gif", "image/gif"},
                {".hqx", "application/mac-binhex40"},
                {".htc", "text/x-component"},
                {".htm", "text/html"},
                {".html", "text/html"},
                {".ico", "image/x-icon"},
                {".img", "application/octet-stream"},
                {".iso", "application/octet-stream"},
                {".jar", "application/java-archive"},
                {".jardiff", "application/x-java-archive-diff"},
                {".jng", "image/x-jng"},
                {".jnlp", "application/x-java-jnlp-file"},
                {".jpeg", "image/jpeg"},
                {".jpg", "image/jpeg"},
                {".js", "application/x-javascript"},
                {".mml", "text/mathml"},
                {".mng", "video/x-mng"},
                {".mov", "video/quicktime"},
                {".mp3", "audio/mpeg"},
                {".mpeg", "video/mpeg"},
                {".mpg", "video/mpeg"},
                {".msi", "application/octet-stream"},
                {".msm", "application/octet-stream"},
                {".msp", "application/octet-stream"},
                {".pdb", "application/x-pilot"},
                {".pdf", "application/pdf"},
                {".pem", "application/x-x509-ca-cert"},
                {".pl", "application/x-perl"},
                {".pm", "application/x-perl"},
                {".png", "image/png"},
                {".prc", "application/x-pilot"},
                {".ra", "audio/x-realaudio"},
                {".rar", "application/x-rar-compressed"},
                {".rpm", "application/x-redhat-package-manager"},
                {".rss", "text/xml"},
                {".run", "application/x-makeself"},
                {".sea", "application/x-sea"},
                {".shtml", "text/html"},
                {".sit", "application/x-stuffit"},
                {".swf", "application/x-shockwave-flash"},
                {".tcl", "application/x-tcl"},
                {".tk", "application/x-tcl"},
                {".txt", "text/plain"},
                {".war", "application/java-archive"},
                {".wbmp", "image/vnd.wap.wbmp"},
                {".wmv", "video/x-ms-wmv"},
                {".xml", "text/xml"},
                {".xpi", "application/x-xpinstall"},
                {".zip", "application/zip"},

                #endregion
            };

        private CancellationTokenSource _serverTokenSource;

        private Task _serverThread;

        private static bool _keepGoing = true;

        private HttpListener _listener;

        public Dictionary<string, Action<HttpListenerContext>> EventDictionary =
            new Dictionary<string, Action<HttpListenerContext>>();

        private readonly Dictionary<long, Task<bool>> _processTasks = new Dictionary<long, Task<bool>>();

        private readonly Dictionary<long, CancellationTokenSource> _processTaskTokens =
            new Dictionary<long, CancellationTokenSource>();

        private readonly AdvancedTimer _taskCollection = new AdvancedTimer();

        public int Port { get; private set; }

        public static string MimeTypeFinder(string extension) => MimeTypeMappings[extension];

        public HTTPServer(int port, List<string> prefix)
        {
            Initialize(port, prefix);
        }

        /// <summary>
        /// Stop server and dispose all functions.
        /// </summary>
        public void Stop()
        {
            var len = _processTasks.Keys.Count;

            var ids = new long[len];

            _processTasks.Keys.CopyTo(ids, 0);

            foreach (long id in ids)
            {
                _processTaskTokens[id].Cancel();
                _processTasks[id].Wait(5);
                _processTasks[id].Dispose();
                _processTasks.Remove(id);
            }

            _keepGoing = false;
            lock (_listener)
            {
                try
                {
                    //Use a lock so we don't kill a request that's currently being processed
                    _listener.Stop();
                }
                catch
                {
                    // ignored
                }
            }

            try
            {
                _serverTokenSource.Cancel();
                _serverThread.Wait(1000);
            }

            catch
            {
                // ignored
            }
        }

        private async Task Listen(List<string> prefixList = null)
        {

            lock (_listener)
            {
                _listener = new HttpListener();

                _listener.Prefixes.Add("http://127.0.0.1:" + Port.ToString() + "/");

                if (prefixList != null)
                {
                    foreach (string prefix in prefixList)
                    {
                        _listener.Prefixes.Add(prefix);
                    }
                }

                _listener.Start();
            }


            while (_keepGoing)
            {

                try
                {
                    HttpListenerContext context = await _listener.GetContextAsync();

                    var id = DateTime.Now.Ticks;

                    _processTaskTokens[id] = new CancellationTokenSource();

                    var process = new Task<bool>(() => Process(context, id), _processTaskTokens[id].Token);

                    _processTasks.Add(id, process);

                    process.Start();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        private bool Process(HttpListenerContext context, long id)
        {
            var keynames = context.Request.Url.AbsolutePath;
            Console.WriteLine(keynames);
            Console.WriteLine(context.Request.Url);
            if (EventDictionary.ContainsKey(keynames))
            {
                EventDictionary[keynames]?.Invoke(context);
                context.Response.StatusCode = (int) HttpStatusCode.OK;
                context.Response.OutputStream.Flush();
            }
            else
            {
                context.Response.StatusCode = (int) HttpStatusCode.NotFound;
                context.Response.OutputStream.Flush();
                context.Response.OutputStream.Close();
            }






            return true;
        }

        private void Initialize(int port, List<string> prefixList = null)
        {
            Port = port;

            _serverTokenSource = new CancellationTokenSource();

            _serverThread = Task.Run(() => Listen(prefixList), _serverTokenSource.Token);

            _taskCollection.Interval = 30000;

            _taskCollection.Elapsed += _taskCollection_Elapsed;

            _taskCollection.Start();
        }

        private void _taskCollection_Elapsed(object sender, EventArgs e)
        {
            var len = _processTasks.Keys.Count;

            var ids = new long[len];

            _processTasks.Keys.CopyTo(ids, 0);

            foreach (var id in ids)
            {
                if (!_processTasks[id].IsCompleted && !_processTasks[id].IsCanceled &&
                    !_processTasks[id].IsFaulted) continue;
                try
                {
                    _processTasks[id].Dispose();
                    _processTaskTokens[id].Dispose();

                    _processTasks.Remove(id);
                    _processTaskTokens.Remove(id);
                }
                catch
                {
                    //
                }
            }
        }

        public void Dispose()
        {
            Stop();

            ((IDisposable) _listener)?.Dispose();

            var len = _processTasks.Keys.Count;

            var ids = new long[len];

            _processTasks.Keys.CopyTo(ids, 0);

            foreach (var id in ids)
            {
                try
                {
                    _processTaskTokens[id].Cancel();
                    _processTasks[id].Dispose();
                    _processTasks.Remove(id);
                }
                catch
                {
                    //
                }
            }
        }
    }
}


