using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Rekyl.Attributes;
using Rekyl.Schema;

namespace Rekyl.Database
{
    public partial class DbContext
    {
        private static IEnumerable<TableInfo> DefaultTypes =>
            UsesDefaultDbReadTypes
            .Select(type => new TableInfo(type.GetCustomAttribute<TableAttribute>()?.TableName ?? type.Name, GetSecondaryIndexes(type)));

        private static IEnumerable<Type> UsesDefaultDbReadTypes => Assembly.GetEntryAssembly()
            .GetTypes()
            .Where(type => type.UsesDefaultDbRead());

        private static string[] GetSecondaryIndexes(Type type)
        {
            var properties = type.GetProperties().Where(d => d.GetCustomAttribute<SecondaryIndexAttribute>() != null);

            return properties.Select(d=>d.GetJPropertyName()).ToArray();
        }

        private static IEnumerable<TableInfo> TableInfos =>
            DefaultTypes
            .ToList();

        private class TableInfo
        {
            public TableInfo(string tableName, string[] secondaryIndexes)
            {
                TableName = tableName;
                SecondaryIndexes = secondaryIndexes;
            }

            public string TableName { get; }
            public string[] SecondaryIndexes { get; }
        }
    }
}
