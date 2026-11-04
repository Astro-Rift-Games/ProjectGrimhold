'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const mongoose = require('mongoose');
const request = require('supertest');
const jwt = require('jsonwebtoken');
const app = require('../src/app');
const env = require('../src/config/env');
const Character = require('../src/models/Character');
const Result = require('../src/models/AuthoritativeExtractionResult');
const Revoked = require('../src/models/RevokedToken');
const Inventory = require('../src/services/InventoryService');
const Extraction = require('../src/services/ExtractionCommitService');
const Session = require('../src/services/SessionTicketService');
const Raid = require('../src/services/RaidResultService');
const { EQUIPMENT_SLOTS } = require('../src/config/equipmentSlots');
const id = n => n.toString(16).padStart(24, '0');
const plain = () => ({ gemSocketCount: 0, gems: [] });
const mods = (gems = [null, null]) => ({ primary: { attribute: 'strength', grade: 'E' }, gemSocketCount: 2, gems });
const equipment = (lootId = 'arming_sword', n = 1, modifiers = plain()) => ({ lootId, instance: {
  instanceId: id(n), ...(lootId === 'arming_sword' ? { modifiers } : {}) } });
const unit = (slot, lootId = 'arming_sword', n = 1, modifiers) => ({ slot, amount: 1, ...equipment(lootId, n, modifiers) });
const emptyEquipment = () => Object.fromEntries(EQUIPMENT_SLOTS.map(slot => [slot, null]));
const accountId = new mongoose.Types.ObjectId().toString();
const token = jwt.sign({ sub: accountId }, env.jwtSecret);

