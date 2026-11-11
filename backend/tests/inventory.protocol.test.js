'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const request = require('supertest');
const jwt = require('jsonwebtoken');
const mongoose = require('mongoose');
const app = require('../src/app');
const env = require('../src/config/env');
const Character = require('../src/models/Character');
const RevokedToken = require('../src/models/RevokedToken');
const Result = require('../src/models/AuthoritativeExtractionResult');
const Inventory = require('../src/services/InventoryService');
const Extraction = require('../src/services/ExtractionCommitService');
const Session = require('../src/services/SessionTicketService');

const accountId = new mongoose.Types.ObjectId().toString();
const token = jwt.sign({ sub: accountId }, env.jwtSecret);
const gatedRoutes = [
  ['get', '/me/inventory'], ['put', '/me/inventory/order'], ['post', '/me/inventory/slots/move'],
  ['post', '/me/inventory/slots/split'], ['post', '/me/inventory/transfer-all'],
  ['post', '/me/inventory/stash/move-to-loadout'], ['post', '/me/inventory/loadout/move-to-stash'],
  ['post', '/me/inventory/shop/sell'], ['post', '/me/inventory/shop/buy'], ['post', '/me/inventory/shop/buyback'],
  ['put', '/me/inventory/prepared-equipment'], ['post', '/me/inventory/reservation'],
  ['delete', '/me/inventory/reservation'], ['post', '/me/inventory/reservation/admit'],
  ['post', '/me/inventory/extraction'], ['post', '/me/extraction/commit'],
  ['post', '/debug/mock-fusion-result'], ['post', '/me/raid/admission-ticket'],
  ['post', '/raid/verify-admission'], ['post', '/raid/extraction-result'], ['post', '/me']
];

function fixture(version = 1) {
  return {
    _id: new mongoose.Types.ObjectId(), accountId, name: 'Hero', revision: 4,
    profile: { customNote: '', lastSeen: null },
    inventory: { inventorySchemaVersion: version, currency: 500,
      stash: [{ slot: 9, lootId: 'bone', amount: 2 }, { slot: 3, lootId: 'bone', amount: 1 }],
      loadout: [], preparedEquipment: {}, pendingReservation: null, appliedExtractionReceipts: [] }
  };
}

function auth(method, path, header) {
  const req = request(app)[method](`/character${path}`).set('Authorization', `Bearer ${token}`);
  if (header !== undefined) req.set('X-Grimhold-Inventory-Protocol', header);
  return req;
}

