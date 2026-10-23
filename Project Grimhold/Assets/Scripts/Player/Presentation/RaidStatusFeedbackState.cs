using System;

/// <summary>Pure local feedback timing and confirmed-state baseline. It never predicts a gameplay outcome.</summary>
public sealed class RaidStatusFeedbackState
{
    private bool _hasWeaponSet;
    private WeaponSetSlot _weaponSet;
    private string _notice = string.Empty;
    private float _noticeUntil;

    public bool ObserveWeaponSet(WeaponSetSlot confirmedSet)
    {
        bool changed = _hasWeaponSet && _weaponSet != confirmedSet;
        _hasWeaponSet = true;
        _weaponSet = confirmedSet;
        return changed;
    }

    public void ShowNotice(string message, float now, float duration)
    {
        _notice = message ?? string.Empty;
        _noticeUntil = now + Math.Max(.1f, duration);
    }

    public string ReadNotice(float now) => now < _noticeUntil ? _notice : string.Empty;

    public void Reset()
    {
        _hasWeaponSet = false;
        _weaponSet = WeaponSetSlot.None;
        _notice = string.Empty;
        _noticeUntil = 0;
    }
}
