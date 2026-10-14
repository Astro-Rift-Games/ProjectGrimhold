#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[Category("TASK206")]
public sealed class AbilityVendorPrefabConfigurationTests
{
    private const string ViewPath = "Assets/Prefabs/UI/AbilityVendor/TownAbilityVendor.prefab";
    private const string RowPath = "Assets/Prefabs/UI/AbilityVendor/TownAbilityVendorRow.prefab";

    private static readonly string[] RequiredViewFields =
    {
        "_rowContainer", "_rowPrefab", "_goldText", "_feedbackText", "_noticeText", "_closeButton"
    };

    [Test]
    public void RowPrefab_PassesValidation()
    {
        TownAbilityVendorRowView row = LoadRow();

        Assert.That(row.TryValidate(out string error), Is.True, error);
    }

    [Test]
    public void RowPrefab_BuyLabelIsChildOfBuyButton()
    {
        TownAbilityVendorRowView row = LoadRow();
        var serialized = new SerializedObject(row);
        var buyLabel = serialized.FindProperty("_buyLabel").objectReferenceValue as Component;
        var buyButton = serialized.FindProperty("_buyButton").objectReferenceValue as Component;

        Assert.That(buyLabel, Is.Not.Null, "_buyLabel is not assigned");
        Assert.That(buyButton, Is.Not.Null, "_buyButton is not assigned");
        Assert.That(buyLabel.transform.IsChildOf(buyButton.transform), Is.True);
        Assert.That(buyLabel.transform, Is.Not.SameAs(buyButton.transform));
    }

    [TestCaseSource(nameof(RequiredViewFields))]
    public void ViewPrefab_RequiredFieldIsAssigned(string fieldName)
    {
        var view = AssetDatabase.LoadAssetAtPath<GameObject>(ViewPath).GetComponent<TownAbilityVendorView>();
        Assert.That(view, Is.Not.Null);

        SerializedProperty property = new SerializedObject(view).FindProperty(fieldName);

        Assert.That(property, Is.Not.Null, $"Field '{fieldName}' does not exist");
        Assert.That(property.objectReferenceValue, Is.Not.Null, $"Field '{fieldName}' is not assigned");
    }

    private static TownAbilityVendorRowView LoadRow()
    {
        var row = AssetDatabase.LoadAssetAtPath<GameObject>(RowPath).GetComponent<TownAbilityVendorRowView>();
        Assert.That(row, Is.Not.Null);
        return row;
    }
}
#endif
