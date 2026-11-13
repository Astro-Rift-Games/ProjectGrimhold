# Changelog

What's new in Project Grimhold. Only changes included in published releases are listed, newest first.

## [v0.6.0-dev] - En desarrollo

### Added

- Knocking an enemy into a trap now counts as your knockdown when the trap's damage takes them down.
- Dungeon doors can be opened and closed with F; closed doors block movement, attacks and sight, and open away from you.
- Pilgrim's Light, Void Bell, and Wanderer's Eye now apply their passive effects while carried in raids.
- In-raid HUD displays active contracts and objective progress notifications.
- Crafting and mission resources like black iron now drop from chests and enemies in raids.
- Ruby, Sapphire and Attunement gems can now be found in Dungeon chests and enemy drops.
- In Town, drag a gem onto a weapon in your Inventory or Stash, or use "Incrustar" on the gem, to permanently add its damage bonus to the weapon.
- Weapons inside Dungeon chests and enemy drops now each have their own slot, showing their gems and bonus damage, and you take exactly the one you pick.
- Added the Catacombs Specter enemy with alternating physical immunity, ground telegraph attacks, and spectral relocation.
- Charge now stuns the enemy it hits for a moment, stopping it from moving or attacking and cancelling the Specter's area attack.
- Added basic training short sword, dagger, and wand for contingency expeditions.
- If you return to Town before your partner, you now see that they are still in the Dungeon and can start the next expedition together once they are back.
- Ask the Trainer for a free training weapon in Town.
- Players are told on screen when the host leaves and who the new host is.
- Dungeon wall torches now burn with an animated flickering flame.
- Standard and locked chests now play an opening animation when opened.

### Improved

- Characters now appear behind or in front of doors depending on where they stand.
- Doors opening toward you now swing smoothly from their hinge.
- Doors now open consistently toward the north.
- The close prompt appears only when closing is available.
- Activating a Rune Compass now projects a dynamic celestial needle sprite that stays pointed toward the nearest Extraction Sanctuary in real time as you move.
- Consumables and utilities have distinct world sprites and inventory icons.
- Inventory item icons and labels are easier to read.
- While you are Ready in Town your build, equipment and Stash are locked with a clear notice, and your Quick Slots can still be rearranged.
- Items that cannot be dropped, such as training weapons, now show their drop options disabled and can no longer be thrown on the ground.
- Training weapons now appear dimmed when a chest is open and can no longer be stored in chests or other containers.
- When the host dies and returns to town, the raid keeps going for everyone else and another player becomes the host.
- Abandoning a raid now clearly warns that you lose all your equipment.
- When the Host cannot start a raid because a player's group is still loading in Town, the Host now sees a message asking to wait a few seconds and try again.

### Fixed

- Doors opening toward you now swing from their hinge instead of sliding down.
- Doors opened from the north now visibly end open toward the south.
- Doors opened from the north now hang open toward the south from their hinge.
- Doors close from either side when the doorway is clear.
- Storing a Rune Compass or Arcane Detector in a chest or corpse no longer resets its active cooldown.
- Dropping items in a Raid and opening loot chests or defeated player containers work properly again.
- Sprinting with the Void Bell no longer repeatedly chimes when pressing and releasing sprint rapidly.
- Dropped items display their correct world sprite instead of the prefab placeholder.
- The throwing dagger points toward its direction of travel.
- Restored missing and unrendered font characters in UI panels and menus.
- Prevented raid launch failures caused by inventories exceeding physical stack limits.
- Raid launch from Town spawns player avatars reliably without crashing back to town.
- New accounts can log in and create their first character again.
- The merchant accepts carts with several items to buy and sell.
- Characters on the item-instance inventory can buy and sell again.
- Joining a raid no longer fails when your loadout has split stacks or modified gear.
- You can start a raid with a full backpack while wearing equipment.
- After extracting, you can start another raid without the loadout failing to reserve.
- Enter the Dungeon with the training weapon you selected.
- Ready now directs you to the Trainer when no weapon is equipped.
- The Catacombs Specter now cleanly avoids damage and projectile collisions during its fade-out, attacks with a visible telegraph build-up, and relocates behind cover.
- Aiming no longer jitters or drifts when the camera shakes during dungeon pressure and collapse phases.
- The host can no longer end the raid for everyone; the host now abandons or returns like any other player.
- Reconnecting to a raid from town after losing connection works again.
- If abandoning fails, you get a message and can try again instead of being stuck.
- If a raid fails to start or the connection drops, you return to town and see which equipment was lost instead of losing it silently.
- Prevent game freezes in the Guild when closing menus, track in-raid container loot towards missions, and ensure extracted loot saves reliably without progression errors.
- Prevent game errors and transition freezes when returning to Town after extraction.
- Extraction now completes when a Duo and other players share a raid launched from Town, and Solo players no longer see another team's quota as their own.
- Players already inside an objective zone when receiving a contract get instant credit without exiting and re-entering.
- Extraction transitions no longer throw unspawned pressure exceptions.
- Passive treasure bonuses and minimap restrictions now stop immediately once extraction is completed.
- Depositing and withdrawing utilities into chests or corpses properly respects the single-unit carry limit.
- Defeated players with full equipment and backpack no longer lock up in raid closure and their corpse loot displays correctly.
- Extracting players who disconnect or experience host migration no longer lose raid progress or block match closure.
- Profile saves with catalog drift or transient file locks no longer lose pending extraction progress or overwrite the latest save.
- Defeated players no longer lose corpse loot on post-clear errors and forced defeats properly report lost items in the results screen.
- Experience gained before a definitive defeat now persists reliably in your profile after returning to Town and relogging.
- Ability purchases now fail safely with an error message instead of desyncing when the game server is unreachable.


