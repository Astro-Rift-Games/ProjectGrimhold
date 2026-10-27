using System;

/// <summary>
/// Reglas puras para la identificación de elementos arcanos compatibles con el Detector Arcano (Doc 14 §13).
/// </summary>
public static class ArcaneElementDetectionRules
{
    public const float DetectionRadius = 10.0f;
    public const float HighlightDuration = 5.0f;

    /// <summary>
    /// Determina si una definición de loot corresponde a un elemento arcano detectable.
    /// </summary>
    public static bool IsArcaneDefinition(LootDefinition definition)
    {
        if (definition == null)
        {
            return false;
        }

        if (string.Equals(definition.Family, "ArcaneComponents", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(definition.Family, "Arcane", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (definition.Tags != null)
        {
            for (int i = 0; i < definition.Tags.Count; i++)
            {
                if (string.Equals(definition.Tags[i], "arcane", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
