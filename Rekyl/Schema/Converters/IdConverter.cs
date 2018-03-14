using System;
using Newtonsoft.Json;

namespace Rekyl.Schema.Converters
{
    internal class IdConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString());
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var id = objectType.GetConstructor(new[] {typeof(string)}).Invoke(new[] {reader.Value.ToString()});
            return id;
        }

        public override bool CanConvert(Type objectType)
        {
            throw new NotImplementedException();
        }
    }
}