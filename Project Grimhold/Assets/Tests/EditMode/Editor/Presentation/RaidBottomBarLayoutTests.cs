using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Layout contract for the bottom action bar (weapon cooldown + ability slots) and the left vitals block.
/// Rects are resolved analytically in the HUD root space (origin bottom-left, size = canvas size) because the
/// ability HUD (NetworkPlayer.prefab) and the weapon cooldown (LocalGameplayHud.prefab) live in different prefabs.
/// </summary>
public sealed class RaidBottomBarLayoutTests
{
    private const string HudPrefabPath = "Assets/Prefabs/UI/PlayerUI/LocalGameplayHud.prefab";
    private const string PlayerPrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";
    private const float Tolerance = 0.01f;

    [TestCase(1920f, 1080f)]
    [TestCase(1440f, 1080f)]
    public void WeaponAndAbilitySlotsFormOneEvenRowWeaponThenQThenE(float width, float height)
    {
        (Rect weapon, Rect q, Rect e, _) = LoadBottomBar(width, height);

        Assert.That(q.size.x, Is.EqualTo(weapon.size.x).Within(Tolerance), "Q width");
        Assert.That(e.size.x, Is.EqualTo(weapon.size.x).Within(Tolerance), "E width");
        Assert.That(q.size.y, Is.EqualTo(weapon.size.y).Within(Tolerance), "Q height");
        Assert.That(e.size.y, Is.EqualTo(weapon.size.y).Within(Tolerance), "E height");

        Assert.That(q.center.y, Is.EqualTo(weapon.center.y).Within(Tolerance), "Q row");
        Assert.That(e.center.y, Is.EqualTo(weapon.center.y).Within(Tolerance), "E row");

        Assert.That(weapon.center.x, Is.LessThan(q.center.x), "weapon before Q");
        Assert.That(q.center.x, Is.LessThan(e.center.x), "Q before E");

        float gapWeaponQ = q.xMin - weapon.xMax;
        float gapQE = e.xMin - q.xMax;
        Assert.That(gapWeaponQ, Is.GreaterThan(0f), "weapon and Q must not overlap");
        Assert.That(gapQE, Is.GreaterThan(0f), "Q and E must not overlap");
        Assert.That(gapQE, Is.EqualTo(gapWeaponQ).Within(Tolerance), "even spacing");
    }

    [TestCase(1920f, 1080f)]
    [TestCase(1440f, 1080f)]
    public void ActionBarIsCenteredAtTheBottomAndLeavesThePlayAreaFree(float width, float height)
    {
        (Rect weapon, Rect q, Rect e, _) = LoadBottomBar(width, height);

        float left = Mathf.Min(weapon.xMin, Mathf.Min(q.xMin, e.xMin));
        float right = Mathf.Max(weapon.xMax, Mathf.Max(q.xMax, e.xMax));
        Assert.That((left + right) * 0.5f, Is.EqualTo(width * 0.5f).Within(Tolerance), "row centered");

        float top = Mathf.Max(weapon.yMax, Mathf.Max(q.yMax, e.yMax));
        Assert.That(top, Is.LessThanOrEqualTo(height * 0.15f), "bar stays in the bottom 15% of the screen");
        Assert.That(Mathf.Min(weapon.yMin, Mathf.Min(q.yMin, e.yMin)), Is.GreaterThanOrEqualTo(0f));
    }

    [Test]
    public void AbilitySlotsReuseTheWeaponSlotFrameStyle()
    {
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        RaidAbilityHudView abilityView = player.GetComponentInChildren<RaidAbilityHudView>(true);
        Assert.That(abilityView, Is.Not.Null);
        Image weaponFrame = FindUnder(player.transform, "RaidCooldownHud").Find("CooldownFrame").GetComponent<Image>();

        foreach (string slotName in new[] { "AbilitySlot1", "AbilitySlot2" })
        {
            Image frame = abilityView.transform.Find(slotName).Find("Frame").GetComponent<Image>();
            Assert.That(frame.sprite, Is.EqualTo(weaponFrame.sprite), slotName + " frame sprite");
            Assert.That(frame.type, Is.EqualTo(weaponFrame.type), slotName + " frame type");
        }
    }

