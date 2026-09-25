'use strict';
const Character = require('../models/Character');

class CurrencyService {
  /**
   * Returns the current confirmed currency balance for the account's character.
   * Used to hydrate the local Unity state after login.
   * 
   * NOTE: As per MVP design, there is no award/deduct path for currency outside
   * the Shop transactions. Thus, this service only exposes queries.
   */
  static async getBalance(accountId) {
    const character = await Character.findOne({ accountId });
    if (!character) {
      throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found for this account.' };
    }
    return character.inventory?.currency || 0;
  }
}

module.exports = CurrencyService;
