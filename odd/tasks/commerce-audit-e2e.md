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
- [ ] T2 Audit report with severity and `path:line` evidence; user selects fixes | report: `docs/audit_commerce_2026-10-25.md`
- [ ] T3 E2E in Editor against backend (Town buy/sell, backend state check)
- [ ] T4 Permanent PlayMode E2E tests
- [ ] T5 Approved fixes (one commit each)

## Log
- L1 (2026-10-25, user): scope all four areas; reference Architecture + live GD; E2E Editor + real backend (already running); report + tests + fixes; new branch; work-unit commits authorized.
- L2 Env: nothing listens on localhost:3000; Unity loads `Resources/BackendConfiguration.asset` = `https://projectgrimhold.onrender.com` (health ok, mongo connected). L3 (user): E2E runs against Render with a dedicated test account/character.