// A detached read models Mongoose: changing a fetched candidate cannot change stored state.
test('v3 instance public HTTP/service contract', async t => {
  let stored, result, writes, claims, publications, loseWrite;
  const originals = { find: Character.findOne, update: Character.findOneAndUpdate, exists: Character.exists,
    result: Result.findOne, claim: Result.findOneAndUpdate, create: Result.create, pending: Result.exists, revoked: Revoked.exists };
  const flags = ['INVENTORY_PROTOCOL_GATE', 'INVENTORY_INSTANCE_PROTOCOL_GATE', 'INVENTORY_SPLIT_ENABLED'];
  const previous = flags.map(key => process.env[key]);
  t.after(() => {
    Character.findOne = originals.find; Character.findOneAndUpdate = originals.update; Character.exists = originals.exists;
    Result.findOne = originals.result; Result.findOneAndUpdate = originals.claim; Result.create = originals.create;
    Result.exists = originals.pending; Revoked.exists = originals.revoked;
    flags.forEach((key, index) => { if (previous[index] === undefined) delete process.env[key]; else process.env[key] = previous[index]; });
  });
  t.beforeEach(() => {
    process.env.INVENTORY_PROTOCOL_GATE = 'false'; process.env.INVENTORY_INSTANCE_PROTOCOL_GATE = 'false';
    process.env.INVENTORY_SPLIT_ENABLED = 'true';
    writes = 0; claims = 0; publications = 0; loseWrite = false;
    stored = { _id: new mongoose.Types.ObjectId().toString(), accountId, name: 'Instances', revision: 4, level: 1, experience: 0,
      characterAttributes: { availablePoints: 0 }, appliedShopReceipts: [], appliedProgressionReceipts: [],
      inventory: { inventorySchemaVersion: 3, currency: 500, stash: [], loadout: [], preparedEquipment: emptyEquipment(),
        pendingReservation: null, buyback: { sessionEpoch: 'town', entries: [] }, appliedExtractionReceipts: [] } };
    result = { _id: 'result', raidId: 'raid', accountId, items: [], experienceGranted: 0, consumed: false };
    Character.findOne = async () => structuredClone(stored);
    Character.exists = async () => false;
    Character.findOneAndUpdate = async (filter, update) => {
      if (loseWrite || filter.revision !== stored.revision) return null;
      writes++;
      const get = path => path.split('.').reduce((object, key) => object[key], stored);
      const put = (path, value) => {
        const keys = path.split('.'); let object = stored;
        for (const key of keys.slice(0, -1)) object = object[key];
        object[keys.at(-1)] = structuredClone(value);
      };
      for (const [path, value] of Object.entries(update.$set || {})) put(path, value);
      for (const path of Object.keys(update.$unset || {})) put(path, null);
      for (const [path, amount] of Object.entries(update.$inc || {})) put(path, get(path) + amount);
      for (const [path, value] of Object.entries(update.$push || {})) {
        const values = (get(path) || []).concat(value.$each || [value]);
        put(path, value.$slice ? values.slice(value.$slice) : values);
      }
      for (const [path, match] of Object.entries(update.$pull || {})) put(path, get(path).filter(value => value.entryId !== match.entryId));
      return structuredClone(stored);
    };
    Result.findOne = async () => structuredClone(result);
    Result.findOneAndUpdate = async () => { claims++; result.consumed = true; return structuredClone(result); };
    Result.create = async value => { publications++; result = structuredClone(value); return result; };
    Result.exists = async () => false; Revoked.exists = async () => false;
  });
  const send = (method, path, body, header) => {
    const req = request(app)[method](`/character${path}`).set('Authorization', `Bearer ${token}`);
    if (header !== undefined) req.set('X-Grimhold-Inventory-Protocol', header);
    return body === undefined ? req : req.send(body);
  };
  const snapshot = () => Inventory.getInventory(accountId);
  async function rejected(operation, code, statusCode = 409) {
    const before = structuredClone(stored); const counters = [writes, claims, publications];
    await assert.rejects(operation, { statusCode, errorCode: code });
    assert.deepEqual(stored, before); assert.deepEqual([writes, claims, publications], counters);
  }
  async function rejectedHttp(path, body, code, status = 409) {
    const before = structuredClone(stored); const counters = [writes, claims, publications];
    const res = await send('post', path, body);
    assert.equal(res.status, status, JSON.stringify(res.body)); assert.equal(res.body.error, code);
    assert.deepEqual(stored, before); assert.deepEqual([writes, claims, publications], counters);
  }

  await t.test('independent protocol 3 cutover precedes body validation and leaves v2 gate intact', async () => {
    process.env.INVENTORY_INSTANCE_PROTOCOL_GATE = 'true';
    for (const header of [undefined, '1', '2']) {
      const before = structuredClone(stored);
      const res = await send('post', '/me/inventory/gems/install', {}, header);
      assert.equal(res.status, 426); assert.deepEqual(res.body, { error: 'CLIENT_UPGRADE_REQUIRED', message: 'Inventory protocol 3 is required.' });
      assert.deepEqual(stored, before); assert.equal(writes, 0);
    }
    assert.equal((await send('get', '/me/inventory', undefined, '3')).status, 200);
    stored.inventory.inventorySchemaVersion = 2;
    const legacy = await send('get', '/me/inventory', undefined, '3');
    assert.equal(legacy.status, 409); assert.deepEqual(legacy.body, { error: 'INVENTORY_MIGRATION_REQUIRED', message: 'Inventory migration is required.' });
    process.env.INVENTORY_INSTANCE_PROTOCOL_GATE = 'false'; process.env.INVENTORY_PROTOCOL_GATE = 'true';
    assert.equal((await send('get', '/me/inventory', undefined, '2')).status, 200);
    stored.inventory.inventorySchemaVersion = 3;
    assert.equal((await send('get', '/me/inventory', undefined, '2')).status, 200); assert.equal(writes, 0);
  });

  for (const list of ['stash', 'loadout']) await t.test(`v3 ${list} read preserves complete instances and sorted slots without writes`, async () => {
    stored.inventory[list] = [unit(9), unit(3, 'shield', 2)];
    const before = structuredClone(stored);
    const res = await send('get', '/me/inventory');
    assert.equal(res.status, 200); assert.deepEqual(res.body, { inventorySchemaVersion: 3, currency: 500,
      stash: list === 'stash' ? [unit(3, 'shield', 2), unit(9)] : [], loadout: list === 'loadout' ? [unit(3, 'shield', 2), unit(9)] : [],
      preparedEquipment: emptyEquipment(), pendingReservation: null, buyback: { entries: [] }, lastAppliedExtractionReceipt: null, revision: 4 });
    assert.deepEqual(stored, before); assert.equal(writes, 0);
  });

  for (const direction of ['moveToLoadout', 'moveToStash']) await t.test(`${direction} carries unique instances, never merges, and rejects full capacity`, async () => {
    const from = direction === 'moveToLoadout' ? 'stash' : 'loadout'; const to = from === 'stash' ? 'loadout' : 'stash';
    stored.inventory[from] = [unit(8)]; stored.inventory[to] = [unit(5, 'arming_sword', 2)];
    const response = await Inventory[direction](accountId, 'arming_sword', 1, 4, 8);
    assert.deepEqual(response, { [from]: [], [to]: [unit(0), unit(5, 'arming_sword', 2)], revision: 5 });
    stored.inventory[from] = [unit(8, 'arming_sword', 3)];
    stored.inventory[to] = Array.from({ length: to === 'loadout' ? 30 : 60 }, (_, slot) => unit(slot, 'shield', slot + 10));
    await rejected(() => Inventory[direction](accountId, 'arming_sword', 1, 5, 8), 'INVENTORY_FULL');
  });

  await t.test('same-template slot move swaps instances; order preserves identity; split rejects without mutation', async () => {
    stored.inventory.stash = [unit(9), unit(3, 'arming_sword', 2, mods())];
    const moved = await Inventory.moveSlot(accountId, 'stash', 9, 'stash', 3, 'arming_sword', 4);
    assert.deepEqual(moved, { stash: [unit(3), unit(9, 'arming_sword', 2, mods())], loadout: [], revision: 5 });
    const ordered = await Inventory.updateOrder(accountId, 'stash', undefined, 5, [9, 3]);
    assert.deepEqual(ordered, { stash: [unit(0, 'arming_sword', 2, mods()), unit(1)], loadout: [], revision: 6 });
    await rejected(() => Inventory.splitSlot(accountId, 'stash', 0, 1, 2, 'arming_sword', 6), 'INSTANCE_NOT_SPLITTABLE');
  });

  await t.test('equipment resolves instanceId, preserves authoritative modifiers, reassigns, and unequips whole entries', async () => {
    stored.inventory.stash = [unit(9, 'arming_sword', 1, mods()), unit(3, 'arming_sword', 2)];
    const equipped = await Inventory.updatePreparedEquipment(accountId, { weaponSlot1: { lootId: 'arming_sword', instanceId: id(1) } }, 4);
    assert.deepEqual(equipped, { preparedEquipment: { ...emptyEquipment(), weaponSlot1: equipment('arming_sword', 1, mods()) },
      stash: [unit(3, 'arming_sword', 2)], loadout: [], revision: 5 });
    const reassigned = await Inventory.updatePreparedEquipment(accountId, { weaponSlot2: { lootId: 'arming_sword', instanceId: id(1) } }, 5);
    assert.deepEqual(reassigned.preparedEquipment, { ...emptyEquipment(), weaponSlot2: equipment('arming_sword', 1, mods()) });
    const unequipped = await Inventory.updatePreparedEquipment(accountId, {}, 6);
    assert.deepEqual(unequipped, { preparedEquipment: emptyEquipment(), stash: [unit(3, 'arming_sword', 2)], loadout: [unit(0, 'arming_sword', 1, mods())], revision: 7 });
    await rejected(() => Inventory.updatePreparedEquipment(accountId, { weaponSlot1: 'arming_sword' }, 7), 'INVALID_INVENTORY', 400);
    await rejected(() => Inventory.updatePreparedEquipment(accountId, { weaponSlot1: { lootId: 'arming_sword', instanceId: id(99) } }, 7), 'ITEM_NOT_FOUND', 422);
  });

  await t.test('buy creates one backend-issued plain instance per unit and replay does not issue again', async () => {
    const bought = await Inventory.shopBuy(accountId, 'placeholder_helmet', 2, 0, 4, 'buy');
    assert.equal(bought.currency, 480); assert.equal(bought.revision, 5); assert.deepEqual(bought.buyback, { entries: [] });
    assert.deepEqual(bought.loadout.map(({ instance, ...item }) => item), [
      { slot: 0, lootId: 'placeholder_helmet', amount: 1 }, { slot: 1, lootId: 'placeholder_helmet', amount: 1 }]);
    assert.equal(new Set(bought.loadout.map(i => i.instance.instanceId)).size, 2);
    for (const item of bought.loadout) { assert.match(item.instance.instanceId, /^[a-f0-9]{24}$/); assert.deepEqual(Object.keys(item.instance), ['instanceId']); }
    assert.deepEqual(await Inventory.shopBuy(accountId, 'placeholder_helmet', 2, 0, 4, 'buy'), { ...bought, alreadySecured: true });
    assert.equal(writes, 1);
  });

  for (const gems of [[], ['RES_RUBY_GEM'], ['RES_RUBY_GEM', 'RES_SAPPHIRE_GEM']]) await t.test(`sell accepts zero-base weapon with ${gems.length} gems and no buyback`, async () => {
    stored.inventory.loadout = [unit(8, 'arming_sword', 1, { gemSocketCount: gems.length, gems })];
    const sold = await Inventory.shopSell(accountId, 'arming_sword', 1, 99999, 4, 'sell', 8);
    assert.deepEqual(sold, { revision: 5, currency: 500 + gems.length * 70, stash: [], loadout: [], preparedEquipment: emptyEquipment(), buyback: { entries: [] } });
    assert.deepEqual(await Inventory.shopSell(accountId, 'arming_sword', 1, 0, 4, 'sell', 8), { ...sold, alreadySecured: true });
    await rejected(() => Inventory.shopSell(accountId, 'arming_sword', 1, 0, 5, 'sell', 3), 'TRANSACTION_ID_REUSED'); assert.equal(writes, 1);
  });

  await t.test('buyback rejects a pre-migration equipment entry on v3 without mutating', async () => {
    stored.inventory.buyback.entries = [{ entryId: 'old-sale', lootId: 'arming_sword', amount: 1, unitPrice: 0 }];
    const before = structuredClone(stored);
    await rejected(() => Inventory.shopBuyback(accountId, 'old-sale', 1, 4, 'rebuy'), 'BUYBACK_NOT_ELIGIBLE');
    assert.equal(writes, 0);
    assert.deepEqual(stored, before);
  });

  await t.test('reservation, admission snapshot and rollback retain exact instances; new fallback is plain', async () => {
    stored.inventory.loadout = [unit(8, 'arming_sword', 1, mods()), unit(3, 'shield', 2)];
    const reserved = await Inventory.savePendingReservation(accountId, 'reserve');
    assert.deepEqual(reserved.pendingReservation.items, [unit(3, 'shield', 2)]);
    assert.deepEqual(reserved.pendingReservation.preparedEquipment.weaponSlot1, equipment('arming_sword', 1, mods()));
    const ticket = await Session.issueRaidAdmissionTicket(accountId, 'ABC123', 'reserve');
    assert.deepEqual(ticket.snapshot.items, [unit(3, 'shield', 2)]);
    assert.deepEqual(ticket.snapshot.preparedEquipment, reserved.pendingReservation.preparedEquipment);
    const admitted = await Inventory.admitPendingReservation(accountId, 'reserve');
    assert.deepEqual(admitted.pendingReservation.items, reserved.pendingReservation.items); assert.equal(admitted.revision, 6);
    await rejected(() => Inventory.clearPendingReservation(accountId, { rollback: true }), 'RESERVATION_ADMITTED');
    await Inventory.clearPendingReservation(accountId); assert.equal((await snapshot()).pendingReservation, null);
    const fallback = await Inventory.savePendingReservation(accountId, 'fallback');
    const weapon = fallback.pendingReservation.preparedEquipment.weaponSlot1;
    assert.equal(weapon.lootId, 'arming_sword'); assert.match(weapon.instance.instanceId, /^[a-f0-9]{24}$/);
    assert.deepEqual(weapon.instance.modifiers, plain());
    await Inventory.clearPendingReservation(accountId, { rollback: true });
    assert.deepEqual((await snapshot()).preparedEquipment.weaponSlot1, weapon);
  });

  const installBody = () => ({ transactionId: 'install', weaponList: 'stash', weaponSlot: 9, instanceId: id(1),
    gemList: 'loadout', gemSlot: 3, gemLootId: 'RES_RUBY_GEM', expectedRevision: 4 });
  const setupInstall = (modifiers = mods(), amount = 2) => {
    stored.inventory.stash = [unit(9, 'arming_sword', 1, modifiers)];
    stored.inventory.loadout = [{ slot: 3, lootId: 'RES_RUBY_GEM', amount }];
  };
  for (const [weaponList, gemList] of [['stash', 'loadout'], ['stash', 'stash'], ['loadout', 'stash'], ['loadout', 'loadout']]) {
    await t.test(`install ${gemList} -> ${weaponList} fills lowest empty socket in one write and replays`, async () => {
      stored.inventory[weaponList] = [unit(9, 'arming_sword', 1, mods([null, 'RES_SAPPHIRE_GEM']))];
      stored.inventory[gemList].push({ slot: 3, lootId: 'RES_RUBY_GEM', amount: 1 });
      const body = { ...installBody(), weaponList, gemList };
      const response = await send('post', '/me/inventory/gems/install', body);
      assert.equal(response.status, 200, JSON.stringify(response.body));
      const expected = { stash: [], loadout: [], revision: 5 };
      expected[weaponList] = [unit(9, 'arming_sword', 1, mods(['RES_RUBY_GEM', 'RES_SAPPHIRE_GEM']))];
      assert.deepEqual(response.body, expected); assert.equal(writes, 1);
      const replay = await send('post', '/me/inventory/gems/install', body);
      assert.equal(replay.status, 200); assert.deepEqual(replay.body, { ...expected, alreadySecured: true }); assert.equal(writes, 1);
      await rejectedHttp('/me/inventory/gems/install', { ...body, gemSlot: 4 }, 'TRANSACTION_ID_REUSED');
    });
  }
  for (const [label, patch, modifiers, code] of [
    ['missing weapon', { weaponSlot: 8 }, mods(), 'INSTANCE_MISMATCH'],
    ['instance mismatch', { instanceId: id(2) }, mods(), 'INSTANCE_MISMATCH'],
    ['missing gem', { gemSlot: 4 }, mods(), 'GEM_NOT_AVAILABLE'],
    ['not a gem', { gemLootId: 'bone' }, mods(), 'GEM_NOT_AVAILABLE'],
    ['no sockets', {}, plain(), 'GEM_NO_SOCKETS'],
    ['occupied sockets', {}, mods(['RES_ATTUNEMENT_GEM', 'RES_SAPPHIRE_GEM']), 'GEM_NO_EMPTY_SOCKET'],
    ['stale revision', { expectedRevision: 3 }, mods(), 'REVISION_CONFLICT']
  ]) await t.test(`install rejects ${label} without consuming gem, receipts or revision`, async () => {
    setupInstall(modifiers); await rejectedHttp('/me/inventory/gems/install', { ...installBody(), ...patch }, code);
  });
  await t.test('install decrements a stack, cannot target equipped/nonweapon entries, and loses a write race safely', async () => {
    setupInstall();
    const success = await send('post', '/me/inventory/gems/install', installBody());
    assert.equal(success.status, 200); assert.deepEqual(success.body.loadout, [{ slot: 3, lootId: 'RES_RUBY_GEM', amount: 1 }]);
    assert.equal(success.body.revision, 5);
    setupInstall(); stored.revision = 4; stored.appliedShopReceipts = []; loseWrite = true;
    await rejectedHttp('/me/inventory/gems/install', installBody(), 'REVISION_CONFLICT'); loseWrite = false;
    stored.inventory.preparedEquipment.weaponSlot1 = equipment('arming_sword', 1, mods()); stored.inventory.stash = [];
    await rejectedHttp('/me/inventory/gems/install', installBody(), 'INSTANCE_MISMATCH');
    stored.inventory.stash = [unit(9, 'shield', 2)];
    await rejectedHttp('/me/inventory/gems/install', installBody(), 'INSTANCE_MISMATCH');
  });
  for (const patch of [{ weaponList: 'preparedEquipment' }, { weaponSlot: '9' }, { gemSlot: -1 }, { instanceId: 'ABC' },
    { expectedRevision: null }, { transactionId: '' }, { gemLootId: '' }]) await t.test(`install validates DTO ${JSON.stringify(patch)}`, async () => {
    setupInstall(); await rejectedHttp('/me/inventory/gems/install', { ...installBody(), ...patch }, 'VALIDATION_FAILED', 400);
  });

  await t.test('extraction keeps same-template variants separate, issues fresh IDs for rewards and equipment, replay is read-only', async () => {
    result.items = [{ lootId: 'arming_sword', amount: 1, modifiers: mods() }, { lootId: 'arming_sword', amount: 1, modifiers: plain() },
      { lootId: 'bone', amount: 2 }, { lootId: 'bone', amount: 3 }];
    result.preparedEquipment = { weaponSlot1: { lootId: 'arming_sword', modifiers: mods(['RES_RUBY_GEM', null]) } };
    stored.inventory.stash = [unit(7, 'shield', 99)];
    const committed = await Extraction.commit(accountId, { raidId: 'raid', resultSequence: 1 });
    assert.equal(committed.revision, 5); assert.equal(committed.alreadySecured, false); assert.equal(writes, 1); assert.equal(claims, 1);
    assert.equal(committed.loadout.length, 3); assert.deepEqual(committed.loadout[0].instance.modifiers, mods());
    assert.deepEqual(committed.loadout[1].instance.modifiers, plain()); assert.deepEqual(committed.loadout[2], { slot: 2, lootId: 'bone', amount: 5 });
    const after = await snapshot(); assert.deepEqual(after.stash, [unit(7, 'shield', 99)]);
    assert.deepEqual(after.preparedEquipment.weaponSlot1.instance.modifiers, mods(['RES_RUBY_GEM', null]));
    const ids = [...committed.loadout.slice(0, 2).map(item => item.instance.instanceId), after.preparedEquipment.weaponSlot1.instance.instanceId];
    assert.equal(new Set(ids).size, 3); ids.forEach(value => assert.match(value, /^[a-f0-9]{24}$/));
    const replay = await Extraction.commit(accountId, { raidId: 'raid', resultSequence: 1 });
    assert.deepEqual(replay, { ...committed, alreadySecured: true }); assert.equal(writes, 1); assert.equal(claims, 1);
  });
  for (const item of [
    { lootId: 'arming_sword', amount: 2 },
    { lootId: 'shield', amount: 1, modifiers: plain() },
    { lootId: 'bone', amount: 1, modifiers: plain() },
    { lootId: 'arming_sword', amount: 1, instance: { instanceId: id(1) } },
    ...[
      { ...mods(), primary: { attribute: 'dexterity', grade: 'E' } },
      { ...plain(), secondary: { attribute: 'luck', grade: 'E' } },
      { ...mods(), secondary: { attribute: 'strength', grade: 'S' } },
      { ...mods(), primary: { attribute: 'strength', grade: 'F' } },
      { ...mods(), primary: { attribute: 'strength', grade: ['E'] } },
      { ...mods(), primary: { attribute: 'Strength', grade: 'E' } },
      { gemSocketCount: 3, gems: [null, null, null] },
      { gemSocketCount: 0, gems: ['RES_RUBY_GEM'] },
      { gemSocketCount: 1, gems: [null, 'RES_RUBY_GEM'] },
      { gemSocketCount: 1, gems: ['bone'] },
      { gemSocketCount: -1, gems: [] }
    ].map(modifiers => ({ lootId: 'arming_sword', amount: 1, modifiers }))
  ]) await t.test(`extraction rejects illegal result ${JSON.stringify(item)} before claim`, async () => {
    result.items = [item]; await rejected(() => Extraction.commit(accountId, { raidId: 'raid', resultSequence: 1 }), 'INVALID_INVENTORY', 400);
    assert.equal(result.consumed, false);
  });
  for (const grade of ['E', 'D', 'C', 'B', 'A', 'S']) await t.test(`extraction accepts legal grade ${grade}, different secondary and catalog gems`, async () => {
    const modifiers = { primary: { attribute: 'strength', grade }, secondary: { attribute: 'luck', grade }, gemSocketCount: 1, gems: ['RES_ATTUNEMENT_GEM'] };
    result.items = [{ lootId: 'arming_sword', amount: 1, modifiers }];
    const committed = await Extraction.commit(accountId, { raidId: 'raid', resultSequence: 1 });
    assert.deepEqual(committed.loadout[0].instance.modifiers, modifiers); assert.equal(committed.revision, 5);
  });

  await t.test('host result immutability binds modifiers, not just template and quantity', async () => {
    stored.inventory.pendingReservation = { reservationId: 'reserve', items: [], preparedEquipment: emptyEquipment() };
    Result.findOne = async () => null;
    const payload = { raidId: 'raid', resultSequence: 1, items: [{ lootId: 'arming_sword', amount: 1, modifiers: mods() }], preparedEquipment: {}, experienceGranted: 0 };
    const published = await Raid.publishHostResult(accountId, payload);
    assert.equal(published.alreadyPublished, false); assert.equal(publications, 1);
    Result.findOne = async () => structuredClone(result);
    assert.equal((await Raid.publishHostResult(accountId, payload)).alreadyPublished, true);
    await rejected(() => Raid.publishHostResult(accountId, { ...payload, items: [{ ...payload.items[0], modifiers: mods(['RES_RUBY_GEM', null]) }] }), 'RESULT_CONFLICT');
  });

  await t.test('PRESERVE v2 transfers, move, order, split, equipment, buy/sell/buyback, reservation and extraction retain their formats', async () => {
    stored.inventory.inventorySchemaVersion = 2; stored.inventory.preparedEquipment = {};
    stored.inventory.stash = [{ slot: 9, lootId: 'bone', amount: 4 }];
    assert.deepEqual((await Inventory.moveToLoadout(accountId, 'bone', 2, 4, 9)).loadout, [{ slot: 0, lootId: 'bone', amount: 2 }]);
    assert.deepEqual((await Inventory.moveToStash(accountId, 'bone', 1, 5, 0)).stash, [{ slot: 9, lootId: 'bone', amount: 3 }]);
    assert.deepEqual((await Inventory.moveSlot(accountId, 'stash', 9, 'loadout', 0, 'bone', 6)).loadout, [{ slot: 0, lootId: 'bone', amount: 4 }]);
    await Inventory.splitSlot(accountId, 'loadout', 0, 1, 9, 'bone', 7);
    assert.deepEqual((await Inventory.updateOrder(accountId, 'loadout', undefined, 8, [9, 0])).loadout,
      [{ slot: 0, lootId: 'bone', amount: 1 }, { slot: 1, lootId: 'bone', amount: 3 }]);
    const bought = await Inventory.shopBuy(accountId, 'placeholder_helmet', 2, 0, 9, 'buy');
    assert.deepEqual(bought.loadout.at(-1), { slot: 2, lootId: 'placeholder_helmet', amount: 2 });
    const equipped = await Inventory.updatePreparedEquipment(accountId, { helmet: 'placeholder_helmet' }, 10);
    assert.equal(equipped.preparedEquipment.helmet, 'placeholder_helmet');
    const sold = await Inventory.shopSell(accountId, 'placeholder_helmet', 1, 0, 11, 'sell', 2);
    assert.equal(sold.currency, 485); assert.equal(sold.buyback.entries.length, 1);
    const line = sold.buyback.entries[0];
    assert.deepEqual((await Inventory.shopBuyback(accountId, line.entryId, 1, 12, 'rebuy')).loadout.at(-1), { slot: 2, lootId: 'placeholder_helmet', amount: 1 });
    const reserved = await Inventory.savePendingReservation(accountId, 'reserve');
    assert.equal(reserved.pendingReservation.preparedEquipment.weaponSlot1, 'arming_sword');
    const admitted = await Inventory.admitPendingReservation(accountId, 'reserve'); assert.ok(admitted.pendingReservation.admittedAt);
    assert.deepEqual((await Session.issueRaidAdmissionTicket(accountId, 'ABC123', 'reserve')).snapshot.items,
      [{ lootId: 'bone', amount: 4 }, { lootId: 'placeholder_helmet', amount: 1 }]);
    await Inventory.clearPendingReservation(accountId);
    result.items = [{ lootId: 'bone', amount: 2 }, { lootId: 'bone', amount: 3 }];
    assert.deepEqual((await Extraction.commit(accountId, { raidId: 'raid', resultSequence: 1 })).loadout, [{ slot: 0, lootId: 'bone', amount: 5 }]);
  });
});

