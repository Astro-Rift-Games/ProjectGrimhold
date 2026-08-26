// src/db/connection.js
const mongoose = require('mongoose');

/// Establishes the Mongoose connection to MongoDB.
/// Throws if the connection fails; caller is responsible for handling the error
/// and terminating the process (fail-fast strategy).
async function connectToDatabase(uri) {
  await mongoose.connect(uri, {
    // Limit how long Mongoose waits for a server to be available.
    // Keeps fail-fast behaviour responsive without relying on the OS TCP timeout.
    serverSelectionTimeoutMS: 5000,
  });
  console.log('[DB] MongoDB connected.');
}

module.exports = { connectToDatabase };
