'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { ABILITY_CATALOG } = require('../src/config/abilityCatalog');

const definitionsDir = path.resolve(__dirname, '../../Project Grimhold/Assets/Scriptable Objects/Abilities/Definitions');
const catalogAssetPath = path.resolve(__dirname, '../../Project Grimhold/Assets/Scriptable Objects/Abilities/Catalogs/AbilityVendorCatalog.asset');

test('ability catalog mirrors every Unity AbilityVendorCatalog offer and price', t => {
  if (!fs.existsSync(definitionsDir) || !fs.existsSync(catalogAssetPath)) {
    t.skip('Unity ability definitions or vendor catalog asset absent; skipping parity test.');
    return;
  }

  // 1. Build map of GUID -> abilityId from definitions
  const guidToAbilityId = new Map();
  const defFiles = fs.readdirSync(definitionsDir).filter(f => f.endsWith('.asset'));
  for (const defFile of defFiles) {
    const assetPath = path.join(definitionsDir, defFile);
    const metaPath = `${assetPath}.meta`;
    if (!fs.existsSync(metaPath)) continue;

    const metaText = fs.readFileSync(metaPath, 'utf8');
    const guidMatch = metaText.match(/^guid:\s*([a-f0-9]+)/m);
    if (!guidMatch) continue;
    const guid = guidMatch[1];

    const assetText = fs.readFileSync(assetPath, 'utf8');
    const idMatch = assetText.match(/^\s*_id:\s*(\S+)/m);
    if (!idMatch) continue;
    const abilityId = idMatch[1].trim().toLowerCase();

    guidToAbilityId.set(guid, abilityId);
  }

  assert.ok(guidToAbilityId.size > 0, 'Expected at least one ability definition with valid GUID');

  // 2. Parse offers from AbilityVendorCatalog.asset
  const catalogText = fs.readFileSync(catalogAssetPath, 'utf8');
  const offerRegex = /-\s*_ability:\s*\{fileID:[^}]+guid:\s*([a-f0-9]+)[^}]*\}\s*\n\s*_price:\s*(\d+)/g;

  const unityOffers = new Map();
  let match;
  while ((match = offerRegex.exec(catalogText)) !== null) {
    const guid = match[1];
    const price = Number(match[2]);
    const abilityId = guidToAbilityId.get(guid);
    assert.ok(abilityId, `Unknown ability GUID in vendor catalog: ${guid}`);
    unityOffers.set(abilityId, price);
  }

  assert.ok(unityOffers.size > 0, 'Expected at least one offer in AbilityVendorCatalog.asset');

  // 3. Parity checks
  const mismatches = [];

  // Every Unity offer must exist in backend ABILITY_CATALOG with matching price
  for (const [abilityId, expectedPrice] of unityOffers.entries()) {
    const offer = ABILITY_CATALOG[abilityId];
    if (!offer) {
      mismatches.push(`${abilityId}: missing from backend ABILITY_CATALOG (expected price ${expectedPrice})`);
    } else if (offer.price !== expectedPrice) {
      mismatches.push(`${abilityId}: price mismatch (backend: ${offer.price}, unity: ${expectedPrice})`);
    }
  }

  // Every backend ABILITY_CATALOG entry must exist in Unity catalog
  for (const [abilityId, offer] of Object.entries(ABILITY_CATALOG)) {
    if (!unityOffers.has(abilityId)) {
      mismatches.push(`${abilityId}: present in backend ABILITY_CATALOG with price ${offer.price}, but not offered in Unity asset`);
    }
  }

  assert.equal(mismatches.length, 0, `Ability catalog parity mismatches:\n${mismatches.join('\n')}`);
});