## [0.5.0](https://github.com/Astro-Rift-Games/ProjectGrimhold/releases/tag/v0.5.0) - 2026-11-08

### Added

- When the dungeon collapses after time runs out, remaining raiders are definitively defeated and leave their corpses behind.
- Dungeon lighting darkens and shifts toward crimson as the instance nears collapse, accompanied by tremors and flickering torches.
- Added dynamic edge vignette shading that intensifies as dungeon pressure rises.
- Weapon sets, stamina and progression show clearer feedback.
- Consumables take time and are interrupted by damage.
- Purification removes and prevents rival trap immobilization.
- A confirmation appears before receiving a recovery weapon.
- Use a weapon or shield in either hand with independent attacks.
- The main menu now has working Settings, Credits, Exit and Log out options.
- Sound settings can now be opened from the Town options and from the menu during a raid.
- Inventories and the Stash now have a Sort button, and items stay in the order you leave them.
- In expedition preparation you can select another party member's card to see all of their equipment and check each item's details.
- Camera shake accessibility scale to reduce motion sickness.
- Torch and brazier lighting props that extinguish during collapse.
- Dramatic collapse presentation with ceiling dust and collapse music.
- Complete MVP catalog of 39 non-equippable items, including creature remains, arcane components, cursed and divine treasures, offensive throwing consumables, and expedition utilities.
- Carrying the Chaos Coin now grants your team an independent 20% chance to find bonus loot when opening dungeon chests.
- Bombs, throwing daggers, and smoke bombs can now be used from quick slots.
- Rune Compass and Arcane Detector expedition utilities are now usable in raids.
- Dungeon collapse progression accelerates dynamically based on monster kills and acquired treasures while preserving the 8-minute early collapse barrier and 17-minute final closure.
- World pickup prefabs for explosive bombs, smoke bombs, and throwing daggers are now available in raids.
- Added physical knockback reaction animations in all six directions when players receive knockback impulses.
- Teammates can no longer hurt, push or down each other in a raid unless the host enables friendly fire, and even then they can never knock an ally down.
- Downing an enemy player now earns experience once they are finally defeated, and a teammate who helped earns assist experience; reviving the enemy cancels both.
- The crossbow now has to reload after each shot; sprinting or switching weapon sets interrupts the reload and it starts over.
- Melee hits now splatter colored droplets from the struck enemy onto the floor, colored like the creature you hit.
- Your screen fades to black and white while you are downed and regains its colour as you are revived.
- Carry a Self-reviver to get back up on your own while downed, or tap the interact key on a downed ally to give them yours.
- Locked chests can be opened with a lockpick after a short uninterrupted interaction.

### Improved

