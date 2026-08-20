using NUnit.Framework;
using UnityEngine;
using System.Reflection;

public class PlayerArmorPresenterTests
{
    private GameObject _root;
    private PlayerArmorPresenter _presenter;

    private SpriteRenderer _headBase;
    private SpriteRenderer _helmetVisual;

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

        // Inject dependencies using reflection
        SetPrivateField("_presenter", "_headBase", _headBase);
        SetPrivateField("_presenter", "_helmetVisual", _helmetVisual);
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
    public void SyncSprite_WhenPlaceholderConfigExists_CopiesSpriteAndSorting()
    {
        // Arrange
        Sprite testSprite = Sprite.Create(new Texture2D(2, 2), new Rect(0, 0, 2, 2), Vector2.zero);
        _headBase.sprite = testSprite;
        _headBase.flipX = true;
        _headBase.flipY = false;
        _headBase.sortingOrder = 5;
        _headBase.sortingLayerID = 10;

        _helmetVisual.enabled = true; // Pretend it's equipped
        
        EquipmentVisualDefinition testConfig = new EquipmentVisualDefinition(Color.red);
        SetPrivateField("_presenter", "_helmetConfig", testConfig);

        // Act
        CallPrivateMethod("_presenter", "UpdateVisualsToMatchBase");

        // Assert
        Assert.AreEqual(testSprite, _helmetVisual.sprite);
        Assert.IsTrue(_helmetVisual.flipX);
        Assert.IsFalse(_helmetVisual.flipY);
        Assert.AreEqual(5, _helmetVisual.sortingOrder);
        Assert.AreEqual(10, _helmetVisual.sortingLayerID);

        Object.DestroyImmediate(testSprite.texture);
        Object.DestroyImmediate(testSprite);
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
}
