# Town Player Menu Architecture

## Scope

The Town player menu is one framed window with a tab bar that hosts the local player's personal screens
in Town. It currently hosts **Inventory**, **Attributes** and **Options**. Abilities is the planned next
tab. Options replaces the former standalone Town pause menu and is where sound, graphics and other
settings will be added; today it only offers Log out and Exit game. Raid keeps
the standalone personal inventory and does not use this menu. Stash, Merchant, Mission Board and Raid
Preparation stay NPC-opened panels: they depend on a confirmed interaction and are not tabs yet.

## Ownership

`TownPlayerMenuPresenter` (plain `MonoBehaviour` on `SocialPlayer`, no networking) is the single owner of:

- the Tab (inventory) and C (attributes) hotkeys, as `PlayerInputReader.InventoryToggleRequested` and
  `AttributesToggleRequested`;
- the Escape close, as a `PlayerInputReader.InventoryCloseRequested` handler that returns true only while
  the menu is open, and the Escape open of the Options tab (see Escape ownership);
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
`Hidden`. It also resets the content's local scale to one: a prefab whose root is a Canvas can be saved with a
zero scale that Unity only drives while it is a root canvas, which left the Attributes tab invisible once it was
hosted. It never moves content back: hosted content belongs to the same hierarchy as the window and is
destroyed with it, and reparenting during teardown would target a parent that is already being destroyed.
A feature that owns its content instance (Attributes, Options) destroys it when it unregisters; the
inventory screen stays hosted, hidden, until it is registered again. The feature keeps ownership of its own
state and visibility. A tab whose registration is
absent is not listed and its hotkey is ignored. Tab ids and their order live in `TownMenuTabIds`; adding a
tab means adding an id there and a registration from the feature.

| Tab | Registered by | Content | Hosted behavior |
|---|---|---|---|
| Inventory | `TownInventoryBinder` | `RaidInventoryView.ScreenRootRect` | `RaidInventoryPresenter.BindTown(..., externallyHosted: true)`, `ShowHosted`, `HideHosted` |
| Attributes | `TownAttributeAssignmentPresenter` | the instantiated `TownAttributeAssignmentView` root | `Open` / `Close`; the panel's own close button is hidden |
| Options | `TownOptionsPresenter`, registered by `TownInventoryBinder` | the instantiated `TownOptionsView` root (`TownOptions.prefab`) | `Open` / `Close`; Log out and Exit game buttons |

## Attributes tab: character sheet

The Attributes tab is a three-column character sheet: attributes with their assignment buttons (left), the
eight prepared equipment slots (center, read-only; equipping stays in the Inventory tab) and the derived
statistics (right). Everything is projected from the confirmed profile and refreshes on the single
`ApplicationStashContext.ProfileCommitted` signal, so attribute assignment and equipment changes both update it.

`TownCharacterStatisticsBuilder` (pure, EditMode-tested) builds `TownCharacterStatisticsPresentation` from
`CharacterAttributeState`, `PreparedEquipmentLoadout` and the loot catalog. It reuses the calculators the Raid
player uses (`CharacterDerivedStatisticsCalculator`, `EquipmentStatisticsCalculator`,
`PlayerRuntimeStatisticsCalculator`, `WeaponScalingContributionsResolver`, `WeaponDamageCalculator`) with
`ProgressionBalanceDefaults`, so Town and Raid numbers match. `TownAttributeAssignmentBinding` optionally
rebuilds it together with the attributes and clears it when they or the builder are unavailable;
`TownCharacterStatisticsLines` turns it into culture-independent rows.

Only statistics the Game Design defines are shown (docs 08 and 09):

| Section | Rows |
|---|---|
| Core | Max Health, Max Stamina, Max Mana, each with its equipment contribution ("+20 from equipment") |
| Defense | Physical and Magical Defense from armor, with the mitigation `Defense / (Defense + K)`, K = 100 |
| Weapons | One row per equipped weapon with its effective damage (base x attribute scaling), slot and damage type |
| Utility | Loot Bonus (additional loot chance from Luck) |

There is deliberately no Melee/Ranged/Magic Power or Move Speed row, and no ring or amulet slot: the Game Design
defines no global damage stat (FUE, DES and INT only feed weapon scaling and requirements), no attribute or
equipment influence on move speed, and only four armor slots plus two Weapon Sets. `K` is read from
`TownCharacterStatisticsBuilder.DefaultDefenseMitigationConstant`, which mirrors the serialized
`PlayerCharacter._defenseMitigationConstant`. All authored weapons currently have a zero scaling coefficient, so
effective damage equals base damage today.

## Hosted mode contract

When hosted, a feature presenter does not subscribe to its own hotkeys or Escape and does not acquire an
input-suppression token. Without a wired menu (`TownAttributeAssignmentPresenter._menu` null, or
`BindTown` called without `externallyHosted`) the previous standalone behavior is unchanged, and the Raid
inventory binding never uses hosted mode.

## Escape ownership

Escape has one owner, `PlayerInputReader`. It offers the press to every `InventoryCloseRequested` handler
(the Town menu, Stash, standalone panels); only when none consumes it does it raise `MenuToggleRequested`.
`TownPlayerMenuPresenter` opens the Options tab from `MenuToggleRequested`, so Escape opens Options only when
no other panel is open: the press that closes the menu or another panel never also opens Options, and the
open is refused while any panel still holds an input-suppression token. While the menu is open Escape closes
it from any tab. The old standalone pause menu (`TownPauseMenuPresenter`, `TownPauseMenuView` and the
`TownPauseMenu` object in `Lobby-Town`) no longer exists; its logout sequence now lives in
`TownOptionsPresenter`.

Exit game calls `Application.Quit()` (`EditorApplication.ExitPlaymode()` in the Editor). Both actions are
disabled while a logout is running.

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
- `MissionBoardUI` still reads Escape (and its toggle key) through the legacy `Input` API, so closing the
  Mission Board with Escape can also open the Options tab.
- Options is only reachable once the local player exists and its menu is bound. Before the player spawns,
  or if the Town lifecycle fails to bind, there is no Escape-to-logout path; the standalone pause menu that
  covered that case was removed.
- The framed window uses placeholder colors; final art is authored in `TownPlayerMenu.prefab`.
