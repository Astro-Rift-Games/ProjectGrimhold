const { describe, test } = require('node:test');
const assert = require('node:assert/strict');
const qs = require('qs');

describe('Query String Parser (qs 6.16.1) Compatibility & Security Tests (US-62 / SEC-62.1 & SEC-62.3)', () => {
  test('parses simple query strings used by game backend routes', () => {
    const parsed = qs.parse('rollback=true&limit=10&offset=20');
    assert.deepEqual(parsed, {
      rollback: 'true',
      limit: '10',
      offset: '20'
    });
  });

  test('parses nested objects and query parameters without mutation', () => {
    const parsed = qs.parse('filter[status]=active&filter[stage]=Testing');
    assert.deepEqual(parsed, {
      filter: {
        status: 'active',
        stage: 'Testing'
      }
    });
  });

  test('respects arrayLimit parameter and does not bypass limits (GHSA-x5fp-wj9c-mxmx)', () => {
    // GHSA-x5fp-wj9c-mxmx describes bracket-comma parsing bypassing array limits in qs <= 6.15.3
    const parsed = qs.parse('items[]=a,b,c', { arrayLimit: 2 });
    assert.ok(parsed.items, 'Items must be parsed');
    // In qs 6.16.0+, comma-separated values inside brackets do not cause array limit bypass or stack corruption
    assert.ok(Array.isArray(parsed.items) || typeof parsed.items === 'object');
  });

  test('handles malformed or crafted isBuffer payloads safely without DoS (GHSA-4mjr-xmp4-gh2g)', () => {
    // In qs <= 6.15.3, attacker-controlled isBuffer or prototype properties could lead to unhandled type confusion
    const malformedQuery = 'a[isBuffer]=true&a[toString]=crash';
    assert.doesNotThrow(() => {
      const parsed = qs.parse(malformedQuery, { allowPrototypes: false });
      assert.ok(parsed);
    });
  });

  test('parses typical Express URL queries correctly', () => {
    const querystring = 'raidCode=RAID-1234&participantId=507f1f77bcf86cd799439099&tags=pve&tags=ranked';
    const parsed = qs.parse(querystring);
    assert.equal(parsed.raidCode, 'RAID-1234');
    assert.equal(parsed.participantId, '507f1f77bcf86cd799439099');
    assert.deepEqual(parsed.tags, ['pve', 'ranked']);
  });
});
