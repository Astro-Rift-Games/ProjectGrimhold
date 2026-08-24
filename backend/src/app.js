// src/app.js
const express = require('express');
const authRoutes = require('./routes/auth.routes');
const characterRoutes = require('./routes/character.routes');
const errorHandler = require('./middleware/errorHandler');

const app = express();

// Middleware to parse JSON bodies
app.use(express.json());

// Routes
app.use('/auth', authRoutes);
app.use('/character', characterRoutes);

// Health check endpoint.
app.get('/health', (_req, res) => {
  res.json({ status: 'ok' });
});

// Centralized error handling middleware. Must be registered last.
app.use(errorHandler);

module.exports = app;
