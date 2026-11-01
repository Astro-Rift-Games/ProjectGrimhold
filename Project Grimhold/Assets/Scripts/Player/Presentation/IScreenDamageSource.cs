public interface IScreenDamageSource
{
    bool TryRead(out float health, out float maximum);
}
