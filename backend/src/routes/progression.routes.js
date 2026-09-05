// src/routes/progression.routes.js
const express = require('express');
const router = express.Router();
const ProgressionService = require('../services/ProgressionService');
const authenticate = require('../middleware/authenticate');

// All progression routes require a valid JWT token.
router.use(authenticate);

// GET /character/me/progression
// Returns the character's progression snapshot (level, experience, attributes, etc).
router.get('/me/progression', async (req, res, next) => {
  try {
    const progression = await ProgressionService.getProgression(req.accountId);
    res.json(progression);
  } catch (err) {
    next(err);
  }
});

// POST /character/me/progression/commit
router.post('/me/progression/commit', async (req, res, next) => {
  console.log(`[Progression Route] Hit POST /me/progression/commit for accountId: ${req.accountId}`);
  try {
    console.log('[Progression Route] commitProgression payload:', req.body);
    const result = await ProgressionService.commitProgression(req.accountId, req.body);
    console.log('[Progression Route] commitProgression result:', result);
    res.json(result);
  } catch (err) {
    console.error('[Progression Route] Error in commitProgression:', err);
    next(err);
  }
});

module.exports = router;
