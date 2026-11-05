using System;

namespace Grimhold.Backend
{
    [Serializable]
    public sealed class EquipmentInstanceData
    {
        public string instanceId;
        public WeaponInstanceModifiersData modifiers;

        public static EquipmentInstanceData FromInstance(EquipmentInstance instance) => instance == null ? null : new()
        {
            instanceId = instance.InstanceId,
            modifiers = instance.Modifiers.HasValue ? WeaponInstanceModifiersData.FromModifiers(instance.Modifiers.Value) : null
        };

        public bool TryToInstance(LootDefinition definition, bool requireIdentity, out EquipmentInstance instance, out string error, LootDefinitionCatalog catalog = null)
        {
            instance = null;
            error = "Invalid equipment instance.";
            if (!EquipmentInstance.IsEquipment(definition) ||
                (requireIdentity ? !EquipmentInstance.IsValidId(instanceId) : !string.IsNullOrEmpty(instanceId) && !EquipmentInstance.IsValidId(instanceId))) return false;
            WeaponInstanceModifiers? value = null;
            if (modifiers != null)
            {
                if (definition.Category != LootCategory.Weapon || definition.WeaponDefinition == null ||
                    !modifiers.TryToModifiers(definition.WeaponDefinition.NaturalScalingAttribute, out var parsed, out error)) return false;
                if (catalog != null)
                    foreach (string gem in modifiers.gems)
                        if (gem != null && (!catalog.TryGet(gem, out var gemDefinition) || gemDefinition.GemDefinition == null))
                        { error = "Installed gem is not a catalog gem."; return false; }
                value = parsed;
            }
            instance = new EquipmentInstance(instanceId, value);
            error = null;
            return true;
        }
    }

    [Serializable]
    [Newtonsoft.Json.JsonConverter(typeof(WeaponScalingModifierJsonConverter))]
    public sealed class WeaponScalingModifierData
    {
        public string attribute;
        public string grade;

        public bool TryToModifier(out WeaponScalingModifier modifier, out string error)
        {
            modifier = default;
            error = "Invalid weapon scaling attribute or grade.";
            // Enum.TryParse alone accepts numeric strings and is not a wire-format validator.
            if (!Enum.TryParse(attribute, true, out CharacterAttribute parsedAttribute) ||
                parsedAttribute.ToString().ToLowerInvariant() != attribute ||
                !Enum.TryParse(grade, out WeaponScalingGrade parsedGrade) || parsedGrade.ToString() != grade)
                return false;
            return WeaponScalingModifier.TryCreate(parsedAttribute, parsedGrade, out modifier, out error);
        }

        public static WeaponScalingModifierData FromModifier(WeaponScalingModifier value) => !value.IsPresent ? null : new()
        { attribute = value.Attribute.ToString().ToLowerInvariant(), grade = value.Grade.ToString() };
    }

    [Serializable]
    public sealed class WeaponInstanceModifiersData
    {
        public WeaponScalingModifierData primary;
        public WeaponScalingModifierData secondary;
        public int gemSocketCount;
        public string[] gems;

        public bool TryToModifiers(CharacterAttribute naturalAttribute, out WeaponInstanceModifiers value, out string error)
        {
            value = default;
            error = "Invalid weapon modifier sockets.";
            WeaponScalingModifier first = default, second = default;
            if (primary != null && !primary.TryToModifier(out first, out error) ||
                secondary != null && !secondary.TryToModifier(out second, out error)) return false;
            if (gemSocketCount < 0 || gemSocketCount > 2 || gems == null || gems.Length != gemSocketCount) return false;
            WeaponGemSocket socket0 = default, socket1 = default;
            if (gemSocketCount > 0 && gems[0] != null &&
                !WeaponGemSocket.TryCreateOccupied(new LootId(gems[0]), out socket0, out error)) return false;
            if (gemSocketCount > 1 && gems[1] != null &&
                !WeaponGemSocket.TryCreateOccupied(new LootId(gems[1]), out socket1, out error)) return false;
            return WeaponInstanceModifiers.TryCreate(first, second, naturalAttribute, gemSocketCount, socket0, socket1, out value, out error);
        }

        public static WeaponInstanceModifiersData FromModifiers(WeaponInstanceModifiers value)
        {
            var gems = new string[value.GemSocketCount];
            for (int i = 0; i < gems.Length; i++)
                if (value.TryGetGemSocket(i, out var socket) && socket.IsOccupied) gems[i] = socket.GemLootId.Value;
            return new WeaponInstanceModifiersData
            { primary = WeaponScalingModifierData.FromModifier(value.Primary), secondary = WeaponScalingModifierData.FromModifier(value.Secondary),
                gemSocketCount = value.GemSocketCount, gems = gems };
        }
    }
}
