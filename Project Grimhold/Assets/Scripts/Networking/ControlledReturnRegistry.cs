using System.Collections.Generic;

/// <summary>
/// Stores one-shot controlled-return authorizations and generation-scoped terminal profiles.
/// This registry is owned by one NetworkSpawnManager and never crosses runner lifetimes.
/// </summary>
public sealed class ControlledReturnRegistry
{
    private readonly HashSet<ControlledReturnKey> _pending = new();
    private readonly HashSet<ControlledReturnKey> _terminal = new();

    public bool TryRegister(in ControlledReturnKey key) => key.IsValid && _pending.Add(key);

    public bool TryConsume(in ControlledReturnKey key) => key.IsValid && _pending.Remove(key);

    public bool MarkTerminal(in ControlledReturnKey key) => key.IsValid && _terminal.Add(key);

    public bool IsTerminal(in ControlledReturnKey key) => key.IsValid && _terminal.Contains(key);

    public void Clear()
    {
        _pending.Clear();
        _terminal.Clear();
    }
}

