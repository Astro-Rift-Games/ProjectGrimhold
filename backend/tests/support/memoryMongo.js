'use strict';

const { MongoMemoryServer } = require('mongodb-memory-server');

// node --test runs test files in parallel processes. Each picks a free port and then starts mongod,
// so two processes can choose the same port before either binds it. Only that race is retried.
const PORT_IN_USE = /already in use/i;

async function startMemoryMongo({ create = () => MongoMemoryServer.create(), attempts = 5 } = {}) {
  let lastError;
  for (let attempt = 1; attempt <= attempts; attempt++) {
    try {
      return await create();
    } catch (error) {
      if (!PORT_IN_USE.test(String(error && error.message))) throw error;
      lastError = error;
    }
  }
  throw lastError;
}

module.exports = { startMemoryMongo };
