/// <summary>Checks ownership and spawn validity before touching any Fusion properties.</summary>
public sealed class PlayerScreenDamageSource : IScreenDamageSource
{
    private readonly PlayerCharacter _character;
    public PlayerScreenDamageSource(PlayerCharacter character) => _character = character;
    public bool TryRead(out float health, out float maximum)
    {
        health = maximum = 0;
        if (_character == null || _character.Object == null || !_character.Object.IsValid ||
            !_character.HasInputAuthority || !_character.IsAlive || _character.IsDowned) return false;
        health = _character.Health;
        maximum = _character.MaxHealth;
        return true;
    }
}
