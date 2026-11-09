using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Infrastructure.Parsers.Converters
{
    public class BooleanConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(bool);
        }

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            if (reader.Value is bool boolean) return boolean;
            var text = reader.Value?.ToString();
            if (bool.TryParse(text, out var result)) return result;
            if (long.TryParse(text, out var numeric)) return numeric != 0;
            throw new JsonSerializationException("Invalid boolean value: " + text);
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            writer.WriteValue(value);
        }
    }
}
