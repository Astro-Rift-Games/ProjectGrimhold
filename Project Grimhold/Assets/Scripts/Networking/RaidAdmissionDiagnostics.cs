using Fusion;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>
/// FIX-06 passive diagnostics: one greppable trace line per Raid admission event so the arrival order of
/// the Client admission payload relative to the Host-side registration, and the authoritative-ticket
/// fallback, can be reproduced or ruled out from a Host log and a Client log. Logging only; never
/// changes admission behavior.
/// </summary>
/// <remarks>
/// Lines carry presence, lengths, outcome codes, the <c>PlayerRef</c>, the profile id (already logged by
/// the admission code), the Fusion tick and the realtime. Callers must never pass ticket contents,
/// tokens, credentials or raw payload bytes in <c>detail</c>.
/// </remarks>
public static class RaidAdmissionDiagnostics
{
    /// <summary>Greppable prefix shared by every FIX-06 trace line.</summary>
    public const string Prefix = "[RaidAdmissionTrace]";

    /// <summary>Placeholder for a value that is not known at the trace site.</summary>
    public const string Unknown = "n/a";

    /// <summary>Upper bound of the free-form detail so a trace stays one short line.</summary>
    public const int MaxDetailLength = 200;

    /// <summary>
    /// Formats one trace line:
    /// <c>[RaidAdmissionTrace] event=E player=P profile=F tick=T t=R detail=D</c>.
    /// A negative <paramref name="tick"/> or empty value renders as <see cref="Unknown"/>;
    /// <c>detail</c> is omitted when empty, flattened to one line and truncated.
    /// </summary>
    public static string FormatTrace(
        string traceEvent,
        string playerRef,
        string profile,
        int tick,
        float realtime,
        string detail)
    {
        var builder = new StringBuilder(160);
        builder.Append(Prefix)
            .Append(" event=").Append(OrUnknown(traceEvent))
            .Append(" player=").Append(OrUnknown(playerRef))
            .Append(" profile=").Append(OrUnknown(profile))
            .Append(" tick=").Append(tick >= 0 ? tick.ToString(CultureInfo.InvariantCulture) : Unknown)
            .Append(" t=").Append(realtime.ToString("F3", CultureInfo.InvariantCulture));

        string flattened = Flatten(detail);
        if (flattened.Length > 0)
        {
            builder.Append(" detail=").Append(flattened);
        }

        return builder.ToString();
    }

    /// <summary>Emits a trace line with the runner tick and current realtime. Main thread only.</summary>
    public static void Trace(
        NetworkRunner runner,
        string traceEvent,
        PlayerRef player,
        string profile = null,
        string detail = null,
        Object context = null)
    {
        int tick = runner != null && runner.IsRunning ? runner.Tick.Raw : -1;
        Debug.Log(
            FormatTrace(traceEvent, player.ToString(), profile, tick, Time.realtimeSinceStartup, detail),
            context);
    }

    private static string OrUnknown(string value) =>
        string.IsNullOrWhiteSpace(value) ? Unknown : value;

    private static string Flatten(string detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return string.Empty;
        }

        string flattened = detail.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return flattened.Length > MaxDetailLength
            ? flattened.Substring(0, MaxDetailLength)
            : flattened;
    }
}
