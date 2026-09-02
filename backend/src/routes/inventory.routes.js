// src/routes/inventory.routes.js
const express = require('express');
const router = express.Router();
const InventoryService = require('../services/InventoryService');
const authenticate = require('../middleware/authenticate');
const {
  moveItemValidator,
  preparedEquipmentValidator,
  pendingReservationValidator,
  commitExtractionValidator
} = require('../validators/inventory.validators');

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
    const { lootId, amount } = req.body;
    const result = await InventoryService.moveToLoadout(req.accountId, lootId, amount);
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
    const { lootId, amount } = req.body;
    const result = await InventoryService.moveToStash(req.accountId, lootId, amount);
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
    const slots = {
      weaponSlot1: req.body.weaponSlot1 || '',
      weaponSlot2: req.body.weaponSlot2 || '',
      helmet:      req.body.helmet      || '',
      armor:       req.body.armor       || '',
      gloves:      req.body.gloves      || '',
      boots:       req.body.boots       || ''
    };
    const result = await InventoryService.updatePreparedEquipment(req.accountId, slots);
    res.json(result);
  } catch (err) {
    next(err);
  }
});

// POST /character/me/inventory/reservation
// Persists a raid reservation snapshot to survive disconnection.
// Body: { reservationId: string, items: ItemData[], preparedEquipment?: PreparedEquipmentData }
router.post('/me/inventory/reservation', pendingReservationValidator, async (req, res, next) => {
  try {
    const { reservationId, items, preparedEquipment } = req.body;
    const result = await InventoryService.savePendingReservation(
      req.accountId, reservationId, items, preparedEquipment
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

module.exports = router;
