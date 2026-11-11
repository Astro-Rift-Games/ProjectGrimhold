using System;

/// <summary>
/// Text of the notice shown to the remaining participants after a Host Migration (FIX-50 S3).
/// Pure formatting; presentation only.
/// </summary>
public static class HostHandoffAnnouncement
{
    /// <summary>Used when the departed Host's character name is not available on this peer.</summary>
    public const string UnknownDepartedName = "El Host";

    /// <summary>Used when the new Host's character name is not available on this peer.</summary>
    public const string UnknownNewHostName = "Otro jugador";

    public static string Format(string departedName, string newHostName)
    {
        return $"{Resolve(departedName, UnknownDepartedName)} salió. " +
               $"{Resolve(newHostName, UnknownNewHostName)} es el nuevo Host.";
    }

    private static string Resolve(string name, string fallback)
    {
        return string.IsNullOrWhiteSpace(name) ? fallback : name.Trim();
    }
}

/// <summary>
/// Runner-scoped, single-delivery mailbox between the Host Migration lifecycle and the Raid HUD.
/// It carries presentation text only and never drives gameplay.
/// </summary>
public sealed class HostHandoffNoticeSlot
{
    private string _pending;

    public void Publish(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _pending = message;
    }

    public bool TryConsume(out string message)
    {
        message = _pending;
        _pending = null;
        return message != null;
    }
}
