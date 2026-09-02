// src/routes/progression.routes.js
const express = require('express');
const router = express.Router();
const ProgressionService = require('../services/ProgressionService');
const authenticate = require('../middleware/authenticate');
const { commitProgressionValidator } = require('../validators/progression.validators');

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
// Persists the character's progression after an expedition.
// Body: { raidId, resultSequence, consolidatedExperience, resultingLevel, newLevel, newExperience, characterAttributes }
router.post('/me/progression/commit', commitProgressionValidator, async (req, res, next) => {
  try {
    const result = await ProgressionService.commitProgression(req.accountId, req.body);
    res.json(result);
  } catch (err) {
    next(err);
  }
});

module.exports = router;
