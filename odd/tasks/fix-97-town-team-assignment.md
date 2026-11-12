# FIX-97 — Town launch team assignment (multi-player extraction stall)

HacknPlan: FIX-97 (ID 809), parent FIX-US-05 (ID 686). Scope/acceptance contract is the HacknPlan item; this file only tracks execution and evidence.
Branch: fix/fix-97-town-team-assignment (from New-Testing); commit authorized by owner (L8).

## Specs

- **S1** (HacknPlan AC 1): "En una sesión Host + Cliente (equipo Duo o múltiples equipos), al completar el ritual y permanecer los miembros requeridos dentro de la zona hasta llegar el contador a 0, el estado avanza a extraído sin quedarse detenido en 0."
- **S2** (HacknPlan AC 2): "Cada jugador extraído recibe sus resultados de botín/progresión y puede retornar a Town con el botín y la XP acreditados."
- **S3** (HacknPlan AC 3): "Se mantiene intacta la extracción Solo."
- **S4** (owner decision, L3): team rule for Town launch is "Party = equipo; Solo = propio equipo". A Duo party becomes one `RaidTeamId`; a member without a party (Solo) gets its own `RaidTeamId`; ids are numbered by first appearance in the frozen cohort order. Party membership is captured authoritatively at freeze time and replicated per member; peers never derive it from local party state.
- **S5** (HacknPlan validation): "Test automatizado en Unity (EditMode o PlayMode según la capa donde resida la causa raíz): RED reproducido antes del cambio y GREEN después." Plus mandatory manual multi-process Host + Client validation (owner).
- **S6**: update the owning architecture (`LobbyAndSessionTransitionArchitecture.md` §"the current Town flow assigns Team 1 explicitly to the complete cohort") for the authorized contract change; remove all temporary `[FIX-97 diag]` logs before closing.
- **S7** (L9): "actualiza el contrato base" — `Project_Grimhold_Contratos_Base_v1.md` must stop stating "The MVP has individual progress only and no team progress, shared quota or global progress service."
- **S8** (L9): "implementa el feedback para el host en caso de que falte la party." — when Start is refused because a member's Town party (or the party directory) is unavailable, the requesting Host sees a visible UI notice instead of only a console error.

## Tasks

| ID | Specs | Route | Status | Commit |
|---|---|---|---|---|
| T1 | S4, S5 | worker: RED tests (TownRaidPreparationRules: Duo+Solo => 2 teams; launch context builds valid RaidTeamRoster) then implement party-key capture at freeze, replicate in member struct/snapshot, team mapping in `TryCreateLaunchContext`; update existing Team-1 assertion deliberately | done (worker): RED 7 failing -> GREEN TownRaidPreparationRulesTests 30/30; TownRaidPreparation 63, RaidLaunch 9, RaidTeamRoster 6, Extraction 242, TownParty 23 all pass; live Host+Client unverified | uncommitted |
| T2 | S6 | inline: architecture doc update | done: LobbyAndSessionTransitionArchitecture.md Team rule paragraph | uncommitted |
| T3 | S6 | inline: remove `[FIX-97 diag]` logs; restore `RaidAvatarParticipantLink.NotifyExtractionCompleted` original form | done: both files restored to HEAD (git checkout of diag-only diff); no FIX-97 in Assets/Scripts | n/a |
| T5 | S7 | inline: contract base doc | done | see commit |
| T6 | S8 | worker: Start result enum + targeted RPC to requester + presenter/view notice, TDD | done (worker): RED 7 failing (TownRaidStart filter) -> GREEN TownRaidStart 18/18; TownRaidPreparation 65, ExpeditionWeaponRequirement 17, TownParty 23, RaidLaunch 9, SessionConnection 51 all pass; live Host+Client unverified | uncommitted |
| T4 | S1-S3, S5 | verify: EditMode suites (Networking, Extraction) + independent verify; owner runs live Duo + Solo multi-process test | verify FAIL fixed (Expedition fixtures w/o team ordinal; fail-closed party lookup; +2 probe tests). EditMode: TownRaidPreparation 65/65, ExpeditionWeaponRequirement 17/17, RaidLaunch 9/9, RaidTeamRoster 6/6, TownParty 23/23, SessionConnection 51/51, Extraction 242/242. owner live test PASS (L7-L8) | see commit |

