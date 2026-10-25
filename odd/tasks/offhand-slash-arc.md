# Feature: offhand-slash-arc

## Objective
Off Hand sword slashes follow one anatomical arc in all six facings, and the slash VFX matches it.

## Contract (user)
- Off Hand (left hand) sword slash, every facing, in the body frame (forward = facing direction, right = character's right): starts BEHIND the character on its LEFT side, sweeps a semicircle back-to-front, ends on the character's RIGHT side (crossing the body). Common sword-slash motion.
- Applies to slash weapons in Off Hand: ArmingSword, MagicSword.
- Reported symptoms: SW, NE, NW incoherent; slash VFX wrong in NW (likely due to the hand motion); MagicSword motion and VFX still go the opposite way.
- Branch New-Testing, no commits until authorized.
- Decision 2026-10-24: canonical arc applies to BOTH hands and BOTH weapons (ArmingSword, MagicSword). Main = mirror (starts behind on the RIGHT, ends on the LEFT). Same ~180 deg semicircle for both weapons (MagicSword loses its 200 deg arc). Presentation only: timing/release/hit unchanged.

## Hypotheses (to verify by measurement)
- Current Mirrored derivation reflects screen X (correct only for N/S where body sagittal axis = screen vertical); diagonals need reflection across the facing axis.
- MagicSword Main stroke may sweep opposite to ArmingSword, so mirroring it lands on the wrong side.

## Tasks
- [x] A1 Measure Main/Off grip + blade tip + VFX arcs in body frame for ArmingSword/MagicSword x 6 facings; confirm causes.
- [x] A2 Validator check encoding the Off slash arc contract (tests first, RED on current assets).
- [x] A3 Fix derivation (+ VFX), regenerate, GREEN, renders, full regression.

## Evidence / log
- A1 (worker, bridge, no edits): H1 confirmed (OffHandAnimationGenerator.cs:212-214 negates X; :98-100 fixed 180 deg blade basis; PlayerWeaponPresenter.cs:535-540 VFX X reflection; diagonals are +-45 deg, CharacterVisualDirectionResolver.cs:25-30). H2 confirmed: ArmingSword Main tip sweep ~-108 deg, MagicSword Main ~+200 deg (opposite); MagicSword Off set is Mirrored and fresh. Main hand endpoints are not anatomically consistent across facings, so derivation from Main cannot satisfy the contract -> generate canonical arcs.
- A2/A3 (worker, bridge): canonical arc, VisualRoot origin pivot, radius = mean neutral grip distance; Main +135 -> -45 deg, Off = facing-axis reflection -135 -> +45; blade radial; stroke windows from source angular keys (ArmingSword .15-.45 s, MagicSword .30-.55 s); recovery reaches neutral at authored endpoint (ArmingSword .60 s, MagicSword .80 s). Presenter VFX reflected across facing axis; slash VFX poses (both assets, only _poses fields, via Unity; Unity also materialized default `_releaseLeadSeconds: 0`, accepted) centered on body pivot, Main mirrored playback. New validator check SlashArcContract. Format off-hand-retarget/5.
- RED: 0aae55a1 (8 failures), recovery bf5e2637 (1). GREEN focused e26c3a49 627/627. Catalog 696/696 pass, 0 violations. Thrust/cast Off clips 18/18 equal to pre-task hashes (signed-zero diff came from the atomic writer experiment, writer now slash-only).
- Scope incidents: pre-existing 2H regeneration test reordered 42 two-handed clips under the shared atomic writer; parent verified order-only (same line multiset as HEAD) and `git restore`d exactly those 42; after restricting the writer they regenerate byte-equal to HEAD (git status shows them only as line-ending/stat entries, empty diff).
- Parent: renders arc-ArmingSword.png / arc-MagicSword.png inspected (S/N Main back-right -> front-left, Off back-left -> front-right; MagicSword same direction). Full EditMode bb809256: 4646 run, 10 known + 2 new PlayerAnimatorViewTests that pinned Juan's authored ArmingSword swing -> ArmingSword removed from those two tests (canonical coverage elsewhere); PlayerAnimatorViewTests 46/46 (d5654cee). Full PlayMode 1ffc23d3: 805 run, same 25 known.
- Commits: none.
