using System;
using System.Linq;
using System.Net;
using GraphQL.Conventions;

namespace Rekyl.Handlers
{
    public abstract class DefaultImageHandler : SpecialHandler
    {
        public abstract IDefaultImage GetImage(string key);

        public override string Path => "images";
        public override void Process(HttpListenerContext context)
        {
            try
            {
                if (string.Compare(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    var keyString = context.Request.RawUrl.Split('/').Where(d => !string.IsNullOrEmpty(d)).ToArray()[1];
                    var image = GetImage(keyString);
                    context.Response.Headers.Add("Content-Type", image.ContentType);
                    var imageBytes = image.ImageData;
                    context.Response.StatusCode = 200;

                    context.Response.OutputStream.Write(imageBytes, 0, imageBytes.Length);
                    context.Response.OutputStream.Close();
                    return;
                }
            }
            catch (Exception)
            {
                // ignored
            }

            context.Response.StatusCode = 400;
        }
    }

    public interface IDefaultImage
    {
        string ContentType { get; }
        [Ignore]
        byte[] ImageData { get; }
    }
}
