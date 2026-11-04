'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const catalog = require('../src/config/shopCatalog');
const root = path.resolve(__dirname, '../../Project Grimhold/Assets/Scriptable Objects/Loot');
const attributes = ['vitality', 'resistance', 'strength', 'dexterity', 'intelligence', 'luck'];
const categories = { 5: 'Weapon', 6: 'Helmet', 7: 'Armor', 8: 'Gloves', 9: 'Boots', 10: 'Shield' };

test('instance templates and gem catalog mirror Unity categories, natural scaling, legacy coefficients and gem assets', t => {
  if (!fs.existsSync(root)) return t.skip('Unity assets absent in backend-only deployment.');
  const directory = path.join(root, 'Definitions');
  const files = fs.readdirSync(directory).filter(name => name.endsWith('.asset'));
  const byGuid = new Map(files.map(name => {
    const file = path.join(directory, name);
    return [fs.readFileSync(`${file}.meta`, 'utf8').match(/^guid: (\w+)/m)?.[1], fs.readFileSync(file, 'utf8')];
  }));
  const expectedEquipment = {}; const expectedGems = {};
  for (const name of files) {
    const text = fs.readFileSync(path.join(directory, name), 'utf8');
    const lootId = text.match(/^\s*_id: (\S+)/m)?.[1]; if (!lootId) continue;
    const category = categories[text.match(/^\s*_category: (\d+)/m)?.[1]];
    const sellValue = Number(text.match(/^\s*_sellValuePerUnit: (\d+)/m)?.[1] || 0);
    if (category) {
      const template = { category, sellValue };
      if (category === 'Weapon') {
        const guid = text.match(/_weaponDefinition:.*guid: (\w+)/)?.[1];
        const weapon = byGuid.get(guid); assert.ok(weapon, `${lootId} weapon reference`);
        template.naturalAttribute = attributes[Number(weapon.match(/_naturalScalingAttribute: (\d+)/)?.[1])];
        template.legacyCoefficient = Number(weapon.match(/_offensiveScaling:\s*\n\s*_attribute: \d+\s*\n\s*_coefficient: ([\d.]+)/)?.[1]);
      }
      expectedEquipment[lootId] = template;
    }
    const gemGuid = text.match(/_gemDefinition:.*guid: (\w+)/)?.[1];
    if (gemGuid) {
      const gems = fs.readdirSync(path.join(root, 'Gems')).filter(file => file.endsWith('.asset'));
      assert.ok(gems.some(file => fs.readFileSync(path.join(root, 'Gems', `${file}.meta`), 'utf8').includes(`guid: ${gemGuid}`)), `${lootId} GemDefinition`);
      expectedGems[lootId] = { sellValue };
    }
  }
  assert.deepEqual(catalog.EQUIPMENT_TEMPLATES, expectedEquipment);
  assert.deepEqual(catalog.GEM_CATALOG, expectedGems);
  for (const lootId of Object.keys(expectedEquipment)) {
    assert.equal(catalog.isBuybackEligible(lootId), false, `${lootId} v3 buyback disabled`);
    assert.equal(catalog.isBuybackEligible(lootId, 2), true, `${lootId} v2 buyback preserved`);
  }
});
