using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Grimhold.Backend
{
    /// <summary>Only this boundary understands the v2 string / v3 instance-entry union.</summary>
    public sealed class PreparedEquipmentJsonConverter : JsonConverter<PreparedEquipmentData>
    {
        public static string Name(EquipmentSlot slot) => slot switch
        {
            EquipmentSlot.WeaponSetAMainHand => "weaponSlot1", EquipmentSlot.WeaponSetBMainHand => "weaponSlot2",
            EquipmentSlot.WeaponSetAOffHand => "offHand1", EquipmentSlot.WeaponSetBOffHand => "offHand2",
            EquipmentSlot.Helmet => "helmet", EquipmentSlot.Armor => "armor", EquipmentSlot.Gloves => "gloves", EquipmentSlot.Boots => "boots",
            _ => throw new ArgumentOutOfRangeException(nameof(slot))
        };

        public override PreparedEquipmentData ReadJson(JsonReader reader, Type objectType, PreparedEquipmentData existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return default;
            var json = JObject.Load(reader);
            var data = new PreparedEquipmentData();
            foreach (var slot in EquipmentSlotRules.AllSlots)
            {
                var token = json[Name(slot)];
                if (token == null || token.Type == JTokenType.Null) continue;
                if (token.Type == JTokenType.String) data.Set(slot, token.Value<string>());
                else if (token is JObject entry)
                    data.Set(slot, entry.Value<string>("lootId"), entry["instance"]?.ToObject<EquipmentInstanceData>(serializer), true);
                else throw new JsonSerializationException("Invalid prepared equipment entry.");
            }
            return data;
        }

        public override void WriteJson(JsonWriter writer, PreparedEquipmentData value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            foreach (var slot in EquipmentSlotRules.AllSlots)
            {
                writer.WritePropertyName(Name(slot));
                string id = value.Get(slot);
                if (value.instances == null) writer.WriteValue(id ?? "");
                else if (string.IsNullOrEmpty(id)) writer.WriteNull();
                else
                {
                    writer.WriteStartObject(); writer.WritePropertyName("lootId"); writer.WriteValue(id);
                    writer.WritePropertyName("instance");
                    value.instances.TryGetValue(slot, out var instance);
                    serializer.Serialize(writer, instance);
                    writer.WriteEndObject();
                }
            }
            writer.WriteEndObject();
        }
    }
}
