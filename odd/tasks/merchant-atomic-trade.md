# Merchant atomic multi-line trade

Town merchant carts with more than one line (buy and/or sell) must confirm as one atomic backend operation.
Branch: New-Testing. Contract owner: Project Grimhold/Docs/Architecture/CommerceArchitecture.md. Game Design: "UI - Auditoría Funcional de Interfaces MVP" §7.1–7.2.

## Specs

- S1 (user report): "hay un problema en la tienda, cuando queres vender mas de un objeto a la vez el vendedor rechaza la operacion, solamente funciona la venta cuando vendes de un objeto"
- S2 (user decision): "hay que armar como un carrito de ventas, todo lo que se vende se suma a una lista y despues se confirma"
- S3 (user decision): the atomic endpoint covers "Compras y ventas mezcladas" — one ticket with N purchases + N sales.
- S4 (Game Design §7.1, verbatim): "el jugador prepara compras y ventas, visualiza el balance resultante y confirma la operación completa al finalizar." / "Agregar un objeto a compra o venta no modifica inmediatamente inventarios ni Oro." / "Cerrar o cancelar la operación restaura el estado previo."
- S5 (Game Design §7.2, verbatim): "El espacio se valida sobre el estado final de la transacción, considerando simultáneamente objetos vendidos y comprados." / "Si el Oro resultante es insuficiente, el jugador puede continuar editando la operación, pero Confirmar transacción permanece deshabilitado y comunica el faltante."
- S6 (wire contract, derived): `POST /character/me/inventory/shop/trade`, header `X-Grimhold-Inventory-Protocol` gated like the other shop routes. Body `{ transactionId, expectedRevision, purchases: [{ lootId, amount }], sales: [{ lootId, amount, sourceSlot }] }` (`sourceSlot` required for schema v2/v3, ignored for v1; `declaredPrice`/`declaredSellValue` not accepted — prices are authoritative). At least one line; purchases and sales each limited to the client limits (`MerchantRequestValidator` MaxSaleLines / purchase limit), amount 1..`MAX_TRANSACTION_AMOUNT`; duplicate sale `sourceSlot` or duplicate lootId within a side → 400. Response: the same shape as single-line shop responses (`shopSnapshot` + `alreadySecured`). All-or-nothing: one `findOneAndUpdate` guarded by revision, pending reservation and buyback epoch; any line failing → whole ticket rejected with the existing single-line error codes (REVISION_CONFLICT, RESERVATION_PENDING, INSUFFICIENT_ITEMS, INSUFFICIENT_FUNDS/whatever buy uses, ITEM_NOT_SELLABLE, inventory full code, INTEGER_OVERFLOW). Order of application: sales first, then purchases in ticket order, matching `LocalProfileStore.PlanTrade`, so space and gold are validated on the final state (S5). One receipt per ticket (`type: 'trade'`) with a canonical lines fingerprint; same transactionId + same lines → `alreadySecured: true`; different lines → 409 TRANSACTION_ID_REUSED. Single-line `/shop/buy`, `/shop/sell`, `/shop/buyback` keep their current behavior.
- S7 (client): `TownMerchantTradeSession` stops rejecting multi-line drafts; `RemoteShopTransactionService.ExecuteTradeAsync` sends any ticket (1..N lines, mixed) through the trade endpoint and keeps its existing dry-run → backend → local commit → revision/buyback flow and error mapping. Buyback tickets stay single and alone (`BuybackMustBeAlone`).

## Tasks

- T1 [S3,S5,S6] backend trade endpoint + integration tests + README — route: worker (backend surfaces) — commit: ec13a114 — done
- T2 [S1,S2,S4,S7] Unity client: InventoryClient.ShopTradeAsync, DTO, RemoteShopTransactionService, TownMerchantTradeSession guard removal, tests — route: worker (Unity surfaces) — commit: client commit (fix(commerce)) — done
- T3 [S6,S7] update CommerceArchitecture.md (incompatibility 10, IShopTransactionService, backend contract, error mapping) — route: inline — commit: client commit (fix(commerce)) — done
- T4 [S1–S7] verify: backend npm test, EditMode merchant/commerce tests, review diff — route: gentle-ai-verify (Risk item 1: currency and items written by a new mutation path) — commit: n/a — done: S1–S7 pass; backend npm test 514 passed/3 skipped, shop integration 56/56; EditMode RemoteShopTransactionService 30/30, TownMerchantTradeSession 38/38, Merchant 260/260, Shop 116/116; PlayMode merchant 7/7 (worker run)

## Log

- L1 (user, verbatim): "hay un problema en la tienda, cuando queres vender mas de un objeto a la vez el vendedor rechaza la operacion, solamente funciona la venta cuando vendes de un objeto"
- L2 (user, verbatim): "hay que armar como un carrito de ventas, todo lo que se vende se suma a una lista y despues se confirma"
- L3 (user choice): "Compras y ventas mezcladas (recomendado)".
- L4 (evidence): rejection happens client-side before any request: `Player/Presentation/TownMerchantTradeSession.cs:116-122` (RejectedByMerchant for Purchases+Sales > 1) and `Stash/RemoteShopTransactionService.cs:134-138` (LineCount != 1). The draft cart (`Commerce/MerchantTradeDraft.cs`) and `LocalProfileStore.PlanTrade` already handle N lines; backend only exposes single-line endpoints (`backend/src/routes/inventory.routes.js:115`). CommerceArchitecture.md documents this as incompatibility 10 / "H3, out of scope".
- L5 (deploy): Render (`https://projectgrimhold.onrender.com`) must be redeployed with T1 before a T2 client can confirm multi-line tickets; the user deploys.
- L6 (verify): the T2 client sends every normal ticket, single-line included, through `/shop/trade`; against a backend without T1 every buy and sell fails (404 → PersistenceFailed). Deploy the backend before shipping the client.
