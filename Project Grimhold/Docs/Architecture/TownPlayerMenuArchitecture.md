# Town Player Menu Architecture

## Scope

The Town player menu is one framed window with a tab bar that hosts the local player's personal screens
in Town. It currently hosts **Inventory** and **Attributes**. Abilities is the planned next tab. Raid keeps
the standalone personal inventory and does not use this menu. Stash, Merchant, Mission Board and Raid
Preparation stay NPC-opened panels: they depend on a confirmed interaction and are not tabs yet.

## Ownership

`TownPlayerMenuPresenter` (plain `MonoBehaviour` on `SocialPlayer`, no networking) is the single owner of:

- the Tab (inventory) and C (attributes) hotkeys, as `PlayerInputReader.InventoryToggleRequested` and
  `AttributesToggleRequested`;
- the Escape close, as a `PlayerInputReader.InventoryCloseRequested` handler that returns true only while
  the menu is open;
- the one `AcquireGameplayInputSuppression()` token held while the menu is open;
- tab switching, through the pure `TownMenuTabState`.

`TownMenuTabState` is plain C# with EditMode tests. Opening the tab that is already showing closes the
menu (hotkey toggle); opening another tab while open switches; clicking a tab switches; opening is refused
while another panel already holds a suppression token, so two overlays cannot stack.

`TownPlayerMenuView` is passive: window, tab bar, tab button template, content root, title and close
button. `TownPlayerMenu.prefab` is instantiated by the presenter as the first sibling of the Town inventory
canvas content, so the inventory tooltip and drag preview, which live under that canvas, render above it.

## Tab registry

A feature plugs in with a `TownMenuTabRegistration` (id, label, content `RectTransform`, shown and hidden
callbacks). The menu reparents the content under its content root, stretches it, and calls `Shown` and
`Hidden`; it restores the original parent, anchors and sibling index when the tab is unregistered or the
menu is destroyed. The feature keeps ownership of its own state and visibility. A tab whose registration is
absent is not listed and its hotkey is ignored. Tab ids and their order live in `TownMenuTabIds`; adding a
tab means adding an id there and a registration from the feature.

| Tab | Registered by | Content | Hosted behavior |
|---|---|---|---|
| Inventory | `TownInventoryBinder` | `RaidInventoryView.ScreenRootRect` | `RaidInventoryPresenter.BindTown(..., externallyHosted: true)`, `ShowHosted`, `HideHosted` |
| Attributes | `TownAttributeAssignmentPresenter` | the instantiated `TownAttributeAssignmentView` root | `Open` / `Close`; the panel's own close button is hidden |

## Hosted mode contract

When hosted, a feature presenter does not subscribe to its own hotkeys or Escape and does not acquire an
input-suppression token. Without a wired menu (`TownAttributeAssignmentPresenter._menu` null, or
`BindTown` called without `externallyHosted`) the previous standalone behavior is unchanged, and the Raid
inventory binding never uses hosted mode.

## HUD while the menu is open

While the menu is open, the Town HUD is hidden so it never overlaps the window. `TownHudVisibility` (plain C#)
hides each registered HUD root through a `CanvasGroup` (alpha 0, not interactable, no raycasts) and restores
the original group values when the menu closes. It never calls `SetActive`, so each HUD keeps its own
activation logic. `TownPlayerMenuPresenter` serializes the static roots (`TownInteractionHud`, `TownPartyHud`)
and exposes `RegisterHud` / `UnregisterHud` for HUD created at runtime: `TownProgressionPresenter` registers
its instantiated view and unregisters it before destroying it. A HUD registered while the menu is open is
hidden immediately.

## Party HUD visibility

Every local player is given a solo party by `TownPartyDirectory`, so "has a party" is always true. The party
HUD therefore shows only for an **active party**, meaning a party with a companion
(`TownPartyPresentation.HasCompanion`), and only for the Input Authority player. A solo party, a missing
party and a disbanded party all keep it hidden (`TownPartyHudView.ClearParty`).

## Known limits

- Ability slot 2 is bound to R, not E. The reference mock-up shows Q/E; decide labels and bindings before
  building the Abilities tab.
- The Abilities tab still needs display metadata on `AbilityDefinition`, a read-only catalog enumerator
  and a Town/Ready-gated equip endpoint (see `AbilitySystemArchitecture.md`).
- `TownPauseMenuPresenter` polls the legacy Escape key independently of `PlayerInputReader`, so Escape
  can still toggle the pause menu while a panel is open.
- The framed window uses placeholder colors; final art is authored in `TownPlayerMenu.prefab`.
