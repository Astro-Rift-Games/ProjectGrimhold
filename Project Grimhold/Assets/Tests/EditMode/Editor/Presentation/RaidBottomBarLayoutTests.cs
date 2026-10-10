using NUnit.Framework;
using TMPro;
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

    [TestCase(1920f, 1080f)]
    [TestCase(1440f, 1080f)]
    public void SlotsAreLargeEnoughToReadAndIdentical(float width, float height)
    {
        (Rect weapon, Rect q, Rect e, _) = LoadBottomBar(width, height);

        foreach (Rect slot in new[] { weapon, q, e })
        {
            Assert.That(slot.width, Is.GreaterThanOrEqualTo(72f), "slot width");
            Assert.That(slot.height, Is.EqualTo(slot.width).Within(Tolerance), "slot is square");
        }
    }

    [Test]
    public void SlotIconsAndOverlaysFillMostOfTheSlotFrame()
    {
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        RaidAbilityHudView abilityView = player.GetComponentInChildren<RaidAbilityHudView>(true);
        RectTransform hudRoot = (RectTransform)abilityView.transform.parent;
        Vector2 canvas = new Vector2(1920f, 1080f);

        (RectTransform slot, string[] layers)[] slots =
        {
            ((RectTransform)FindUnder(hudRoot, "RaidCooldownHud"), new[] { "CooldownIcon", "CooldownFill" }),
            ((RectTransform)abilityView.transform.Find("AbilitySlot1"), new[] { "Icon", "Empty", "InsufficientResource", "CooldownFill" }),
            ((RectTransform)abilityView.transform.Find("AbilitySlot2"), new[] { "Icon", "Empty", "InsufficientResource", "CooldownFill" })
        };

        foreach ((RectTransform slot, string[] layers) in slots)
        {
            Rect slotRect = ResolveRect(slot, hudRoot, canvas);
            foreach (string layer in layers)
            {
                Rect rect = ResolveRect((RectTransform)slot.Find(layer), hudRoot, canvas);
                Assert.That(rect.width, Is.GreaterThanOrEqualTo(slotRect.width - 24f), slot.name + "/" + layer + " width");
                Assert.That(rect.width, Is.LessThanOrEqualTo(slotRect.width - 12f), slot.name + "/" + layer + " stays inside the frame border");
                Assert.That(rect.center.x, Is.EqualTo(slotRect.center.x).Within(Tolerance), slot.name + "/" + layer + " centered");
                Assert.That(rect.center.y, Is.EqualTo(slotRect.center.y).Within(Tolerance), slot.name + "/" + layer + " centered");
            }
        }
    }

    [TestCase(1920f, 1080f)]
    [TestCase(1440f, 1080f)]
    public void ContainerBarEnclosesTheThreeSlotsWithEvenPaddingAndIsCenteredAtTheBottom(float width, float height)
    {
        (Rect weapon, Rect q, Rect e, RectTransform hudRoot) = LoadBottomBar(width, height);
        Rect bar = ResolveRect(LoadActionBarFrame(hudRoot), hudRoot, new Vector2(width, height));

        float left = Mathf.Min(weapon.xMin, Mathf.Min(q.xMin, e.xMin));
        float right = Mathf.Max(weapon.xMax, Mathf.Max(q.xMax, e.xMax));
        float bottom = Mathf.Min(weapon.yMin, Mathf.Min(q.yMin, e.yMin));
        float top = Mathf.Max(weapon.yMax, Mathf.Max(q.yMax, e.yMax));

        float padLeft = left - bar.xMin;
        float padRight = bar.xMax - right;
        float padBottom = bottom - bar.yMin;
        float padTop = bar.yMax - top;
        Assert.That(padLeft, Is.InRange(8f, 20f), "left padding");
        Assert.That(padRight, Is.EqualTo(padLeft).Within(Tolerance), "right padding matches left");
        Assert.That(padBottom, Is.EqualTo(padLeft).Within(Tolerance), "bottom padding matches left");
        Assert.That(padTop, Is.EqualTo(padLeft).Within(Tolerance), "top padding matches left");

        Assert.That(bar.center.x, Is.EqualTo(width * 0.5f).Within(Tolerance), "container centered");
        Assert.That(bar.yMin, Is.GreaterThanOrEqualTo(0f), "container inside the screen");
        Assert.That(bar.yMax, Is.LessThanOrEqualTo(height * 0.15f), "container stays in the bottom 15% of the screen");
    }

    [Test]
    public void ContainerBarIsDrawnBehindEverySlotAndUsesTheDarkHudFrame()
    {
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        RaidAbilityHudView abilityView = player.GetComponentInChildren<RaidAbilityHudView>(true);
        RectTransform hudRoot = (RectTransform)abilityView.transform.parent;
        RectTransform bar = LoadActionBarFrame(hudRoot);

        Image image = bar.GetComponent<Image>();
        Assert.That(image, Is.Not.Null, "container image");
        Assert.That(image.sprite, Is.Not.Null, "container sprite");
        Assert.That(image.type, Is.EqualTo(Image.Type.Sliced), "container is a sliced frame");
        Assert.That(image.raycastTarget, Is.False, "container never blocks clicks");

        int barIndex = bar.GetSiblingIndex();
        Assert.That(FindUnder(hudRoot, "RaidCooldownHud").GetSiblingIndex(), Is.GreaterThan(barIndex), "weapon slot above container");
        Assert.That(abilityView.transform.GetSiblingIndex(), Is.GreaterThan(barIndex), "ability slots above container");
    }

    [Test]
    public void EverySlotHasAReadableKeyBadgeAtItsTopLeftCorner()
    {
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        RaidAbilityHudView abilityView = player.GetComponentInChildren<RaidAbilityHudView>(true);
        RectTransform hudRoot = (RectTransform)abilityView.transform.parent;
        Vector2 canvas = new Vector2(1920f, 1080f);

        (Transform slot, string expected)[] slots =
        {
            (FindUnder(hudRoot, "RaidCooldownHud"), RaidWeaponSlotKeyLabel.Label),
            (abilityView.transform.Find("AbilitySlot1"), TownAbilitySlotKeyLabels.Slot1),
            (abilityView.transform.Find("AbilitySlot2"), TownAbilitySlotKeyLabels.Slot2)
        };

        foreach ((Transform slot, string expected) in slots)
        {
            Rect slotRect = ResolveRect((RectTransform)slot, hudRoot, canvas);
            RectTransform badge = (RectTransform)slot.Find("KeyBadge");
            RectTransform key = (RectTransform)slot.Find("Key");
            Assert.That(badge, Is.Not.Null, slot.name + " KeyBadge");
            Assert.That(key, Is.Not.Null, slot.name + " Key label");

            Image badgeImage = badge.GetComponent<Image>();
            Assert.That(badgeImage, Is.Not.Null, slot.name + " badge image");
            Assert.That(badgeImage.color.a, Is.GreaterThanOrEqualTo(0.8f), slot.name + " badge is opaque enough to read over the icon");
            Assert.That(badgeImage.raycastTarget, Is.False, slot.name + " badge never blocks clicks");

            Rect badgeRect = ResolveRect(badge, hudRoot, canvas);
            Rect keyRect = ResolveRect(key, hudRoot, canvas);
            Assert.That(badgeRect.height, Is.GreaterThanOrEqualTo(20f), slot.name + " badge height");
            Assert.That(badgeRect.xMin - slotRect.xMin, Is.InRange(0f, 8f), slot.name + " badge hugs the left edge");
            Assert.That(slotRect.yMax - badgeRect.yMax, Is.InRange(0f, 8f), slot.name + " badge hugs the top edge");
            Assert.That(badgeRect.width, Is.LessThanOrEqualTo(slotRect.width * 0.6f), slot.name + " badge leaves most of the icon visible");
            Assert.That(badgeRect.height, Is.LessThanOrEqualTo(slotRect.height * 0.35f), slot.name + " badge stays compact");
            Assert.That(keyRect.xMin, Is.GreaterThanOrEqualTo(badgeRect.xMin - Tolerance), slot.name + " key inside badge (left)");
            Assert.That(keyRect.xMax, Is.LessThanOrEqualTo(badgeRect.xMax + Tolerance), slot.name + " key inside badge (right)");
            Assert.That(keyRect.yMax, Is.LessThanOrEqualTo(badgeRect.yMax + Tolerance), slot.name + " key inside badge (top)");
            Assert.That(keyRect.yMin, Is.GreaterThanOrEqualTo(badgeRect.yMin - Tolerance), slot.name + " key inside badge (bottom)");

            TMP_Text text = key.GetComponent<TMP_Text>();
            Assert.That(text.text, Is.EqualTo(expected), slot.name + " key text");
            Assert.That(text.fontSize, Is.GreaterThanOrEqualTo(14f), slot.name + " key font size");
            Assert.That(key.GetSiblingIndex(), Is.GreaterThan(badge.GetSiblingIndex()), slot.name + " key drawn over its badge");
        }
    }

    [Test]
    public void CooldownSecondsAreLargeWithAnOutlineSoTheyStayReadableOverTheIcon()
    {
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        RaidAbilityHudView abilityView = player.GetComponentInChildren<RaidAbilityHudView>(true);
        RectTransform hudRoot = (RectTransform)abilityView.transform.parent;

        (Transform seconds, string name)[] labels =
        {
            (FindUnder(hudRoot, "RaidCooldownHud").Find("CooldownSeconds"), "weapon"),
            (abilityView.transform.Find("AbilitySlot1").Find("Seconds"), "Q"),
            (abilityView.transform.Find("AbilitySlot2").Find("Seconds"), "E")
        };

        foreach ((Transform seconds, string name) in labels)
        {
            TMP_Text text = seconds.GetComponent<TMP_Text>();
            Assert.That(text, Is.Not.Null, name + " seconds label");
            float size = text.enableAutoSizing ? text.fontSizeMax : text.fontSize;
            Assert.That(size, Is.GreaterThanOrEqualTo(20f), name + " seconds font size");
            Material material = text.fontSharedMaterial;
            Assert.That(material.HasProperty("_OutlineWidth"), Is.True, name + " material supports an outline");
            Assert.That(material.GetFloat("_OutlineWidth"), Is.GreaterThan(0.05f), name + " outline is visible");
            Assert.That(material.GetColor("_OutlineColor").a, Is.GreaterThan(0.5f), name + " outline colour is opaque");
        }
    }

    [TestCase(1920f, 1080f)]
    [TestCase(1440f, 1080f)]
    public void ContainerBarDoesNotOverlapVitalsTeammateHudOrPressureHud(float width, float height)
    {
        (_, _, _, RectTransform hudRoot) = LoadBottomBar(width, height);
        Vector2 canvas = new Vector2(width, height);
        Rect bar = ResolveRect(LoadActionBarFrame(hudRoot), hudRoot, canvas);

        GameObject hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
        RectTransform root = (RectTransform)hud.transform;
        RectTransform frame = (RectTransform)hud.transform.Find("RaidMainHud");
        Assert.That(bar.Overlaps(ResolveRect(frame, root, canvas)), Is.False, "container vs vitals");
        Assert.That(bar.Overlaps(ResolveRect((RectTransform)frame.Find("DefeatedIndicator"), root, canvas)), Is.False, "container vs defeated indicator");
        Assert.That(bar.Overlaps(ResolveRect((RectTransform)hud.transform.Find("RaidDuoHud"), root, canvas)), Is.False, "container vs teammate HUD");
        Assert.That(bar.Overlaps(ResolveRect((RectTransform)hud.transform.Find("DungeonPressureHUD/Timer"), root, canvas)), Is.False, "container vs pressure timer");
        Assert.That(bar.Overlaps(ResolveRect((RectTransform)hud.transform.Find("InteractionPrompt"), root, canvas)), Is.False, "container vs interaction prompt");
        Assert.That(bar.Overlaps(ResolveRect((RectTransform)hud.transform.Find("InteractionFeedback"), root, canvas)), Is.False, "container vs interaction feedback");
    }

    [Test]
    public void AttackTextOverlayFollowsTheWeaponSlot()
    {
        (Rect weapon, _, _, RectTransform hudRoot) = LoadBottomBar(1920f, 1080f);
        Rect attack = ResolveRect((RectTransform)hudRoot.Find("AttackText"), hudRoot, new Vector2(1920f, 1080f));

        Assert.That(attack.center.x, Is.EqualTo(weapon.center.x).Within(Tolerance), "AttackText x");
        Assert.That(attack.center.y, Is.EqualTo(weapon.center.y).Within(Tolerance), "AttackText y");
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
    public void VitalsBlockIsAnchoredAtTheBottomLeftWithAMargin()
    {
        GameObject hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
        RectTransform frame = (RectTransform)hud.transform.Find("RaidMainHud");

        Assert.That(frame.anchorMin, Is.EqualTo(Vector2.zero), "anchor min bottom-left");
        Assert.That(frame.anchorMax, Is.EqualTo(Vector2.zero), "anchor max bottom-left");
        Assert.That(frame.pivot, Is.EqualTo(Vector2.zero), "pivot bottom-left");
        Assert.That(frame.anchoredPosition.x, Is.InRange(16f, 32f), "left margin");
        Assert.That(frame.anchoredPosition.y, Is.InRange(16f, 32f), "bottom margin");
    }

    [Test]
    public void VitalsRowsAreHealthManaStaminaStackedWithoutOverlapInsideTheFrame()
    {
        GameObject hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
        RectTransform root = (RectTransform)hud.transform;
        RectTransform frame = (RectTransform)hud.transform.Find("RaidMainHud");
        Vector2 size = new Vector2(1920f, 1080f);
        Rect frameRect = ResolveRect(frame, root, size);

        string[] order = { "Health", "Mana", "Stamina" };
        Rect previous = default;
        for (int i = 0; i < order.Length; i++)
        {
            Transform rowTransform = frame.Find(order[i]);
            Assert.That(rowTransform, Is.Not.Null, order[i] + " row");
            Rect row = ResolveRect((RectTransform)rowTransform, root, size);
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

        Assert.That(previous.yMin - frameRect.yMin, Is.LessThanOrEqualTo(16f), "frame is shrunk to its content");
    }

    [Test]
    public void EachVitalsRowHasAnIconOnTheLeftAndABarToItsRight()
    {
        GameObject hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
        RectTransform root = (RectTransform)hud.transform;
        RectTransform frame = (RectTransform)hud.transform.Find("RaidMainHud");
        Vector2 size = new Vector2(1920f, 1080f);

        foreach (string rowName in new[] { "Health", "Mana", "Stamina" })
        {
            RectTransform row = (RectTransform)frame.Find(rowName);
            RectTransform icon = (RectTransform)row.Find(rowName + "Icon");
            RectTransform track = (RectTransform)row.Find(rowName + "Track");
            Assert.That(icon, Is.Not.Null, rowName + "Icon");
            Assert.That(track, Is.Not.Null, rowName + "Track");
            Assert.That(icon.GetComponent<Image>().sprite, Is.Not.Null, rowName + " icon sprite");

            Rect iconRect = ResolveRect(icon, root, size);
            Rect trackRect = ResolveRect(track, root, size);
            Assert.That(iconRect.xMax, Is.LessThanOrEqualTo(trackRect.xMin + Tolerance), rowName + " icon left of the bar");
            Assert.That(trackRect.width, Is.GreaterThan(160f), rowName + " bar is wide enough for the value text");
        }
    }

    [Test]
    public void DefeatedIndicatorSitsDirectlyAboveTheVitalsFrame()
    {
        GameObject hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
        RectTransform root = (RectTransform)hud.transform;
        RectTransform frame = (RectTransform)hud.transform.Find("RaidMainHud");
        Vector2 size = new Vector2(1920f, 1080f);
        Rect frameRect = ResolveRect(frame, root, size);
        Rect defeated = ResolveRect((RectTransform)frame.Find("DefeatedIndicator"), root, size);

        Assert.That(defeated.yMin - frameRect.yMax, Is.InRange(0f, 12f), "defeated indicator hugs the frame top");
        Assert.That(defeated.xMin, Is.GreaterThanOrEqualTo(frameRect.xMin - Tolerance));
        Assert.That(defeated.xMax, Is.LessThanOrEqualTo(frameRect.xMax + Tolerance));
    }

    [Test]
    public void TeammateHudSitsAboveTheVitalsBlockAndDefeatedIndicator()
    {
        GameObject hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
        RectTransform root = (RectTransform)hud.transform;
        Vector2 size = new Vector2(1920f, 1080f);
        RectTransform frame = (RectTransform)hud.transform.Find("RaidMainHud");
        Rect vitals = ResolveRect(frame, root, size);
        Rect defeated = ResolveRect((RectTransform)frame.Find("DefeatedIndicator"), root, size);
        Rect duo = ResolveRect((RectTransform)hud.transform.Find("RaidDuoHud"), root, size);

        Assert.That(duo.yMin, Is.GreaterThanOrEqualTo(defeated.yMax), "teammate HUD above the defeated indicator");
        Assert.That(duo.yMin - vitals.yMax, Is.LessThanOrEqualTo(64f), "teammate HUD stays close to the vitals block");
        Assert.That(duo.xMin, Is.EqualTo(vitals.xMin).Within(Tolerance), "teammate HUD shares the vitals left edge");
    }

    [TestCase(1920f, 1080f)]
    [TestCase(1440f, 1080f)]
    public void VitalsBlockDoesNotOverlapTheActionBarTeammateHudOrOtherHudBlocks(float width, float height)
    {
        GameObject hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
        RectTransform root = (RectTransform)hud.transform;
        Vector2 size = new Vector2(width, height);
        RectTransform frame = (RectTransform)hud.transform.Find("RaidMainHud");
        Rect vitals = ResolveRect(frame, root, size);
        Rect defeated = ResolveRect((RectTransform)frame.Find("DefeatedIndicator"), root, size);
        Rect duo = ResolveRect((RectTransform)hud.transform.Find("RaidDuoHud"), root, size);
        Rect timer = ResolveRect((RectTransform)hud.transform.Find("DungeonPressureHUD/Timer"), root, size);
        Rect phase = ResolveRect((RectTransform)hud.transform.Find("DungeonPressureHUD/Phase"), root, size);
        Rect prompt = ResolveRect((RectTransform)hud.transform.Find("InteractionPrompt"), root, size);
        (Rect weapon, Rect q, Rect e, _) = LoadBottomBar(width, height);

        Assert.That(vitals.Overlaps(duo), Is.False, "vitals vs teammate HUD");
        Assert.That(defeated.Overlaps(duo), Is.False, "defeated indicator vs teammate HUD");
        Assert.That(vitals.Overlaps(timer), Is.False, "vitals vs pressure timer");
        Assert.That(vitals.Overlaps(phase), Is.False, "vitals vs pressure phase");
        Assert.That(duo.Overlaps(timer), Is.False, "teammate HUD vs pressure timer");
        Assert.That(duo.Overlaps(phase), Is.False, "teammate HUD vs pressure phase");
        Assert.That(vitals.Overlaps(weapon), Is.False, "vitals vs weapon slot");
        Assert.That(vitals.Overlaps(q), Is.False, "vitals vs Q slot");
        Assert.That(vitals.Overlaps(e), Is.False, "vitals vs E slot");
        Assert.That(duo.Overlaps(weapon), Is.False, "teammate HUD vs weapon slot");
        Assert.That(vitals.Overlaps(prompt), Is.False, "vitals vs interaction prompt");
        Assert.That(vitals.xMin, Is.GreaterThanOrEqualTo(0f));
        Assert.That(vitals.yMin, Is.GreaterThanOrEqualTo(0f));
    }

    [TestCase(1920f, 1080f)]
    [TestCase(1440f, 1080f)]
    public void InteractionPromptSitsClearlyAboveTheActionBarAndAbilityMessages(float width, float height)
    {
        (Rect weapon, Rect q, Rect e, RectTransform abilityHudRoot) = LoadBottomBar(width, height);
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        RaidAbilityHudView abilityView = player.GetComponentInChildren<RaidAbilityHudView>(true);

        float barTop = Mathf.Max(weapon.yMax, Mathf.Max(q.yMax, e.yMax));
        barTop = Mathf.Max(barTop, ResolveRect(LoadActionBarFrame(abilityHudRoot), abilityHudRoot, new Vector2(width, height)).yMax);
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

    private static RectTransform LoadActionBarFrame(RectTransform hudRoot)
    {
        Transform bar = hudRoot.Find("ActionBarFrame");
        Assert.That(bar, Is.Not.Null, "ActionBarFrame container under LocalGameplayHud");
        return (RectTransform)bar;
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
