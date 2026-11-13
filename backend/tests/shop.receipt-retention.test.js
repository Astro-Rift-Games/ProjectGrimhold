// tests/shop.receipt-retention.test.js
//
// FIX-31: a timestamped transaction id whose receipt was evicted from the 64-receipt window must be
// rejected (409 TRANSACTION_RETENTION_EXCEEDED) instead of being applied a second time.
// Runs against an in-memory MongoDB through the public service interface.

'use strict';

const { describe, test, before, after, beforeEach } = require('node:test');
const assert = require('node:assert/strict');
const mongoose = require('mongoose');
const { startMemoryMongo } = require('./support/memoryMongo');
const Character = require('../src/models/Character');
const InventoryService = require('../src/services/InventoryService');
const AbilityService = require('../src/services/AbilityService');
const { parseTransactionTimestamp, watermarkUpdate, assertWithinRetention, SHOP_RECEIPT_WINDOW } = require('../src/services/ShopReceiptRetention');
const { createInstance } = require('../src/services/ItemInstances');

const BASE = 1_700_000_000_000;
const hex = n => n.toString(16).padStart(32, '0');
// Same wire shape as Unity's ShopTransactionId.ToString(): `<ms>_<32 hex>`.
const txId = n => `${BASE + n}_${hex(n)}`;
const RETENTION = { statusCode: 409, errorCode: 'TRANSACTION_RETENTION_EXCEEDED' };

describe('ShopReceiptRetention id parsing', () => {
  test('parses the timestamp of a timestamped id', () => {
    assert.equal(parseTransactionTimestamp(txId(5)), BASE + 5);
    assert.equal(parseTransactionTimestamp(`${BASE}_${hex(1).toUpperCase()}`), BASE);
  });

  test('returns null for legacy and malformed ids', () => {
    for (const id of [hex(7), '3f2504e0-4f89-11d3-9a0c-0305e82c3301', 'tx-old', `${BASE}_abc`, `_${hex(1)}`,
      `-5_${hex(1)}`, `${BASE}_${hex(1)}x`, '', undefined, null, 42]) {
      assert.equal(parseTransactionTimestamp(id), null, `id ${String(id)}`);
    }
  });

  test('exposes the receipt window used by every writer', () => {
    assert.equal(SHOP_RECEIPT_WINDOW, 64);
  });
});

describe('ShopReceiptRetention watermark rules', () => {
  // `count` receipts, oldest first; ids/timestamps strictly increasing unless `oldest` overrides the first.
  const receipts = (count, oldest) => Array.from({ length: count }, (_, i) => ({
    transactionId: txId(i), timestamp: new Date(BASE + i), ...(i === 0 ? oldest : {})
  }));

  test('with exactly 64 receipts the watermark is a $max to the oldest evicted timestamp', () => {
    assert.deepEqual(watermarkUpdate({ appliedShopReceipts: receipts(SHOP_RECEIPT_WINDOW) }),
      { $max: { shopReceiptWatermark: BASE } });
  });

  test('with 63 receipts nothing is evicted and there is no watermark update', () => {
    assert.deepEqual(watermarkUpdate({ appliedShopReceipts: receipts(SHOP_RECEIPT_WINDOW - 1) }), {});
  });

  test('an id timestamp in the future is capped by the receipt server timestamp', () => {
    const future = { transactionId: `${BASE * 10}_${hex(9)}`, timestamp: new Date(BASE + 7) };
    assert.deepEqual(watermarkUpdate({ appliedShopReceipts: receipts(SHOP_RECEIPT_WINDOW, future) }),
      { $max: { shopReceiptWatermark: BASE + 7 } });
  });

  test('an evicted bare-GUID id is legacy and does not move the watermark', () => {
    assert.deepEqual(watermarkUpdate({ appliedShopReceipts: receipts(SHOP_RECEIPT_WINDOW, { transactionId: hex(3) }) }), {});
    assert.doesNotThrow(() => assertWithinRetention({ shopReceiptWatermark: BASE * 10 }, hex(3)));
  });

  test('a character without shopReceiptWatermark accepts timestamped ids', () => {
    assert.doesNotThrow(() => assertWithinRetention({}, txId(0)));
    assert.doesNotThrow(() => assertWithinRetention({ appliedShopReceipts: [] }, `1_${hex(1)}`));
  });

  test('an id exactly at the watermark is rejected with 409 and watermark + 1 is accepted', () => {
    const character = { shopReceiptWatermark: BASE + 5 };
    assert.throws(() => assertWithinRetention(character, txId(5)),
      error => error.statusCode === RETENTION.statusCode && error.errorCode === RETENTION.errorCode);
    assert.doesNotThrow(() => assertWithinRetention(character, txId(6)));
  });
});

