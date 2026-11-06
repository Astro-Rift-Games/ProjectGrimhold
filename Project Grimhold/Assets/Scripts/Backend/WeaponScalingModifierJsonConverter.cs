using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Grimhold.Backend
{
    public sealed class WeaponScalingModifierJsonConverter : JsonConverter<WeaponScalingModifierData>
    {
        public override WeaponScalingModifierData ReadJson(JsonReader reader, Type objectType, WeaponScalingModifierData existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var json = JObject.Load(reader);
            var value = new WeaponScalingModifierData { attribute = json.Value<string>("attribute"), grade = json.Value<string>("grade") };
            if (!value.TryToModifier(out _, out var error)) throw new JsonSerializationException(error);
            return value;
        }
        public override void WriteJson(JsonWriter writer, WeaponScalingModifierData value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("attribute"); writer.WriteValue(value.attribute);
            writer.WritePropertyName("grade"); writer.WriteValue(value.grade);
            writer.WriteEndObject();
        }
    }
}
