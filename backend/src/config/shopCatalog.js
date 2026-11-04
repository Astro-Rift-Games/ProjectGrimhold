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

// Unity LootDefinitions are the source; parity tests check both references and values.
const weapon = naturalAttribute => Object.freeze({ category: 'Weapon', naturalAttribute, legacyCoefficient: 0, sellValue: 0 });
const armor = category => Object.freeze({ category, sellValue: 5 });
const EQUIPMENT_TEMPLATES = Object.freeze({
  arming_sword: weapon('strength'), great_hammer: weapon('strength'), compound_bow: weapon('dexterity'),
  light_crossbow: weapon('dexterity'), long_bow: weapon('dexterity'), long_sword: weapon('strength'),
  magic_cinquedea: weapon('dexterity'), magic_staff: weapon('intelligence'), magic_sword: weapon('strength'),
  magic_wand: weapon('intelligence'), rapier: weapon('strength'), rondel_dagger: weapon('dexterity'),
  spell_book: weapon('intelligence'), zweihander: weapon('strength'), shield: Object.freeze({ category: 'Shield', sellValue: 0 }),
  placeholder_helmet: armor('Helmet'), placeholder_armor: armor('Armor'), placeholder_gloves: armor('Gloves'), placeholder_boots: armor('Boots'),
  army_ranger_armor: armor('Armor'), army_ranger_gloves: armor('Gloves'), army_ranger_hat: armor('Helmet'), army_ranger_trousers: armor('Boots'),
  fire_mage_garb: armor('Armor'), fire_mage_gloves: armor('Gloves'), fire_mage_hood: armor('Helmet'), fire_mage_trousers: armor('Boots'),
  forest_ranger_gloves: armor('Gloves'), forest_ranger_hood: armor('Helmet'), forest_ranger_leather_armor: armor('Armor'), forest_ranger_trousers: armor('Boots'),
  heavy_armor_breastplate: armor('Armor'), heavy_armor_gauntlets: armor('Gloves'), heavy_armor_helmet: armor('Helmet'), heavy_armor_leg_plate: armor('Boots'),
  light_armor_chain_mail_armor: armor('Armor'), light_armor_chain_mail_trousers: armor('Boots'), light_armor_gloves: armor('Gloves'), light_armor_open_sallet: armor('Helmet')
});
const GEM_CATALOG = Object.freeze({
  RES_ATTUNEMENT_GEM: Object.freeze({ sellValue: 70 }),
  RES_RUBY_GEM: Object.freeze({ sellValue: 70 }),
  RES_SAPPHIRE_GEM: Object.freeze({ sellValue: 70 })
});
function getEquipmentTemplate(lootId) {
  return Object.hasOwn(EQUIPMENT_TEMPLATES, normalizeLootId(lootId)) ? EQUIPMENT_TEMPLATES[normalizeLootId(lootId)] : null;
}
function isBuybackEligible(lootId, version = 3) {
  // Older characters retain their existing ledger behavior until explicitly migrated.
  return version < 3 && Boolean(getEquipmentTemplate(lootId));
}

/**
 * Authoritative catalog of items offered for purchase by the Town Merchant.
 * Unity LootDefinition assets are the source of truth: mirror positive buy values only.
 * Backend-only ids use GD-14 values and have no Unity asset yet.
 * Prices are in gold (currency) units.
 * Legitimate free items are explicitly marked with `isFree: true`.
 */
