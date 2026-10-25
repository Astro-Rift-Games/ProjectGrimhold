# Proposal: durable Ability Vendor, atomic multi-line trades, host result trust

Status: **proposal, not approved**. Source: `docs/audit_commerce_2026-10-25.md` (H1, H3, C1). No code changes until each option is chosen. Each section changes a backend contract, so an approved option must also update `Project Grimhold/Docs/Architecture/CommerceArchitecture.md` (and the persistence/identity architecture for C1).

## H1 Durable Ability Vendor purchase

**Problem.**
- `LocalProfileStore.TryPurchaseAbility` debits Currency and adds `UnlockedAbilities` only locally. The price comes from the client-authored `AbilityVendorOffer._price`.
- The backend has no unlocked-ability state, and hydration overwrites Currency but keeps local unlocks, so abilities are free after reconciliation and lost with the local cache.

**Game Design.** GD 11 only says Town merchants "pueden ofrecer habilidades bajo sus propias condiciones de compra". Prices and conditions are not defined in the live GD.

**Proposed option (recommended): backend-owned unlocks with server-side prices.**
- Model: `Character.unlockedAbilities: [String]` (ability ids), returned by `GET /character/me/progression` (or inventory) and hydrated into `UnlockedAbilities`. Hydration replaces the local list instead of preserving it.
- Endpoint: `POST /character/me/abilities/purchase` `{ transactionId, abilityId, expectedRevision }`.
  - The backend prices the ability from an `ABILITY_CATALOG` mirrored from the Unity `AbilityVendorCatalog` asset, guarded by a parity test like `shop.catalog.parity.test.js`.
  - One atomic update applies `$inc currency`, `$addToSet unlockedAbilities` and `$inc revision`, guarded by revision, `currency >= price`, ability not owned and no `pendingReservation`.
  - It records a receipt in the same idempotency log as the shop.
  - Errors: `ABILITY_NOT_OFFERED`, `ALREADY_UNLOCKED`, `INSUFFICIENT_FUNDS`, `RESERVATION_PENDING`, `REVISION_CONFLICT`, `TRANSACTION_ID_REUSED`.
- Unity:
  - `TownAbilityVendorPresenter` goes through a remote service: remote first, then apply the response snapshot. It does not commit locally before the backend confirms, which avoids H5's optimistic pattern.
  - `TryPurchaseAbility` stays as the local projection applied from the server result.
- Migration: existing local-only unlocks have no backend record. The choice is to drop them on first hydration (simple; MVP data) or to grant them once through a one-off backend import. **Decision needed.**

**Alternative:** keep abilities local-only and make them free (no Currency). Simpler, but it contradicts "condiciones de compra" and still loses unlocks with the cache.

## H3 Atomic multi-line trade

**Problem.** GD UI.1.4 §7.1 requires confirming the whole buy/sell operation. The backend only has single-item `shop/buy` and `shop/sell`, so `RemoteShopTransactionService` refuses tickets with more than one line (documented gap, contract item 11).

**Proposed option (recommended): one trade endpoint.**
- `POST /character/me/inventory/shop/trade` `{ transactionId, expectedRevision, purchases: [{lootId, amount}], sales: [{lootId, amount}] }`.
  - Lines are bounded: at most N lines, amounts 1..999, no duplicate lootId per side.
  - The backend prices every line from `shopCatalog`, then validates ownership of sold units, final Currency ≥ 0 and final capacity, matching GD §7.2 "el espacio se valida sobre el estado final".
  - It applies everything in one single-document `findOneAndUpdate` (same revision and reservation guards as today) and stores one receipt with a hash of the lines for idempotent replay.
- Keep `shop/buy` and `shop/sell` as thin wrappers over the same function, or deprecate them once Unity migrates.
- Unity: `RemoteShopTransactionService.TryExecuteTrade` sends the whole ticket. This removes the `LineCount != 1` refusal and lets the merchant authority receive one outcome per ticket.
- Server-side capacity (audit M3) is needed here anyway. It requires the slot-capacity rule (30 loadout slots) on the backend, mirrored from `ProfileInventoryRules`.

## H5 (approved, informs H1/H3)

Report `persisted = true` to the merchant authority only after the backend acknowledges. Both proposals above assume remote-first commits, so the shared stock and the profile can only diverge on transport loss, not on a backend rejection.

## C1 Host result trust (outside commerce, blocks economy integrity)

**Problem.**
- `POST /character/raid/extraction-result` trusts an HMAC keyed by a secret that ships in the client asset, and Render accepts it.
- A player-hosted raid has no trustworthy host, so any shared client secret is equivalent to no signature.

**Immediate mitigation (no design change).**
- Remove `WebhookSecret` from `Resources/BackendConfiguration.asset`.
- Set a strong `WEBHOOK_SECRET` and `NODE_ENV=production` on Render. Today this would break player-hosted extraction persistence; that is the honest state until a trusted authority exists.

**Structural options. Decision needed; involves the Fusion topology.**
1. A dedicated/server-hosted raid authority (Photon Fusion Server mode, or a headless host the team controls) holds the secret and alone publishes results.
2. Photon webhooks or a custom authentication callback that report the session outcome to the backend, with no client-held secret.
3. Accept player-hosted trust for the MVP, but bound damage server-side:
   - per-raid loot budgets derived from the raid seed and loot tables;
   - rate limits;
   - results only for accounts admitted to that raid (admission tickets already exist, `POST /character/me/raid/admission-ticket`);
   - the host account must be a raid member.

   This is weaker but contained.

## Decisions requested

1. H1: backend-owned unlocks (recommended) or local-free abilities; migration of existing local unlocks (drop or import); and who defines ability prices (GD or the Unity catalog asset).
2. H3: approve the single trade endpoint and the backend capacity rule.
3. C1: immediate mitigation now (yes/no) and the structural option.
