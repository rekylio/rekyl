using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using GraphQL;
using GraphQL.Conventions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rekyl.Attributes;
using Rekyl.Schema.Types;

namespace Rekyl.Schema
{
    public static class Utils
    {
        private const BindingFlags Flags =
            BindingFlags.Instance
            | BindingFlags.GetProperty
            | BindingFlags.SetProperty
            | BindingFlags.GetField
            | BindingFlags.SetField
            | BindingFlags.NonPublic;

        public static bool UsesDefaultDbRead(this Type type)
        {
            if (!type.IsNodeBase() || type.IsAbstract)
                return false;
            var tableAttribute = type.GetTypeInfo().GetCustomAttribute<TableAttribute>();
            return tableAttribute?.UseDefaultDbRead ?? true;
        }

        private static object CreateEmptyObject(Type type)
        {
            return FormatterServices.GetUninitializedObject(type);
        }

        public static void InitalizeArrays(object item)
        {
            var arrayProperties = item.GetType().GetProperties().Where(d => d.PropertyType.IsArray).ToList();
            foreach (var arrayProperty in arrayProperties)
            {
                if (arrayProperty.GetCustomAttribute<JsonIgnoreAttribute>() != null)
                    continue;
                var value = arrayProperty.GetValue(item);
                if (value == null)
                {
                    var newArray = Array.CreateInstance(arrayProperty.PropertyType.GetElementType(), 0);
                    ForceSetValue(item, arrayProperty.Name, newArray);
                }
            }
        }

        public static Id CreateNewId<T>()
        {
            var str = RandomIdGenerator.GetBase36(8);
            var ret = Id.New<T>(str);
            return ret;
        }

        public static object CreateDummyObject(Type type, Id id)
        {
            if (!id.IsIdentifierForType(type))
            {
                throw new ArgumentException($"Id is not identifyer for type {type}");
            }
            var item = CreateEmptyObject(type);
            ForceSetValue(item, "Id", id);
            return item;
        }

        internal static void ForceSetValue(object item, string propertyName, object value)
        {
            var fields = GetFields(item.GetType());
            var field = fields.First(d => d.Name.StartsWith($"<{propertyName}>"));
            field.SetValue(item, value);
        }

        public static T CreateDummyObject<T>(Id id) where T : class
        {
            var item = CreateDummyObject(typeof(T), id);
            return item as T;
        }

        public static T[] AddOrInitializeArray<T>(T[] array, params T[] items)
        {
            var list = new List<T>(array ?? new T[0]);
            list.AddRange(items);
            return list.ToArray();
        }

        public static object DeserializeObject(Type type, JToken jToken)
        {
            switch (jToken.Type)
            {
                case JTokenType.Object:
                    return HandleObject(type, jToken);
                case JTokenType.Array:
                    return HandleArray(type, jToken);
                case JTokenType.String:
                    return HandleString(type, jToken);
                case JTokenType.Null:
                    return null;
                case JTokenType.Integer:
                    return HandleDefault(type, jToken);
                case JTokenType.Boolean:
                    return HandleDefault(type, jToken);
                case JTokenType.Float:
                    return HandleDefault(type, jToken);
            }
            throw new NotImplementedException($"Type: {jToken.Type.ToString()} is not handled yet");
        }

        private static object HandleDefault(Type type, JToken jToken)
        {
            var ret = type.GetMethod("Parse", new[] { typeof(string) }).Invoke(null, new object[] { jToken.ToString() });
            return ret;
        }

        private static object HandleString(Type type, JToken jToken)
        {
            var strVal = jToken.GetValue().ToString();
            if (type == typeof(Id) || type == typeof(Id?))
                return new Id(strVal);
            if (!type.IsNodeBase()) return strVal;

            var dummyRet = CreateDummyObject(type, new Id(strVal));
            return dummyRet;
        }

