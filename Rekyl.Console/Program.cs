using System;
using System.Net.Http;
using Rekyl.Database;
using Rekyl.Handlers;

namespace Rekyl.Console
{
    class Program
    {
        private static readonly DatabaseUrl DatabaseUrl =
            new DatabaseUrl("localhost");
        private static readonly DatabaseName DatabaseName =
            new DatabaseName("test");
        private static readonly string Host = "localhost";

        static void Main(string[] args)
        {

            var server = new SimpleHttpServer(1234, Host, DatabaseName, DatabaseUrl,
                    new GraphQlDefaultHandler());

            using (var client = new HttpClient())
            {
                var result1 = client.GetStringAsync("http://localhost:1234/index.html").Result;
                var result2 = client.GetStringAsync("http://localhost:1234/index.html?_sw-precache=3b0574e5bb73113c46c0ebba65ab959b").Result;
            }
        }
    }
}
