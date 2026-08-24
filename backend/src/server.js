// src/server.js
// Entry point. Validates environment variables, connects to MongoDB,
// and starts Express only if the database connection succeeds (fail-fast).
const config = require('./config/env');
const { connectToDatabase } = require('./db/connection');
const app = require('./app');

async function start() {
  try {
    await connectToDatabase(config.mongodbUri);
    app.listen(config.port, () => {
      console.log('[Server] Running on port ' + config.port + '.');
    });
  } catch (err) {
    console.error('[Server] Failed to connect to MongoDB. Server will not start.', err.message);
    process.exit(1);
  }
}

start();
