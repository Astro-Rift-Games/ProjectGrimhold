using System;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class AttackVfxTintTests
{
    private const string PrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";
    private const string PalettePath = "Assets/Scriptable Objects/AttackVfxTintPalette.asset";
    private const string WandLootPath = "Assets/Scriptable Objects/Loot/Definitions/MagicWand.asset";
    private const string SwordLootPath = "Assets/Scriptable Objects/Loot/Definitions/ArmingSword.asset";
    private static readonly Color ArcaneTint = new Color(0.75f, 0.6f, 1f, 1f);
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject _contents;
    private PlayerAttackVfxPresenter _presenter;
    private SpriteRenderer _renderer;
    private LootDefinitionCatalog _catalog;

    [SetUp]
    public void SetUp()
    {
        _contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        _presenter = _contents.GetComponentInChildren<PlayerAttackVfxPresenter>(true);
        Assert.That(_presenter, Is.Not.Null);
        _renderer = (SpriteRenderer)new SerializedObject(_presenter).FindProperty("_vfxRenderer").objectReferenceValue;
        var equipment = _contents.GetComponent<PlayerWeaponEquipmentNetworkController>();
        _catalog = (LootDefinitionCatalog)new SerializedObject(equipment).FindProperty("_lootCatalog").objectReferenceValue;
        Assert.That(_catalog, Is.Not.Null);
    }

    [TearDown]
    public void TearDown()
    {
        if (_contents != null) PrefabUtility.UnloadPrefabContents(_contents);
    }

    private static AttackVfxTintPalette Palette(params (DamageType type, Color color)[] entries)
    {
        var palette = ScriptableObject.CreateInstance<AttackVfxTintPalette>();
        var serialized = new SerializedObject(palette);
        SerializedProperty list = serialized.FindProperty("_entries");
        list.arraySize = entries.Length;
        for (int i = 0; i < entries.Length; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("_damageType").intValue = (int)entries[i].type;
            entry.FindPropertyRelative("_tint").colorValue = entries[i].color;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return palette;
    }

    private void Raise(int catalogIndexPlusOne)
    {
        var attack = new AttackPerformedEvent(default, AttackType.Ranged, Vector2.zero, Vector2.down, 0, catalogIndexPlusOne);
        typeof(PlayerAttackVfxPresenter).GetMethod("OnAttackPerformed", Private).Invoke(_presenter, new object[] { attack });
    }

    private void Perform(string lootPath)
    {
        LootDefinition loot = AssetDatabase.LoadAssetAtPath<LootDefinition>(lootPath);
        Assert.That(_catalog.TryGetIndex(loot.LootId, out int index), Is.True);
        Raise(index + 1);
        Assert.That(typeof(PlayerAttackVfxPresenter).GetField("_pending", Private).GetValue(_presenter), Is.True,
            "The confirmed attack must start a VFX.");
    }

    [Test]
    public void Palette_WithOneEntryPerDamageType_IsValidAndResolvesEachTint()
    {
        AttackVfxTintPalette palette = Palette((DamageType.Physical, Color.white), (DamageType.Magical, Color.red),
            (DamageType.TrueDamage, Color.blue));
        try
        {
            Assert.That(palette.TryValidate(out string error), Is.True, error);
            Assert.That(palette.TryGetTint(DamageType.Magical, out Color tint), Is.True);
            Assert.That(tint, Is.EqualTo(Color.red));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(palette);
        }
    }

    [Test]
    public void Palette_MissingADamageType_FailsNamingIt()
    {
        AttackVfxTintPalette palette = Palette((DamageType.Physical, Color.white), (DamageType.Magical, Color.red));
        try
        {
            Assert.That(palette.TryValidate(out string error), Is.False);
            Assert.That(error, Does.Contain(nameof(DamageType.TrueDamage)));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(palette);
        }
    }

    [Test]
    public void Palette_DuplicateDamageType_FailsNamingIt()
    {
        AttackVfxTintPalette palette = Palette((DamageType.Physical, Color.white), (DamageType.Magical, Color.red),
            (DamageType.TrueDamage, Color.blue), (DamageType.Magical, Color.green));
        try
        {
            Assert.That(palette.TryValidate(out string error), Is.False);
            Assert.That(error, Does.Contain(nameof(DamageType.Magical)));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(palette);
        }
    }

    [Test]
    public void ProjectPalette_CoversEveryDamageType_AndPhysicalLeavesArtUntinted()
    {
        var palette = AssetDatabase.LoadAssetAtPath<AttackVfxTintPalette>(PalettePath);
        Assert.That(palette, Is.Not.Null);
        Assert.That(palette.TryValidate(out string error), Is.True, error);
        foreach (DamageType type in Enum.GetValues(typeof(DamageType)))
        {
            Assert.That(palette.TryGetTint(type, out _), Is.True, type.ToString());
        }
        Assert.That(palette.TryGetTint(DamageType.Physical, out Color physical), Is.True);
        Assert.That(physical, Is.EqualTo(Color.white));
        Assert.That(palette.TryGetTint(DamageType.Magical, out Color magical), Is.True);
        Assert.That(magical, Is.EqualTo(ArcaneTint));
    }

    [Test]
    public void NetworkPlayerPrefab_PresenterReferencesTheProjectPalette()
    {
        var palette = AssetDatabase.LoadAssetAtPath<AttackVfxTintPalette>(PalettePath);
        Assert.That(palette, Is.Not.Null);
        SerializedProperty reference = new SerializedObject(_presenter).FindProperty("_tintPalette");
        Assert.That(reference, Is.Not.Null);
        Assert.That(reference.objectReferenceValue, Is.SameAs(palette));
    }

    [Test]
    public void ConfirmedAttack_TintsTheRendererByTheWeaponDamageType()
    {
        var palette = AssetDatabase.LoadAssetAtPath<AttackVfxTintPalette>(PalettePath);
        Assert.That(AssetDatabase.LoadAssetAtPath<LootDefinition>(WandLootPath).WeaponDefinition.DamageType, Is.EqualTo(DamageType.Magical));
        Assert.That(AssetDatabase.LoadAssetAtPath<LootDefinition>(SwordLootPath).WeaponDefinition.DamageType, Is.EqualTo(DamageType.Physical));

        Perform(WandLootPath);
        Assert.That(palette.TryGetTint(DamageType.Magical, out Color magical), Is.True);
        Assert.That(_renderer.color, Is.EqualTo(magical));

        // The next confirmed attack replaces the tint instead of inheriting the previous weapon's.
        Perform(SwordLootPath);
        Assert.That(palette.TryGetTint(DamageType.Physical, out Color physical), Is.True);
        Assert.That(_renderer.color, Is.EqualTo(physical));
        Assert.That(_renderer.color, Is.EqualTo(Color.white));
    }

    [Test]
    public void Tint_DoesNotLeakAfterCancel()
    {
        Perform(WandLootPath);
        Assert.That(_renderer.color, Is.Not.EqualTo(Color.white));
        _presenter.CancelAndRestore();
        Assert.That(_renderer.color, Is.EqualTo(Color.white));
    }

    [Test]
    public void Tint_DoesNotLeakIntoAnAttackWithoutVfx()
    {
        Perform(WandLootPath);
        Raise(0);
        Assert.That(_renderer.color, Is.EqualTo(Color.white));
    }

    [Test]
    public void Presenter_WithoutPalette_FailsClearlyOnEnable()
    {
        var serialized = new SerializedObject(_presenter);
        serialized.FindProperty("_tintPalette").objectReferenceValue = null;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        _presenter.enabled = true;
        LogAssert.Expect(LogType.Error, new Regex("tint palette"));
        typeof(PlayerAttackVfxPresenter).GetMethod("OnEnable", Private).Invoke(_presenter, null);
        Assert.That(_presenter.enabled, Is.False);
    }
}
