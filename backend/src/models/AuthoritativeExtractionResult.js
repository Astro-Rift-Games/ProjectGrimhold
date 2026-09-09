const mongoose = require('mongoose');

const itemSchema = new mongoose.Schema({
  lootId: { type: String, required: true },
  amount: { type: Number, required: true, min: 1 }
}, { _id: false });

const authoritativeExtractionResultSchema = new mongoose.Schema({
  raidId: { type: String, required: true, index: true },
  accountId: { type: String, required: true, index: true },
  items: [itemSchema],
  experienceGranted: { type: Number, required: true, default: 0 },
  createdAt: { type: Date, default: Date.now, expires: 86400 } // TTL index: documents expire after 24 hours
});

module.exports = mongoose.model('AuthoritativeExtractionResult', authoritativeExtractionResultSchema);
