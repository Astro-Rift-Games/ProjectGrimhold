# Commerce audit and E2E

Objective: audit the implementation and behavior of the commerce system, then validate it end to end in the Unity Editor against the running backend. Branch `audit/commerce-e2e` (from `New-Testing` @ `4d8a6663`). Work-unit commits authorized; push/PR stay with the user. Fixes only after per-finding user approval.
Engram mirror topic: `odd/commerce-audit-e2e/tasks`.

## Specs
- S0 Scope: Town Merchant (Fusion: `Scripts/Commerce/*`, `TownMerchantNetworkController`, `MerchantRequestValidator`, `MerchantTradeWireCodec`, presentation), transaction persistence (`IShopTransactionService` Local/Remote, `LocalProfileStore`, `InventoryClient`), backend shop (`shopCatalog`, `InventoryService`, `CurrencyService`, routes/validators), Ability Vendor.
- S1 References: `Docs/Architecture/CommerceArchitecture.md`, `docs/plan_us57_shop_autoritativo.md`, live Game Design commerce sections (grimhold-docs). Discrepancies are reported, not resolved unilaterally.
- S2 E2E: Unity Editor via Unity MCP (`127.0.0.1:8081`), `RemoteShopTransactionService` against the already-running backend.
- S3 Deliverables: audit report, permanent PlayMode E2E tests, approved fixes.

## Tasks
- [x] T1 Audit (read-only): code + tests vs architecture and Game Design | route: 2 delegated explorers (Unity, backend) + delegated EditMode baseline verifier (283/283) + parent spot checks (H1, H2 backend, H3)
- [x] T2 Audit report with severity and `path:line` evidence; user selects fixes | report: `docs/audit_commerce_2026-10-25.md` | commit: `4226ba51` `docs(commerce): add commerce system audit report`
- [ ] T3 E2E in Editor against backend (Town buy/sell, backend state check)
- [ ] T4 Permanent PlayMode E2E tests
- [ ] T5a H2 reservation gate (Unity `TryCommitTrade` + backend `shopBuy`/`shopSell` 409) | backend part done: delegated worker, RED (unit `Missing expected rejection.`, integration `200 !== 409`) -> GREEN; npm test 159/159, integration 68/68, concurrency 7/7; commit `fix(backend): reject shop trades while a raid reservation is pending`; Unity part pending (Editor busy with E2E)
- [ ] T5b H4 align `shopCatalog.js` to Unity assets + EditMode parity test; drop dead `training_sword` / duplicate `health_potion`
- [ ] T5c H5 report persisted only after backend ack; M2 RPC null guard; L1 missing config is an error; L3 docs
- [ ] T5d Design proposal for H1 (durable ability unlock) and H3 (atomic multi-line trade endpoint); no code until approved

## Log
- L1 (2026-10-25, user): scope all four areas; reference Architecture + live GD; E2E Editor + real backend (already running); report + tests + fixes; new branch; work-unit commits authorized.
- L2 Env: nothing listens on localhost:3000; Unity loads `Resources/BackendConfiguration.asset` = `https://projectgrimhold.onrender.com` (health ok, mongo connected). L3 (user): E2E runs against Render with a dedicated test account/character.
- L4 (user): approved fixes H2, H4, H5+M2+L1/L3; plan-only for H1 and H3. H4 price source = Unity LootDefinition assets; backend mirrors them. E2E account: create a new dedicated account on Render. Backend fixes cannot be verified against Render until deployed (deploy is out of scope).
- L5 C1 found during E2E feasibility (runtime probe on Render, user-authorized): client-shipped HMAC secret accepted -> item minting; recorded in report commit `7c42f7dd`. E2E funding route: real raid in Editor (user choice).
