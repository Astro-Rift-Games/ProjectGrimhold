'use strict';

const { test, describe } = require('node:test');
const assert = require('node:assert/strict');
const validate = require('../src/middleware/validate');

describe('Validate Middleware', () => {
  test('passes to next() when there are no validation errors', () => {
    let nextArg = 'not-called';
    const req = {};
    const res = {};
    validate(req, res, (err) => {
      nextArg = err;
    });

    assert.equal(nextArg, undefined);
  });
});