## Log

- **L1** (user, original request): "revisa FIX-97 — Extracción con varios jugadores bloqueada al llegar el contador a 0 y empeza con la implementacion siguiendo ODD"
- **L2** (runtime evidence, 2026-11-12, MPPM Host=Main Editor + 2 clients, launched from Town, Duo + Solo): host `TeamExtractionCoordinator` never initialized (`_isInitialized=False`, roster null, all slots empty); `NetworkSpawnManager.TryGetRaidTeamRoster` false; `RaidTeamRoster.TryCreate` error: "Team '1' has invalid size (3). Only Solo (1) and Duo (2) teams are supported."; all 3 launch participants had TeamId=1. Source: `TownRaidPreparationRules.TryCreateLaunchContext` hardcodes `RaidTeamId.TryCreate(1, ...)` for every member. User: "desde town, si, note que cuando complete la cuota del duo, el solo tambien figuraba como cuota completada."
- **L3** (owner decisions): team rule "Party = equipo; Solo = propio equipo (Recomendado)"; scope "Sí, dentro de FIX-97 (Recomendado)".
- **L4** Out of scope follow-ups noted: Team-1 silent fallbacks (`PlayerExtractionProgressController.cs:219` etc., FIX-28 territory); `ExtractionConfig.MaxTeamsPerRaid` (4) not passed to roster; stale "no team progress" line in `Project_Grimhold_Contratos_Base_v1.md:68`; dev launcher `_developmentTeamSize=0` default.
- **L5** Independent verify (mwdzno8v-7-yfg6): FAIL on S5 (ExpeditionWeaponRequirementTests 2 failures: snapshot members lacked team ordinal) + advisory: TryCollectPartyIds fell back to Solo on missing party. Both fixed; owner asked to be told when to test: "ok, avisame cuando tenga que probar algo".
- **L6** Independent re-verify (mwe0kac5-8-zdmx): PASS S4, S5 (automated), S6; no corrections. Advisories: refused Start has no UI feedback; Join/Ready don't require a party (short window right after arriving in Town can refuse Start with "no valid Town party" error); Host and Client must run the same build (MemberNetwork layout). Pending: owner live test S1-S3.
- **L7** Owner live test (Town launch, Duo with Host + Solo client): "perfecto, extrajimos en duo, y el solo quedo en la raid, como uno de los integrantes del duo era el host, el host paso a ser el solo sin problemas." S1 Duo path confirmed live (also unblocks FIX-51 manual validation: voluntary Host handoff to remaining Raiding Solo worked). Pending: Solo extraction (S3) and loot/XP credited in Town (S2).
- **L8** Owner confirmation and delivery authorization: "esta perfecto, hace el commit". S1-S3 accepted live.
- **L9** (user): "actualiza el contrato base, e implementa el feedback para el host en caso de que falte la party." Design from explorer mwe1j4z2-9-34og: follow TownPartyDirectory invitation-result pattern (targeted [RpcTarget] RPC + queue drained in Render + event), pure text mapping class, Spanish voseo UI copy.
- **L10** Independent verify (mwe1zfxg-b-lvqt): PASS S7, S8; no corrections. EditMode: TownRaidStart 18/18, TownRaidPreparation 65/65, ExpeditionWeaponRequirement 17/17, TownParty 23/23, RaidLaunch 9/9, SessionConnection 51/51, Extraction 243/243. Pending: owner live check of the Start notice (targeted RPC path untested in EditMode). Advisory: Plan-Implementacion-Sistema-Misiones.md:211 names the old section title in plain text.
- **L11** Owner live check: forced PartyUnavailable on Host (party directory unregistered in memory) -> "Si funciona."; after restore, "si, se lanzo bien." Delivery authorization: "ok commitealo ya fue".
