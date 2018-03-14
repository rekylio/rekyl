using System.Net;

namespace Rekyl.Handlers
{
    public abstract class SpecialHandler
    {
        public abstract string Path { get; }
        public abstract void Process(HttpListenerContext context);
    }
}
