using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Grimhold.Backend
{
    /// <summary>Admission-only legacy string / ID-free unit union, not the durable instance format.</summary>
    public sealed class RaidPreparedEquipmentJsonConverter : JsonConverter<PreparedEquipmentDto>
    {
        public override PreparedEquipmentDto ReadJson(JsonReader reader, Type objectType, PreparedEquipmentDto existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return default;
            var json = JObject.Load(reader);
            var data = new PreparedEquipmentDto();
            foreach (var slot in EquipmentSlotRules.AllSlots)
            {
                var token = json[PreparedEquipmentJsonConverter.Name(slot)];
                if (token == null || token.Type == JTokenType.Null) continue;
                if (token.Type == JTokenType.String) data.Definitions.Set(slot, token.Value<string>());
                else if (token is JObject entry && entry["instance"] == null && entry["instanceId"] == null)
                {
                    data.Definitions.Set(slot, entry.Value<string>("lootId"));
                    data.Units ??= new Dictionary<EquipmentSlot, WeaponInstanceModifiersData>();
                    data.Units.Add(slot, entry["modifiers"]?.ToObject<WeaponInstanceModifiersData>(serializer));
                }
                else throw new JsonSerializationException("Invalid ID-free prepared equipment entry.");
            }
            return data;
        }

        public override void WriteJson(JsonWriter writer, PreparedEquipmentDto value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            foreach (var slot in EquipmentSlotRules.AllSlots)
            {
                writer.WritePropertyName(PreparedEquipmentJsonConverter.Name(slot));
                string id = value.Definitions.Get(slot);
                if (string.IsNullOrEmpty(id)) writer.WriteNull();
                else if (value.Units != null && value.Units.TryGetValue(slot, out var modifiers))
                {
                    writer.WriteStartObject(); writer.WritePropertyName("lootId"); writer.WriteValue(id);
                    if (modifiers != null) { writer.WritePropertyName("modifiers"); serializer.Serialize(writer, modifiers); }
                    writer.WriteEndObject();
                }
                else writer.WriteValue(id);
            }
            writer.WriteEndObject();
        }
    }
}
