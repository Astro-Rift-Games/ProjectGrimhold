using System;
using System.Collections.Generic;

/// <summary>Player-facing copy and item lines of the Town loss notice.</summary>
public sealed class TownRaidLossNoticePresentation
{
    private const string TitleText = "Incursión perdida";
    private const string StartFailedBody =
        "La incursión no pudo iniciarse. Perdiste el equipamiento que llevabas:";
    private const string ConnectionLostBody =
        "Se perdió la conexión con la incursión. Perdiste el equipamiento que llevabas:";
    private const string ContinueText = "Continuar";

    private TownRaidLossNoticePresentation(string body, IReadOnlyList<string> lines)
    {
        Body = body;
        Lines = lines;
    }

    public string Title => TitleText;
    public string Body { get; }
    public string ContinueLabel => ContinueText;
    public IReadOnlyList<string> Lines { get; }

    /// <param name="displayNameOf">Resolves a localized item name; null or empty falls back to the LootId.</param>
    public static TownRaidLossNoticePresentation Build(
        RaidLossNotice notice,
        Func<LootId, string> displayNameOf)
    {
        string body = notice.Reason == RaidLossNoticeReason.ConnectionLost
            ? ConnectionLostBody
            : StartFailedBody;
        var lines = new List<string>(notice.Lost.Entries.Count);
        for (int index = 0; index < notice.Lost.Entries.Count; index++)
        {
            RaidResultsLootEntry entry = notice.Lost.Entries[index];
            string name = displayNameOf?.Invoke(entry.LootId);
            if (string.IsNullOrEmpty(name))
            {
                name = entry.LootId.Value;
            }

            lines.Add(entry.Amount > 1 ? $"{name} x{entry.Amount}" : name);
        }

        return new TownRaidLossNoticePresentation(body, lines);
    }
}
