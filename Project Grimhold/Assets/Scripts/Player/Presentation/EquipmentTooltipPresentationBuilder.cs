using System;
using System.Globalization;
using System.Text;

/// <summary>Builds definition and weapon instance equipment information for inventory presentation.</summary>
public static class EquipmentTooltipPresentationBuilder
{
    private const string InvalidConfigurationMessage =
        "Estadísticas no disponibles por configuración inválida";
    private const string NoStatisticsMessage = "Sin estadísticas de equipamiento";
    private static readonly CultureInfo SpanishCulture = CultureInfo.GetCultureInfo("es-ES");

    public static EquipmentTooltipPresentation Build(LootDefinition definition)
    {
        if (definition == null)
        {
            return new EquipmentTooltipPresentation(
                EquipmentTooltipPresentationStatus.UnresolvedDefinition,
                string.Empty,
                string.Empty);
        }

        string title = ResolveTitle(definition);

        if (definition.Category == LootCategory.Weapon)
        {
            return BuildWeapon(title, definition.WeaponDefinition);
        }

        if (definition.Category == LootCategory.Shield)
        {
            return BuildShield(title, definition.ShieldDefinition);
        }

        if (IsArmorCategory(definition.Category))
        {
            return BuildArmor(title, definition.ArmorDefinition);
        }

        return new EquipmentTooltipPresentation(
            EquipmentTooltipPresentationStatus.NoEquipmentStatistics,
            title,
            NoStatisticsMessage);
    }

    /// <summary>
    /// Builds a weapon instance tooltip with its own scaling, installed gems and the effective damage
    /// for the supplied attributes. Installed gems resolve their name and bonus from
    /// <paramref name="catalog"/>, which may be null only when no gem is installed.
    /// Non-weapon definitions use the definition-only presentation.
    /// </summary>
    public static EquipmentTooltipPresentation Build(
        LootDefinition definition,
        in WeaponInstanceModifiers instance,
        in CharacterAttributeState attributes,
        LootDefinitionCatalog catalog)
    {
        if (definition == null || definition.Category != LootCategory.Weapon)
        {
            return Build(definition);
        }

        string title = ResolveTitle(definition);
        WeaponDefinition weapon = definition.WeaponDefinition;
        if (weapon == null || !weapon.TryValidate(out _) ||
            !WeaponScalingContributionsResolver.TryResolve(
                instance,
                weapon.NaturalScalingAttribute,
                out WeaponScalingContributions contributions) ||
            !WeaponGemDamageBonusResolver.TryResolve(instance, catalog, out int gemDamageBonus) ||
            !WeaponDamageCalculator.TryCalculate(
                weapon.BaseDamage,
                gemDamageBonus,
                attributes,
                contributions,
                out float effectiveDamage))
        {
            return Invalid(title);
        }

        StringBuilder body = BuildWeaponBaseStatistics(weapon);
        body.Append("Escalado: ");
        body.Append(FormatScaling(instance));
        AppendLine(body, FormatGemSockets(instance, catalog));
        if (gemDamageBonus > 0)
        {
            AppendLine(body, $"Bono de gemas: +{gemDamageBonus}");
        }

        AppendLine(body, $"Daño efectivo: {FormatNumber(effectiveDamage)}");

        return new EquipmentTooltipPresentation(
            EquipmentTooltipPresentationStatus.FunctionalStatistics,
            title,
            body.ToString());
    }

    private static EquipmentTooltipPresentation BuildWeapon(string title, WeaponDefinition weapon)
    {
        if (weapon == null || !weapon.TryValidate(out _))
        {
            return Invalid(title);
        }

        StringBuilder body = BuildWeaponBaseStatistics(weapon);
        body.Append("Escalado: ");
        body.Append(FormatScaling(weapon.OffensiveScaling));

        return new EquipmentTooltipPresentation(
            EquipmentTooltipPresentationStatus.FunctionalStatistics,
            title,
            body.ToString());
    }

    private static StringBuilder BuildWeaponBaseStatistics(WeaponDefinition weapon)
    {
        var body = new StringBuilder(160);
        AppendLine(body, $"Daño base: {FormatNumber(weapon.BaseDamage)} ({FormatDamageType(weapon.DamageType)})");
        AppendLine(body, $"Intervalo: {FormatNumber(weapon.AttackIntervalSeconds)} s");
        AppendLine(body, $"Alcance: {FormatNumber(weapon.Range)}");
        AppendLine(body, $"Costo de Stamina: {FormatNumber(weapon.StaminaCost)}");
        AppendLine(body, weapon.Handedness == WeaponHandedness.TwoHanded ? "Manos: 2" : "Manos: 1");
        AppendLine(body, $"Requisito: {FormatRequirements(weapon.AttributeRequirements)}");
        body.AppendLine();
        return body;
    }

    private static EquipmentTooltipPresentation BuildArmor(string title, ArmorDefinition armor)
    {
        if (armor == null || !armor.TryValidate(out _))
        {
            return Invalid(title);
        }

        MaximumResourceModifier modifier = armor.MaximumResourceModifier;
        var body = new StringBuilder(96);
        AppendLine(body, $"Defensa Física: {armor.PhysicalDefense}");
        AppendLine(body, $"Defensa Mágica: {armor.MagicalDefense}");
        body.Append(FormatResourceBonus(modifier));

        return new EquipmentTooltipPresentation(
            EquipmentTooltipPresentationStatus.FunctionalStatistics,
            title,
            body.ToString());
    }

