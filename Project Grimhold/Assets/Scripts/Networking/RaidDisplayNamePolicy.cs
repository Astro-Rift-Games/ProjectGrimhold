using System;
using System.Globalization;
using System.Text;

/// <summary>
/// Server-boundary policy for the character name shown to Raid peers (FIX-50 S3).
/// The name is display-only: it is never an identity, key or authorization input.
/// </summary>
public static class RaidDisplayNamePolicy
{
    /// <summary>Clamp applied before the name is replicated (fits <c>NetworkString&lt;_32&gt;</c>).</summary>
    public const int MaxLength = 24;

    /// <summary>
    /// Removes control/format characters, trims, and clamps to <see cref="MaxLength"/> without
    /// splitting a surrogate pair. Returns an empty string when nothing displayable remains.
    /// </summary>
    public static string Sanitize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(Math.Min(raw.Length, MaxLength));
        for (int index = 0; index < raw.Length; index++)
        {
            char current = raw[index];
            bool pair = char.IsHighSurrogate(current) && index + 1 < raw.Length &&
                        char.IsLowSurrogate(raw[index + 1]);
            UnicodeCategory category = pair
                ? CharUnicodeInfo.GetUnicodeCategory(raw, index)
                : CharUnicodeInfo.GetUnicodeCategory(current);
            int width = pair ? 2 : 1;

            bool rejected = category == UnicodeCategory.Control ||
                            category == UnicodeCategory.Format ||
                            category == UnicodeCategory.Surrogate ||
                            category == UnicodeCategory.PrivateUse ||
                            category == UnicodeCategory.OtherNotAssigned ||
                            category == UnicodeCategory.LineSeparator ||
                            category == UnicodeCategory.ParagraphSeparator;
            if (!rejected)
            {
                if (builder.Length + width > MaxLength)
                {
                    break;
                }

                builder.Append(raw, index, width);
            }

            index += width - 1;
        }

        return builder.ToString().Trim();
    }
}
