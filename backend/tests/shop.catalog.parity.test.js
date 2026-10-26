'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { BUY_CATALOG, SELL_CATALOG, BUYBACK_ELIGIBLE_IDS, isBuybackEligible } = require('../src/config/shopCatalog');

const definitions = path.resolve(__dirname, '../../Project Grimhold/Assets/Scriptable Objects/Loot/Definitions');

function assetFiles(directory) {
  return fs.readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
    const file = path.join(directory, entry.name);
    return entry.isDirectory() ? assetFiles(file) : entry.name.endsWith('.asset') ? [file] : [];
  });
}

test('shop catalogs mirror every Unity LootDefinition price and eligibility', t => {
  if (!fs.existsSync(definitions)) {
    t.skip('Unity LootDefinition folder is absent (backend deployed alone); catalog parity cannot be checked.');
    return;
  }

  const mismatches = [];
  const eligibleIds = new Set();
  let ids = 0;
  for (const file of assetFiles(definitions)) {
    const text = fs.readFileSync(file, 'utf8');
    const id = text.match(/^\s*_id:\s*(\S+)\s*$/m)?.[1];
    if (!id) continue; // Combat/visual definitions in this folder are not loot definitions.
    ids++;
    // Eligibility is a classification, independent of sell value. Unsellable equipment
    // stays classified but cannot create a buyback entry because shopSell rejects it.
    const category = Number(text.match(/^\s*_category:\s*(\d+)\s*$/m)?.[1]);
    if (category >= 5 && category <= 10) eligibleIds.add(id);
    assert.equal(isBuybackEligible(id), category >= 5 && category <= 10, id);
    for (const [field, catalog, price] of [
      ['_buyValuePerUnit', BUY_CATALOG, 'buyPrice'],
      ['_sellValuePerUnit', SELL_CATALOG, 'sellValue']
    ]) {
      const value = Number(text.match(new RegExp(`^\\s*${field}:\\s*(\\d+)\\s*$`, 'm'))?.[1] || 0);
      const offer = Object.hasOwn(catalog, id) ? catalog[id] : undefined;
      if (value > 0 ? !offer || offer[price] !== value : offer !== undefined) {
        mismatches.push(`${id}: ${price} expected ${value > 0 ? value : 'no offer'}, got ${offer ? offer[price] : 'no offer'}`);
      }
    }
  }
  assert.deepEqual([...BUYBACK_ELIGIBLE_IDS].sort(), [...eligibleIds].sort());
  assert.ok(ids > 0, 'Expected at least one Unity loot id');
  assert.equal(mismatches.length, 0, `Catalog parity mismatches:\n${mismatches.join('\n')}`);
});