    private static EquipmentTooltipPresentation BuildShield(string title, ShieldDefinition shield)
    {
        if (shield == null || !shield.TryValidate(out _))
        {
            return Invalid(title);
        }

        var body = new StringBuilder(64);
        AppendLine(body, $"Reducción al defender: {FormatNumber(shield.DamageReduction * 100f)}%");
        body.Append($"Cono frontal: {FormatNumber(shield.DefensiveConeDegrees)}°");
        return new EquipmentTooltipPresentation(
            EquipmentTooltipPresentationStatus.FunctionalStatistics,
            title,
            body.ToString());
    }

    private static string ResolveTitle(LootDefinition definition) =>
        string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.name : definition.DisplayName;

    private static EquipmentTooltipPresentation Invalid(string title) =>
        new(
            EquipmentTooltipPresentationStatus.InvalidEquipmentConfiguration,
            title,
            InvalidConfigurationMessage);

    private static string FormatRequirements(in WeaponAttributeRequirements requirements)
    {
        var text = new StringBuilder(32);
        AppendRequirement(text, "FUE", requirements.MinimumStrength);
        AppendRequirement(text, "DES", requirements.MinimumDexterity);
        AppendRequirement(text, "INT", requirements.MinimumIntelligence);
        return text.Length == 0 ? "Ninguno" : text.ToString();
    }

    private static void AppendRequirement(StringBuilder text, string label, int value)
    {
        if (value <= 0)
        {
            return;
        }

        if (text.Length > 0)
        {
            text.Append(" · ");
        }

        text.Append(label);
        text.Append(' ');
        text.Append(value);
    }

    private static string FormatScaling(in WeaponOffensiveScaling scaling)
    {
        if (!scaling.HasScaling)
        {
            return "Sin escalado";
        }

        string attribute = FormatAttribute(scaling.Attribute);
        if (TryGetExactGrade(scaling.Coefficient, out WeaponScalingGrade grade))
        {
            return $"{attribute} {grade}";
        }

        return $"{attribute} ×{FormatNumber(scaling.Coefficient)}";
    }

    private static string FormatScaling(in WeaponInstanceModifiers instance)
    {
        if (!instance.HasPrimary)
        {
            return "Sin escalado";
        }

        string primary = FormatModifier(instance.Primary);
        return instance.HasSecondary ? $"{primary} · {FormatModifier(instance.Secondary)}" : primary;
    }

    private static string FormatModifier(in WeaponScalingModifier modifier) =>
        $"{FormatAttribute(modifier.Attribute)} {modifier.Grade}";

    /// <summary>Lists each socket in order: the installed gem's name, or "vacía" when empty.</summary>
    private static string FormatGemSockets(in WeaponInstanceModifiers instance, LootDefinitionCatalog catalog)
    {
        int socketCount = instance.GemSocketCount;
        if (socketCount == 0)
        {
            return "Ranuras: 0";
        }

        var sockets = new StringBuilder(48);
        bool anyOccupied = false;
        for (int index = 0; index < socketCount; index++)
        {
            if (index > 0)
            {
                sockets.Append(", ");
            }

            if (instance.TryGetGemSocket(index, out WeaponGemSocket socket) && socket.IsOccupied &&
                WeaponGemDamageBonusResolver.TryGetGemLoot(socket.GemLootId, catalog, out LootDefinition gem))
            {
                anyOccupied = true;
                sockets.Append(ResolveTitle(gem));
            }
            else
            {
                sockets.Append("vacía");
            }
        }

        if (!anyOccupied)
        {
            return socketCount > 1 ? $"Ranuras: {socketCount} (vacías)" : "Ranuras: 1 (vacía)";
        }

        return $"Ranuras: {socketCount} ({sockets})";
    }

    private static bool TryGetExactGrade(float coefficient, out WeaponScalingGrade grade)
    {
        if (coefficient == 0.25f) grade = WeaponScalingGrade.E;
        else if (coefficient == 0.40f) grade = WeaponScalingGrade.D;
        else if (coefficient == 0.55f) grade = WeaponScalingGrade.C;
        else if (coefficient == 0.70f) grade = WeaponScalingGrade.B;
        else if (coefficient == 0.85f) grade = WeaponScalingGrade.A;
        else if (coefficient == 1.00f) grade = WeaponScalingGrade.S;
        else
        {
            grade = WeaponScalingGrade.None;
            return false;
        }

        return true;
    }

    private static string FormatResourceBonus(in MaximumResourceModifier modifier)
    {
        string resource = modifier.Resource switch
        {
            MaximumResourceType.Health => "Vida máxima",
            MaximumResourceType.Stamina => "Stamina máxima",
            MaximumResourceType.Mana => "Mana máximo",
            _ => "Recurso máximo"
        };
        return $"Bono: +{modifier.Amount} {resource}";
    }

    private static string FormatDamageType(DamageType damageType) => damageType switch
    {
        DamageType.Physical => "Físico",
        DamageType.Magical => "Mágico",
        DamageType.TrueDamage => "Verdadero",
        _ => damageType.ToString()
    };

    private static string FormatAttribute(CharacterAttribute attribute) => attribute switch
    {
        CharacterAttribute.Vitality => "VIT",
        CharacterAttribute.Resistance => "RES",
        CharacterAttribute.Strength => "FUE",
        CharacterAttribute.Dexterity => "DES",
        CharacterAttribute.Intelligence => "INT",
        CharacterAttribute.Luck => "SUE",
        _ => attribute.ToString()
    };

    private static string FormatNumber(float value) => value.ToString("0.##", SpanishCulture);

    private static void AppendLine(StringBuilder body, string line)
    {
        if (body.Length > 0)
        {
            body.AppendLine();
        }

        body.Append(line);
    }

    private static bool IsArmorCategory(LootCategory category) =>
        category == LootCategory.Helmet ||
        category == LootCategory.Armor ||
        category == LootCategory.Gloves ||
        category == LootCategory.Boots;
}
