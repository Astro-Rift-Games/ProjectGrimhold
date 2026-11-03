/// <summary>
/// Deterministically generates legal Dungeon weapon variants (Doc 09 §8.2, §21, §22) from a
/// weapon template, a seed and Loot/Balance-owned rules. It never mutates the template.
/// </summary>
public static class WeaponInstanceGenerator
{
    private static readonly CharacterAttribute[] Attributes =
    {
        CharacterAttribute.Vitality,
        CharacterAttribute.Resistance,
        CharacterAttribute.Strength,
        CharacterAttribute.Dexterity,
        CharacterAttribute.Intelligence,
        CharacterAttribute.Luck
    };

    private struct DeterministicRandom
    {
        private ulong _state;

        public DeterministicRandom(ulong seed)
        {
            _state = seed;
        }

        public ulong NextUInt64()
        {
            unchecked
            {
                _state += 0x9E3779B97F4A7C15UL;
                ulong value = _state;
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                return value ^ (value >> 31);
            }
        }

        public int NextBelow(int exclusiveUpperBound)
        {
            ulong bound = (ulong)exclusiveUpperBound;
            ulong threshold = unchecked(0UL - bound) % bound;
            while (true)
            {
                ulong value = NextUInt64();
                if (value >= threshold)
                {
                    return (int)(value % bound);
                }
            }
        }

        public bool Roll(int chanceBasisPoints) =>
            chanceBasisPoints > 0 &&
            NextBelow(WeaponInstanceGenerationRules.MaxBasisPoints) < chanceBasisPoints;
    }

    public static bool TryGenerate(
        LootDefinition template,
        ulong seed,
        in WeaponInstanceGenerationRules rules,
        out WeaponInstanceModifiers instance,
        out string error)
    {
        instance = default;
        if (template == null || template.Category != LootCategory.Weapon || template.WeaponDefinition == null)
        {
            error = "Weapon instance generation requires a weapon template with a WeaponDefinition.";
            return false;
        }

        CharacterAttribute naturalAttribute = template.WeaponDefinition.NaturalScalingAttribute;
        if (naturalAttribute != CharacterAttribute.Strength &&
            naturalAttribute != CharacterAttribute.Dexterity &&
            naturalAttribute != CharacterAttribute.Intelligence)
        {
            error = $"Weapon template natural attribute '{naturalAttribute}' must be Strength, Dexterity or Intelligence.";
            return false;
        }

        if (!rules.TryValidate(out error))
        {
            return false;
        }

        var random = new DeterministicRandom(seed);
        WeaponScalingModifier primary = default;
        WeaponScalingModifier secondary = default;
        if (random.Roll(rules.PrimaryScalingChanceBasisPoints) &&
            !TryCreateModifier(naturalAttribute, rules, ref random, out primary, out error))
        {
            return false;
        }

        if (primary.IsPresent &&
            random.Roll(rules.SecondaryScalingChanceBasisPoints) &&
            !TryCreateModifier(PickSecondaryAttribute(naturalAttribute, ref random), rules, ref random, out secondary, out error))
        {
            return false;
        }

        int gemSocketCount = PickWeighted(rules, WeightTable.GemSocketCount, ref random);
        WeaponGemSocket firstGem = default;
        WeaponGemSocket secondGem = default;
        if ((gemSocketCount > 0 && !TryRollGem(rules, ref random, out firstGem, out error)) ||
            (gemSocketCount > 1 && !TryRollGem(rules, ref random, out secondGem, out error)))
        {
            return false;
        }

        return WeaponInstanceModifiers.TryCreate(
            primary,
            secondary,
            naturalAttribute,
            gemSocketCount,
            firstGem,
            secondGem,
            out instance,
            out error);
    }

    private static bool TryCreateModifier(
        CharacterAttribute attribute,
        in WeaponInstanceGenerationRules rules,
        ref DeterministicRandom random,
        out WeaponScalingModifier modifier,
        out string error)
    {
        int gradeIndex = PickWeighted(rules, WeightTable.Grade, ref random);
        return WeaponScalingModifier.TryCreate(
            attribute,
            WeaponScalingGrade.E + gradeIndex,
            out modifier,
            out error);
    }

    private static CharacterAttribute PickSecondaryAttribute(
        CharacterAttribute primaryAttribute,
        ref DeterministicRandom random)
    {
        int index = random.NextBelow(Attributes.Length - 1);
        CharacterAttribute attribute = Attributes[index];
        return attribute == primaryAttribute ? Attributes[Attributes.Length - 1] : attribute;
    }

    private static bool TryRollGem(
        in WeaponInstanceGenerationRules rules,
        ref DeterministicRandom random,
        out WeaponGemSocket socket,
        out string error)
    {
        socket = default;
        error = null;
        if (!random.Roll(rules.GemInstallChanceBasisPoints))
        {
            return true;
        }

        LootId gem = rules.GetGem(random.NextBelow(rules.GemPoolCount));
        return WeaponGemSocket.TryCreateOccupied(gem, out socket, out error);
    }

    private static int PickWeighted(
        in WeaponInstanceGenerationRules rules,
        WeightTable table,
        ref DeterministicRandom random)
    {
        int count = table == WeightTable.Grade
            ? WeaponInstanceGenerationRules.GradeWeightCount
            : WeaponInstanceGenerationRules.GemSocketCountWeightCount;
        int total = 0;
        for (int index = 0; index < count; index++)
        {
            total += GetWeight(rules, table, index);
        }

        int roll = random.NextBelow(total);
        for (int index = 0; index < count; index++)
        {
            roll -= GetWeight(rules, table, index);
            if (roll < 0)
            {
                return index;
            }
        }

        return count - 1;
    }

    private static int GetWeight(in WeaponInstanceGenerationRules rules, WeightTable table, int index) =>
        table == WeightTable.Grade ? rules.GetGradeWeight(index) : rules.GetGemSocketCountWeight(index);

    private enum WeightTable
    {
        Grade,
        GemSocketCount
    }
}
