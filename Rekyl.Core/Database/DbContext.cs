using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using RethinkDb.Driver;
using RethinkDb.Driver.Net.Clustering;

namespace Rekyl.Core.Database
{
    public partial class DbContext
    {
        private static DbContext _instance;
        internal static DbContext Instance => _instance ?? throw new Exception("DbContext is not initialized");
        private static readonly RethinkDB R = RethinkDB.R;
        private readonly ConnectionPool _connection;

        internal static void Initialize(string databaseUrl, string databaseName)
        {
            if (_instance != null)
                throw new Exception("DbContext is already initialized");
            DatabaseName = databaseName;
            _instance = new DbContext(databaseUrl);
        }

        private static string DatabaseName { get; set; }
        public static bool Initalized { get; private set; }

        private DbContext(string hostName)
        {
            var ip = Dns.GetHostAddresses(hostName).First(d => d.AddressFamily == AddressFamily.InterNetwork);
            Console.WriteLine($"Connecting to database pool: {hostName} on ip:{ip}");
            _connection = R.ConnectionPool()
                .Seed($"{ip}:{RethinkDBConstants.DefaultPort}")
                .PoolingStrategy(new RoundRobinHostPool())
                .Discover(true)
                .Connect();
            CheckAndPopulateIfNeeded();
        }

        private void CheckAndPopulateIfNeeded()
        {
            if (Initalized) return;
            var list = R.DbList().Run<List<string>>(_connection);
            if (!list.Contains(DatabaseName))
            {
                R.DbCreate(DatabaseName).Run(_connection);
            }
            var tables = R.Db(DatabaseName).TableList().RunResult<List<string>>(_connection);

            foreach (var tableInfo in TableInfos)
            {
                if (!tables.Contains(tableInfo.TableName))
                {
                    R.Db(DatabaseName).TableCreate(tableInfo.TableName).Run(_connection);
                    foreach (var secondaryIndex in tableInfo.SecondaryIndexes)
                    {
                        var secondaryIndexResult = R.Db(DatabaseName).Table(tableInfo.TableName).IndexCreate(secondaryIndex).Run(_connection);
                    }
                }
            }

            Initalized = true;
        }
    }
}