    [Test]
    public void VitalsRowsDoNotOverlapAndStayInsideTheFrameInOrder()
    {
        GameObject hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
        RectTransform root = (RectTransform)hud.transform;
        RectTransform frame = (RectTransform)hud.transform.Find("RaidMainHud");
        Rect frameRect = ResolveRect(frame, root, new Vector2(1920f, 1080f));

        string[] order = { "Health", "Stamina", "DefeatedIndicator" };
        Rect previous = default;
        for (int i = 0; i < order.Length; i++)
        {
            Rect row = ResolveRect((RectTransform)frame.Find(order[i]), root, new Vector2(1920f, 1080f));
            Assert.That(row.xMin, Is.GreaterThanOrEqualTo(frameRect.xMin - Tolerance), order[i] + " left");
            Assert.That(row.xMax, Is.LessThanOrEqualTo(frameRect.xMax + Tolerance), order[i] + " right");
            Assert.That(row.yMax, Is.LessThanOrEqualTo(frameRect.yMax + Tolerance), order[i] + " top");
            Assert.That(row.yMin, Is.GreaterThanOrEqualTo(frameRect.yMin - Tolerance), order[i] + " bottom");
            if (i > 0)
            {
                Assert.That(row.yMax, Is.LessThanOrEqualTo(previous.yMin + Tolerance),
                    order[i] + " must sit below " + order[i - 1] + " without overlap");
            }

            previous = row;
        }
    }

    [Test]
    public void VitalsFrameHasNoEmptyRowAtTheBottom()
    {
        GameObject hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
        RectTransform root = (RectTransform)hud.transform;
        RectTransform frame = (RectTransform)hud.transform.Find("RaidMainHud");
        Rect frameRect = ResolveRect(frame, root, new Vector2(1920f, 1080f));
        Rect lastRow = ResolveRect((RectTransform)frame.Find("DefeatedIndicator"), root, new Vector2(1920f, 1080f));

        Assert.That(lastRow.yMin - frameRect.yMin, Is.LessThanOrEqualTo(16f), "frame is shrunk to its content");
    }

    [Test]
    public void VitalsFrameHasNoInventoryRowAndDefeatedSitsRightBelowStamina()
    {
        GameObject hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
        RectTransform root = (RectTransform)hud.transform;
        RectTransform frame = (RectTransform)hud.transform.Find("RaidMainHud");
        Vector2 size = new Vector2(1920f, 1080f);

        Assert.That(frame.Find("InventoryText"), Is.Null, "inventory summary was removed from the frame");
        foreach (Transform child in hud.GetComponentsInChildren<Transform>(true))
        {
            Assert.That(child.name, Is.Not.EqualTo("InventoryText"), "no InventoryText object anywhere in the HUD");
        }

        Rect stamina = ResolveRect((RectTransform)frame.Find("Stamina"), root, size);
        Rect defeated = ResolveRect((RectTransform)frame.Find("DefeatedIndicator"), root, size);
        Assert.That(stamina.yMin - defeated.yMax, Is.InRange(0f, 8f), "no empty row between Stamina and the defeated indicator");
    }

    [Test]
    public void TeammateHudStaysJustBelowTheShrunkVitalsFrame()
    {
        GameObject hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
        RectTransform root = (RectTransform)hud.transform;
        Vector2 size = new Vector2(1920f, 1080f);
        Rect vitals = ResolveRect((RectTransform)hud.transform.Find("RaidMainHud"), root, size);
        Rect duo = ResolveRect((RectTransform)hud.transform.Find("RaidDuoHud"), root, size);

        Assert.That(vitals.yMin - duo.yMax, Is.InRange(0f, 24f), "teammate HUD sits right under the vitals frame");
    }

    [TestCase(1920f, 1080f)]
    [TestCase(1440f, 1080f)]
    public void VitalsDuoAndPressureHudsDoNotOverlap(float width, float height)
    {
        GameObject hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
        RectTransform root = (RectTransform)hud.transform;
        Vector2 size = new Vector2(width, height);
        Rect vitals = ResolveRect((RectTransform)hud.transform.Find("RaidMainHud"), root, size);
        Rect duo = ResolveRect((RectTransform)hud.transform.Find("RaidDuoHud"), root, size);
        Rect timer = ResolveRect((RectTransform)hud.transform.Find("DungeonPressureHUD/Timer"), root, size);
        Rect phase = ResolveRect((RectTransform)hud.transform.Find("DungeonPressureHUD/Phase"), root, size);

        Assert.That(vitals.Overlaps(duo), Is.False, "vitals vs teammate HUD");
        Assert.That(vitals.Overlaps(timer), Is.False, "vitals vs pressure timer");
        Assert.That(vitals.Overlaps(phase), Is.False, "vitals vs pressure phase");
        Assert.That(duo.Overlaps(timer), Is.False, "teammate HUD vs pressure timer");
        Assert.That(duo.Overlaps(phase), Is.False, "teammate HUD vs pressure phase");
    }

