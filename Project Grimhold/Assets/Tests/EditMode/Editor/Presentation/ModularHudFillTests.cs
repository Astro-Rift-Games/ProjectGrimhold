using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class ModularHudFillTests
{
    private GameObject _root;

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_root);

    [TestCase(0f)]
    [TestCase(0.25f)]
    [TestCase(0.5f)]
    [TestCase(0.75f)]
    [TestCase(1f)]
    public void FilledVitalsClipWithoutScalingTheCircularGeometry(float fraction)
    {
        _root = new GameObject("HUD");
        RaidHudView view = _root.AddComponent<RaidHudView>();
        Image health = CreateImage("Health", view, "_healthFill", Image.Type.Filled);
        Image mana = CreateImage("Mana", view, "_manaFill", Image.Type.Filled);
        Image stamina = CreateImage("Stamina", view, "_staminaFill", Image.Type.Filled);
        view.PresentHealth(fraction * 100f, 100f);
        view.PresentMana(fraction * 100f, 100f);
        view.PresentStamina(fraction * 100f, 100f, false);
        foreach (Image fill in new[] { health, mana, stamina })
        {
            Assert.That(fill.fillAmount, Is.EqualTo(fraction).Within(0.0001f));
            Assert.That(fill.rectTransform.localScale, Is.EqualTo(Vector3.one));
        }
    }

    [Test]
    public void LegacyLinearResourceAndExpeditionFillsKeepTheirScalingContract()
    {
        _root = new GameObject("HUD");
        RaidHudView view = _root.AddComponent<RaidHudView>();
        Image health = CreateImage("Health", view, "_healthFill", Image.Type.Simple);
        Image expedition = CreateImage("Expedition", view, "_progressFill", Image.Type.Simple);
        view.PresentHealth(25f, 100f);
        view.PresentExpeditionProgress(0.5f);
        Assert.That(health.rectTransform.localScale.x, Is.EqualTo(0.25f));
        Assert.That(expedition.rectTransform.localScale.x, Is.EqualTo(0.5f));
        Assert.That(expedition.fillAmount, Is.EqualTo(0.5f));
    }

    [TestCase(0f)]
    [TestCase(0.5f)]
    [TestCase(1f)]
    public void FilledDownedAndReviveIndicatorsClipWithoutScaling(float fraction)
    {
        _root = new GameObject("Status");
        RaidDownedHudView view = _root.AddComponent<RaidDownedHudView>();
        Image reserve = CreateImage("Reserve", view, "_reserveFill", Image.Type.Filled);
        Image revive = CreateImage("Revive", view, "_reviveFill", Image.Type.Filled);
        view.PresentDowned(true, fraction, "reserve", false);
        view.PresentReviveProgress(true, fraction, false);
        Assert.That(reserve.fillAmount, Is.EqualTo(fraction));
        Assert.That(revive.fillAmount, Is.EqualTo(fraction));
        Assert.That(reserve.rectTransform.localScale, Is.EqualTo(Vector3.one));
        Assert.That(revive.rectTransform.localScale, Is.EqualTo(Vector3.one));
    }

    private Image CreateImage(string name, Component view, string field, Image.Type type)
    {
        GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        child.transform.SetParent(_root.transform, false);
        Image image = child.GetComponent<Image>();
        image.type = type;
        view.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, image);
        return image;
    }

    [Test]
    public void SharedStatusRingPrioritizesReviveAndRestoresTheDownedReserve()
    {
        _root = new GameObject("Status");
        RaidDownedHudView view = _root.AddComponent<RaidDownedHudView>();
        GameObject reserve = new GameObject("Reserve ring");
        reserve.transform.SetParent(_root.transform, false);
        GameObject revive = new GameObject("Revive ring");
        revive.transform.SetParent(_root.transform, false);
        Bind(view, "_reserveRingRoot", reserve);
        Bind(view, "_reviveRingRoot", revive);
        view.PresentDowned(true, 0.5f, "50 / 100", false);
        view.PresentReviveProgress(false, 0f, false);
        Assert.That(reserve.activeSelf, Is.True);
        Assert.That(revive.activeSelf, Is.False);
        view.PresentReviveProgress(true, 0.25f, false);
        Assert.That(reserve.activeSelf, Is.False);
        Assert.That(revive.activeSelf, Is.True);
        view.PresentReviveProgress(false, 0f, false);
        Assert.That(reserve.activeSelf, Is.True);
        view.Clear();
        Assert.That(reserve.activeSelf, Is.False);
        Assert.That(revive.activeSelf, Is.False);
    }

    private static void Bind(Component view, string name, Object value)
    {
        FieldInfo field = view.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, name + " is an explicit serialized dependency");
        field.SetValue(view, value);
    }
}
