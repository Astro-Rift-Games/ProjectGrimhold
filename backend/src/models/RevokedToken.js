// src/models/RevokedToken.js
const mongoose = require('mongoose');

const RevokedTokenSchema = new mongoose.Schema({
  tokenHash: {
    type: String,
    required: true,
    unique: true,
    index: true
  },
  jti: {
    type: String,
    index: true,
    sparse: true
  },
  accountId: {
    type: mongoose.Schema.Types.ObjectId,
    ref: 'Account',
    required: true,
    index: true
  },
  expiresAt: {
    type: Date,
    required: true
  },
  revokedAt: {
    type: Date,
    default: Date.now
  },
  reason: {
    type: String,
    default: 'logout'
  }
});

// TTL index: MongoDB will automatically remove documents when expiresAt is reached.
RevokedTokenSchema.index({ expiresAt: 1 }, { expireAfterSeconds: 0 });

module.exports = mongoose.model('RevokedToken', RevokedTokenSchema);
