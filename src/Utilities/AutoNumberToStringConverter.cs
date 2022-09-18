﻿//From https://stackoverflow.com/questions/59097784/system-text-json-deserialize-json-with-automatic-casting
namespace DBAnonymizer.Utilities
{
    public class AutoNumberToStringConverter : JsonConverter<object>
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeof(string) == typeToConvert;
        }
        public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                return reader.TryGetInt64(out long l) ? l.ToString() : reader.GetDouble().ToString();
            }
            if (reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString()!;
            }
            using (JsonDocument document = JsonDocument.ParseValue(ref reader))
            {
                return document?.RootElement.Clone().ToString()!;
            }
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            writer?.WriteStringValue(value?.ToString());
        }
    }
}
