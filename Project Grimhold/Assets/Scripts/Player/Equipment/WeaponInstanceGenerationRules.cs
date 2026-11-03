using System;
using UnityEngine;

/// <summary>
/// Loot/Balance-owned probabilities for generating Dungeon weapon variants. Chances use basis
/// points (0-10000); weights are relative and indexed by grade E-S or by gem socket count 0-2.
/// </summary>
[Serializable]
public struct WeaponInstanceGenerationRules
{
    public const int MaxBasisPoints = 10000;
    public const int GradeWeightCount = 6;
    public const int GemSocketCountWeightCount = WeaponInstanceModifiers.MaxGemSocketCount + 1;

    [SerializeField] private int _primaryScalingChanceBasisPoints;
    [SerializeField] private int _secondaryScalingChanceBasisPoints;
    [SerializeField] private int[] _gradeWeights;
    [SerializeField] private int[] _gemSocketCountWeights;
    [SerializeField] private int _gemInstallChanceBasisPoints;
    [SerializeField] private string[] _gemPool;

    public int PrimaryScalingChanceBasisPoints => _primaryScalingChanceBasisPoints;
    public int SecondaryScalingChanceBasisPoints => _secondaryScalingChanceBasisPoints;
    public int GemInstallChanceBasisPoints => _gemInstallChanceBasisPoints;
    public int GemPoolCount => _gemPool?.Length ?? 0;

    public static bool TryCreate(
        int primaryScalingChanceBasisPoints,
        int secondaryScalingChanceBasisPoints,
        int[] gradeWeights,
        int[] gemSocketCountWeights,
        int gemInstallChanceBasisPoints,
        LootId[] gemPool,
        out WeaponInstanceGenerationRules rules,
        out string error)
    {
        string[] gemIds = new string[gemPool?.Length ?? 0];
        for (int index = 0; index < gemIds.Length; index++)
        {
            gemIds[index] = gemPool[index].Value;
        }

        rules = new WeaponInstanceGenerationRules
        {
            _primaryScalingChanceBasisPoints = primaryScalingChanceBasisPoints,
            _secondaryScalingChanceBasisPoints = secondaryScalingChanceBasisPoints,
            _gradeWeights = gradeWeights == null ? null : (int[])gradeWeights.Clone(),
            _gemSocketCountWeights = gemSocketCountWeights == null ? null : (int[])gemSocketCountWeights.Clone(),
            _gemInstallChanceBasisPoints = gemInstallChanceBasisPoints,
            _gemPool = gemIds
        };

        if (!rules.TryValidate(out error))
        {
            rules = default;
            return false;
        }

        return true;
    }

    public bool TryValidate(out string error)
    {
        if (!IsBasisPoints(_primaryScalingChanceBasisPoints))
        {
            error = $"Weapon instance primary scaling chance must be 0-{MaxBasisPoints} basis points.";
            return false;
        }

        if (!IsBasisPoints(_secondaryScalingChanceBasisPoints))
        {
            error = $"Weapon instance secondary scaling chance must be 0-{MaxBasisPoints} basis points.";
            return false;
        }

        if (!IsBasisPoints(_gemInstallChanceBasisPoints))
        {
            error = $"Weapon instance gem install chance must be 0-{MaxBasisPoints} basis points.";
            return false;
        }

        if (!AreValidWeights(_gradeWeights, GradeWeightCount))
        {
            error = $"Weapon instance grade weights require {GradeWeightCount} non-negative values (E-S) with a positive total that fits in an int.";
            return false;
        }

        if (!AreValidWeights(_gemSocketCountWeights, GemSocketCountWeightCount))
        {
            error = $"Weapon instance gem socket count weights require {GemSocketCountWeightCount} non-negative values (0-{WeaponInstanceModifiers.MaxGemSocketCount}) with a positive total.";
            return false;
        }

        if (_gemInstallChanceBasisPoints > 0 && GemPoolCount == 0)
        {
            error = "Weapon instance gem pool requires at least one gem when gems can be installed.";
            return false;
        }

        for (int index = 0; index < GemPoolCount; index++)
        {
            if (string.IsNullOrWhiteSpace(_gemPool[index]))
            {
                error = $"Weapon instance gem pool entry {index} has no LootId.";
                return false;
            }
        }

        error = null;
        return true;
    }

    /// <summary>Returns the relative weight of grade E (index 0) through S (index 5).</summary>
    public int GetGradeWeight(int index) => _gradeWeights[index];

    /// <summary>Returns the relative weight of a variant with the given gem socket count.</summary>
    public int GetGemSocketCountWeight(int socketCount) => _gemSocketCountWeights[socketCount];

    public LootId GetGem(int index) => new(_gemPool[index]);

    private static bool IsBasisPoints(int value) => value >= 0 && value <= MaxBasisPoints;

    private static bool AreValidWeights(int[] weights, int expectedCount)
    {
        if (weights == null || weights.Length != expectedCount)
        {
            return false;
        }

        long total = 0;
        for (int index = 0; index < weights.Length; index++)
        {
            if (weights[index] < 0)
            {
                return false;
            }

            total += weights[index];
        }

        return total > 0 && total <= int.MaxValue;
    }
}
