# Town NPC directional art and sequential dialogue

## Objective
Integrate the supplied three-character spritesheet into the existing Stash, Raid, and Merchant NPC prefabs and make confirmed interaction flow through temporary facing, dialogue, then the existing local interface on normal completion only.

## Problem and why
All three Town presenters currently open on `InteractionResolved`, while `DialoguePresenter` also consumes that event. `DialogueController.DialogueEnded` conflates natural completion with abort. The supplied sheet is already imported with tight, inconsistent slices and unverified scale.

## Authorized scope and constraints
User explicitly authorized implementation and validation on `New-Testing`. Preserve each existing single `IInteractable`, `NetworkObject`, collider, prompt, existing panel implementation, and all pre-existing unrelated working-tree changes (especially `Lobby-Town.unity`). No cosmetic network state, second interaction endpoint, Animator Controller, package changes, or final narrative copy. Use only `CharacterVisualDirection` and `CharacterVisualDirectionResolver`. The art and its `.meta` were user-supplied untracked files; retain GUID and image bytes.

## Plan and progress
- [x] T1. Normalize and name the 18 sprites from observed art; implement reusable six-view local presentation and data-only dialogue trigger; author three provisional sequences; wire only the three existing NPC prefabs. Acceptance observed: 18 named 72x96 sprites at PPU 48 with common foot pivot, all six views per NPC, one existing `IInteractable`, scene references unchanged. Checks: Unity importer/prefab inspection; focused `TownNpcPresentationConfigurationTests` EditMode 5/5 passed (job `936e9fc0e3724d1a9de91ea5d2874b63`); compile console 0 errors; Editor Scene View observed correct bald/dark-armor/beret South sprites in Town. Not yet runtime scale/interaction validated. Route: delegated direct writer because multiple non-trivial assets/scripts; mapping and preparation delegated after 4+ file trigger. Commit pending.
- [ ] T2. Separate normal dialogue completion from abort, orient target on confirmed interaction, defer each Town panel until typed normal completion and revalidation, restore facing on abort/closed panel. Acceptance: no simultaneous UI, at-most-once correct opening, abort never opens, input suppression handoff and facing reset. Checks: focused EditMode/PlayMode tests, Unity compile/Test Runner, live Town interactions where accessible. Route: delegated direct writer because multiple non-trivial runtime files; source mapping delegated.
- [ ] T3. Review full diff and serialized references, run applicable checks, inspect Town scene and report manual Host/Client or visual gaps honestly. Route: inline verification; independent read-only review may be delegated if needed.

## Configuration and delivery
TDD: no project-wide strict TDD configuration found; previous strict instruction was limited to an unrelated slice. Mode off for this feature; ordinary focused functional checks required. Runner: Unity MCP EditMode/PlayMode Test Runner in Unity 6000.5.1f1. RDD: explicitly disabled globally (`gentle-ai review mode status`); no native review. Delivery: `exception-ok` for direct work on requested `New-Testing`, no PR requested; forecast >400 authored lines due to three prefab/sequence serializations and tests, not a code-size target. Commit by coherent work unit with Conventional Commit messages, stage only this feature's paths. Running count and commit IDs pending.

## Current evidence and next step
Branch `New-Testing` at `1c2616c4` before feature commits. `Lobby-Town.unity`, `MainMenu.unity`, backend lock and supplied NPC image/meta were dirty/untracked before this feature. T1 retained the supplied PNG bytes/GUID, normalized the importer to Multiple/PPU48/Point/Uncompressed with 18 uniform slices, added local view and data-only triggers, authored three provisional sequences, and wired the existing three prefabs. The Unity Editor loaded `Lobby-Town.unity` without saving it; in-memory MainMenu was clean before load. No scene file was modified by this feature. Generated screenshots were removed after visual inspection. Next: commit T1, then implement T2.

## Mirror
Engram `odd/town-npc-dialogue-art/tasks` pending read-back/sync; no authoritative runtime session ID is available.
