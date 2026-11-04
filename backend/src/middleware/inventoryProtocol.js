'use strict';

const env = require('../config/env');
const Character = require('../models/Character');
const { inventoryVersion, requireMigratedInventory } = require('../services/InventoryEntries');

// Applied on individual routes: these routers share a mount point, so a global
// router.use would accidentally gate metadata/progression and unrelated routes.
async function inventoryProtocol(req, res, next) {
  try {
    if (env.inventoryProtocolGate && req.get('X-Grimhold-Inventory-Protocol') !== '2') {
      throw { statusCode: 426, errorCode: 'CLIENT_UPGRADE_REQUIRED', message: 'Inventory protocol 2 is required.' };
    }
    const slotInput = ['slot', 'fromSlot', 'toSlot', 'sourceSlot', 'destinationSlot', 'slotOrder']
      .some(key => Object.hasOwn(req.body || {}, key));
    if (env.inventoryProtocolGate || slotInput) {
      const character = await Character.findOne({ accountId: req.accountId });
      // Creation must be possible without an existing character. Other handlers
      // retain their own CHARACTER_NOT_FOUND and payload validation contracts.
      if (character) {
        requireMigratedInventory(character);
        if (slotInput && inventoryVersion(character) !== 2) {
          throw { statusCode: 409, errorCode: 'INVENTORY_MIGRATION_REQUIRED', message: 'Inventory migration is required.' };
        }
      }
    }
    next();
  } catch (err) {
    next(err);
  }
}

module.exports = inventoryProtocol;
