// src/routes/inventory.routes.js
const express = require('express');
const router = express.Router();
const InventoryService = require('../services/InventoryService');
const ExtractionCommitService = require('../services/ExtractionCommitService');
const env = require('../config/env');
const { EQUIPMENT_SLOTS } = require('../config/equipmentSlots');
const authenticate = require('../middleware/authenticate');
const inventoryProtocol = require('../middleware/inventoryProtocol');
const { requireSplitEnabled } = require('../services/InventoryEntries');
const {
  inventoryOrderValidator,
  slotMoveValidator,
  slotSplitValidator,
  moveItemValidator,
  preparedEquipmentValidator,
  trainingWeaponValidator,
  pendingReservationValidator,
  admitReservationValidator,
  commitExtractionValidator,
  shopSellValidator,
  shopTradeValidator,
  shopBuyValidator,
  shopBuybackValidator
} = require('../validators/inventory.validators');
const { commitExtractionUnifiedValidator } = require('../validators/extraction.validators');

// All inventory routes require a valid JWT token.
router.use(authenticate);

// GET /character/me/inventory
// Returns the full inventory snapshot (stash, loadout, preparedEquipment, pendingReservation).
// Called at login to hydrate the local Unity state.
router.get('/me/inventory', inventoryProtocol, async (req, res, next) => {
  try {
    const inventory = await InventoryService.getInventory(req.accountId);
    res.json(inventory);
  } catch (err) {
    next(err);
  }
});

// POST /character/me/inventory/gems/install (v3 only)
// Body: { transactionId, weaponList, weaponSlot, instanceId, gemList, gemSlot, gemLootId, expectedRevision }
router.post('/me/inventory/gems/install', inventoryProtocol, async (req, res, next) => {
  try { res.json(await InventoryService.installGem(req.accountId, req.body)); }
  catch (err) { next(err); }
});

// PUT /character/me/inventory/order
// Body v1: { list, lootIds, expectedRevision }; v2: { list, slotOrder, expectedRevision }
router.put('/me/inventory/order', inventoryProtocol, inventoryOrderValidator, async (req, res, next) => {
  try {
    const { list, lootIds, slotOrder, expectedRevision } = req.body;
    const result = await InventoryService.updateOrder(req.accountId, list, lootIds, expectedRevision, slotOrder);
    res.json(result);
  } catch (err) {
    next(err);
  }
});

// POST /character/me/inventory/slots/move (v2 only)
// Body: { fromList, fromSlot, toList, toSlot, lootId, expectedRevision }
router.post('/me/inventory/slots/move', inventoryProtocol, slotMoveValidator, async (req, res, next) => {
  try {
    const { fromList, fromSlot, toList, toSlot, lootId, expectedRevision } = req.body;
    res.json(await InventoryService.moveSlot(req.accountId, fromList, fromSlot, toList, toSlot, lootId, expectedRevision));
  } catch (err) {
    next(err);
  }
});

// POST /character/me/inventory/slots/split (v2 only, feature flag defaults off)
// Body: { list, sourceSlot, amount, destinationSlot, lootId, expectedRevision }
router.post('/me/inventory/slots/split', (req, res, next) => {
  // Check before protocol middleware, which may read the character.
  requireSplitEnabled();
  next();
}, inventoryProtocol, slotSplitValidator, async (req, res, next) => {
  try {
    const { list, sourceSlot, amount, destinationSlot, lootId, expectedRevision } = req.body;
    res.json(await InventoryService.splitSlot(req.accountId, list, sourceSlot, amount, destinationSlot, lootId, expectedRevision));
  } catch (err) {
    next(err);
  }
});

// POST /character/me/inventory/stash/move-to-loadout
// Moves amount units from the guarded source (sourceSlot required for v2).
// Body: { lootId, amount, expectedRevision, sourceSlot? }
router.post('/me/inventory/stash/move-to-loadout', inventoryProtocol, moveItemValidator, async (req, res, next) => {
  try {
    const { lootId, amount, expectedRevision, sourceSlot } = req.body;
    const result = await InventoryService.moveToLoadout(req.accountId, lootId, amount, expectedRevision, sourceSlot);
    res.json(result);
  } catch (err) {
    next(err);
  }
});

