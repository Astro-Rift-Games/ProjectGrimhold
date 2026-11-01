# 2026-11 — Unreleased history

[Archive index](README.md)

Release: Unreleased

1 original commits; 1 canonical entries.

## Canonical classification

1 features

| System | Features | Fixes | Refactors / perf | Infrastructure | Secondary | Reverts / other |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Build & Tooling | 1 | 0 | 0 | 0 | 0 | 0 |
| Combat | 1 | 0 | 0 | 0 | 0 | 0 |
| Documentation | 1 | 0 | 0 | 0 | 0 | 0 |
| Tests | 1 | 0 | 0 | 0 | 0 | 0 |
| UI | 1 | 0 | 0 | 0 | 0 | 0 |
| World & Visibility | 1 | 0 | 0 | 0 | 0 | 0 |

## Original commit records

### 735417b34141b2eb9b37e8bb5c5eb0a87cdc3f3c

[735417b34141b2eb9b37e8bb5c5eb0a87cdc3f3c](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/735417b34141b2eb9b37e8bb5c5eb0a87cdc3f3c)

Date: 2026-10-31T23:42:18-03:00 · Classification: Features (explicit) · Systems: Combat, UI, World & Visibility, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(presentation): fade the local view to black and white while Downed
```

#### Original body

```text
When the local player enters Downed the screen now blends to a
balanced-contrast black and white with a soft vignette, stays that way
while crawling or waiting for revive, and blends back to full colour on
revive or definitive Defeat. Only the Input Authority avatar's HUD
binds it, so proxies and other peers keep their normal camera, and the
Screen Space Overlay HUD is never desaturated.

- LocalPlayerDownedPostProcessPresenter owns a runtime global Volume
  (ColorAdjustments + Vignette, priority above the dungeon ambient
  Volume) with its own profile. It exists only while the effect is
  visible, is a child of the presenter (never DontSave) and is released
  on fade-out, unbind, disable or avatar loss. No renderer feature,
  shader or shared Volume asset is modified.
- DownedPostProcessBlend (pure): fade-in 0.4 s (config range 0.3-0.5),
  fade-out 0.4 s through Volume weight; vignette and dimming deepen as
  the Downed reserve drains (eased so damage chunks never pop); an
  assisted or self revive session gradually recovers part of the
  colour. Reserve and revive values are latched when Downed ends.
- DownedPostProcessConfig ScriptableObject (blend curve, timings,
  saturation -100, contrast, vignette, reserve and revive modulation)
  and its asset, wired on NetworkPlayer RaidDownedHud and bound by
  LocalPlayerHudBinder.
- Reads IsDowned, DownedHealth and TryGetProgress only; no networked
  state is added or written.

Tests: DownedPostProcessBlendTests, LocalPlayerDownedPostProcessPresenterTests
(including a regression that the Volume cannot outlive its presenter).

Docs: DownedAndReviveArchitecture section 9.1 and status table.

Refs: TASK-254
Player-Added: Your screen fades to black and white while you are downed and regains its colour as you are revived.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Scriptable Objects/DownedPostProcessConfig.asset
Project Grimhold/Assets/Scriptable Objects/DownedPostProcessConfig.asset.meta
Project Grimhold/Assets/Scripts/Player/Presentation/DownedPostProcessBlend.cs
Project Grimhold/Assets/Scripts/Player/Presentation/DownedPostProcessBlend.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/DownedPostProcessConfig.cs
Project Grimhold/Assets/Scripts/Player/Presentation/DownedPostProcessConfig.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/DownedPostProcessFrame.cs
Project Grimhold/Assets/Scripts/Player/Presentation/DownedPostProcessFrame.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/IDownedPostProcessSource.cs
Project Grimhold/Assets/Scripts/Player/Presentation/IDownedPostProcessSource.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerDownedPostProcessPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerDownedPostProcessPresenter.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerHudBinder.cs
Project Grimhold/Assets/Scripts/Player/Presentation/PlayerDownedPostProcessSource.cs
Project Grimhold/Assets/Scripts/Player/Presentation/PlayerDownedPostProcessSource.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/DownedPostProcessBlendTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/DownedPostProcessBlendTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/LocalPlayerDownedPostProcessPresenterTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/LocalPlayerDownedPostProcessPresenterTests.cs.meta
Project Grimhold/Docs/Architecture/DownedAndReviveArchitecture.md
```

#### Classification evidence

- type → Features: feat
- message-term → Combat: damage
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerHudBinder.cs
- message-term → UI: HUD
- message-term → World &amp; Visibility: dungeon
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/DownedPostProcessBlendTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/DownedPostProcessBlendTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/LocalPlayerDownedPostProcessPresenterTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/LocalPlayerDownedPostProcessPresenterTests.cs.meta
- path → Documentation: Project Grimhold/Docs/Architecture/DownedAndReviveArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/DownedPostProcessBlendTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/DownedPostProcessBlendTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/LocalPlayerDownedPostProcessPresenterTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/LocalPlayerDownedPostProcessPresenterTests.cs.meta
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

