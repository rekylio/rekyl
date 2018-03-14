namespace Rekyl.Core.Database
{
    public class DatabaseName
    {
        public string Name { get; }

        public DatabaseName(string name)
        {
            Name = name;
        }
    }

    public class DatabaseUrl
    {
        public string Url { get; }

        public DatabaseUrl(string url)
        {
            Url = url;
        }
    }
}
