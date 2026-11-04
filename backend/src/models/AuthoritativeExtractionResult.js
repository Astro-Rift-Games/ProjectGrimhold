const mongoose = require('mongoose');
const { EQUIPMENT_SLOTS } = require('../config/equipmentSlots');

const itemSchema = new mongoose.Schema({
  lootId: { type: String, required: true },
  amount: { type: Number, required: true, min: 1 },
  modifiers: { type: mongoose.Schema.Types.Mixed }
}, { _id: false });

// Legacy strings remain readable until the explicit result migration. V3 is ID-free.
const preparedEquipmentSchema = new mongoose.Schema(Object.fromEntries(EQUIPMENT_SLOTS.map(slot =>
  [slot, { type: mongoose.Schema.Types.Mixed, default: null }])), { _id: false });

const authoritativeExtractionResultSchema = new mongoose.Schema({
  resultId: {
    type: String,
    required: true,
    default: function() {
      return this._id ? this._id.toString() : new mongoose.Types.ObjectId().toString();
    },
    index: true
  },
  raidId: { type: String, required: true, index: true },
  accountId: { type: String, required: true, index: true },
  items: [itemSchema],
  preparedEquipment: { type: preparedEquipmentSchema },
  experienceGranted: { type: Number, required: true, default: 0 },
  hostSignature: { type: String, default: '' },
  consumed: { type: Boolean, default: false, index: true },
  consumedAt: { type: Date, default: null },
  issuedAt: { type: Date, default: Date.now },
  createdAt: { type: Date, default: Date.now, expires: 86400 } // TTL index: documents expire after 24 hours
});

module.exports = mongoose.model('AuthoritativeExtractionResult', authoritativeExtractionResultSchema);
