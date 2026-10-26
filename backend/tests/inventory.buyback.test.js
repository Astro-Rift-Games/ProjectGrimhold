'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const Character = require('../src/models/Character');
const InventoryService = require('../src/services/InventoryService');
const catalog = require('../src/config/shopCatalog');

test('buyback classification normalizes aliases and excludes resources', () => {
  assert.equal(catalog.BUYBACK_PRICE_MULTIPLIER, 1.5);
  assert.equal(catalog.BUYBACK_MAX_ENTRIES, 16);
  assert.equal(catalog.isBuybackEligible('placeholder_helmet'), true);
  assert.equal(catalog.isBuybackEligible('training_sword'), true);
  assert.equal(catalog.isBuybackEligible('heavy_armor_legplate'), true);
  for (const id of ['coins', 'bone', 'healthpotion', '', null]) assert.equal(catalog.isBuybackEligible(id), false);
});

test('buyback rejects guards without writing and replay precedes guards', async t => {
  const originalFind = Character.findOne;
  const originalUpdate = Character.findOneAndUpdate;
  t.after(() => { Character.findOne = originalFind; Character.findOneAndUpdate = originalUpdate; });
  const entry = { entryId: 'entry', lootId: 'placeholder_helmet', amount: 2, unitPrice: 8 };
  const doc = { revision: 0, inventory: { currency: 100, stash: [], loadout: [], buyback: { sessionEpoch: 'epoch', entries: [entry] } }, appliedShopReceipts: [] };
  Character.findOne = async () => structuredClone(doc);
  Character.findOneAndUpdate = async () => { assert.fail('rejected operations must not write'); };
  for (const [changes, id, amount, revision, code] of [
    [{ pendingReservation: { reservationId: 'raid' } }, 'entry', 1, 0, 'RESERVATION_PENDING'],
    [{}, 'entry', 1, 9, 'REVISION_CONFLICT'],
    [{}, 'foreign', 1, 0, 'BUYBACK_ENTRY_NOT_FOUND'],
    [{}, 'entry', 3, 0, 'INSUFFICIENT_BUYBACK_UNITS'],
    [{ currency: 0 }, 'entry', 1, 0, 'INSUFFICIENT_FUNDS']
  ]) {
    const snapshot = structuredClone(doc);
    Object.assign(doc.inventory, changes);
    await assert.rejects(InventoryService.shopBuyback('account', id, amount, revision, 'new'), e => e.errorCode === code);
    Object.assign(doc, snapshot);
  }
  doc.inventory.pendingReservation = { reservationId: 'raid' };
  doc.appliedShopReceipts = [{ transactionId: 'replay', type: 'buyback', entryId: 'entry', lootId: entry.lootId, amount: 1 }];
  assert.equal((await InventoryService.shopBuyback('account', 'entry', 1, 99, 'replay')).alreadySecured, true);
  await assert.rejects(InventoryService.shopBuyback('account', 'other', 1, 99, 'replay'), e => e.errorCode === 'TRANSACTION_ID_REUSED');
});
