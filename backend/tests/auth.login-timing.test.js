// tests/auth.login-timing.test.js
//
// FIX-38: login must not reveal whether a username exists. Both the unknown-user
// and the wrong-password branches run a bcrypt comparison and fail identically.

'use strict';

const { describe, test, before, after, afterEach } = require('node:test');
const assert = require('node:assert/strict');
const mongoose = require('mongoose');
const bcrypt = require('bcryptjs');
const { startMemoryMongo } = require('./support/memoryMongo');
const AuthService = require('../src/services/AuthService');

const SAMPLES = 5;

function expectInvalidCredentials(error) {
  assert.equal(error.statusCode, 401);
  assert.equal(error.errorCode, 'INVALID_CREDENTIALS');
  assert.equal(error.message, 'Invalid username or password.');
  return true;
}

async function medianLoginMs(username, password) {
  const durations = [];
  for (let i = 0; i < SAMPLES; i++) {
    const start = process.hrtime.bigint();
    await assert.rejects(() => AuthService.login(username, password), expectInvalidCredentials);
    durations.push(Number(process.hrtime.bigint() - start) / 1e6);
  }
  durations.sort((a, b) => a - b);
  return durations[Math.floor(SAMPLES / 2)];
}

describe('Login timing does not reveal existing users (FIX-38)', () => {
  let mongoServer;
  const originalCompare = bcrypt.compare;

  before(async () => {
    mongoServer = await startMemoryMongo();
    await mongoose.connect(mongoServer.getUri());
    await AuthService.register('timing_user', 'correct-password');
  });

  after(async () => {
    await mongoose.disconnect();
    await mongoServer.stop();
  });

  afterEach(() => { bcrypt.compare = originalCompare; });

  function countCompares() {
    const calls = { count: 0 };
    bcrypt.compare = (...args) => { calls.count++; return originalCompare.apply(bcrypt, args); };
    return calls;
  }

  test('an unknown username runs one bcrypt comparison and fails like a wrong password', async () => {
    const calls = countCompares();
    await assert.rejects(() => AuthService.login('missing_user', 'any-password'), expectInvalidCredentials);
    assert.equal(calls.count, 1);
  });

  test('a wrong password for an existing user runs one bcrypt comparison', async () => {
    const calls = countCompares();
    await assert.rejects(() => AuthService.login('timing_user', 'wrong-password'), expectInvalidCredentials);
    assert.equal(calls.count, 1);
  });

  test('unknown and existing usernames take comparable time', async () => {
    // Warm up the lazily computed dummy hash so it is not measured.
    await assert.rejects(() => AuthService.login('missing_user', 'warmup'), expectInvalidCredentials);

    const existingMs = await medianLoginMs('timing_user', 'wrong-password');
    const missingMs = await medianLoginMs('missing_user', 'wrong-password');

    // Wide tolerance: both branches are dominated by one bcrypt comparison.
    assert.ok(missingMs >= existingMs * 0.5,
      `unknown user login (${missingMs.toFixed(1)} ms) is much faster than existing user (${existingMs.toFixed(1)} ms)`);
    assert.ok(missingMs <= existingMs * 2,
      `unknown user login (${missingMs.toFixed(1)} ms) is much slower than existing user (${existingMs.toFixed(1)} ms)`);
  });

  test('the correct password still logs in', async () => {
    const result = await AuthService.login('timing_user', 'correct-password');
    assert.ok(result.token);
  });
});
