const { test } = require('node:test');
const assert = require('node:assert/strict');
const { startMemoryMongo } = require('./support/memoryMongo');

// Parallel test processes can pick the same free port; mongod then refuses to bind it.
const portInUse = () => Object.assign(new Error('Port "44917" already in use'), { name: 'StdoutInstanceError' });

test('retries when the chosen port is taken by another test process', async () => {
  let calls = 0;
  const server = { getUri: () => 'mongodb://127.0.0.1:1/' };
  const create = async () => {
    calls++;
    if (calls < 3) throw portInUse();
    return server;
  };

  assert.equal(await startMemoryMongo({ create }), server);
  assert.equal(calls, 3);
});

test('does not retry unrelated startup failures', async () => {
  let calls = 0;
  const create = async () => {
    calls++;
    throw new Error('Unable to download mongod binary');
  };

  await assert.rejects(startMemoryMongo({ create }), /Unable to download mongod binary/);
  assert.equal(calls, 1);
});

test('gives up after the retry budget and reports the port error', async () => {
  let calls = 0;
  const create = async () => {
    calls++;
    throw portInUse();
  };

  await assert.rejects(startMemoryMongo({ create, attempts: 4 }), /already in use/);
  assert.equal(calls, 4);
});
