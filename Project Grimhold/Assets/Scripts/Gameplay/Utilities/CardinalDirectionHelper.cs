using UnityEngine;

/// <summary>
/// Provee cálculos trigonométricos puros y deterministas para determinar la orientación cardinal (Doc 14).
/// </summary>
public static class CardinalDirectionHelper
{
    public const float DisplayDuration = 5.0f;

    /// <summary>
    /// Calcula la dirección cardinal de 8 vías desde la posición origen hacia el destino.
    /// Retorna <see cref="CardinalDirection.None"/> si las posiciones coinciden.
    /// </summary>
    public static CardinalDirection GetCardinalDirection(Vector2 from, Vector2 to)
    {
        Vector2 diff = to - from;
        if (diff.sqrMagnitude < 0.0001f)
        {
            return CardinalDirection.None;
        }

        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
        if (angle < 0f)
        {
            angle += 360f;
        }

        float shifted = angle + 22.5f;
        if (shifted >= 360f)
        {
            shifted -= 360f;
        }

        int octant = (int)(shifted / 45f);
        return octant switch
        {
            0 => CardinalDirection.East,
            1 => CardinalDirection.NorthEast,
            2 => CardinalDirection.North,
            3 => CardinalDirection.NorthWest,
            4 => CardinalDirection.West,
            5 => CardinalDirection.SouthWest,
            6 => CardinalDirection.South,
            7 => CardinalDirection.SouthEast,
            _ => CardinalDirection.None
        };
    }

    /// <summary>
    /// Retorna el nombre localizado en español para la dirección cardinal.
    /// </summary>
    public static string GetLocalizedName(CardinalDirection direction) => direction switch
    {
        CardinalDirection.North => "Norte",
        CardinalDirection.NorthEast => "Noreste",
        CardinalDirection.East => "Este",
        CardinalDirection.SouthEast => "Sureste",
        CardinalDirection.South => "Sur",
        CardinalDirection.SouthWest => "Suroeste",
        CardinalDirection.West => "Oeste",
        CardinalDirection.NorthWest => "Noroeste",
        _ => "Desconocido"
    };

    /// <summary>
    /// Retorna la abreviación cardinal estándar (N, NE, E, SE, S, SW, W, NW).
    /// </summary>
    public static string GetAbbreviation(CardinalDirection direction) => direction switch
    {
        CardinalDirection.North => "N",
        CardinalDirection.NorthEast => "NE",
        CardinalDirection.East => "E",
        CardinalDirection.SouthEast => "SE",
        CardinalDirection.South => "S",
        CardinalDirection.SouthWest => "SW",
        CardinalDirection.West => "W",
        CardinalDirection.NorthWest => "NW",
        _ => "?"
    };
}
