/// <summary>Authoritative semantic cause that may close Expedition Progression.</summary>
public enum ExpeditionProgressionFinalizationCause : byte
{
    None = 0,
    ExtractionConfirmed = 1,
    DefeatConfirmed = 2,
    VoluntaryAbandonConfirmed = 3,
    DefinitiveDisconnectConfirmed = 4
}
