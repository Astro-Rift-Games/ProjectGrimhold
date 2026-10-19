# UI Animated Feedback Architecture

## Scope

Local, presentation-only animation for uGUI screens (TASK-158): hover and press feedback on
controls, show/hide transitions on panels, success/rejection pulses on confirmed results, and a
full-screen fade between scenes. Nothing here is networked, persisted or allowed to advance
gameplay or session state. No tween library is used; the components share a few pure C# states.

All timing uses `Time.unscaledDeltaTime`. No component changes `Time.timeScale`, so screens used
during active gameplay never imply a pause (UI audit, general principles).

## Components

Code lives in `Assets/Scripts/UI/Animation/`.

| Piece | Kind | Responsibility |
|---|---|---|
| `UiEasing` | pure | Symmetric smoothstep, so reversing a transition never jumps. |
| `UiPanelTransitionState` | pure | Show/hide progress with immediate logical visibility. |
| `UiPointerResponse` | pure | Target scale for hover/press and frame-rate independent approach. |
| `UiPulseState` | pure | Success scale punch and rejection shake, resting at neutral. |
| `SceneTransitionFadePolicy` | pure | Which session states keep the screen covered. |
| `UiButtonFeedback` | component | Hover/press/selection scale on a Button or Toggle. |
| `UiPanelTransition` | component | Fade (and optional scale) of a panel root through its `CanvasGroup`. |
| `UiPanelVisibility` | static helper | Drop-in for `root.SetActive(v)` / `root.activeSelf` in views. |
| `UiFeedbackPulse` | component | Plays a `UiPulseState` on its target when asked. |
| `SceneTransitionFader` | component | Full-screen fade between scenes. |

Pure pieces have EditMode tests (`UiAnimationStateTests`); panel transition runtime behavior has
PlayMode tests (`UiPanelTransitionPlayModeTests`). Both use the `TASK158` category.

## Panel transitions

A view keeps owning its visibility. It calls `UiPanelVisibility.SetVisible(root, visible)` and
derives `IsOpen` from `UiPanelVisibility.IsVisible(root)`. A root without `UiPanelTransition`
toggles immediately, exactly like before; adding the component to the root is what enables
animation.

Contract:

- Logical visibility changes immediately. `IsShown` turns false as soon as a hide starts, and the
  `CanvasGroup` stops blocking raycasts and interaction at that moment. Callers that ask whether a
  panel is open, and input-suppression tokens, never wait for the animation.
- Only `SetActive(false)` is deferred until the hide completes.
- Activating the GameObject by any means plays the show transition from transparent.
- Showing during a hide reverses from the current visibility.
- If a parent is disabled mid-hide, the panel finishes hidden on its next active frame.
- A direct `SetActive(false)` mid-hide simply completes the hide.
- `_hiddenScale = 1` disables the scale effect, and the component then never writes the scale.
  Use it for:
  - root canvases, whose scale Unity drives;
  - Town menu tab content, whose host resets the scale;
  - panels that animate their own scale (the container panel's capacity pulse).

Wired panels:

| Panel | `_hiddenScale` |
|---|---|
| Town menu window | 0.96 |
| Raid menu | 0.96 |
| Progression results | 0.96 |
| Raid preparation code panel | 0.96 |
| Dialogue panel | 0.96 |
| Mission board | 0.96 |
| Main Menu, Login and Character creation | 0.96 |
| Inventory screen | 1 (fade only) |
| Merchant | 1 (fade only) |
| Stash | 1 (fade only) |
| Ability vendor | 1 (fade only) |
| Attributes, Abilities and Options tabs | 1 (fade only) |
| Container loot panel | 1 (fade only) |

## Control feedback

`UiButtonFeedback` scales its target toward 1.05 on hover or keyboard/gamepad selection and 0.95
while pressed. It ignores selection caused by a pointer click, so a clicked button does not stay
enlarged. It returns to 1 while the `Selectable` is not interactable and resets on disable.

It complements the existing color/sprite `Selectable` transition and never changes
interactability, layout or click handling.

`Grimhold/UI/Add Button Feedback To Prefabs And Open Scenes` (`UiButtonFeedbackInstaller`) adds it
to every Button and Toggle under `Assets/Prefabs` and in the open scenes. It is idempotent and
skips objects owned by a nested prefab. Re-run it after adding new buttons.

## Result pulses

Views play `UiFeedbackPulse.Play(target, kind)` after a result has been confirmed. A target
without the component is ignored.

| View | Target | Kind |
|---|---|---|
| `MerchantShopUI.PresentResult` | feedback text | Success for `Success`/`AlreadyApplied`, otherwise Rejection |
| `TownAbilityVendorView.PulseFeedback` | feedback text | Purchase result |
| `TownAbilitiesView.PulseFeedback` | feedback line | Rejected equip/clear |
| `RaidInventoryView.ShowTransferFeedback` | transfer feedback root | Rejection |

Container and inventory slots keep their existing transfer and capacity feedback (TASK-183); it is
not replaced.

## Scene transitions

`SceneTransitionFader` (`SceneTransitionFader.prefab`) is nested under the
`SessionConnectionCoordinator` GameObject in `Systems.prefab`, so it persists with the coordinator.

| Trigger | Fade |
|---|---|
| `StateChanged` to `ConnectingTown`, `ConnectingRaid` or `ReturningTown` | Fades to black, blocks input and shows "Cargando..." once fully covered. |
| `StateChanged` to any other state, including `Failed` | Fades in. |
| `SceneManager.sceneLoaded` (single mode) | Snaps to black, then fades in, unless the current state still covers. This covers loads outside a session transition, such as logout to MainMenu. |

The fader only observes `StateChanged`, which the session architecture defines as local
presentation feedback. It never delays a transition. When MainMenu is reloaded, the duplicate
coordinator destroys itself and its fader stays inert.

## Known limits

- Logout loads MainMenu directly, so it fades in but has no fade-out.
- No per-screen timing comes from Game Design yet. Durations and scales are serialized defaults to
  tune once the UI rework (#335) assets land.