        private static object HandleArray(Type type, JToken jToken)
        {
            var arrayType = type.GetElementType();
            var arrayValues = jToken.ToList();
            var arrayRetTemp = arrayValues.Select(d => DeserializeObject(arrayType, d)).ToArray();
            var arrayRet = Array.CreateInstance(arrayType, arrayRetTemp.Length);
            for (var index = 0; index < arrayRetTemp.Length; index++)
            {
                arrayRet.SetValue(arrayRetTemp[index], index);
            }
            return arrayRet;
        }

        private static object HandleObject(Type type, JToken jToken)
        {
            if (type == typeof(DateTime))
            {
                // TODO: this is really just a hack, needs to be overhauled
                var epochdate = jToken.Children().Cast<JProperty>().First(d=>d.Name=="epoch_time").Value.ToString();
                var timezoneValue = jToken.Children().Cast<JProperty>().First(d => d.Name == "timezone")
                    .Value.ToString();
                var sign = timezoneValue.Substring(0, 1);
                var hours = Convert.ToInt32(timezoneValue.Substring(1, 2));
                var minutes = Convert.ToInt32(timezoneValue.Substring(4, 2));
                var timeZoneTime = (hours * 60 + minutes) * (sign == "+" ? 1 : -1);
                var date = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    .AddSeconds(Convert.ToDouble(epochdate))
                    .AddMinutes(timeZoneTime);
                return date;
            }

            var ret = CreateEmptyObject(type);
            var properties = type.GetProperties();
            foreach (var property in properties)
            {
                var jPropertyName = property.GetJPropertyName();
                var value = jToken[jPropertyName];
                if (value == null) continue;

                var deserializedValue = DeserializeObject(property.PropertyType, value);
                ForceSetValue(ret, property.Name, deserializedValue);
            }
            return ret;
        }

        public static bool IsNodeBase(this Type type)
        {
            return typeof(NodeBase).IsAssignableFrom(type)
                || typeof(NodeBase).IsAssignableFrom(type.GetElementType());
        }

        public static string GetJPropertyName(this PropertyInfo propertyInfo)
        {
            return propertyInfo.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName ?? propertyInfo.Name;
        }

        public static IEnumerable<FieldInfo> GetFields(Type type)
        {
            var ret = new List<FieldInfo>(type.GetFields(Flags));
            if (type.BaseType != null)
            {
                ret.AddRange(GetFields(type.BaseType));
            }
            return ret.ToArray();
        }

        public static JToken ChangeTypeBaseItemsToIds(Type type, JToken jToken)
        {
            var ret = jToken.DeepClone();
            ChangeTypeBaseItemsToIds(type, ret, true);
            return ret;
        }

        private static void ChangeTypeBaseItemsToIds(Type type, JToken jToken, bool root)
        {
            if (!root && type.IsNodeBase())
            {
                JValue newToken = null;
                if (jToken.Any())
                {
                    var id = jToken["id"];
                    var value = id.ToString();
                    newToken = new JValue(value);
                }
                
                jToken.Replace(newToken);
                return;
            }

            var jProperties = jToken.ToList();
            var properties = type.GetProperties().ToList();
            foreach (var jProperty in jProperties)
            {
                if (jProperty == null) continue;
                var property = properties.FirstOrDefault(d => d.GetJPropertyName() == GetJPropertyName(jProperty));
                if (property == null) continue;

                if (property.PropertyType.IsArray)
                {
                    var propertyType = property.PropertyType.GetElementType();
                    var list = jProperty.Values().Where(d => d.Type != JTokenType.Null).ToList();
                    list.ForEach(d => ChangeTypeBaseItemsToIds(propertyType, d, false));
                }
                else
                {
                    ChangeTypeBaseItemsToIds(property.PropertyType, jProperty.Single(), false);
                }
            }
        }

        private static string GetJPropertyName(JToken jToken)
        {
            var ret = (jToken as JProperty)?.Name;
            return ret;
        }
    }
}
