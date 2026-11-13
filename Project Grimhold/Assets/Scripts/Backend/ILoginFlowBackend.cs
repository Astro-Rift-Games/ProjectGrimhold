using System.Threading.Tasks;
using Grimhold.Backend;

/// <summary>
/// Backend and local-store calls made by the login hydration sequence. Exists so
/// <see cref="LoginFlowController"/> can be driven deterministically from EditMode tests;
/// production uses <see cref="StaticLoginFlowBackend"/>, which delegates to the static clients.
/// </summary>
internal interface ILoginFlowBackend
{
    Task<(bool success, LoginResult result, BackendError error)> PostLoginAsync(
        BackendConfiguration config, string username, string password);

    Task<(bool success, CharacterData data, BackendError error)> GetCharacterAsync(
        BackendConfiguration config, string token);

    Task<(bool success, CharacterProfileData data, BackendError error)> GetProfileAsync(
        BackendConfiguration config, string token);

    Task<(bool success, InventoryData data, BackendError error)> GetInventoryAsync(
        BackendConfiguration config, string token);

    Task<(bool success, ProgressionData data, BackendError error)> GetProgressionAsync(
        BackendConfiguration config, string token);

    Task<(bool success, TownSessionTicketResponse data, BackendError error)> GetTownSessionTicketAsync(
        BackendConfiguration config, string token);

    bool InitializeStash(ProfileId characterId, InventoryData inventory, ProgressionData progression);
}