// Validation must also protect model saves, not just a particular route.
test('v3 character schema rejects inconsistent instance shapes in every ownership location', () => {
  const make = (inventory) => new Character({ accountId, name: 'Schema', inventory: { inventorySchemaVersion: 3, ...inventory } });
  const valid = make({ stash: [unit(3, 'arming_sword', 5)], loadout: [unit(7, 'shield', 2)], preparedEquipment: { weaponSlot1: equipment() },
    pendingReservation: { reservationId: 'r', items: [unit(9, 'placeholder_helmet', 3)], preparedEquipment: { helmet: equipment('placeholder_helmet', 4) } } });
  assert.equal(valid.validateSync(), undefined);
  assert.deepEqual(valid.inventory.stash[0].toObject(), unit(3, 'arming_sword', 5));
  for (const invalid of [
    { slot: 0, lootId: 'arming_sword', amount: 1 }, { ...unit(0), amount: 2 }, { ...unit(0), lootId: 'bone' },
    { ...unit(0), instance: { instanceId: 'A'.repeat(24) } },
    { ...unit(0, 'shield'), instance: { instanceId: id(1), modifiers: plain() } }
  ]) for (const list of ['stash', 'loadout', 'reservation']) {
    const inventory = list === 'reservation' ? { pendingReservation: { reservationId: 'r', items: [invalid] } } : { [list]: [invalid] };
    assert.ok(make(inventory).validateSync(), `${list}: ${JSON.stringify(invalid)}`);
  }
  assert.ok(make({ preparedEquipment: { weaponSlot1: 'arming_sword' } }).validateSync());
});
