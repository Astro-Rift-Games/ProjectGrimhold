'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const request = require('supertest');
const jwt = require('jsonwebtoken');
const app = require('../src/app');
const env = require('../src/config/env');
const Character = require('../src/models/Character');
const Revoked = require('../src/models/RevokedToken');
const SessionTicketService = require('../src/services/SessionTicketService');

const message = 'Equip a weapon in a Weapon Set or ask the Trainer before preparing an expedition.';
test('weapon required for reservation and admission without automatic grants', async t => {
  const originals = [Character.findOne, Character.findOneAndUpdate, Revoked.exists];
  t.after(() => { [Character.findOne, Character.findOneAndUpdate, Revoked.exists] = originals; });
  const accountId = 'aaaaaaaaaaaaaaaaaaaaaaaa';
  const token = jwt.sign({ sub: accountId }, env.jwtSecret);
  for (const version of [1, 2, 3]) for (const source of ['empty', 'loadout', 'stash', 'retry', 'aborted']) {
    await t.test(`v${version} ${source} rejects without writes`, async () => {
      const stored = { _id: accountId, accountId, revision: 8, inventory: {
        inventorySchemaVersion: version, stash: [], loadout: [], preparedEquipment: {}, pendingReservation: null
      } };
      const item = { slot: 0, lootId: 'arming_sword', amount: 1,
        ...(version === 3 ? { instance: { instanceId: '111111111111111111111111', modifiers: { gemSocketCount: 0, gems: [] } } } : {}) };
      if (['loadout', 'stash'].includes(source)) stored.inventory[source].push(item);
      if (['retry', 'aborted'].includes(source)) stored.inventory.pendingReservation = {
        reservationId: source === 'retry' ? 'new' : 'old', admittedAt: null, items: [item], preparedEquipment: {}
      };
      let writes = 0;
      Character.findOne = async () => structuredClone(stored);
      Character.findOneAndUpdate = async () => { writes++; return structuredClone(stored); };
      Revoked.exists = async () => false;
      const before = structuredClone(stored);
      const response = await request(app).post('/character/me/inventory/reservation')
        .set('Authorization', `Bearer ${token}`).send({ reservationId: 'new' });
      assert.equal(response.status, 409);
      assert.deepEqual(response.body, { error: 'PREPARED_WEAPON_REQUIRED', message });
      assert.deepEqual(stored, before); assert.equal(writes, 0);
    });
  }
  for (const version of [1, 2, 3]) await t.test(`v${version} weaponless ticket fails closed`, async () => {
    const stored = { inventory: { inventorySchemaVersion: version, pendingReservation: {
      reservationId: 'res', items: [], preparedEquipment: {}
    } }, revision: 8 };
    Character.findOne = async () => structuredClone(stored);
    const before = structuredClone(stored);
    await assert.rejects(() => SessionTicketService.issueRaidAdmissionTicket(accountId, 'RAID01', 'res'),
      { statusCode: 409, errorCode: 'PREPARED_WEAPON_REQUIRED', message });
    assert.deepEqual(stored, before);
  });
});