// POST /character/me/inventory/loadout/move-to-stash
// Moves amount units from the guarded source (sourceSlot required for v2).
// Body: { lootId, amount, expectedRevision, sourceSlot? }
router.post('/me/inventory/loadout/move-to-stash', inventoryProtocol, moveItemValidator, async (req, res, next) => {
  try {
    const { lootId, amount, expectedRevision, sourceSlot } = req.body;
    const result = await InventoryService.moveToStash(req.accountId, lootId, amount, expectedRevision, sourceSlot);
    res.json(result);
  } catch (err) {
    next(err);
  }
});

// POST /character/me/inventory/shop/sell
// Sells an item from the loadout, awarding authoritative currency from shop catalog.
// Body: { transactionId, lootId, amount, expectedRevision, sourceSlot? (required v2), declaredSellValue? }
router.post('/me/inventory/shop/sell', inventoryProtocol, shopSellValidator, async (req, res, next) => {
  try {
    const { transactionId, lootId, amount, declaredSellValue, expectedRevision, sourceSlot } = req.body;
    const result = await InventoryService.shopSell(req.accountId, lootId, amount, declaredSellValue, expectedRevision, transactionId, sourceSlot);
    res.json(result);
  } catch (err) {
    next(err);
  }
});

// POST /character/me/inventory/shop/buy
// Buys an item into the loadout, deducting authoritative currency from shop catalog.
// Body: { transactionId, lootId, amount, expectedRevision, declaredPrice? }
router.post('/me/inventory/shop/buy', inventoryProtocol, shopBuyValidator, async (req, res, next) => {
  try {
    const { transactionId, lootId, amount, declaredPrice, expectedRevision } = req.body;
    const result = await InventoryService.shopBuy(req.accountId, lootId, amount, declaredPrice, expectedRevision, transactionId);
    res.json(result);
  } catch (err) {
    next(err);
  }
});

// POST /character/me/inventory/shop/trade
// Body: { transactionId, expectedRevision, purchases: [{lootId, amount}], sales: [{lootId, amount, sourceSlot?}] }
router.post('/me/inventory/shop/trade', inventoryProtocol, shopTradeValidator, async (req, res, next) => {
  try { res.json(await InventoryService.shopTrade(req.accountId, req.body)); }
  catch (err) { next(err); }
});

// POST /character/me/inventory/shop/buyback
// Body: { transactionId, entryId, amount, expectedRevision }
router.post('/me/inventory/shop/buyback', inventoryProtocol, shopBuybackValidator, async (req, res, next) => {
  try {
    const { transactionId, entryId, amount, expectedRevision } = req.body;
    res.json(await InventoryService.shopBuyback(req.accountId, entryId, amount, expectedRevision, transactionId));
  } catch (err) {
    next(err);
  }
});

// PUT /character/me/inventory/prepared-equipment
// Replaces all six equipment slot assignments atomically.
// Body: { weaponSlot1?, weaponSlot2?, helmet?, armor?, gloves?, boots? }
router.put('/me/inventory/prepared-equipment', inventoryProtocol, preparedEquipmentValidator, async (req, res, next) => {
  try {
    // Build the slots object from EQUIPMENT_SLOTS so new slots are covered automatically.
    const slots = {};
    for (const slot of EQUIPMENT_SLOTS) {
      slots[slot] = req.body[slot] || '';
    }
    const expectedRevision = req.body.expectedRevision;
    const result = await InventoryService.updatePreparedEquipment(req.accountId, slots, expectedRevision);
    res.json(result);
  } catch (err) {
    next(err);
  }
});

// POST /character/me/inventory/training-weapon
// Body: { weaponId: string, destinationSlot?: ''|'weaponSlot1'|'weaponSlot2', expectedRevision: number }
router.post('/me/inventory/training-weapon', inventoryProtocol, trainingWeaponValidator, async (req, res, next) => {
  try {
    const { weaponId, destinationSlot = '' } = req.body;
    const expectedRevision = req.body.expectedRevision;
    const result = await InventoryService.requestTrainingWeapon(
      req.accountId, weaponId, destinationSlot, expectedRevision
    );
    res.json(result);
  } catch (err) {
    next(err);
  }
});

// POST /character/me/inventory/reservation
// Persists a raid reservation snapshot to survive disconnection.
// Body: { reservationId: string, preparedAbilities?: { slot1?: string, slot2?: string } }
router.post('/me/inventory/reservation', inventoryProtocol, pendingReservationValidator, async (req, res, next) => {
  try {
    const { reservationId, preparedAbilities } = req.body;
    const result = await InventoryService.savePendingReservation(
      req.accountId, reservationId, preparedAbilities
    );
    res.status(201).json(result);
  } catch (err) {
    next(err);
  }
});

