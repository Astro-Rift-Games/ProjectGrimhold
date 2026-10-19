'use strict';

const test = require('node:test');
const assert = require('node:assert');

const Character = require('../src/models/Character');
const InventoryService = require('../src/services/InventoryService');
const { MAX_TRANSACTION_AMOUNT } = require('../src/config/shopCatalog');

function makeCharacter(overrides = {}) {
  const defaults = {
    _id: 'char123',
    accountId: 'acc123',
    inventory: {
      currency: 100,
      stash:   [],
      loadout: [],
      preparedEquipment: { weaponSlot1: '', weaponSlot2: '', helmet: '', armor: '', gloves: '', boots: '' },
      pendingReservation: null
    },
    appliedShopReceipts: [],
    revision: 0
  };
  const doc = { ...defaults, ...overrides };
  doc.markModified = () => {};
  doc.save = async function () { return this; };
  return doc;
}

function applyUpdate(doc, update) {
  if (update.$set) {
    for (const [key, value] of Object.entries(update.$set)) {
      const parts = key.split('.');
      let current = doc;
      for (let i = 0; i < parts.length - 1; i++) {
        if (!current[parts[i]]) current[parts[i]] = {};
        current = current[parts[i]];
      }
      current[parts[parts.length - 1]] = value;
    }
  }
  if (update.$inc) {
    for (const [key, value] of Object.entries(update.$inc)) {
      const parts = key.split('.');
      let current = doc;
      for (let i = 0; i < parts.length - 1; i++) {
        if (!current[parts[i]]) current[parts[i]] = {};
        current = current[parts[i]];
      }
      current[parts[parts.length - 1]] = (current[parts[parts.length - 1]] || 0) + value;
    }
  }
  if (update.$push && update.$push.appliedShopReceipts) {
    const each = update.$push.appliedShopReceipts.$each || [];
    doc.appliedShopReceipts = (doc.appliedShopReceipts || []).concat(each);
    if (update.$push.appliedShopReceipts.$slice) {
      doc.appliedShopReceipts = doc.appliedShopReceipts.slice(update.$push.appliedShopReceipts.$slice);
    }
  }
}

