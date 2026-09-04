using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using System.Reflection;

public class PlayerArmorPresenterTests
{
    private GameObject _root;
    private PlayerArmorPresenter _presenter;

    private SpriteRenderer _headBase;
    private SpriteRenderer _helmetVisual;
    private SpriteRenderer _leftHandBase;
    private SpriteRenderer _rightHandBase;
    private SpriteRenderer _leftGloveVisual;
    private SpriteRenderer _rightGloveVisual;

    [SetUp]
    public void Setup()
    {
        _root = new GameObject("PlayerVisualRoot");
        _presenter = _root.AddComponent<PlayerArmorPresenter>();

        GameObject headObj = new GameObject("Head");
        headObj.transform.SetParent(_root.transform);
        _headBase = headObj.AddComponent<SpriteRenderer>();

        GameObject helmetObj = new GameObject("Helmet");
        helmetObj.transform.SetParent(headObj.transform);
        _helmetVisual = helmetObj.AddComponent<SpriteRenderer>();

        _leftHandBase = CreateRenderer("LeftHand");
        _rightHandBase = CreateRenderer("RightHand");
        _leftGloveVisual = CreateRenderer("LeftGloveVisual");
        _rightGloveVisual = CreateRenderer("RightGloveVisual");

        // Inject dependencies using reflection
        SetPrivateField("_presenter", "_headBase", _headBase);
        SetPrivateField("_presenter", "_helmetVisual", _helmetVisual);
        SetPrivateField("_presenter", "_leftHandBase", _leftHandBase);
        SetPrivateField("_presenter", "_rightHandBase", _rightHandBase);
        SetPrivateField("_presenter", "_leftGloveVisual", _leftGloveVisual);
        SetPrivateField("_presenter", "_rightGloveVisual", _rightGloveVisual);
    }

    [TearDown]
    public void Teardown()
    {
        if (_root != null)
        {
            Object.DestroyImmediate(_root);
        }
    }

    [Test]
    public void SyncSprite_WhenPlaceholderConfigExists_CopiesSpriteAndRendersAboveSource()
    {
        // Arrange
        Sprite testSprite = Sprite.Create(new Texture2D(2, 2), new Rect(0, 0, 2, 2), Vector2.zero);
        _headBase.sprite = testSprite;
        _headBase.flipX = true;
        _headBase.flipY = false;
        _headBase.sortingOrder = 5;
        int charactersLayer = SortingLayer.NameToID("Characters");
        _headBase.sortingLayerID = charactersLayer;

        _helmetVisual.enabled = true; // Pretend it's equipped
        
        EquipmentVisualDefinition testConfig = new EquipmentVisualDefinition(Color.red);
        SetPrivateField("_presenter", "_helmetConfig", testConfig);

        // Act
        CallPrivateMethod("_presenter", "UpdateVisualsToMatchBase");

        // Assert
        Assert.AreEqual(testSprite, _helmetVisual.sprite);
        Assert.IsTrue(_helmetVisual.flipX);
        Assert.IsFalse(_helmetVisual.flipY);
        Assert.AreEqual(6, _helmetVisual.sortingOrder);
        Assert.Greater(_helmetVisual.sortingOrder, _headBase.sortingOrder);
        Assert.AreEqual(charactersLayer, _helmetVisual.sortingLayerID);

        Object.DestroyImmediate(testSprite.texture);
        Object.DestroyImmediate(testSprite);
    }

    [Test]
    public void SyncSprite_WhenAnimatedSortingChanges_AlwaysStaysAboveSource()
    {
        Sprite testSprite = Sprite.Create(new Texture2D(2, 2), new Rect(0, 0, 2, 2), Vector2.zero);
        _headBase.sprite = testSprite;
        _helmetVisual.enabled = true;
        SetPrivateField("_presenter", "_helmetConfig", new EquipmentVisualDefinition(Color.white));

        foreach (int sourceOrder in new[] { -10, 0, 4, 30 })
        {
            _headBase.sortingOrder = sourceOrder;

            CallPrivateMethod("_presenter", "UpdateVisualsToMatchBase");

            Assert.AreEqual(sourceOrder + 1, _helmetVisual.sortingOrder);
            Assert.Greater(_helmetVisual.sortingOrder, _headBase.sortingOrder);
        }

        Object.DestroyImmediate(testSprite.texture);
        Object.DestroyImmediate(testSprite);
    }

