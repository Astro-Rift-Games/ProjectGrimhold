// tests/env.hardening.test.js
const { test, describe } = require('node:test');
const assert = require('node:assert/strict');
const errorHandler = require('../src/middleware/errorHandler');

describe('Environment Hardening and Error Sanitization (US-60 / SEC-60.3)', () => {
  test('errorHandler sanitizes 500 errors and strips database messages, details and stack traces', () => {
    const rawDbError = new Error('MongoServerError: authentication failed for user "admin" on db "production"');
    rawDbError.statusCode = 500;
    rawDbError.details = { rawQuery: 'db.accounts.find({ secret: "xyz" })' };

    const headers = {};
    const res = {
      statusCode: 200,
      headers,
      status(code) { this.statusCode = code; return this; },
      json(body) { this.body = body; return this; }
    };

    errorHandler(rawDbError, {}, res, () => {});

    assert.equal(res.statusCode, 500);
    assert.equal(res.body.error, 'INTERNAL_SERVER_ERROR');
    assert.equal(res.body.message, 'An unexpected error occurred.');
    assert.equal(res.body.details, undefined, 'Details must not be exposed in 500 errors');
    assert.equal(JSON.stringify(res.body).includes('MongoServerError'), false, 'DB error details must not leak');
  });

  test('errorHandler treats unhandled exceptions without statusCode as 500 and sanitizes them', () => {
    const unhandledException = new TypeError('Cannot read properties of undefined (reading "_id")');

    const res = {
      status(code) { this.statusCode = code; return this; },
      json(body) { this.body = body; return this; }
    };

    errorHandler(unhandledException, {}, res, () => {});

    assert.equal(res.statusCode, 500);
    assert.equal(res.body.error, 'INTERNAL_SERVER_ERROR');
    assert.equal(res.body.message, 'An unexpected error occurred.');
    assert.equal(JSON.stringify(res.body).includes('Cannot read properties'), false);
  });

  test('errorHandler preserves domain errors with 4xx status codes and structured details', () => {
    const domainError = {
      statusCode: 422,
      errorCode: 'UNPROCESSABLE_ENTITY',
      message: 'Insufficient attribute points',
      details: [{ field: 'strength', reason: 'exceeds cap' }]
    };

    const res = {
      status(code) { this.statusCode = code; return this; },
      json(body) { this.body = body; return this; }
    };

    errorHandler(domainError, {}, res, () => {});

    assert.equal(res.statusCode, 422);
    assert.equal(res.body.error, 'UNPROCESSABLE_ENTITY');
    assert.equal(res.body.message, 'Insufficient attribute points');
    assert.deepEqual(res.body.details, [{ field: 'strength', reason: 'exceeds cap' }]);
  });

  test('env.js rejects short or weak WEBHOOK_SECRET in production startup', () => {
    const { spawnSync } = require('node:child_process');
    const path = require('node:path');
    const envPath = path.resolve(__dirname, '../src/config/env.js');

    const result = spawnSync(
      process.execPath,
      ['-e', `require(${JSON.stringify(envPath)})`],
      {
        env: {
          ...process.env,
          NODE_ENV: 'production',
          PORT: '3000',
          MONGODB_URI: 'mongodb://localhost:27017/grimhold',
          JWT_SECRET: 'a-very-long-and-high-entropy-production-jwt-secret-2026',
          WEBHOOK_SECRET: 'short-secret'
        },
        encoding: 'utf8'
      }
    );

    assert.notEqual(result.status, 0, 'Process must exit with non-zero code on weak WEBHOOK_SECRET in production');
    assert.match(result.stderr, /WEBHOOK_SECRET must be set to a secure value/);
  });

  test('env.js rejects synthetic PLACEHOLDER values from .env.example in production startup (US-61 / SEC-61.1)', () => {
    const { spawnSync } = require('node:child_process');
    const path = require('node:path');
    const envPath = path.resolve(__dirname, '../src/config/env.js');

    const jwtPlaceholderResult = spawnSync(
      process.execPath,
      ['-e', `require(${JSON.stringify(envPath)})`],
      {
        env: {
          ...process.env,
          NODE_ENV: 'production',
          PORT: '3000',
          MONGODB_URI: 'mongodb://localhost:27017/grimhold',
          JWT_SECRET: 'PLACEHOLDER_JWT_SECRET_MIN_32_CHARS_LONG',
          WEBHOOK_SECRET: 'a-very-long-and-high-entropy-production-webhook-secret-2026'
        },
        encoding: 'utf8'
      }
    );

    assert.notEqual(jwtPlaceholderResult.status, 0, 'Must reject PLACEHOLDER_JWT_SECRET_MIN_32_CHARS_LONG in production');
    assert.match(jwtPlaceholderResult.stderr, /JWT_SECRET must be at least 32 characters/);

    const mongoPlaceholderResult = spawnSync(
      process.execPath,
      ['-e', `require(${JSON.stringify(envPath)})`],
      {
        env: {
          ...process.env,
          NODE_ENV: 'production',
          PORT: '3000',
          MONGODB_URI: 'mongodb+srv://PLACEHOLDER_USER:PLACEHOLDER_PASSWORD@PLACEHOLDER_CLUSTER.mongodb.net/grimhold_dev',
          JWT_SECRET: 'a-very-long-and-high-entropy-production-jwt-secret-2026',
          WEBHOOK_SECRET: 'a-very-long-and-high-entropy-production-webhook-secret-2026'
        },
        encoding: 'utf8'
      }
    );

    assert.notEqual(mongoPlaceholderResult.status, 0, 'Must reject MONGODB_URI placeholder in production');
    assert.match(mongoPlaceholderResult.stderr, /MONGODB_URI contains an unresolved placeholder/);
  });
});
