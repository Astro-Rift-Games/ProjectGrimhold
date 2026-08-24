// src/app.js
const express = require('express');

const app = express();

app.use(express.json());

// Health check endpoint.
// Verifies that the Express server is running and accepting requests.
// Does not exercise any business logic or database connectivity.
app.get('/health', (_req, res) => {
  res.json({ status: 'ok' });
});

module.exports = app;