    [Test]
    public void ProductivePrefab_AllEquipmentRenderersStartAboveTheirBaseParts()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkPlayer.prefab");
        Assert.That(prefab, Is.Not.Null);

        PlayerArmorPresenter presenter = prefab.GetComponentInChildren<PlayerArmorPresenter>(true);
        Assert.That(presenter, Is.Not.Null);

        var serialized = new SerializedObject(presenter);
        AssertRendererAboveSource(serialized, "_headBase", "_helmetVisual");
        AssertRendererAboveSource(serialized, "_bodyBase", "_armorVisual");
        AssertRendererAboveSource(serialized, "_leftHandBase", "_leftGloveVisual");
        AssertRendererAboveSource(serialized, "_rightHandBase", "_rightGloveVisual");
        AssertRendererAboveSource(serialized, "_legsBase", "_bootsVisual");
    }

    [Test]
    public void SyncSprite_WhenConfigIsNull_DoesNotCopySprite()
    {
        // Arrange
        Sprite testSprite = Sprite.Create(new Texture2D(2, 2), new Rect(0, 0, 2, 2), Vector2.zero);
        _headBase.sprite = testSprite;

        _helmetVisual.enabled = false;
        _helmetVisual.sprite = null;
        
        SetPrivateField("_presenter", "_helmetConfig", null);

        // Act
        CallPrivateMethod("_presenter", "UpdateVisualsToMatchBase");

        // Assert
        Assert.IsNull(_helmetVisual.sprite, "Missing config should not update target");

        Object.DestroyImmediate(testSprite.texture);
        Object.DestroyImmediate(testSprite);
    }

    [Test]
    public void ResolveEquipmentVisualConfigs_WhenGlovesAreEquipped_HidesBothBaseHands()
    {
        SetPrivateField("_presenter", "_glovesConfig", new EquipmentVisualDefinition(Color.white));

        CallPrivateMethod("_presenter", "ApplyGloveBaseVisibility");

        Assert.That(_leftHandBase.enabled, Is.False);
        Assert.That(_rightHandBase.enabled, Is.False);
    }

    [Test]
    public void ResolveEquipmentVisualConfigs_WhenGlovesAreUnequipped_RestoresBothBaseHands()
    {
        _leftHandBase.enabled = false;
        _rightHandBase.enabled = false;
        SetPrivateField("_presenter", "_glovesConfig", null);

        CallPrivateMethod("_presenter", "ApplyGloveBaseVisibility");

        Assert.That(_leftHandBase.enabled, Is.True);
        Assert.That(_rightHandBase.enabled, Is.True);
    }

    [Test]
    public void EquipmentSortingRule_PreservesRelativeOffsetAsBaseOrderChanges()
    {
        // The equipment layer must keep the same relative offset above its base part no
        // matter how the base order changes with direction / animation frames.
        foreach (int baseOrder in new[] { -10, 0, 1, 3, 7, 30 })
        {
            int equipped = EquipmentLayerSortingRule.ResolveEquipmentSortingOrder(baseOrder);
            Assert.That(equipped, Is.EqualTo(baseOrder + EquipmentLayerSortingRule.RelativeSortingOffset));
            Assert.That(equipped, Is.GreaterThan(baseOrder));
        }
    }

    [Test]
    public void ProductivePrefab_BasePartsHaveDistinctSortingOrders()
    {
        // No-armor determinism: the base character must never depend on equipment to
        // disambiguate depth, so every base part owns a unique sorting order.
        SerializedObject serialized = LoadPrefabPresenter();
        int[] baseOrders =
        {
            GetOrder(serialized, "_legsBase"),
            GetOrder(serialized, "_bodyBase"),
            GetOrder(serialized, "_leftHandBase"),
            GetOrder(serialized, "_rightHandBase"),
            GetOrder(serialized, "_headBase"),
        };

        CollectionAssert.AllItemsAreUnique(baseOrders);
    }

    [Test]
    public void ProductivePrefab_NoEquipmentLayerCollidesWithAForeignBasePart()
    {
        // The root cause guarded here: each equipment layer must sit in a reserved slot
        // (its own base + offset) and never share an order with a different base part.
        SerializedObject serialized = LoadPrefabPresenter();
        (string baseProp, string equipProp)[] pairs =
        {
            ("_legsBase", "_bootsVisual"),
            ("_bodyBase", "_armorVisual"),
            ("_leftHandBase", "_leftGloveVisual"),
            ("_rightHandBase", "_rightGloveVisual"),
            ("_headBase", "_helmetVisual"),
        };

        var baseOrderByProp = new System.Collections.Generic.Dictionary<string, int>();
        foreach ((string baseProp, _) in pairs)
        {
            baseOrderByProp[baseProp] = GetOrder(serialized, baseProp);
        }

        foreach ((string baseProp, string equipProp) in pairs)
        {
            int equipOrder = GetOrder(serialized, equipProp);
            Assert.That(equipOrder,
                Is.EqualTo(baseOrderByProp[baseProp] + EquipmentLayerSortingRule.RelativeSortingOffset),
                equipProp);

            foreach (var foreign in baseOrderByProp)
            {
                if (foreign.Key == baseProp)
                {
                    continue;
                }

                Assert.That(equipOrder, Is.Not.EqualTo(foreign.Value),
                    $"{equipProp} collides with base part {foreign.Key}");
            }
        }
    }

    [Test]
    public void ProductivePrefab_PreservesIntendedBackToFrontStack()
    {
        // Legs < Boots < Body < Armor < LeftHand < LeftGlove < Head < Helmet
        SerializedObject serialized = LoadPrefabPresenter();
        int legs = GetOrder(serialized, "_legsBase");
        int boots = GetOrder(serialized, "_bootsVisual");
        int body = GetOrder(serialized, "_bodyBase");
        int armor = GetOrder(serialized, "_armorVisual");
        int leftHand = GetOrder(serialized, "_leftHandBase");
        int leftGlove = GetOrder(serialized, "_leftGloveVisual");
        int head = GetOrder(serialized, "_headBase");
        int helmet = GetOrder(serialized, "_helmetVisual");

        Assert.That(legs, Is.LessThan(boots), "Legs < Boots");
        Assert.That(boots, Is.LessThan(body), "Boots < Body");
        Assert.That(body, Is.LessThan(armor), "Body < Armor");
        Assert.That(armor, Is.LessThan(leftHand), "Armor < LeftHand");
        Assert.That(leftHand, Is.LessThan(leftGlove), "LeftHand < LeftGlove");
        Assert.That(leftGlove, Is.LessThan(head), "LeftGlove < Head");
        Assert.That(head, Is.LessThan(helmet), "Head < Helmet");
    }

    private static SerializedObject LoadPrefabPresenter()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkPlayer.prefab");
        Assert.That(prefab, Is.Not.Null);
        PlayerArmorPresenter presenter = prefab.GetComponentInChildren<PlayerArmorPresenter>(true);
        Assert.That(presenter, Is.Not.Null);
        return new SerializedObject(presenter);
    }

    private static int GetOrder(SerializedObject presenter, string property)
    {
        var renderer = presenter.FindProperty(property).objectReferenceValue as SpriteRenderer;
        Assert.That(renderer, Is.Not.Null, property);
        return renderer.sortingOrder;
    }

    private SpriteRenderer CreateRenderer(string objectName)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(_root.transform);
        return child.AddComponent<SpriteRenderer>();
    }

    private void SetPrivateField(string instanceName, string fieldName, object value)
    {
        object instance = instanceName == "_presenter" ? _presenter : null;
        FieldInfo field = typeof(PlayerArmorPresenter).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        field.SetValue(instance, value);
    }

    private void CallPrivateMethod(string instanceName, string methodName)
    {
        object instance = instanceName == "_presenter" ? _presenter : null;
        MethodInfo method = typeof(PlayerArmorPresenter).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        method.Invoke(instance, null);
    }

    private static void AssertRendererAboveSource(
        SerializedObject presenter,
        string sourceProperty,
        string targetProperty)
    {
        var source = presenter.FindProperty(sourceProperty).objectReferenceValue as SpriteRenderer;
        var target = presenter.FindProperty(targetProperty).objectReferenceValue as SpriteRenderer;

        Assert.That(source, Is.Not.Null, sourceProperty);
        Assert.That(target, Is.Not.Null, targetProperty);
        Assert.That(target.sortingLayerID, Is.EqualTo(source.sortingLayerID), targetProperty);
        Assert.That(target.sortingOrder, Is.EqualTo(source.sortingOrder + 1), targetProperty);
        Assert.That(target.sortingOrder, Is.GreaterThan(source.sortingOrder), targetProperty);
    }
}