- The extraction HUD now shows how many squadmates are inside the sanctuary area during extraction.
- Dungeon atmosphere now darkens with calibrated edge vignette effects during critical pressure and collapse.
- Sword slashes in either hand now sweep in a natural arc from behind the body to the opposite side in every direction.
- Two-handed swords and hammers are held with both hands while standing and walking.
- Logging in keeps you on the main menu so you choose when to press Play.
- The Play button only appears on the main menu once you are logged in.
- A two-handed weapon now shows its second hand slot as reserved, and the active weapon set is easy to spot.
- While downed you can still look at your inventory, but it clearly tells you it cannot be changed.
- The expedition preparation screen now shows a card for each party member with their character, ready state, main gear and who is the host.
- Abilities purchased at the Town Ability Vendor now save reliably to your account and remain unlocked across game sessions.
- Quick slots keep empty items assigned at count 0 so you don't lose bindings when running out of potions.
- Throwing daggers now orient towards aim direction, and smoke bombs simulate a parabolic arc before detonating.
- Dungeon ambient particles now appear as subtle floating dust motes confined to open rooms instead of falling squares.
- Minimap walls now match the dungeon art instead of showing a dark band at their base.
- Equipped armor and robes now properly deform and match character movement during knockback reactions.
- Knockbacks no longer stack: only a stronger hit replaces an ongoing push, armor can resist pushes, and players already downed are no longer pushed around.
- Raid inventory keeps stacks in their own slots and applies actions to the stack you selected.
- Picking up a stack you can only partly carry now takes what fits and leaves the rest on the ground.
- Attacking while sprinting now stops the sprint and attacks instantly, and sprint picks up again on its own if you keep holding it.
- Abilities can no longer be cast in the middle of a weapon attack; the HUD tells you an attack is in progress.
- Carrying the Wanderer's Eye now darkens the minimap instead of making it disappear.
- Melee weapons now deal damage when the blade visibly connects, with a short impact pause and a hit reaction that feels heavier for bigger weapons.
- Enemies flash bright white when struck by a melee weapon, making every hit easier to read.

### Fixed

- Team extraction progress, sanctuary assignment, and area countdown now update properly in real-time on client players during duo raids.
- Prevent modified game clients from forging shop prices or item quantities.
- Remote player extraction rewards are reliably published and secured in multiplayer raids.
- Joining a multiplayer raid with a full loadout no longer gets stuck on the loading screen.
- Disconnecting while solo or after a teammate dies now keeps your character recoverable within the reconnection window instead of evicting immediately.
- Temporary connection dropouts during raid reconnection no longer discard the ability to retry reconnecting.
- Inventory and other Town menus are visible when opened.
- Off-hand weapon attacks now stay in the left hand and the fist is no longer hidden behind the weapon.
- Raising a shield held in the right hand no longer separates the hand from the shield.
- Attribute points can no longer be spent while you are marked as Ready for an expedition.
- Attacking or using an ability while sprinting now stops the sprint, and you can no longer start sprinting in the middle of an attack.
- Pressing attack while an ability is being cast no longer fires a basic attack on top of it.
- Confirming a merchant trade with more than one item is now refused right away instead of failing after the merchant accepts it.
- Passive effects from treasures in inventory now activate reliably, and dropped items display their correct name and sprite.
- Prevent potential log exceptions from missing post-processing volume components in dungeon rooms.
- Minimap walls no longer flicker while your character moves.
- Health, mana and status rings now align correctly without the old rectangular backgrounds.
- Enemy shots can no longer slip through a wall right behind an already-used protective orb.
- Gear equipped in Town now comes with you into the raid, and extracting keeps your loot.
- Enemies and players now visibly flash red when struck by a melee weapon.
- Holding the interact key on a downed ally now keeps reviving them instead of stopping right away, and the revive bar now fills up.

### Balanced

- The spellbook is now a two-handed weapon and can only be equipped in the main hand.
- Each attribute can now be raised up to 30 instead of 25.


## [0.4.0](https://github.com/Astro-Rift-Games/ProjectGrimhold/releases/tag/v0.4.0) - 2026-11-08

_No player-facing changes in this release._


## [0.3.0](https://github.com/Astro-Rift-Games/ProjectGrimhold/releases/tag/v0.3.0) - 2026-11-08

_No player-facing changes in this release._


## [0.2.0](https://github.com/Astro-Rift-Games/ProjectGrimhold/releases/tag/v0.2.0) - 2026-11-08

_No player-facing changes in this release._


## [0.1.0](https://github.com/Astro-Rift-Games/ProjectGrimhold/releases/tag/v0.1.0) - 2026-11-08

_No player-facing changes in this release._
