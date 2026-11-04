'use strict';

const env = require('../config/env');
const Character = require('../models/Character');
const { inventoryVersion, requireMigratedInventory } = require('../services/InventoryEntries');

// Applied on individual routes: these routers share a mount point, so a global
// router.use would accidentally gate metadata/progression and unrelated routes.
async function inventoryProtocol(req, res, next) {
  try {
    const requiredProtocol = env.inventoryInstanceProtocolGate ? '3' : env.inventoryProtocolGate ? '2' : null;
    if (requiredProtocol && req.get('X-Grimhold-Inventory-Protocol') !== requiredProtocol) {
      throw { statusCode: 426, errorCode: 'CLIENT_UPGRADE_REQUIRED', message: `Inventory protocol ${requiredProtocol} is required.` };
    }
    const slotInput = ['slot', 'fromSlot', 'toSlot', 'sourceSlot', 'destinationSlot', 'slotOrder']
      .some(key => Object.hasOwn(req.body || {}, key));
    if (requiredProtocol || slotInput) {
      const character = await Character.findOne({ accountId: req.accountId });
      // Creation must be possible without an existing character. Other handlers
      // retain their own CHARACTER_NOT_FOUND and payload validation contracts.
      if (character) {
        requireMigratedInventory(character);
        if (slotInput && ![2, 3].includes(inventoryVersion(character))) {
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