test('inventory protocol B1', async t => {
  let character, writes, saves, claims;
  const gate = process.env.INVENTORY_PROTOCOL_GATE;
  const split = process.env.INVENTORY_SPLIT_ENABLED;
  const debug = process.env.ENABLE_DEV_DEBUG_ROUTES;
  const legacy = process.env.ENABLE_LEGACY_EXTRACTION;
  const nodeEnv = env.nodeEnv;
  const originals = { find: Character.findOne, update: Character.findOneAndUpdate,
    updateOne: Character.updateOne, byId: Character.findById, save: Character.prototype.save, revoked: RevokedToken.exists,
    result: Result.findOne, claim: Result.findOneAndUpdate };
  t.beforeEach(() => {
    process.env.INVENTORY_PROTOCOL_GATE = 'false';
    process.env.INVENTORY_SPLIT_ENABLED = 'true';
    process.env.ENABLE_DEV_DEBUG_ROUTES = 'true';
    process.env.ENABLE_LEGACY_EXTRACTION = 'true';
    env.nodeEnv = 'test';
    character = fixture(); writes = 0; saves = 0; claims = 0;
    character.save = async () => { saves++; return character; };
    character.markModified = () => {};
    Character.findOne = async () => character;
    Character.findById = async () => character;
    Character.prototype.save = originals.save;
    Character.findOneAndUpdate = async () => { writes++; return character; };
    Character.updateOne = async () => { writes++; };
    RevokedToken.exists = async () => false;
    Result.findOne = async () => ({ items: [], experienceGranted: 0, consumed: false });
    Result.findOneAndUpdate = async () => { claims++; return {}; };
  });
  t.after(() => {
    Character.findOne = originals.find; Character.findOneAndUpdate = originals.update;
    Character.updateOne = originals.updateOne; Character.findById = originals.byId;
    Character.prototype.save = originals.save;
    RevokedToken.exists = originals.revoked; Result.findOne = originals.result;
    Result.findOneAndUpdate = originals.claim; env.nodeEnv = nodeEnv;
    for (const [key, value] of Object.entries({ INVENTORY_PROTOCOL_GATE: gate, INVENTORY_SPLIT_ENABLED: split,
      ENABLE_DEV_DEBUG_ROUTES: debug, ENABLE_LEGACY_EXTRACTION: legacy })) {
      if (value === undefined) delete process.env[key]; else process.env[key] = value;
    }
  });

  for (const [method, path] of gatedRoutes) {
    for (const header of [undefined, '1', '4']) {
      await t.test(`gate on rejects ${method.toUpperCase()} ${path} header ${header}`, async () => {
        process.env.INVENTORY_PROTOCOL_GATE = 'true';
        const before = JSON.stringify(character);
        const res = await auth(method, path, header).send({});
        assert.equal(res.status, 426);
        assert.deepEqual(res.body, { error: 'CLIENT_UPGRADE_REQUIRED', message: 'Inventory protocol 2 is required.' });
        assert.equal(JSON.stringify(character), before);
        assert.equal(writes + saves + claims, 0);
      });
    }
    await t.test(`gate on accepts a v3-capable client (header 3) at ${method.toUpperCase()} ${path}`, async () => {
      process.env.INVENTORY_PROTOCOL_GATE = 'true';
      const before = JSON.stringify(character);
      const res = await auth(method, path, '3').send({});
      assert.equal(res.status, 409);
      assert.deepEqual(res.body, { error: 'INVENTORY_MIGRATION_REQUIRED', message: 'Inventory migration is required.' });
      assert.equal(JSON.stringify(character), before);
    });
    await t.test(`gate on rejects v1 at ${method.toUpperCase()} ${path}`, async () => {
      process.env.INVENTORY_PROTOCOL_GATE = 'true';
      const before = JSON.stringify(character);
      const res = await auth(method, path, '2').send({});
      assert.equal(res.status, 409);
      assert.deepEqual(res.body, { error: 'INVENTORY_MIGRATION_REQUIRED', message: 'Inventory migration is required.' });
      assert.equal(JSON.stringify(character), before);
      assert.equal(writes + saves + claims, 0);
    });
  }

  for (const version of [undefined, 1, 2]) {
    await t.test(`read version ${version} never repairs aliases, duplicates, order or revision`, async () => {
      character.inventory.inventorySchemaVersion = version;
      character.inventory.loadout = [{ lootId: 'training_sword', amount: 2, ...(version === 2 ? { slot: 7 } : {}) }];
      character.inventory.pendingReservation = { reservationId: 'res', items: character.inventory.stash.slice(), preparedEquipment: {} };
      const before = JSON.stringify(character);
      const res = await auth('get', '/me/inventory', 'old');
      assert.equal(res.status, 200);
      assert.equal(res.body.inventorySchemaVersion, version || 1);
      const expected = character.inventory.stash.slice();
      if (version === 2) expected.sort((a, b) => a.slot - b.slot);
      assert.deepEqual(res.body.stash, expected);
      assert.deepEqual(res.body.pendingReservation.items, expected);
      assert.deepEqual(res.body.loadout, character.inventory.loadout);
      assert.equal(res.body.revision, 4);
      assert.equal(JSON.stringify(character), before);
      assert.equal(writes + saves, 0);
    });
  }

  await t.test('gate on accepts protocol 2 reading v2 with duplicates and holes', async () => {
    process.env.INVENTORY_PROTOCOL_GATE = 'true'; character.inventory.inventorySchemaVersion = 2;
    const res = await auth('get', '/me/inventory', '2');
    assert.equal(res.status, 200);
    assert.deepEqual(res.body.stash, character.inventory.stash.slice().reverse());
    assert.equal(writes + saves, 0);
  });

  for (const version of [1, 2]) {
    await t.test(`new character version follows gate (${version})`, async () => {
      process.env.INVENTORY_PROTOCOL_GATE = version === 2 ? 'true' : 'false';
      character = null;
      let created;
      Character.prototype.save = async function () { saves++; created = this; return this; };
      const res = await auth('post', '/me', version === 2 ? '2' : undefined).send({ name: 'Hero' });
      assert.equal(res.status, 201);
      assert.equal(created.inventory.inventorySchemaVersion, version);
      assert.equal(saves, 1);
      assert.deepEqual(created.inventory.stash.toObject(), []);
    });
  }

  for (const path of ['/me', '/me/profile']) {
    await t.test(`metadata ${path} contains no lists and stays ungated`, async () => {
      process.env.INVENTORY_PROTOCOL_GATE = 'true';
      const res = await auth('get', path);
      assert.equal(res.status, 200); assert.equal(res.body.inventory, undefined);
      assert.equal(writes + saves, 0);
    });
  }

  for (const [method, path, body] of [
    ['post', '/me/inventory/stash/move-to-loadout', { lootId: 'bone', amount: 1, expectedRevision: 4, fromSlot: 9 }],
    ['put', '/me/inventory/order', { list: 'stash', slotOrder: [9, 3], expectedRevision: 4 }]
  ]) {
    await t.test(`slot-addressed input on v1 fails closed (${path})`, async () => {
      const before = JSON.stringify(character);
      const res = await auth(method, path).send(body);
      assert.equal(res.status, 409); assert.equal(res.body.error, 'INVENTORY_MIGRATION_REQUIRED');
      assert.equal(JSON.stringify(character), before); assert.equal(writes + saves, 0);
    });
  }

  for (const operation of [
    () => Inventory.moveToLoadout(accountId, 'bone', 1, 4),
    () => Inventory.moveToStash(accountId, 'bone', 1, 4),
    () => Inventory.updateOrder(accountId, 'stash', ['bone', 'bone'], 4),
    () => Inventory.shopSell(accountId, 'bone', 1, 1, 4, 'tx')
  ]) {
    await t.test(`v2 unaddressed source/order rejects before any mutation: ${operation}`, async () => {
      character.inventory.inventorySchemaVersion = 2;
      const before = JSON.stringify(character);
      await assert.rejects(operation, { statusCode: 409, errorCode: 'INVENTORY_SLOT_ADDRESS_REQUIRED' });
      assert.equal(JSON.stringify(character), before); assert.equal(writes + saves + claims, 0);
    });
  }

  for (const operation of [
    () => Inventory.moveToLoadout(accountId, 'bone', 1, 4),
    () => Inventory.moveToStash(accountId, 'bone', 1, 4),
    () => Inventory.updateOrder(accountId, 'stash', ['bone', 'bone'], 4),
    () => Inventory.updatePreparedEquipment(accountId, { helmet: 'bone' }, 4),
    () => Inventory.savePendingReservation(accountId, 'new'),
    () => Inventory.shopSell(accountId, 'bone', 1, 1, 4, 'tx'),
    () => Inventory.shopBuy(accountId, 'bone', 1, 1, 4, 'tx')
  ]) {
    await t.test(`v1 addressed duplicates reject rather than merge: ${operation}`, async () => {
      character.inventory.loadout = character.inventory.stash.slice();
      const before = JSON.stringify(character);
      await assert.rejects(operation, { statusCode: 409, errorCode: 'INVENTORY_MIGRATION_REQUIRED' });
      assert.equal(JSON.stringify(character), before); assert.equal(writes + saves + claims, 0);
    });
  }

  await t.test('legacy v1 extraction folds only the inbound aggregate, not stored stash', async () => {
    const before = JSON.stringify(character.inventory.stash);
    const res = await Inventory.commitExtraction(accountId, 'raid', 1,
      [{ lootId: 'bone', amount: 2 }, { lootId: 'bone', amount: 3 }]);
    assert.deepEqual(res.loadout, [{ lootId: 'bone', amount: 5 }]);
    assert.equal(JSON.stringify(character.inventory.stash), before); assert.equal(saves, 1);
  });

  await t.test('v2 reservation admission uses a path-scoped update and retains exact entries', async () => {
    character.inventory.inventorySchemaVersion = 2;
    character.inventory.pendingReservation = { reservationId: 'res', items: character.inventory.stash.slice(), preparedEquipment: {} };
    const before = JSON.stringify(character.inventory);
    Character.findOneAndUpdate = async (filter, update) => {
      writes++;
      assert.deepEqual(Object.keys(update.$set), ['inventory.pendingReservation.admittedAt']);
      character.inventory.pendingReservation.admittedAt = update.$set['inventory.pendingReservation.admittedAt'];
      character.revision++;
      return character;
    };
    const res = await Inventory.admitPendingReservation(accountId, 'res');
    assert.deepEqual(res.pendingReservation.items, character.inventory.stash.slice().reverse());
    delete character.inventory.pendingReservation.admittedAt;
    assert.equal(JSON.stringify(character.inventory), before); assert.equal(writes, 1);
  });

  await t.test('v2 admission ticket aggregates duplicate IDs only in the Raid boundary payload', async () => {
    character.inventory.inventorySchemaVersion = 2;
    character.inventory.pendingReservation = { reservationId: 'res', items: character.inventory.stash.slice(), preparedEquipment: { weaponSlot2: 'training_wand' } };
    const before = JSON.stringify(character);
    const res = await Session.issueRaidAdmissionTicket(accountId, 'ABC123', 'res');
    assert.deepEqual(res.snapshot.items, [{ lootId: 'bone', amount: 3 }]);
    assert.equal(JSON.stringify(character), before); assert.equal(writes + saves, 0);
  });

  await t.test('v1 buyback rejects addressed duplicates without changing currency, receipts or entries', async () => {
    character.inventory.loadout = character.inventory.stash.slice();
    character.inventory.buyback = { entries: [{ entryId: 'sale', lootId: 'bone', amount: 1, unitPrice: 1 }] };
    const before = JSON.stringify(character);
    await assert.rejects(() => Inventory.shopBuyback(accountId, 'sale', 1, 4, 'tx'),
      { statusCode: 409, errorCode: 'INVENTORY_MIGRATION_REQUIRED' });
    assert.equal(JSON.stringify(character), before); assert.equal(writes + saves, 0);
  });

  await t.test('v2 authenticated host cannot verify a v1 participant ticket', async () => {
    process.env.INVENTORY_PROTOCOL_GATE = 'true';
    character.inventory.inventorySchemaVersion = 2;
    const participant = fixture(1);
    Character.findById = async () => participant;
    const ticket = Session.buildSignedTicketForTesting({ characterId: participant._id.toString(),
      raidCode: 'ABC123', expiresAt: Math.floor(Date.now() / 1000) + 60, snapshot: { items: [] } });
    const before = JSON.stringify(participant);
    const res = await auth('post', '/raid/verify-admission', '2').send({ ticket, raidCode: 'ABC123' });
    assert.equal(res.status, 409); assert.equal(res.body.error, 'INVENTORY_MIGRATION_REQUIRED');
    assert.equal(JSON.stringify(participant), before); assert.equal(writes + saves + claims, 0);
  });

  await t.test('v2 authenticated host cannot publish rewards for a v1 recipient', async () => {
    process.env.INVENTORY_PROTOCOL_GATE = 'true';
    character.inventory.inventorySchemaVersion = 2;
    const participant = fixture(1);
    Character.findById = async () => participant;
    const before = JSON.stringify(participant);
    const res = await auth('post', '/raid/extraction-result', '2').send({ raidId: 'raid', resultSequence: 1,
      targetCharacterId: participant._id.toString(), items: [], experienceGranted: 0 });
    assert.equal(res.status, 409); assert.equal(res.body.error, 'INVENTORY_MIGRATION_REQUIRED');
    assert.equal(JSON.stringify(participant), before); assert.equal(writes + saves + claims, 0);
  });

  await t.test('production debug and legacy endpoints remain disabled even with gate on', async () => {
    env.nodeEnv = 'production'; process.env.INVENTORY_PROTOCOL_GATE = 'true';
    for (const path of ['/debug/mock-fusion-result', '/me/inventory/extraction']) {
      const res = await auth('post', path).send({ raidId: 'raid', resultSequence: 1, items: [] });
      assert.equal(res.status, 404);
    }
    assert.equal(writes + saves + claims, 0);
  });
});

test('slot schema validates stash, loadout and reservation entries without weakening strictness', () => {
  const id = new mongoose.Types.ObjectId();
  const make = (slot) => new Character({ accountId: id, name: 'Hero', inventory: {
    stash: [{ slot, lootId: 'bone', amount: 1, arbitrary: true }],
    loadout: [{ slot, lootId: 'bone', amount: 1 }],
    pendingReservation: { reservationId: 'res', items: [{ slot, lootId: 'bone', amount: 1 }] }
  } });
  for (const slot of [undefined, 0, 60]) {
    const doc = make(slot); assert.equal(doc.validateSync(), undefined);
    assert.equal(doc.inventory.stash[0].slot, slot);
    assert.equal(doc.inventory.loadout[0].slot, slot);
    assert.equal(doc.inventory.pendingReservation.items[0].slot, slot);
    assert.equal(doc.inventory.stash[0].toObject().arbitrary, undefined);
    assert.equal(doc.inventory.inventorySchemaVersion, 1);
  }
  for (const slot of [-1, 0.5, NaN, Infinity, null]) assert.ok(make(slot).validateSync(), `invalid slot ${slot}`);
});
