/// <summary>
/// Maps a refused Start result to the notice shown to the requesting Host.
/// Only <see cref="TownRaidStartResult.PartyUnavailable"/> carries a notice: the other refusals
/// are already prevented by the Start button state, so they stay silent.
/// </summary>
public static class TownRaidStartRejectionText
{
    public const string PartyUnavailableMessage =
        "No se pudo iniciar: un integrante todavía no tiene party en Town. Esperá unos segundos y volvé a intentar.";

    public static bool TryGetMessage(TownRaidStartResult result, out string message)
    {
        message = result == TownRaidStartResult.PartyUnavailable ? PartyUnavailableMessage : null;
        return message != null;
    }
}
