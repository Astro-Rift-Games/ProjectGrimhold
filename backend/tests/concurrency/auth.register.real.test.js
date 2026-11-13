// tests/concurrency/auth.register.real.test.js
//
// FIX-38: two concurrent registrations of the same username must resolve to
// one success and one 409 USERNAME_TAKEN, never a raw duplicate-key 500.

'use strict';

const { describe, test, before, after } = require('node:test');
const assert = require('node:assert/strict');
const mongoose = require('mongoose');
const { startMemoryMongo } = require('../support/memoryMongo');
const Account = require('../../src/models/Account');
const Character = require('../../src/models/Character');
const AuthService = require('../../src/services/AuthService');
const CharacterService = require('../../src/services/CharacterService');

describe('Concurrent registration with real MongoDB (FIX-38)', () => {
  let mongoServer;

  before(async () => {
    mongoServer = await startMemoryMongo();
    await mongoose.connect(mongoServer.getUri());
    // The unique indexes are the arbiter of the race; without them the test is vacuous.
    await Account.init();
    await Character.init();
  });

  after(async () => {
    await mongoose.disconnect();
    await mongoServer.stop();
  });

  test('two simultaneous registrations: one succeeds and the other is USERNAME_TAKEN', async () => {
    const results = await Promise.allSettled([
      AuthService.register('race_user', 'password123'),
      AuthService.register('race_user', 'password123')
    ]);

    const fulfilled = results.filter(r => r.status === 'fulfilled');
    const rejected = results.filter(r => r.status === 'rejected');
    assert.equal(fulfilled.length, 1);
    assert.equal(rejected.length, 1);
    assert.equal(rejected[0].reason.statusCode, 409);
    assert.equal(rejected[0].reason.errorCode, 'USERNAME_TAKEN');
    assert.equal(rejected[0].reason.message, 'Username already exists');
    assert.equal(await Account.countDocuments({ username: 'race_user' }), 1);
  });

  test('two simultaneous character creations: one succeeds and the other is CHARACTER_ALREADY_EXISTS', async () => {
    const accountId = new mongoose.Types.ObjectId();
    const results = await Promise.allSettled([
      CharacterService.createForAccount(accountId, 'RaceHero'),
      CharacterService.createForAccount(accountId, 'RaceHero')
    ]);

    const rejected = results.filter(r => r.status === 'rejected');
    assert.equal(results.filter(r => r.status === 'fulfilled').length, 1);
    assert.equal(rejected.length, 1);
    assert.equal(rejected[0].reason.statusCode, 409);
    assert.equal(rejected[0].reason.errorCode, 'CHARACTER_ALREADY_EXISTS');
    assert.equal(await Character.countDocuments({ accountId }), 1);
  });
});
