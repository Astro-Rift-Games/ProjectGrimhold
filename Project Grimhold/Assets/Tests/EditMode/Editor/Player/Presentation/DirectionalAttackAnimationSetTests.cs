using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class DirectionalAttackAnimationSetTests
{
    private const string Root = "Assets/Scriptable Objects/Loot/AttackAnimationSets/";
    private static readonly string[] Directions = { "N", "NE", "NW", "S", "SE", "SW" };

    [TestCase("Sword1H", "ArmingSword")]
    [TestCase("Rapier", "Rapier")]
    [TestCase("Dagger", "RondelDagger")]
    [TestCase("Wand", "MagicWand")]
    public void Set_HasExactlySixMappedClipsAndIsComplete(string setName, string sourceName)
    {
        DirectionalAttackAnimationSet set = AssetDatabase.LoadAssetAtPath<DirectionalAttackAnimationSet>(Root + setName + ".asset");
        Assert.That(set, Is.Not.Null);
        Assert.That(set.IsComplete, Is.True);
        Assert.That(set.TryValidate(out string error), Is.True, error);
        SerializedObject serialized = new SerializedObject(set);
        Assert.That(serialized.GetIterator(), Is.Not.Null);
        Assert.That(Directions.Select(direction => serialized.FindProperty("_attack" + direction)).All(property => property != null), Is.True);
        for (int index = 0; index < Directions.Length; index++)
            Assert.That(set.GetAttackClip(index), Is.SameAs(AssetDatabase.LoadAssetAtPath<AnimationClip>(
                $"Assets/Animations/Weapons/Directional/{sourceName}/{sourceName}_Attack_{Directions[index]}.anim")));
        Assert.That(set.GetAttackClip(-1), Is.Null);
        Assert.That(set.GetAttackClip(6), Is.Null);
    }

    [Test]
    public void MissingAnyDirection_DisablesCompleteness()
    {
        DirectionalAttackAnimationSet set = ScriptableObject.CreateInstance<DirectionalAttackAnimationSet>();
        AnimationClip clip = new AnimationClip();
        try
        {
            SerializedObject serialized = new SerializedObject(set);
            foreach (string direction in Directions)
                serialized.FindProperty("_attack" + direction).objectReferenceValue = clip;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(set.TryValidate(out _), Is.True);
            foreach (string direction in Directions)
            {
                serialized.FindProperty("_attack" + direction).objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(set.IsComplete, Is.False, direction);
                Assert.That(set.TryValidate(out string error), Is.False);
                Assert.That(error, Does.Contain("six clips"));
                serialized.FindProperty("_attack" + direction).objectReferenceValue = clip;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(clip);
            UnityEngine.Object.DestroyImmediate(set);
        }
    }

    [Test]
    public void Definitions_ShareDaggerIdentityButOtherFamiliesAreDistinct()
    {
        string[] names = { "ArmingSword", "Rapier", "RondelDagger", "MagicCinquedea", "MagicWand" };
        DirectionalAttackAnimationSet[] sets = names.Select(name =>
            AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
                $"Assets/Scriptable Objects/Loot/Definitions/{name}WeaponDefinition.asset")
                .Presentation.AttackAnimationSet).ToArray();
        Assert.That(sets, Has.All.Not.Null);
        Assert.That(sets[2], Is.SameAs(sets[3]));
        Assert.That(new[] { sets[0], sets[1], sets[2], sets[4] }.Distinct().Count(), Is.EqualTo(4));
        foreach (string name in names)
        {
            WeaponDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
                $"Assets/Scriptable Objects/Loot/Definitions/{name}WeaponDefinition.asset");
            Assert.That(definition.Presentation.HasGenericAttack, Is.True, name);
            Assert.That(definition.Presentation.AttackAnimationSet.TryValidate(out _), Is.True, name);
        }
    }
}
