// src/routes/inventory.routes.js
const express = require('express');
const router = express.Router();
const InventoryService = require('../services/InventoryService');
const ExtractionCommitService = require('../services/ExtractionCommitService');
const env = require('../config/env');
const { EQUIPMENT_SLOTS } = require('../config/equipmentSlots');
const authenticate = require('../middleware/authenticate');
const {
  moveItemValidator,
  preparedEquipmentValidator,
  pendingReservationValidator,
  commitExtractionValidator,
  shopSellValidator,
  shopBuyValidator
} = require('../validators/inventory.validators');
const { commitExtractionUnifiedValidator } = require('../validators/extraction.validators');

// All inventory routes require a valid JWT token.
router.use(authenticate);

// GET /character/me/inventory
// Returns the full inventory snapshot (stash, loadout, preparedEquipment, pendingReservation).
// Called at login to hydrate the local Unity state.
router.get('/me/inventory', async (req, res, next) => {
  try {
    const inventory = await InventoryService.getInventory(req.accountId);
    res.json(inventory);
  } catch (err) {
    next(err);
  }
});

// POST /character/me/inventory/stash/move-to-loadout
// Moves amount units of lootId from the stash to the loadout.
// Body: { lootId: string, amount: number }
router.post('/me/inventory/stash/move-to-loadout', moveItemValidator, async (req, res, next) => {
  try {
    const { lootId, amount, expectedRevision } = req.body;
    const result = await InventoryService.moveToLoadout(req.accountId, lootId, amount, expectedRevision);
    res.json(result);
  } catch (err) {
    next(err);
  }
});

// POST /character/me/inventory/loadout/move-to-stash
// Moves amount units of lootId from the loadout to the stash.
// Body: { lootId: string, amount: number }
router.post('/me/inventory/loadout/move-to-stash', moveItemValidator, async (req, res, next) => {
  try {
    const { lootId, amount, expectedRevision } = req.body;
    const result = await InventoryService.moveToStash(req.accountId, lootId, amount, expectedRevision);
    res.json(result);
  } catch (err) {
    next(err);
  }
});

// POST /character/me/inventory/shop/sell
// Sells an item from the loadout, awarding currency.
// Body: { lootId: string, amount: number, declaredSellValue: number, expectedRevision: number }
router.post('/me/inventory/shop/sell', shopSellValidator, async (req, res, next) => {
  try {
    const { lootId, amount, declaredSellValue, expectedRevision } = req.body;
    const result = await InventoryService.shopSell(req.accountId, lootId, amount, declaredSellValue, expectedRevision);
    res.json(result);
  } catch (err) {
    next(err);
  }
});

// POST /character/me/inventory/shop/buy
// Buys an item into the loadout, deducting currency.
// Body: { lootId: string, amount: number, declaredPrice: number, expectedRevision: number }
router.post('/me/inventory/shop/buy', shopBuyValidator, async (req, res, next) => {
  try {
    const { lootId, amount, declaredPrice, expectedRevision } = req.body;
    const result = await InventoryService.shopBuy(req.accountId, lootId, amount, declaredPrice, expectedRevision);
    res.json(result);
  } catch (err) {
    next(err);
  }
});

// PUT /character/me/inventory/prepared-equipment
// Replaces all six equipment slot assignments atomically.
// Body: { weaponSlot1?, weaponSlot2?, helmet?, armor?, gloves?, boots? }
router.put('/me/inventory/prepared-equipment', preparedEquipmentValidator, async (req, res, next) => {
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

// POST /character/me/inventory/reservation
// Persists a raid reservation snapshot to survive disconnection.
// Body: { reservationId: string }
router.post('/me/inventory/reservation', pendingReservationValidator, async (req, res, next) => {
  try {
    const { reservationId } = req.body;
    const result = await InventoryService.savePendingReservation(
      req.accountId, reservationId
    );
    res.status(201).json(result);
  } catch (err) {
    next(err);
  }
});

// DELETE /character/me/inventory/reservation
// Clears the pending reservation once a raid completes or the player exits voluntarily.
router.delete('/me/inventory/reservation', async (req, res, next) => {
  try {
    const result = await InventoryService.clearPendingReservation(req.accountId);
    res.json(result);
  } catch (err) {
    next(err);
  }
});

// POST /character/me/inventory/extraction
// Persists the loot obtained from a successful raid extraction.
// Idempotent: replaying the same (raidId, resultSequence) pair returns { alreadySecured: true }.
// Body: { raidId: string, resultSequence: number, items: [{ lootId, amount }] }
router.post('/me/inventory/extraction', commitExtractionValidator, async (req, res, next) => {
  try {
    if (env.nodeEnv === 'production') {
      return res.status(404).json({ message: 'Legacy extraction endpoint is disabled in production.' });
    }
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
router.post('/me/extraction/commit', commitExtractionUnifiedValidator, async (req, res, next) => {
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

router.post('/debug/mock-fusion-result', async (req, res, next) => {
  try {
    if (env.nodeEnv === 'production') {
      return res.status(404).json({ message: 'Mock endpoint is disabled in production. This endpoint is necessary in dev/staging because there is no dedicated Fusion server yet.' });
    }
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
