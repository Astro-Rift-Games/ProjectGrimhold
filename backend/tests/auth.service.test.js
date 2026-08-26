const test = require('node:test');
const assert = require('node:assert');
const bcrypt = require('bcryptjs');
const jwt = require('jsonwebtoken');

// We must require the exact modules used by AuthService to mock them properly.
const Account = require('../src/models/Account');
const AuthService = require('../src/services/AuthService');

test('AuthService', async (t) => {
  // Store original methods to restore them after tests
  const originalFindOne = Account.findOne;
  const originalCompare = bcrypt.compare;
  const originalSign = jwt.sign;
  const originalHash = bcrypt.hash;

    t.afterEach(() => {
    Account.findOne = originalFindOne;
    bcrypt.compare = originalCompare;
    jwt.sign = originalSign;
    bcrypt.hash = originalHash;
  });
  
  await t.test('register() - should create account and return token', async () => {
    Account.findOne = async () => null;
    bcrypt.hash = async () => 'hashedpassword';
    jwt.sign = () => 'mocked.jwt.token';
    const originalSave = Account.prototype.save;
    Account.prototype.save = async function() { this._id = 'new_id'; return this; };

    const result = await AuthService.register('newuser', 'password123');
    
    assert.strictEqual(result.token, 'mocked.jwt.token');
    assert.ok(result.expiresIn > 0);

    Account.prototype.save = originalSave;
  });

  await t.test('register() - should throw 409 when username already exists', async () => {
    Account.findOne = async () => ({ _id: '123' });

    try {
      await AuthService.register('existinguser', 'password123');
      assert.fail('Should have thrown an error');
    } catch (error) {
      assert.strictEqual(error.statusCode, 409);
      assert.strictEqual(error.errorCode, 'USERNAME_TAKEN');
    }
  });

  await t.test('login() - should return token when credentials are valid', async () => {
    const mockAccount = { _id: '12345', passwordHash: 'hashedpassword' };
    Account.findOne = async () => mockAccount;
    bcrypt.compare = async () => true;
    jwt.sign = () => 'mocked.jwt.token';

    const result = await AuthService.login('tester_a', 'test1234');
    
    assert.strictEqual(result.token, 'mocked.jwt.token');
    assert.ok(result.expiresIn > 0);
  });

  await t.test('login() - should throw 401 when username is not found', async () => {
    Account.findOne = async () => null;

    try {
      await AuthService.login('invalid_user', 'test1234');
      assert.fail('Should have thrown an error');
    } catch (error) {
      assert.strictEqual(error.statusCode, 401);
      assert.strictEqual(error.errorCode, 'INVALID_CREDENTIALS');
    }
  });

  await t.test('login() - should throw 401 when password does not match', async () => {
    const mockAccount = { _id: '12345', passwordHash: 'hashedpassword' };
    Account.findOne = async () => mockAccount;
    bcrypt.compare = async () => false;

    try {
      await AuthService.login('tester_a', 'wrongpassword');
      assert.fail('Should have thrown an error');
    } catch (error) {
      assert.strictEqual(error.statusCode, 401);
      assert.strictEqual(error.errorCode, 'INVALID_CREDENTIALS');
    }
  });
});
