using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GraphQL.Conventions;
using GraphQLParser.AST;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rekyl.Core.Attributes;
using Rekyl.Core.Schema;
using RethinkDb.Driver.Ast;
using RethinkDb.Driver.Model;

namespace Rekyl.Core.Database
{
    public partial class DbContext
    {
        private static string GetTableName(Type unsafeType)
        {
            var safeType = GetTypeIfArray(unsafeType);
            var name = safeType.GetCustomAttribute<TableAttribute>()?.TableName ?? safeType.Name;
            return name;
        }

        private static Table GetTable(Type type)
        {
            return R.Db(DatabaseName).Table(GetTableName(type));
        }

        private static GraphQLSelectionSet GetSelectionSet(GraphQLDocument document)
        {
            var operation =
                document.Definitions.First(d =>
                    d.Kind == ASTNodeKind.OperationDefinition) as GraphQLOperationDefinition;
            var selectionSet = (operation?.SelectionSet?.Selections?.SingleOrDefault() as GraphQLFieldSelection)?.SelectionSet;
            return selectionSet;
        }

        private T GetWithDocument<T>(GraphQLSelectionSet selectionSet, Id id) where T : class
        {
            var type = typeof(T);
            var hashMap = GetHashMap(selectionSet, type);
            try
            {
                var result = GetFromDb<T>(id, hashMap);

                var ret = Utils.DeserializeObject(typeof(T), result);
                return ret as T;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private JArray GetFromDb<T>(Id[] ids, MapObject hashMap)
        {
            var importTree = GetImportTree(typeof(T), hashMap, null);
            var idStrings = ids.Select(d => d.ToString()).ToArray();
            var table = GetTable(typeof(T));
            ReqlExpr get = table.GetAll(R.Args(idStrings));
            get = get.Map(item => Merge(item, importTree));
            get = get.Map(item => item.Pluck(hashMap));
            var result = get.CoerceTo("ARRAY").Run(_connection) as JArray;
            return result;
        }

        private JObject GetFromDb<T>(Id id, MapObject hashMap)
        {
            var importTree = GetImportTree(typeof(T), hashMap, null);
            var table = GetTable(typeof(T));
            ReqlExpr get = table
                .Get(id.ToString());
            get = Merge(get, importTree);
            get = get.Pluck(hashMap);
            var result = get.Run(_connection) as JObject;
            return result;
        }

        private T GetShallow<T>(Id id) where T : class
        {
            var table = GetTable(typeof(T));
            try
            {
                var result = table.Get(id.ToString())
                    .Run(_connection) as JObject;
                var ret = Utils.DeserializeObject(typeof(T), result);
                return ret as T;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private ReqlExpr Merge(ReqlExpr expr, ImportTreeItem importTree)
        {
            var ret = expr;

            foreach (var importItem in importTree.ImportItems)
            {
                if (importItem.IsArray)
                {
                    ret = ret.Merge(item => R.HashMap(importItem.PropertyName,
                        GetItem(item, importItem)
                            .Map(subItem => Merge(subItem, importItem))
                            .CoerceTo("ARRAY")));
                }
                else
                {
                    ret = ret.Merge(item => R.HashMap(importItem.PropertyName,
                        Merge(GetItem(item, importItem), importItem)));
                }
            }

            return ret;
        }

        private ReqlExpr GetItem(ReqlExpr item, ImportTreeItem importItem)
        {
            // Get array of items from other table by key
            if (importItem.IsArray && importItem.NodeBase)
                return importItem.Table.GetAll(R.Args(
                    item.G(importItem.PropertyName)
                    .Filter(key => key != null)));
            // Get single item from other table by key
            if (importItem.NodeBase)
                return importItem.Table.Get(item.G(importItem.PropertyName)).Default_((object)null);
            // Return raw property (array or object)
            return item.G(importItem.PropertyName);
        }

        private static MapObject GetHashMap(
            GraphQLSelectionSet selectionSet,
            Type unsafeType)
        {
            var mapObject = new MapObject();
            var type = GetTypeIfArray(unsafeType);

            foreach (var selection in selectionSet.Selections)
            {
                if (!(selection is GraphQLFieldSelection fieldSelection)) continue;

                var name = GetName(fieldSelection.Name.Value, type);
                if (fieldSelection.SelectionSet == null)
                {
                    mapObject = mapObject.With(name, true);
                }
                else
                {
                    var property = type.GetProperty(name);
                    var ignore = property.GetCustomAttribute<JsonIgnoreAttribute>() != null;
                    if (ignore)
                        continue;

                    var newType = property.PropertyType;
                    mapObject = mapObject.With(name, GetHashMap(fieldSelection.SelectionSet, newType));
                }
            }

            return mapObject;
        }

        private static string GetName(string name, Type type)
        {
            var properties = type.GetProperties();
            var property = properties.First(d => string.Equals(d.Name, name, StringComparison.CurrentCultureIgnoreCase));
            var ret = property.GetJPropertyName();
            return ret;
        }

        private ImportTreeItem GetImportTree(Type unsafeType, MapObject hashMap, string rootProperty)
        {
            var type = GetTypeIfArray(unsafeType);

            var properties = hashMap.Select(d =>
            {
                var property = type.GetProperty(d.Key.ToString());
                return new { Property = property, HashMap = d.Value as MapObject };
            }).Where(d => d.HashMap != null).ToList();
            var importItems = properties
                .Select(d => GetImportTree(d.Property.PropertyType, d.HashMap, d.Property.Name)).ToList();
            var ret = new ImportTreeItem(
                GetTable(type),
                rootProperty,
                unsafeType.IsArray,
                type.IsNodeBase(),
                importItems
            );
            return ret;
        }

        private class ImportTreeItem
        {
            private readonly List<ImportTreeItem> _importItems;

            public ImportTreeItem(Table table, string propertyName, bool isArray, bool nodeBase, List<ImportTreeItem> importItems)
            {
                _importItems = importItems;
                Table = table;
                PropertyName = propertyName;
                IsArray = isArray;
                NodeBase = nodeBase;
                Clean();
            }

            public Table Table { get; }
            public bool NodeBase { get; }
            private bool HasNodeBase => NodeBase || ImportItems.Any(d => d.HasNodeBase);
            public string PropertyName { get; }
            public bool IsArray { get; }
            public IEnumerable<ImportTreeItem> ImportItems => _importItems;

            private void Clean()
            {
                _importItems.ForEach(d => d.Clean());
                var toRemove = _importItems.Where(d => !d.HasNodeBase).ToList();
                toRemove.ForEach(d => _importItems.Remove(d));
            }
        }

        private static Type GetTypeIfArray(Type type)
        {
            return type.IsArray ? type.GetElementType() : type;
        }
    }
}