describe('shop receipt retention (FIX-31)', () => {
  let mongoServer;
  let accountId;

  before(async () => {
    mongoServer = await startMemoryMongo();
    await mongoose.connect(mongoServer.getUri());
  });

  after(async () => {
    await mongoose.disconnect();
    await mongoServer.stop();
  });

  beforeEach(async () => {
    await Character.deleteMany({});
    accountId = new mongoose.Types.ObjectId();
    await Character.create({
      accountId, name: 'Retention', revision: 0,
      inventory: { currency: 100000, loadout: [{ lootId: 'bone', amount: 500 }, { lootId: 'placeholder_helmet', amount: 10 }],
        buyback: { sessionEpoch: 'epoch', entries: [] } }
    });
  });

  const revision = async () => (await Character.findOne({ accountId })).revision;
  const buy = async (id, expectedRevision) => InventoryService.shopBuy(accountId, 'bone', 1, 0, expectedRevision ?? await revision(), id);

  // `count` later purchases with strictly increasing ids, starting at txId(from).
  async function fill(from, count) {
    for (let i = 0; i < count; i++) await buy(txId(from + i));
  }

  const snapshot = async () => Character.findOne({ accountId }).lean();

  async function assertRejectedUnchanged(action) {
    const before = await snapshot();
    await assert.rejects(action, error => {
      assert.equal(error.statusCode, RETENTION.statusCode);
      assert.equal(error.errorCode, RETENTION.errorCode);
      assert.match(error.message, /retention|window|64/i);
      return true;
    });
    assert.deepEqual(await snapshot(), before, 'rejection must not change currency, inventory, revision or receipts');
  }

  describe('buy', () => {
    test('replaying tx-old after 64 later transactions is rejected without any change', async () => {
      await buy(txId(0));
      await fill(1, SHOP_RECEIPT_WINDOW);
      const current = await snapshot();
      assert.equal(current.appliedShopReceipts.length, SHOP_RECEIPT_WINDOW);
      assert.ok(!current.appliedShopReceipts.some(r => r.transactionId === txId(0)), 'tx-old must be evicted');
      await assertRejectedUnchanged(() => buy(txId(0), current.revision));
    });

    test('replay inside the 64-receipt window is still alreadySecured', async () => {
      await buy(txId(0));
      await fill(1, SHOP_RECEIPT_WINDOW - 1);
      const before = await snapshot();
      assert.ok(before.appliedShopReceipts.some(r => r.transactionId === txId(0)));
      const replay = await buy(txId(0), before.revision);
      assert.equal(replay.alreadySecured, true);
      assert.deepEqual(await snapshot(), before);
    });

    test('a fresh timestamped id still succeeds after eviction', async () => {
      await buy(txId(0));
      await fill(1, SHOP_RECEIPT_WINDOW);
      const result = await buy(txId(1000));
      assert.equal(result.alreadySecured, undefined);
      assert.equal(result.revision, SHOP_RECEIPT_WINDOW + 2);
    });

    test('a legacy bare-GUID id keeps the window-only behavior', async () => {
      await buy(hex(1));
      await fill(1, SHOP_RECEIPT_WINDOW);
      const before = await snapshot();
      const replay = await buy(hex(1), before.revision);
      assert.equal(replay.revision, before.revision + 1, 'legacy replay applies as before');
      assert.equal(replay.currency, before.inventory.currency - 5);
    });

    test('a far-future id timestamp cannot raise the watermark above server time', async () => {
      await buy(`${BASE * 10}_${hex(9)}`);
      await fill(1, SHOP_RECEIPT_WINDOW);
      const stored = await snapshot();
      assert.ok(stored.shopReceiptWatermark <= Date.now(), `watermark ${stored.shopReceiptWatermark}`);
      const result = await buy(`${Date.now() + 5}_${hex(10)}`);
      assert.equal(result.revision, stored.revision + 1);
    });

    test('the watermark advances to the newest evicted timestamp only', async () => {
      await buy(txId(0));
      await fill(1, SHOP_RECEIPT_WINDOW - 1);
      assert.equal((await snapshot()).shopReceiptWatermark, 0, 'nothing evicted yet');
      await buy(txId(100));
      assert.equal((await snapshot()).shopReceiptWatermark, BASE, 'only tx-old was evicted');
      await buy(txId(101));
      assert.equal((await snapshot()).shopReceiptWatermark, BASE + 1);
    });
  });

  describe('sell', () => {
    const sell = async (id, expectedRevision) => InventoryService.shopSell(accountId, 'bone', 1, 0, expectedRevision ?? await revision(), id);

    test('replaying tx-old after 64 later transactions is rejected without any change', async () => {
      await sell(txId(0));
      await fill(1, SHOP_RECEIPT_WINDOW);
      const current = await snapshot();
      await assertRejectedUnchanged(() => sell(txId(0), current.revision));
    });

    test('replay inside the window is still alreadySecured; fresh id after eviction succeeds', async () => {
      await sell(txId(0));
      await fill(1, SHOP_RECEIPT_WINDOW - 1);
      assert.equal((await sell(txId(0))).alreadySecured, true);
      await buy(txId(500));
      const fresh = await sell(txId(501));
      assert.equal(fresh.alreadySecured, undefined);
    });

    test('a legacy bare-GUID id keeps the window-only behavior', async () => {
      await sell(hex(2));
      await fill(1, SHOP_RECEIPT_WINDOW);
      const before = await snapshot();
      const replay = await sell(hex(2), before.revision);
      assert.equal(replay.revision, before.revision + 1);
    });
  });

  describe('trade', () => {
    const trade = async (id, expectedRevision) => InventoryService.shopTrade(accountId, {
      transactionId: id, expectedRevision: expectedRevision ?? await revision(),
      purchases: [{ lootId: 'bone', amount: 1 }], sales: [] });

    test('replaying tx-old after 64 later transactions is rejected without any change', async () => {
      await trade(txId(0));
      await fill(1, SHOP_RECEIPT_WINDOW);
      const current = await snapshot();
      await assertRejectedUnchanged(() => trade(txId(0), current.revision));
    });

    test('replay inside the window is still alreadySecured', async () => {
      await trade(txId(0));
      await fill(1, SHOP_RECEIPT_WINDOW - 1);
      assert.equal((await trade(txId(0))).alreadySecured, true);
    });
  });

  describe('buyback', () => {
    const buyback = async (id, entryId, expectedRevision) => InventoryService.shopBuyback(accountId, entryId, 1, expectedRevision ?? await revision(), id);

    async function saleEntry() {
      const sale = await InventoryService.shopSell(accountId, 'placeholder_helmet', 3, 0, await revision(), txId(0));
      return sale.buyback.entries[0].entryId;
    }

    test('replaying tx-old after 64 later transactions is rejected without any change', async () => {
      const entryId = await saleEntry();
      await buyback(txId(1), entryId);
      await fill(2, SHOP_RECEIPT_WINDOW);
      const current = await snapshot();
      await assertRejectedUnchanged(() => buyback(txId(1), entryId, current.revision));
    });

    test('replay inside the window is still alreadySecured', async () => {
      const entryId = await saleEntry();
      await buyback(txId(1), entryId);
      await fill(2, 10);
      assert.equal((await buyback(txId(1), entryId)).alreadySecured, true);
    });
  });

  describe('install gem', () => {
    test('replaying tx-old after 64 later transactions is rejected without any change', async () => {
      const instance = createInstance('arming_sword', { gemSocketCount: 2, gems: [null, null] });
      await Character.deleteMany({});
      await Character.create({ accountId, name: 'RetentionGem', revision: 0, inventory: { inventorySchemaVersion: 3, currency: 100000,
        loadout: [{ slot: 9, lootId: 'arming_sword', amount: 1, instance }, { slot: 3, lootId: 'RES_RUBY_GEM', amount: 2 }] } });
      const install = async (id, expectedRevision) => InventoryService.installGem(accountId, {
        transactionId: id, weaponList: 'loadout', weaponSlot: 9, instanceId: instance.instanceId,
        gemList: 'loadout', gemSlot: 3, gemLootId: 'RES_RUBY_GEM', expectedRevision: expectedRevision ?? await revision() });
      await install(txId(0));
      await fill(1, SHOP_RECEIPT_WINDOW);
      const current = await snapshot();
      assert.ok(!current.appliedShopReceipts.some(r => r.transactionId === txId(0)), 'tx-old must be evicted');
      await assertRejectedUnchanged(() => install(txId(0), current.revision));
    });
  });

  describe('ability purchase', () => {
    const purchase = async (id, abilityId, expectedRevision) => AbilityService.purchaseAbility(accountId, {
      transactionId: id, abilityId, expectedRevision: expectedRevision ?? await revision() });

    test('replaying tx-old after 64 later transactions is rejected without any change', async () => {
      await purchase(txId(0), 'arcane_projectile');
      await fill(1, SHOP_RECEIPT_WINDOW);
      const current = await snapshot();
      await assertRejectedUnchanged(() => purchase(txId(0), 'arcane_projectile', current.revision));
    });

    test('replay inside the window is still alreadySecured; a fresh id after eviction succeeds', async () => {
      await purchase(txId(0), 'arcane_projectile');
      await fill(1, SHOP_RECEIPT_WINDOW - 1);
      assert.equal((await purchase(txId(0), 'arcane_projectile')).alreadySecured, true);
      await buy(txId(700));
      const fresh = await purchase(txId(701), 'charge');
      assert.equal(fresh.alreadySecured, false);
    });

    test('a legacy id keeps the previous behavior (already unlocked once evicted)', async () => {
      await purchase('tx-legacy', 'arcane_projectile');
      await fill(1, SHOP_RECEIPT_WINDOW);
      const current = await snapshot();
      await assert.rejects(() => purchase('tx-legacy', 'arcane_projectile', current.revision),
        error => error.errorCode === 'ALREADY_UNLOCKED');
    });
  });
});
