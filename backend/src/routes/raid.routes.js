// src/routes/raid.routes.js
const express = require('express');
const router = express.Router();
const RaidResultService = require('../services/RaidResultService');
const authenticate = require('../middleware/authenticate');

// All raid routes require a valid JWT token.
router.use(authenticate);

// POST /character/raid/extraction-result
// Published by the Host via HMAC signature to create an AuthoritativeExtractionResult.
router.post('/extraction-result', async (req, res, next) => {
  try {
    const result = await RaidResultService.publishHostResult(req.accountId, req.body);
    res.json(result);
  } catch (err) {
    next(err);
  }
});

module.exports = router;
