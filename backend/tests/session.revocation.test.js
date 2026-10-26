// tests/session.revocation.test.js
const { test, describe, before, after } = require('node:test');
const assert = require('node:assert/strict');
const mongoose = require('mongoose');
const { MongoMemoryServer } = require('mongodb-memory-server');
const jwt = require('jsonwebtoken');
const Account = require('../src/models/Account');
const RevokedToken = require('../src/models/RevokedToken');
const AuthService = require('../src/services/AuthService');
const authenticate = require('../src/middleware/authenticate');
const config = require('../src/config/env');

describe('Session Revocation (US-60 / SEC-60.1)', () => {
  let mongoServer;
  let account;

  before(async () => {
    mongoServer = await MongoMemoryServer.create();
    await mongoose.connect(mongoServer.getUri());

    account = await Account.create({
      username: 'revocation_tester',
      passwordHash: 'dummy_hash'
    });
  });

  after(async () => {
    await mongoose.disconnect();
    await mongoServer.stop();
  });

  test('login and register emit JWT with unique jti claim', async () => {
    const regResult = await AuthService.register('new_user_revocation', 'password123');
    assert.ok(regResult.token);
    const decodedReg = jwt.decode(regResult.token);
    assert.ok(decodedReg.jti, 'Register token must contain jti');

    const loginResult = await AuthService.login('new_user_revocation', 'password123');
    assert.ok(loginResult.token);
    const decodedLogin = jwt.decode(loginResult.token);
    assert.ok(decodedLogin.jti, 'Login token must contain jti');
    assert.notEqual(decodedReg.jti, decodedLogin.jti, 'Different logins must have distinct jti claims');
  });

  test('valid unrevoked token passes authenticate middleware', async () => {
    const user = await AuthService.register('test_auth_user', 'Passw0rd!');
    const req = {
      headers: {
        authorization: `Bearer ${user.token}`
      }
    };
    let nextCalled = false;
    let nextErr = null;

    await authenticate(req, {}, (err) => {
      nextCalled = true;
      nextErr = err;
    });

    assert.equal(nextCalled, true);
    assert.equal(nextErr, undefined);
    assert.ok(req.accountId);
    assert.equal(req.token, user.token);
    assert.ok(req.jti);
  });

  test('logout persistently revokes token; subsequent authenticate call fails with 401', async () => {
    const user = await AuthService.register('logout_user', 'Passw0rd!');
    const token = user.token;

    // 1. Token is initially valid
    let initialError = null;
    await authenticate({ headers: { authorization: `Bearer ${token}` } }, {}, (err) => {
      initialError = err;
    });
    assert.equal(initialError, undefined, 'Initial token must be valid');

    // 2. Perform logout
    const decoded = jwt.decode(token);
    const logoutSuccess = await AuthService.logout(decoded.sub, token);
    assert.equal(logoutSuccess, true);

    // 3. Verify RevokedToken was stored in DB
    const revokedDoc = await RevokedToken.findOne({ accountId: decoded.sub });
    assert.ok(revokedDoc, 'RevokedToken document must exist in DB');
    assert.equal(revokedDoc.jti, decoded.jti);
    assert.ok(revokedDoc.expiresAt instanceof Date);

    // 4. Token is now rejected by authenticate middleware
    let postLogoutError = null;
    await authenticate({ headers: { authorization: `Bearer ${token}` } }, {}, (err) => {
      postLogoutError = err;
    });

    assert.ok(postLogoutError, 'Post-logout request must fail');
    assert.equal(postLogoutError.statusCode, 401);
    assert.equal(postLogoutError.errorCode, 'UNAUTHORIZED');
    assert.equal(postLogoutError.message, 'Token has been revoked.');
  });

  test('login after logout issues a working token and the old one stays revoked', async () => {
    const old = await AuthService.register('relogin_user', 'Secret123!');
    await AuthService.logout(jwt.decode(old.token).sub, old.token);
    const fresh = await AuthService.login('relogin_user', 'Secret123!');
    assert.notEqual(fresh.token, old.token);
    for (const [token, revoked] of [[fresh.token, false], [old.token, true]]) {
      let error;
      await authenticate({ headers: { authorization: `Bearer ${token}` } }, {}, err => { error = err; });
      if (revoked) {
        assert.equal(error.statusCode, 401);
        assert.equal(error.message, 'Token has been revoked.');
      } else {
        assert.equal(error, undefined);
      }
    }
  });

  test('multiple concurrent sessions: revoking session A leaves session B active', async () => {
    await AuthService.register('multi_session_user', 'Secret123!');
    const sessionA = await AuthService.login('multi_session_user', 'Secret123!');
    const sessionB = await AuthService.login('multi_session_user', 'Secret123!');

    assert.notEqual(sessionA.token, sessionB.token, 'Sessions must have distinct tokens');

    // Revoke session A
    const decodedA = jwt.decode(sessionA.token);
    await AuthService.logout(decodedA.sub, sessionA.token);

    // Session A must be rejected
    let errorA = null;
    await authenticate({ headers: { authorization: `Bearer ${sessionA.token}` } }, {}, (err) => {
      errorA = err;
    });
    assert.ok(errorA, 'Session A must be revoked');
    assert.equal(errorA.statusCode, 401);
    assert.equal(errorA.message, 'Token has been revoked.');

    // Session B must remain valid
    let errorB = null;
    await authenticate({ headers: { authorization: `Bearer ${sessionB.token}` } }, {}, (err) => {
      errorB = err;
    });
    assert.equal(errorB, undefined, 'Session B must remain active and valid');
  });

  test('tampered signature and missing header are rejected', async () => {
    let errNoHeader = null;
    await authenticate({ headers: {} }, {}, (err) => { errNoHeader = err; });
    assert.equal(errNoHeader.statusCode, 401);

    const validUser = await AuthService.register('tamper_user', 'Secret123!');
    const tampered = validUser.token.slice(0, -6) + 'abcdef';
    let errTampered = null;
    await authenticate({ headers: { authorization: `Bearer ${tampered}` } }, {}, (err) => { errTampered = err; });
    assert.equal(errTampered.statusCode, 401);
  });
});
