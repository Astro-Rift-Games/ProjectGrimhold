// tests/rate.limiter.test.js
const { test, describe, beforeEach } = require('node:test');
const assert = require('node:assert/strict');
const { MemoryRateLimiter, UserFailedLoginLimiter } = require('../src/middleware/rateLimiter');

describe('Rate Limiter Middleware (US-60 / SEC-60.2)', () => {
  let currentTime;
  const fakeClock = () => currentTime;

  beforeEach(() => {
    currentTime = 1000000;
  });

  test('MemoryRateLimiter allows requests up to max and rejects subsequent with 429 and Retry-After', () => {
    const limiter = new MemoryRateLimiter({
      windowMs: 60000, // 1 minute
      max: 3,
      nowProvider: fakeClock
    });

    const mw = limiter.middleware();
    const createMocks = (ip = '127.0.0.1') => {
      const headers = {};
      const res = {
        statusCode: 200,
        headers,
        setHeader(k, v) { headers[k] = v; },
        status(code) { this.statusCode = code; return this; },
        json(body) { this.body = body; }
      };
      const req = { ip };
      return { req, res };
    };

    // 1st, 2nd, 3rd requests succeed
    for (let i = 0; i < 3; i++) {
      const { req, res } = createMocks();
      let nextCalled = false;
      mw(req, res, () => { nextCalled = true; });
      assert.equal(nextCalled, true, `Request ${i + 1} should be allowed`);
    }

    // 4th request must be rejected with 429
    const { req: req4, res: res4 } = createMocks();
    let next4Called = false;
    mw(req4, res4, () => { next4Called = true; });

    assert.equal(next4Called, false, '4th request must be blocked');
    assert.equal(res4.statusCode, 429);
    assert.equal(res4.headers['Retry-After'], 60);
    assert.equal(res4.body.error, 'TOO_MANY_REQUESTS');
    assert.equal(res4.body.retryAfter, 60);

    // Another IP must be unaffected (Isolation)
    const { req: reqOther, res: resOther } = createMocks('192.168.1.100');
    let nextOtherCalled = false;
    mw(reqOther, resOther, () => { nextOtherCalled = true; });
    assert.equal(nextOtherCalled, true, 'Different IP must not be blocked');

    // Advance clock by 61 seconds: original IP is allowed again
    currentTime += 61000;
    const { req: reqAfterExpiry, res: resAfterExpiry } = createMocks();
    let nextExpiryCalled = false;
    mw(reqAfterExpiry, resAfterExpiry, () => { nextExpiryCalled = true; });
    assert.equal(nextExpiryCalled, true, 'Request after window expiration must be allowed');
  });

  test('UserFailedLoginLimiter blocks only after max failures and isolates users', () => {
    const userLimiter = new UserFailedLoginLimiter({
      windowMs: 60000,
      max: 3,
      nowProvider: fakeClock
    });

    const preCheck = userLimiter.preCheck();
    const createMocks = (username) => {
      const headers = {};
      const res = {
        statusCode: 200,
        headers,
        setHeader(k, v) { headers[k] = v; },
        status(code) { this.statusCode = code; return this; },
        json(body) { this.body = body; }
      };
      const req = { body: { username } };
      return { req, res };
    };

    // User "alice" has 2 failures
    userLimiter.recordFailure('alice');
    userLimiter.recordFailure('alice');

    // 3rd attempt precheck passes (only 2 failures so far)
    const { req: req3, res: res3 } = createMocks('alice');
    let next3Called = false;
    preCheck(req3, res3, () => { next3Called = true; });
    assert.equal(next3Called, true, 'Alice should not be blocked after 2 failures');

    // 3rd failure occurs
    userLimiter.recordFailure('alice');

    // 4th attempt precheck must be blocked with 429
    const { req: req4, res: res4 } = createMocks('alice');
    let next4Called = false;
    preCheck(req4, res4, () => { next4Called = true; });
    assert.equal(next4Called, false, 'Alice must be blocked on 4th attempt');
    assert.equal(res4.statusCode, 429);
    assert.equal(res4.headers['Retry-After'], 60);
    assert.equal(res4.body.error, 'TOO_MANY_REQUESTS');

    // User "bob" is unaffected (Isolation)
    const { req: reqBob, res: resBob } = createMocks('bob');
    let nextBobCalled = false;
    preCheck(reqBob, resBob, () => { nextBobCalled = true; });
    assert.equal(nextBobCalled, true, 'Bob should not be blocked by Alice failures');

    // Successful login clears Alice failures
    userLimiter.clearFailures('alice');
    const { req: reqAliceReset, res: resAliceReset } = createMocks('alice');
    let nextResetCalled = false;
    preCheck(reqAliceReset, resAliceReset, () => { nextResetCalled = true; });
    assert.equal(nextResetCalled, true, 'Alice should be unblocked after clearFailures');
  });
});
