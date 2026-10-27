// src/config/abilityCatalog.js
'use strict';

/**
 * Server-side Ability Vendor Catalog.
 * Mirrors Project Grimhold/Assets/Scriptable Objects/Abilities/Catalogs/AbilityVendorCatalog.asset
 * Guarded by tests/ability.catalog.parity.test.js.
 */
const ABILITY_CATALOG = Object.freeze({
  arcane_projectile: Object.freeze({ price: 100 }),
  charge:            Object.freeze({ price: 100 }),
  empower:           Object.freeze({ price: 100 }),
  life_drain:        Object.freeze({ price: 100 }),
  protective_orbs:   Object.freeze({ price: 100 }),
  purification:      Object.freeze({ price: 100 }),
  restoration:       Object.freeze({ price: 100 }),
  seismic_strike:    Object.freeze({ price: 100 }),
  trap:              Object.freeze({ price: 100 }),
});

function getAbilityOffer(abilityId) {
  if (!abilityId || typeof abilityId !== 'string') return null;
  return ABILITY_CATALOG[abilityId.trim().toLowerCase()] || null;
}

module.exports = {
  ABILITY_CATALOG,
  getAbilityOffer,
};