// POST /character/me/inventory/reservation/admit
// Body: { reservationId: string }. Retains reserved ownership until the Raid outcome.
// Returns { pendingReservation, revision }; repeated admission of the same id is unchanged.
// Missing/mismatched reservation: 422 NO_ACTIVE_RESERVATION.
router.post('/me/inventory/reservation/admit', inventoryProtocol, admitReservationValidator, async (req, res, next) => {
  try {
    const result = await InventoryService.admitPendingReservation(req.accountId, req.body.reservationId);
    res.json(result);
  } catch (err) {
    next(err);
  }
});

// DELETE /character/me/inventory/reservation
// Consumes a reservation as a loss without restoring ownership. ?rollback=true restores
// only pre-admission reservations (409 RESERVATION_ADMITTED otherwise). Loss clear or
// replacement of an admitted reservation rejects with 409 EXTRACTION_RESULT_PENDING
// while an unconsumed authoritative result targets this character.
router.delete('/me/inventory/reservation', inventoryProtocol, async (req, res, next) => {
  try {
    const rollback = req.query.rollback === 'true' || req.body?.rollback === true;
    const expectedReservationId = req.query.expectedReservationId ?? req.body?.expectedReservationId;
    const result = await InventoryService.clearPendingReservation(req.accountId, { rollback, expectedReservationId });
    res.json(result);
  } catch (err) {
    next(err);
  }
});

// POST /character/me/inventory/extraction
// Persists the loot obtained from a successful raid extraction.
// Idempotent: replaying the same (raidId, resultSequence) pair returns { alreadySecured: true }.
// Body: { raidId: string, resultSequence: number, items: [{ lootId, amount }] }
router.post('/me/inventory/extraction', (req, res, next) => {
  if (!env.enableLegacyExtraction) return res.status(404).json({ error: 'NOT_FOUND', message: 'Legacy extraction endpoint is disabled or not found.' });
  next();
}, inventoryProtocol, commitExtractionValidator, async (req, res, next) => {
  try {
    const { raidId, resultSequence, items } = req.body;
    const result = await InventoryService.commitExtraction(
      req.accountId,
      raidId,
      resultSequence,
      items || []
    );
    res.json(result);
  } catch (err) {
    next(err);
  }
});

// POST /character/me/extraction/commit  [UNIFIED — Etapa 1]
// Persists loot and progression from a successful raid extraction in one atomic write.
// Idempotent: replaying the same (raidId, resultSequence) returns { alreadySecured: true }.
// Body: {
//   raidId:         string,
//   resultSequence: number,
//   items?:         [{ lootId, amount }],
//   progression?:   { consolidatedExperience: number, resultingLevel: number }
// }
router.post('/me/extraction/commit', inventoryProtocol, commitExtractionUnifiedValidator, async (req, res, next) => {
  try {
    const result = await ExtractionCommitService.commit(req.accountId, req.body);
    res.status(result.alreadySecured ? 200 : 201).json(result);
  } catch (err) {
    next(err);
  }
});

// ------------------------------------------------------------------
// TEMPORARY DEBUG ENDPOINT FOR TESTING (Stage 2)
// Simulated Fusion Webhook
// ------------------------------------------------------------------
const AuthoritativeExtractionResult = require('../models/AuthoritativeExtractionResult');

router.post('/debug/mock-fusion-result', (req, res, next) => {
  if (!env.enableDebugRoutes) return res.status(404).json({ error: 'NOT_FOUND', message: 'Mock endpoint is disabled or not found.' });
  next();
}, inventoryProtocol, async (req, res, next) => {
  try {
    const { raidId, items, experienceGranted, preparedEquipment } = req.body;
    
    await AuthoritativeExtractionResult.findOneAndUpdate(
      { raidId, accountId: req.accountId },
      { 
        items: items || [], 
        experienceGranted: experienceGranted || 100,
        ...(preparedEquipment ? { preparedEquipment } : {})
      },
      { upsert: true, new: true }
    );

    res.json({ status: 'mock_injected', raidId, accountId: req.accountId });
  } catch (err) {
    next(err);
  }
});

module.exports = router;