    [TestCase(1920f, 1080f)]
    [TestCase(1440f, 1080f)]
    public void InteractionPromptSitsClearlyAboveTheActionBarAndAbilityMessages(float width, float height)
    {
        (Rect weapon, Rect q, Rect e, RectTransform abilityHudRoot) = LoadBottomBar(width, height);
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        RaidAbilityHudView abilityView = player.GetComponentInChildren<RaidAbilityHudView>(true);

        float barTop = Mathf.Max(weapon.yMax, Mathf.Max(q.yMax, e.yMax));
        foreach (string slotName in new[] { "AbilitySlot1", "AbilitySlot2" })
        {
            Rect message = ResolveRect(
                (RectTransform)abilityView.transform.Find(slotName).Find("Message"), abilityHudRoot, new Vector2(width, height));
            barTop = Mathf.Max(barTop, message.yMax);
        }

        GameObject hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
        RectTransform root = (RectTransform)hud.transform;
        RectTransform prompt = (RectTransform)hud.transform.Find("InteractionPrompt");
        Assert.That(prompt, Is.Not.Null, "InteractionPrompt in LocalGameplayHud.prefab");
        Rect promptRect = ResolveRect(prompt, root, new Vector2(width, height));

        Assert.That(promptRect.yMin, Is.GreaterThanOrEqualTo(barTop + 16f), "prompt clearly above the bar and ability messages");
        Assert.That(promptRect.center.x, Is.EqualTo(width * 0.5f).Within(Tolerance), "prompt centered");
    }

    private static (Rect weapon, Rect q, Rect e, RectTransform hudRoot) LoadBottomBar(float width, float height)
    {
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        RaidAbilityHudView abilityView = player.GetComponentInChildren<RaidAbilityHudView>(true);
        Assert.That(abilityView, Is.Not.Null, "RaidAbilityHudView in NetworkPlayer.prefab");

        // The ability HUD and the weapon cooldown share the same HUD root inside NetworkPlayer.prefab.
        RectTransform hudRoot = (RectTransform)abilityView.transform.parent;
        RectTransform weaponRoot = (RectTransform)hudRoot.Find("RaidCooldownHud");
        Assert.That(weaponRoot, Is.Not.Null, "RaidCooldownHud next to the ability HUD");

        Vector2 canvas = new Vector2(width, height);
        Rect weapon = ResolveRect(weaponRoot, hudRoot, canvas);
        Rect q = ResolveRect((RectTransform)abilityView.transform.Find("AbilitySlot1"), hudRoot, canvas);
        Rect e = ResolveRect((RectTransform)abilityView.transform.Find("AbilitySlot2"), hudRoot, canvas);
        return (weapon, q, e, hudRoot);
    }

    private static Transform FindUnder(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
            {
                return child;
            }
        }

        Assert.Fail("Missing " + name);
        return null;
    }

    /// <summary>
    /// Resolves the rect of <paramref name="target"/> in the space of <paramref name="root"/>, which is assumed to
    /// stretch over the whole canvas (origin bottom-left, size <paramref name="rootSize"/>).
    /// </summary>
    private static Rect ResolveRect(RectTransform target, RectTransform root, Vector2 rootSize)
    {
        Rect parent = target.parent == root || target.parent == null
            ? new Rect(Vector2.zero, rootSize)
            : ResolveRect((RectTransform)target.parent, root, rootSize);

        Vector2 anchorMin = parent.min + Vector2.Scale(target.anchorMin, parent.size);
        Vector2 anchorMax = parent.min + Vector2.Scale(target.anchorMax, parent.size);
        Vector2 size = (anchorMax - anchorMin) + target.sizeDelta;
        Vector2 pivotPoint = Vector2.Lerp(anchorMin, anchorMax, 0f) +
                             Vector2.Scale(anchorMax - anchorMin, target.pivot) + target.anchoredPosition;
        Vector2 min = pivotPoint - Vector2.Scale(size, target.pivot);
        return new Rect(min, size);
    }
}
