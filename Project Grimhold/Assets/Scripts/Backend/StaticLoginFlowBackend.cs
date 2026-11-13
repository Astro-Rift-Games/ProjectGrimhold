using System.Threading.Tasks;
using Grimhold.Backend;

/// <summary>Production <see cref="ILoginFlowBackend"/>: delegates to the static backend clients and stash bootstrapper.</summary>
internal sealed class StaticLoginFlowBackend : ILoginFlowBackend
{
    public static readonly StaticLoginFlowBackend Instance = new();

    private StaticLoginFlowBackend() { }

    public Task<(bool success, LoginResult result, BackendError error)> PostLoginAsync(
        BackendConfiguration config, string username, string password)
        => AuthenticationClient.PostLoginAsync(config, username, password);

    public Task<(bool success, CharacterData data, BackendError error)> GetCharacterAsync(
        BackendConfiguration config, string token)
        => CharacterClient.GetCharacterAsync(config, token);

    public Task<(bool success, CharacterProfileData data, BackendError error)> GetProfileAsync(
        BackendConfiguration config, string token)
        => CharacterClient.GetProfileAsync(config, token);

    public Task<(bool success, InventoryData data, BackendError error)> GetInventoryAsync(
        BackendConfiguration config, string token)
        => InventoryClient.GetInventoryAsync(config, token);

    public Task<(bool success, ProgressionData data, BackendError error)> GetProgressionAsync(
        BackendConfiguration config, string token)
        => ProgressionClient.GetProgressionAsync(config, token);

    public Task<(bool success, TownSessionTicketResponse data, BackendError error)> GetTownSessionTicketAsync(
        BackendConfiguration config, string token)
        => CharacterClient.GetTownSessionTicketAsync(config, token);

    public bool InitializeStash(ProfileId characterId, InventoryData inventory, ProgressionData progression)
        => ApplicationStashServiceBootstrapper.InitializeWithProfile(
            characterId, inventory, progression, allowCorruptCacheRecovery: true);
}
