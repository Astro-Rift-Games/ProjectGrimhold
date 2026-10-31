using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Attacker-owned local VFX. One lazily-created world emitter per prefab, not per target/contact.
/// Nothing here reads or writes simulation state, Animator speed or the hitstop clock.
/// </summary>
public sealed class MeleeImpactParticleEmitter : IDisposable
{
    private readonly Dictionary<ParticleSystem, ParticleSystem> _instances = new();
    private readonly System.Random _random = new();
    public int InstanceCount => _instances.Count;

    public bool Emit(MeleeImpactParticleProfile profile, Vector2 hitPoint, Vector2 hitDirection,
        Color? colorOverride = null)
    {
        if (profile == null || !IsFinite(hitPoint.x) || !IsFinite(hitPoint.y) ||
            !profile.TryValidate(out _)) return false;

        if (!_instances.TryGetValue(profile.EmitterPrefab, out ParticleSystem instance) || instance == null)
        {
            instance = Object.Instantiate(profile.EmitterPrefab, Vector3.zero, Quaternion.identity);
            instance.name = "MeleeImpactParticles (Local)";
            instance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _instances[profile.EmitterPrefab] = instance;
        }
        // Restart a finished emitter's normal clock without clearing still-live particles.
        if (!instance.isPlaying) instance.Play(false);
        int count = SampleCount(profile, _random);
        for (int i = 0; i < count; i++)
        {
            ParticleSystem.EmitParams parameters = Sample(profile, hitPoint, hitDirection, _random);
            if (colorOverride.HasValue) parameters.startColor = colorOverride.Value;
            instance.Emit(parameters, 1);
        }
        return true;
    }

    /// <summary>Pure sampling helpers: callers supply a random stream; no Unity/global state or allocation.</summary>
    public static int SampleCount(MeleeImpactParticleProfile profile, System.Random random) =>
        random.Next(profile.CountMin, profile.CountMax + 1);

    public static ParticleSystem.EmitParams Sample(MeleeImpactParticleProfile profile, Vector2 point,
        Vector2 direction, System.Random random)
    {
        // atan2 avoids normalizing a very large finite vector (which could overflow its squared length).
        bool directed = IsFinite(direction.x) && IsFinite(direction.y) &&
            (Mathf.Abs(direction.x) > .0001f || Mathf.Abs(direction.y) > .0001f);
        float center = directed ? Mathf.Atan2(direction.y, direction.x) : 0f;
        float spread = directed ? profile.SpreadDegrees : 360f;
        float angle = center + Range(random, -spread * .5f, spread * .5f) * Mathf.Deg2Rad;
        float speed = Range(random, profile.SpeedMin, profile.SpeedMax);
        float ppu = profile.PixelsPerUnit;
        return new ParticleSystem.EmitParams
        {
            position = new Vector3(Mathf.Round(point.x * ppu) / ppu, Mathf.Round(point.y * ppu) / ppu, 0f),
            velocity = new Vector3(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed, 0f),
            startSize = random.Next(profile.SizePixelsMin, profile.SizePixelsMax + 1) / ppu,
            startLifetime = Range(random, profile.LifetimeMin, profile.LifetimeMax),
            startColor = profile.Color,
            rotation = 0f,
            applyShapeToPosition = false
        };
    }

    public void Dispose()
    {
        foreach (ParticleSystem instance in _instances.Values)
        {
            if (instance == null) continue;
            instance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (Application.isPlaying) Object.Destroy(instance.gameObject);
            else Object.DestroyImmediate(instance.gameObject);
        }
        _instances.Clear();
    }

    private static float Range(System.Random random, float min, float max) =>
        min + (max - min) * (float)random.NextDouble();
    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
