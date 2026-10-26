'use strict';

const { normalizeLootId } = require('../services/InventoryLootIdNormalizer');

/**
 * Maximum quantity allowed in a single shop purchase or sale transaction.
 * Prevents numerical overflow and excessive item creation.
 */
const MAX_TRANSACTION_AMOUNT = 999;
const BUYBACK_PRICE_MULTIPLIER = 1.5;
const BUYBACK_MAX_ENTRIES = 16;

// Classification mirrors LootCategory 5..10, including currently unsellable equipment.
const BUYBACK_ELIGIBLE_IDS = Object.freeze([
  'placeholder_helmet', 'placeholder_armor', 'placeholder_gloves', 'placeholder_boots',
  'army_ranger_armor', 'army_ranger_gloves', 'army_ranger_hat', 'army_ranger_trousers',
  'fire_mage_garb', 'fire_mage_gloves', 'fire_mage_hood', 'fire_mage_trousers',
  'forest_ranger_gloves', 'forest_ranger_hood', 'forest_ranger_leather_armor', 'forest_ranger_trousers',
  'heavy_armor_breastplate', 'heavy_armor_gauntlets', 'heavy_armor_helmet', 'heavy_armor_leg_plate',
  'light_armor_chain_mail_armor', 'light_armor_chain_mail_trousers', 'light_armor_gloves', 'light_armor_open_sallet',
  'arming_sword', 'great_hammer', 'compound_bow', 'light_crossbow', 'long_bow', 'long_sword',
  'magic_cinquedea', 'magic_staff', 'magic_sword', 'magic_wand', 'rapier', 'rondel_dagger',
  'shield', 'spell_book', 'zweihander'
]);

function isBuybackEligible(lootId) {
  // Keep future rules (upgrades or market availability) behind this single boundary.
  if (!lootId || typeof lootId !== 'string') return false;
  return BUYBACK_ELIGIBLE_IDS.includes(lootId) || BUYBACK_ELIGIBLE_IDS.includes(normalizeLootId(lootId));
}

/**
 * Authoritative catalog of items offered for purchase by the Town Merchant.
 * Unity LootDefinition assets are the source of truth: mirror positive buy values only.
 * Backend-only ids use GD-14 values and have no Unity asset yet.
 * Prices are in gold (currency) units.
 * Legitimate free items are explicitly marked with `isFree: true`.
 */
const BUY_CATALOG = Object.freeze({
  healthpotion: { buyPrice: 20, isFree: false },
  bone: { buyPrice: 5, isFree: false },
  placeholder_helmet: { buyPrice: 10, isFree: false },
  placeholder_armor: { buyPrice: 10, isFree: false },
  placeholder_gloves: { buyPrice: 10, isFree: false },
  placeholder_boots: { buyPrice: 10, isFree: false }
});

/**
 * Authoritative catalog of items the Town Merchant is willing to purchase from players.
 * Values are in gold (currency) units per single unit.
 * Unity LootDefinition assets are the source of truth: mirror positive sell values only.
 * Backend-only ids use GD-14 values and have no Unity asset yet.
 */
const SELL_CATALOG = Object.freeze({
  healthpotion: { sellValue: 10 },
  bone: { sellValue: 5 },
  placeholder_helmet: { sellValue: 5 },
  placeholder_armor: { sellValue: 5 },
  placeholder_gloves: { sellValue: 5 },
  placeholder_boots: { sellValue: 5 },
  army_ranger_armor: { sellValue: 5 },
  army_ranger_gloves: { sellValue: 5 },
  army_ranger_hat: { sellValue: 5 },
  army_ranger_trousers: { sellValue: 5 },
  fire_mage_garb: { sellValue: 5 },
  fire_mage_gloves: { sellValue: 5 },
  fire_mage_hood: { sellValue: 5 },
  fire_mage_trousers: { sellValue: 5 },
  forest_ranger_gloves: { sellValue: 5 },
  forest_ranger_hood: { sellValue: 5 },
  forest_ranger_leather_armor: { sellValue: 5 },
  forest_ranger_trousers: { sellValue: 5 },
  heavy_armor_breastplate: { sellValue: 5 },
  heavy_armor_gauntlets: { sellValue: 5 },
  heavy_armor_helmet: { sellValue: 5 },
  heavy_armor_leg_plate: { sellValue: 5 },
  light_armor_chain_mail_armor: { sellValue: 5 },
  light_armor_chain_mail_trousers: { sellValue: 5 },
  light_armor_gloves: { sellValue: 5 },
  light_armor_open_sallet: { sellValue: 5 },
  coins: { sellValue: 1 },
  gold_coin: { sellValue: 1 },
  // GD-14 master list resources
  slime_gel: { sellValue: 8 },
  slime_core: { sellValue: 24 },
  withered_bone: { sellValue: 10 },
  corrupt_marrow: { sellValue: 28 },
  ancient_armor_fragment: { sellValue: 30 },
  goblin_scrap: { sellValue: 10 },
  goblin_insignia: { sellValue: 32 },
  goblin_arrowhead: { sellValue: 14 },
  bat_wing: { sellValue: 12 },
  bat_fang: { sellValue: 22 },
  spectral_essence: { sellValue: 38 },
  soul_fragment: { sellValue: 65 },
  golem_core: { sellValue: 70 },
  black_iron_ore: { sellValue: 15 },
  tanned_leather: { sellValue: 10 },
  ancient_root_wood: { sellValue: 12 },
  ether_dust: { sellValue: 18 },
  runic_fragment: { sellValue: 20 },
  tuning_gem: { sellValue: 70 },
  lost_grimoire_page: { sellValue: 35 },
  warden_seal: { sellValue: 45 }
});

/**
 * Resolves a buy offer for the given lootId.
 * Checks direct key match first, then normalized alias.
 * @param {string} lootId
 * @returns {{ buyPrice: number, isFree: boolean } | null}
 */
function getBuyOffer(lootId) {
  if (!lootId || typeof lootId !== 'string') return null;
  if (BUY_CATALOG[lootId]) return BUY_CATALOG[lootId];
  const normalized = normalizeLootId(lootId);
  return BUY_CATALOG[normalized] || null;
}

/**
 * Resolves a sell offer for the given lootId.
 * Checks direct key match first, then normalized alias.
 * @param {string} lootId
 * @returns {{ sellValue: number } | null}
 */
function getSellOffer(lootId) {
  if (!lootId || typeof lootId !== 'string') return null;
  if (SELL_CATALOG[lootId]) return SELL_CATALOG[lootId];
  const normalized = normalizeLootId(lootId);
  return SELL_CATALOG[normalized] || null;
}

/**
 * Checks whether an item is eligible for purchase from the shop.
 * @param {string} lootId
 * @returns {boolean}
 */
function isItemBuyable(lootId) {
  return getBuyOffer(lootId) !== null;
}

/**
 * Checks whether an item can be sold to the shop.
 * @param {string} lootId
 * @returns {boolean}
 */
function isItemSellable(lootId) {
  return getSellOffer(lootId) !== null;
}

module.exports = {
  BUYBACK_PRICE_MULTIPLIER,
  BUYBACK_MAX_ENTRIES,
  BUYBACK_ELIGIBLE_IDS,
  isBuybackEligible,
  MAX_TRANSACTION_AMOUNT,
  BUY_CATALOG,
  SELL_CATALOG,
  getBuyOffer,
  getSellOffer,
  isItemBuyable,
  isItemSellable
};
