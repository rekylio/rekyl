using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using Rekyl.Database;
using Rekyl.Handlers;

namespace Rekyl
{
    public class SimpleHttpServer
    {
        public SpecialHandler[] Handlers { get; }
        private readonly Thread _serverThread;
        private HttpListener _listener;

        public int Port { get; }
        public string Host { get; }

        public SimpleHttpServer(int port, string host, DatabaseName databaseName, DatabaseUrl databaseUrl, params SpecialHandler[] handlers)
        {
            UserContext.InitDb(databaseUrl, databaseName);
            Handlers = handlers;
            Port = port;
            Host = host;
            Console.WriteLine($"Running server on port: {port}");
            foreach (var handler in handlers)
            {
                Console.WriteLine($"Using handler: {handler.GetType().Name} on /{handler.Path}");
            }
            _serverThread = new Thread(Listen);
            _serverThread.Start();
        }

        public void Stop()
        {
            _serverThread.Abort();
            _listener.Stop();
        }

        private void Listen()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://{Host}:{Port}/");
            _listener.Start();
            while (true)
            {
                try
                {
                    var context = _listener.GetContext();
                    Process(context);
                }
                catch (Exception ex)
                {
                    // ignored
                }
            }
        }

        private void Process(HttpListenerContext context)
        {
            var specialHandler = Handlers
                .FirstOrDefault(d => context.Request.RawUrl.Split('/').FirstOrDefault(e => !string.IsNullOrEmpty(e)) == d.Path);

            if (specialHandler != null)
                specialHandler.Process(context);
            else
            {
                ProcessStaticFiles(context);
            }
        }

        public void ProcessStaticFiles(HttpListenerContext context)
        {
            const string indexDefault = "/index.html";
            var path = context.Request.RawUrl;
            if (path == "/") path = indexDefault;
            var pathParts = new[] { ".", "static" }.Concat(path.Split('/')).Where(d => !string.IsNullOrEmpty(d)).ToArray();
            var filename = Path.Combine(pathParts);
            filename = filename.Split('?')[0];
            if (File.Exists(filename))
            {
                try
                {
                    Stream input = new FileStream(filename, FileMode.Open);

                    //Adding permanent http response headers
                    var extension = Path.GetExtension(filename);
                    var contentType = MimeTypeMappings.ContainsKey(extension)
                        ? MimeTypeMappings[extension]
                        : "application/octet-stream";
                    context.Response.ContentType = contentType;
                    context.Response.ContentLength64 = input.Length;
                    context.Response.AddHeader("Date", DateTime.Now.ToString("r"));
                    context.Response.AddHeader("Last-Modified", File.GetLastWriteTime(filename).ToString("r"));

                    var buffer = new byte[1024 * 16];
                    int nbytes;
                    while ((nbytes = input.Read(buffer, 0, buffer.Length)) > 0)
                        context.Response.OutputStream.Write(buffer, 0, nbytes);
                    input.Close();

                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.OutputStream.Flush();
                }
                catch (Exception ex)
                {
                    WriteMessage(ex);
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                }

            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            }

            context.Response.OutputStream.Close();
        }

        private void WriteMessage(Exception ex)
        {
            Console.WriteLine(ex.Message);
            if (ex.InnerException != null)
                WriteMessage(ex.InnerException);
        }


        public static readonly Dictionary<string, string> MimeTypeMappings = new Dictionary<string, string> {
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
        };
    }
}
