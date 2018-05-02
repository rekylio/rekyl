using System;
using GraphQL.Conventions;
using GraphQLParser.AST;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rekyl.Schema;
using Rekyl.Schema.Types;

namespace Rekyl.Database
{
    public partial class DbContext
    {
        public T AddDefault<T>(T item) where T : NodeBase
        {
            var type = typeof(T);
            var table = GetTable(type);
            Utils.InitalizeArrays(item);
            var jObject = JObject.FromObject(item, new JsonSerializer
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Serialize
            });

            var jToken = Utils.ChangeTypeBaseItemsToIds(type, jObject);
            var result = table.Insert(jToken).RunResult(_connection);
            if (result.Errors > 0)
            {
                throw new Exception("Something went wrong");
            }

            return item;
        }

        public T UpdateDeafult<T>(T item, Id replaces)
        {
            var type = typeof(T);
            var table = GetTable(type);
            Utils.InitalizeArrays(item);
            var jObject = JObject.FromObject(item, new JsonSerializer
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Serialize
            });
            jObject["id"] = replaces.ToString();

            var jToken = Utils.ChangeTypeBaseItemsToIds(type, jObject);
            var result = table.Get(replaces.ToString()).Update(jToken).RunResult(_connection);
            if (result.Errors > 0)
            {
                throw new Exception("Something went wrong");
            }

            Utils.ForceSetValue(item, "Id", replaces);
            return item;
        }

        public T ReadByIdDefault<T>(Id id, UserContext.ReadType readType, GraphQLDocument document) where T : class
        {
            var selectionSet = document != null ? GetSelectionSet(document) : null;

            switch (readType)
            {
                case UserContext.ReadType.WithDocument:
                    return GetWithDocument<T>(selectionSet, id);
                case UserContext.ReadType.Shallow:
                    return GetShallow<T>(id);
                case UserContext.ReadType.Full:
                    return GetFull<T>(id);
                default:
                    throw new ArgumentOutOfRangeException(nameof(readType), readType, null);
            }
        }

        public void Remove<T>(Id id)
        {
            var type = typeof(T);
            if (!id.IsIdentifierForType<T>())
                throw new Exception($"Id is not valid for type {type.Name}");
            var result = GetTable(typeof(T)).Get(id.ToString()).Delete().RunResult(_connection);
            if (result.Errors > 0)
            {
                throw new Exception("Something went wrong");
            }
        }
    }
}