const BUY_CATALOG = Object.freeze({
  healthpotion: { buyPrice: 40, isFree: false },
  CON_EXPLOSIVE_BOMB: { buyPrice: 60, isFree: false },
  CON_HEALTH_POTION: { buyPrice: 40, isFree: false },
  CON_LOCKPICK: { buyPrice: 50, isFree: false },
  CON_MANA_FRAGMENT: { buyPrice: 16, isFree: false },
  CON_MANA_POTION: { buyPrice: 40, isFree: false },
  CON_SELF_REVIVE: { buyPrice: 600, isFree: false },
  CON_SMOKE_BOMB: { buyPrice: 90, isFree: false },
  CON_THROWING_DAGGER: { buyPrice: 24, isFree: false },
  RES_ANCIENT_ARMOR: { buyPrice: 60, isFree: false },
  RES_ANCIENT_ROOT: { buyPrice: 24, isFree: false },
  RES_ATTUNEMENT_GEM: { buyPrice: 140, isFree: false },
  RES_BAT_FANG: { buyPrice: 44, isFree: false },
  RES_BAT_WING: { buyPrice: 24, isFree: false },
  RES_BLACK_IRON: { buyPrice: 30, isFree: false },
  RES_CORRUPT_MARROW: { buyPrice: 56, isFree: false },
  RES_CUSTODIAN_SEAL: { buyPrice: 90, isFree: false },
  RES_ETHER_DUST: { buyPrice: 36, isFree: false },
  RES_GOBLIN_ARROWHEAD: { buyPrice: 28, isFree: false },
  RES_GOBLIN_INSIGNIA: { buyPrice: 64, isFree: false },
  RES_GOBLIN_SCRAP: { buyPrice: 20, isFree: false },
  RES_GOLEM_CORE: { buyPrice: 140, isFree: false },
  RES_LOST_GRIMOIRE_PAGE: { buyPrice: 70, isFree: false },
  RES_RUBY_GEM: { buyPrice: 140, isFree: false },
  RES_RUNE_FRAGMENT: { buyPrice: 40, isFree: false },
  RES_SAPPHIRE_GEM: { buyPrice: 140, isFree: false },
  RES_SLIME_CORE: { buyPrice: 48, isFree: false },
  RES_SLIME_GEL: { buyPrice: 16, isFree: false },
  RES_SOUL_FRAGMENT: { buyPrice: 130, isFree: false },
  RES_SPECTRAL_ESSENCE: { buyPrice: 76, isFree: false },
  RES_TANNED_LEATHER: { buyPrice: 20, isFree: false },
  RES_WITHERED_BONE: { buyPrice: 20, isFree: false },
  TRE_CHALICE: { buyPrice: 400, isFree: false },
  TRE_CHAOS_COIN: { buyPrice: 800, isFree: false },
  TRE_CROWN: { buyPrice: 700, isFree: false },
  TRE_FAMINE_IDOL: { buyPrice: 800, isFree: false },
  TRE_PILGRIM_LIGHT: { buyPrice: 600, isFree: false },
  TRE_SANCTUARY_HEART: { buyPrice: 700, isFree: false },
  TRE_VOID_BELL: { buyPrice: 900, isFree: false },
  TRE_WANDERER_EYE: { buyPrice: 900, isFree: false },
  UTL_ARCANE_DETECTOR: { buyPrice: 360, isFree: false },
  UTL_RUNE_COMPASS: { buyPrice: 280, isFree: false },
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
  healthpotion: { sellValue: 20 },
  CON_EXPLOSIVE_BOMB: { sellValue: 30 },
  CON_HEALTH_POTION: { sellValue: 20 },
  CON_LOCKPICK: { sellValue: 25 },
  CON_MANA_FRAGMENT: { sellValue: 8 },
  CON_MANA_POTION: { sellValue: 20 },
  CON_SELF_REVIVE: { sellValue: 300 },
  CON_SMOKE_BOMB: { sellValue: 45 },
  CON_THROWING_DAGGER: { sellValue: 12 },
  RES_ANCIENT_ARMOR: { sellValue: 30 },
  RES_ANCIENT_ROOT: { sellValue: 12 },
  ...GEM_CATALOG,
  RES_BAT_FANG: { sellValue: 22 },
  RES_BAT_WING: { sellValue: 12 },
  RES_BLACK_IRON: { sellValue: 15 },
  RES_CORRUPT_MARROW: { sellValue: 28 },
  RES_CUSTODIAN_SEAL: { sellValue: 45 },
  RES_ETHER_DUST: { sellValue: 18 },
  RES_GOBLIN_ARROWHEAD: { sellValue: 14 },
  RES_GOBLIN_INSIGNIA: { sellValue: 32 },
  RES_GOBLIN_SCRAP: { sellValue: 10 },
  RES_GOLEM_CORE: { sellValue: 70 },
  RES_LOST_GRIMOIRE_PAGE: { sellValue: 35 },
  RES_RUNE_FRAGMENT: { sellValue: 20 },
  RES_SLIME_CORE: { sellValue: 24 },
  RES_SLIME_GEL: { sellValue: 8 },
  RES_SOUL_FRAGMENT: { sellValue: 65 },
  RES_SPECTRAL_ESSENCE: { sellValue: 38 },
  RES_TANNED_LEATHER: { sellValue: 10 },
  RES_WITHERED_BONE: { sellValue: 10 },
  TRE_CHALICE: { sellValue: 200 },
  TRE_CHAOS_COIN: { sellValue: 400 },
  TRE_CROWN: { sellValue: 350 },
  TRE_FAMINE_IDOL: { sellValue: 400 },
  TRE_PILGRIM_LIGHT: { sellValue: 300 },
  TRE_SANCTUARY_HEART: { sellValue: 350 },
  TRE_VOID_BELL: { sellValue: 450 },
  TRE_WANDERER_EYE: { sellValue: 450 },
  UTL_ARCANE_DETECTOR: { sellValue: 180 },
  UTL_RUNE_COMPASS: { sellValue: 140 },
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
  EQUIPMENT_TEMPLATES,
  GEM_CATALOG,
  getEquipmentTemplate,
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
