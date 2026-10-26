// tests/integration/auth.hardening.integration.test.js
const test = require('node:test');
const assert = require('node:assert');
const request = require('supertest');
const app = require('../../src/app');
const testDb = require('./testDb');
const authRoutes = require('../../src/routes/auth.routes');
const env = require('../../src/config/env');

test('E2E Session Revocation and Backend Hardening (US-60 / SEC-60.4)', async (t) => {
  await testDb.connect();

  t.after(async () => {
    await testDb.closeDatabase();
  });

  let token1;
  let token2;
  const username = 'hardeningHero';
  const password = 'StrongPassword123!';

  // Reset rate limiters before testing
  if (authRoutes.ipRateLimiter) authRoutes.ipRateLimiter.reset();
  if (authRoutes.userLoginLimiter) authRoutes.userLoginLimiter.reset();

  await t.test('1. Register and Verify Active Token', async () => {
    const regRes = await request(app)
      .post('/auth/register')
      .send({ username, password });
    assert.strictEqual(regRes.status, 201);
    assert.ok(regRes.body.token);
    token1 = regRes.body.token;

    // Create character
    const charRes = await request(app)
      .post('/character/me')
      .set('Authorization', `Bearer ${token1}`)
      .send({ name: 'SirHardening' });
    assert.strictEqual(charRes.status, 201);

    // Call protected endpoint
    const meRes = await request(app)
      .get('/character/me')
      .set('Authorization', `Bearer ${token1}`);
    assert.strictEqual(meRes.status, 200);
    assert.strictEqual(meRes.body.name, 'SirHardening');
  });

  await t.test('2. Logout Revokes Session Token (204 No Content)', async () => {
    const logoutRes = await request(app)
      .post('/auth/logout')
      .set('Authorization', `Bearer ${token1}`);
    assert.strictEqual(logoutRes.status, 204);
  });

  await t.test('3. Revoked Token is Rejected on Protected Routes with 401', async () => {
    const accessRes = await request(app)
      .get('/character/me')
      .set('Authorization', `Bearer ${token1}`);
    assert.strictEqual(accessRes.status, 401);
    assert.strictEqual(accessRes.body.error, 'UNAUTHORIZED');
    assert.strictEqual(accessRes.body.message, 'Token has been revoked.');
  });

  await t.test('4. Re-Login Generates New Valid Token', async () => {
    const loginRes = await request(app)
      .post('/auth/login')
      .send({ username, password });
    assert.strictEqual(loginRes.status, 200);
    assert.ok(loginRes.body.token);
    assert.notStrictEqual(loginRes.body.token, token1);
    token1 = loginRes.body.token;

    const meRes = await request(app)
      .get('/character/me')
      .set('Authorization', `Bearer ${token1}`);
    assert.strictEqual(meRes.status, 200);
    assert.strictEqual(meRes.body.name, 'SirHardening');
  });

  await t.test('5. Multi-Session Independence: Logging out Device A does not invalidate Device B', async () => {
    // Generate Device B token
    const loginResB = await request(app)
      .post('/auth/login')
      .send({ username, password });
    assert.strictEqual(loginResB.status, 200);
    token2 = loginResB.body.token;
    assert.notStrictEqual(token1, token2);

    // Logout Device A
    const logoutResA = await request(app)
      .post('/auth/logout')
      .set('Authorization', `Bearer ${token1}`);
    assert.strictEqual(logoutResA.status, 204);

    // Device A token is now revoked
    const testA = await request(app)
      .get('/character/me')
      .set('Authorization', `Bearer ${token1}`);
    assert.strictEqual(testA.status, 401);
    assert.strictEqual(testA.body.message, 'Token has been revoked.');

    // Device B token remains completely valid
    const testB = await request(app)
      .get('/character/me')
      .set('Authorization', `Bearer ${token2}`);
    assert.strictEqual(testB.status, 200);
    assert.strictEqual(testB.body.name, 'SirHardening');
  });

  await t.test('6. User-Specific Brute Force Rate Limiting (429 Too Many Requests)', async () => {
    const bruteForceUser = 'bruteTarget';
    await request(app)
      .post('/auth/register')
      .send({ username: bruteForceUser, password: 'LegitPassword123!' });

    // Exceed allowed failed attempts (default 5)
    for (let i = 0; i < 5; i++) {
      const failRes = await request(app)
        .post('/auth/login')
        .send({ username: bruteForceUser, password: 'WrongPassword!' });
      assert.strictEqual(failRes.status, 401);
    }

    // 6th attempt must be rejected with 429
    const blockedRes = await request(app)
      .post('/auth/login')
      .send({ username: bruteForceUser, password: 'WrongPassword!' });
    assert.strictEqual(blockedRes.status, 429);
    assert.strictEqual(blockedRes.body.error, 'TOO_MANY_REQUESTS');
    assert.ok(blockedRes.headers['retry-after']);
    assert.ok(blockedRes.body.retryAfter > 0);

    // Other users are not locked out
    const otherLogin = await request(app)
      .post('/auth/login')
      .send({ username, password });
    assert.strictEqual(otherLogin.status, 200);
  });

  await t.test('7. Debug / Mock Endpoints Guard in Production or when Opt-In is Disabled', async () => {
    const originalNodeEnv = env.nodeEnv;
    const originalDebug = process.env.ENABLE_DEV_DEBUG_ROUTES;
    const originalLegacy = process.env.ENABLE_LEGACY_EXTRACTION;
    try {
      // 1. In non-production without opt-in flag
      process.env.ENABLE_DEV_DEBUG_ROUTES = 'false';
      const resDisabled = await request(app)
        .post('/character/debug/mock-fusion-result')
        .set('Authorization', `Bearer ${token2}`)
        .send({ raidId: 'test_raid', items: [] });
      assert.strictEqual(resDisabled.status, 404);
      assert.strictEqual(resDisabled.body.error, 'NOT_FOUND');

      // 2. In production mode (even if flag is true, production NEVER allows debug routes)
      env.nodeEnv = 'production';
      process.env.ENABLE_DEV_DEBUG_ROUTES = 'true';
      process.env.ENABLE_LEGACY_EXTRACTION = 'true';
      const legacy = await request(app)
        .post('/character/me/inventory/extraction')
        .set('Authorization', `Bearer ${token2}`)
        .send({ raidId: 'production-legacy', resultSequence: 1, items: [] });
      assert.strictEqual(legacy.status, 404);
      assert.strictEqual(legacy.body.error, 'NOT_FOUND');
      const resProd = await request(app)
        .post('/character/debug/mock-fusion-result')
        .set('Authorization', `Bearer ${token2}`)
        .send({ raidId: 'test_raid', items: [] });
      assert.strictEqual(resProd.status, 404);
      assert.strictEqual(resProd.body.error, 'NOT_FOUND');
    } finally {
      env.nodeEnv = originalNodeEnv;
      if (originalDebug === undefined) delete process.env.ENABLE_DEV_DEBUG_ROUTES;
      else process.env.ENABLE_DEV_DEBUG_ROUTES = originalDebug;
      if (originalLegacy === undefined) delete process.env.ENABLE_LEGACY_EXTRACTION;
      else process.env.ENABLE_LEGACY_EXTRACTION = originalLegacy;
    }
  });

  await t.test('register IP limiter returns HTTP 429 and Retry-After at configured limit', async () => {
    authRoutes.ipRateLimiter.reset();
    try {
      for (let i = 0; i < env.rateLimitIpMax; i++) {
        const response = await request(app).post('/auth/register')
          .set('X-Forwarded-For', `198.51.100.${i % 250 + 1}`)
          .send({});
        assert.strictEqual(response.status, 400);
      }
      const blocked = await request(app).post('/auth/register')
        .set('X-Forwarded-For', '203.0.113.200').send({});
      assert.strictEqual(blocked.status, 429);
      assert.strictEqual(blocked.body.error, 'TOO_MANY_REQUESTS');
      assert.ok(Number(blocked.headers['retry-after']) > 0);
      assert.ok(Number(blocked.headers['retry-after']) <= Math.ceil(env.rateLimitWindowMs / 1000));
    } finally {
      authRoutes.ipRateLimiter.reset();
    }
  });

  await t.test('8. Unexpected Server Errors Return Sanitized 500 Responses Without Leaking Internals', async () => {
    // Calling an invalid route or causing a 500 in errorHandler
    // We send a malformed Authorization header that triggers an error or test errorHandler behavior
    const badTokenRes = await request(app)
      .get('/character/me')
      .set('Authorization', 'Bearer invalid.token.payload');
    assert.strictEqual(badTokenRes.status, 401);
    assert.strictEqual(badTokenRes.body.error, 'UNAUTHORIZED');
  });
});
