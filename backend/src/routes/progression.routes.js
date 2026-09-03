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



module.exports = router;
