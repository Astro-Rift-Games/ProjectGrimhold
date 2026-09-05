// src/app.js
const express = require('express');
const mongoose = require('mongoose');
const authRoutes = require('./routes/auth.routes');
const characterRoutes = require('./routes/character.routes');
const inventoryRoutes = require('./routes/inventory.routes');
const progressionRoutes = require('./routes/progression.routes');
const errorHandler = require('./middleware/errorHandler');

const app = express();

// Middleware to parse JSON bodies
app.use(express.json());

// Global request logging for debugging
app.use((req, res, next) => {
  console.log(`[Express] Received ${req.method} request to: ${req.originalUrl}`);
  next();
});

// Routes
app.use('/auth', authRoutes);
app.use('/character', characterRoutes);
app.use('/character', inventoryRoutes);
app.use('/character', progressionRoutes);

// Health check endpoint.
app.get('/health', (_req, res) => {
  const mongoState = mongoose.connection.readyState;
  // 1 = connected
  if (mongoState === 1) {
    res.json({ status: 'ok', mongo: 'connected' });
  } else {
    res.status(503).json({ status: 'degraded', mongo: 'disconnected', mongoState });
  }
});

// Centralized error handling middleware. Must be registered last.
app.use(errorHandler);

module.exports = app;