test('InventoryService - Shop Authoritative Security (US-57 / SEC-57.2)', async (t) => {
  const originalFindOne = Character.findOne;
  const originalFindOneAndUpdate = Character.findOneAndUpdate;

  t.afterEach(() => {
    Character.findOne = originalFindOne;
    Character.findOneAndUpdate = originalFindOneAndUpdate;
  });

  const setupMocks = (mockChar) => {
    Character.findOne = async () => mockChar;
    Character.findOneAndUpdate = async (filter, update) => {
      if (!mockChar) return null;
      // Filter check for currency balance if present
      if (filter['inventory.currency'] && filter['inventory.currency'].$gte !== undefined) {
        if ((mockChar.inventory.currency || 0) < filter['inventory.currency'].$gte) {
          return null; // Concurrency/insufficient funds at atomic level
        }
      }
      applyUpdate(mockChar, update);
      return mockChar;
    };
  };

  await t.test('Exploit: Client declaredPrice = 0 cannot bypass server price or exceed max amount', async () => {
    const char = makeCharacter({ inventory: { currency: 0, loadout: [], stash: [] } });
    setupMocks(char);

    // Attacker tries to get 1,000,000 health potions for free by sending declaredPrice = 0
    await assert.rejects(
      async () => {
        await InventoryService.shopBuy('acc123', 'healthpotion', 1000000, 0, 0, 'tx_free_million');
      },
      (err) => {
        // Must reject either due to quantity limit or insufficient funds
        return err.statusCode === 400 || err.statusCode === 409;
      },
      'Should reject free million units exploit'
    );
  });

  await t.test('Exploit: Client declaredSellValue = 1,000,000,000 does NOT grant arbitrary gold', async () => {
    const char = makeCharacter({
      inventory: {
        currency: 0,
        loadout: [{ lootId: 'bone', amount: 1 }],
        stash: []
      }
    });
    setupMocks(char);

    // Attacker sells 1 bone declaring 1 billion gold
    const result = await InventoryService.shopSell('acc123', 'bone', 1, 1000000000, 0, 'tx_billion_gold');
    // Authoritative sell value of bone is 5 gold, NOT 1,000,000,000
    assert.strictEqual(result.currency, 5, 'Server must calculate authoritative price (5), ignoring client declared value');
    assert.strictEqual(char.inventory.currency, 5);
  });

  await t.test('Rejection: Item not offered in shop cannot be bought', async () => {
    const char = makeCharacter({ inventory: { currency: 10000, loadout: [], stash: [] } });
    setupMocks(char);

    await assert.rejects(
      async () => {
        await InventoryService.shopBuy('acc123', 'unlisted_cheat_sword', 1, 10, 0, 'tx_unlisted');
      },
      (err) => err.statusCode === 404 && err.errorCode === 'ITEM_NOT_OFFERED'
    );
  });

  await t.test('Rejection: Item not sellable cannot be sold', async () => {
    const char = makeCharacter({
      inventory: {
        currency: 0,
        loadout: [{ lootId: 'unsellable_quest_relic', amount: 1 }],
        stash: []
      }
    });
    setupMocks(char);

    await assert.rejects(
      async () => {
        await InventoryService.shopSell('acc123', 'unsellable_quest_relic', 1, 50, 0, 'tx_unsellable');
      },
      (err) => err.statusCode === 404 && err.errorCode === 'ITEM_NOT_SELLABLE'
    );
  });

  await t.test('Rejection: Amount exceeding MAX_TRANSACTION_AMOUNT is rejected', async () => {
    const char = makeCharacter({ inventory: { currency: 100000, loadout: [], stash: [] } });
    setupMocks(char);

    await assert.rejects(
      async () => {
        await InventoryService.shopBuy('acc123', 'bone', MAX_TRANSACTION_AMOUNT + 1, 0, 0, 'tx_overflow');
      },
      (err) => err.statusCode === 400 && err.errorCode === 'INVALID_QUANTITY'
    );
  });

  await t.test('Authoritative calculation: Legitimate buy deducts server-calculated cost', async () => {
    const char = makeCharacter({ inventory: { currency: 100, loadout: [], stash: [] } });
    setupMocks(char);

    // Bone buyPrice is 5. Buying 4 bones should cost 20 gold.
    const result = await InventoryService.shopBuy('acc123', 'bone', 4, 999999, 0, 'tx_buy_bones');
    assert.strictEqual(result.currency, 80, '100 - (4 * 5) = 80 gold');
    assert.strictEqual(char.inventory.currency, 80);
    assert.strictEqual(result.loadout.find(i => i.lootId === 'bone').amount, 4);
  });

  await t.test('Authoritative calculation: Insufficient funds rejects purchase', async () => {
    const char = makeCharacter({ inventory: { currency: 15, loadout: [], stash: [] } });
    setupMocks(char);

    // Health potion costs 20 gold. Player only has 15 gold.
    await assert.rejects(
      async () => {
        await InventoryService.shopBuy('acc123', 'healthpotion', 1, 0, 0, 'tx_broke');
      },
      (err) => err.statusCode === 409 && err.errorCode === 'INSUFFICIENT_FUNDS'
    );
    assert.strictEqual(char.inventory.currency, 15, 'Balance must remain unchanged');
  });

  await t.test('Idempotency: Replaying exact transactionId returns alreadySecured', async () => {
    const char = makeCharacter({ inventory: { currency: 100, loadout: [], stash: [] } });
    setupMocks(char);

    // First buy
    const res1 = await InventoryService.shopBuy('acc123', 'bone', 2, 0, 0, 'tx_idempotent');
    assert.strictEqual(res1.currency, 90);

    // Replay with exact same payload
    const res2 = await InventoryService.shopBuy('acc123', 'bone', 2, 0, res1.revision, 'tx_idempotent');
    assert.strictEqual(res2.alreadySecured, true);
    assert.strictEqual(res2.currency, 90);
    assert.strictEqual(char.inventory.currency, 90, 'No double charge');
  });

  await t.test('Idempotency: Reusing transactionId with different payload is rejected', async () => {
    const char = makeCharacter({ inventory: { currency: 100, loadout: [], stash: [] } });
    setupMocks(char);

    // First buy bone
    await InventoryService.shopBuy('acc123', 'bone', 2, 0, 0, 'tx_reuse');

    // Attempt to reuse tx_reuse to buy healthpotion
    await assert.rejects(
      async () => {
        await InventoryService.shopBuy('acc123', 'healthpotion', 1, 0, 1, 'tx_reuse');
      },
      (err) => err.statusCode === 409 && err.errorCode === 'TRANSACTION_ID_REUSED'
    );
  });
});
