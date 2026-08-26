const mongoose = require('mongoose');

const characterSchema = new mongoose.Schema({
  accountId: {
    type: mongoose.Schema.Types.ObjectId,
    ref: 'Account',
    required: true,
    unique: true // Garantiza: 1 cuenta = 1 personaje
  },
  name: {
    type: String,
    required: true
  },
  profile: {
    lastSeen: { type: Date, default: null },
    customNote: { type: String, maxlength: 256, default: '' }
  },
  level: { type: Number, default: 1 },
  experience: { type: Number, default: 0 },
  createdAt: {
    type: Date,
    default: Date.now
  }
});

module.exports = mongoose.model('Character', characterSchema);
