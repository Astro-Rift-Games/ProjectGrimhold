// Conservative historical inference: paths identify systems, never behavior.
const typeSections = {
  feat: 'Features', fix: 'Bug Fixes', perf: 'Performance', refactor: 'Refactoring',
  test: 'Tests', docs: 'Documentation', build: 'Architecture & Infrastructure',
  ci: 'Architecture & Infrastructure', chore: 'Maintenance', style: 'Maintenance', revert: 'Reverts',
};
const secondarySections = new Set(['Tests', 'Documentation', 'Maintenance']);
const systemRules = [
  ['Combat', ['combat', 'attack', 'attacks', 'weapon', 'weapons', 'damage', 'projectile', 'projectiles', 'health'], /\b(?:combat|damage|dual wield|weapons?|projectiles?)\b/i],
  ['Abilities', ['ability', 'abilities', 'spell', 'spells', 'skill', 'skills', 'casting', 'utility'], /\b(?:abilit(?:y|ies)|spells?|casting|utility slots?)\b/i],
  ['Extraction', ['extraction', 'extract', 'return', 'abandon', 'raidresults'], /\b(?:extraction|extracting|raid results)\b/i],
  ['Movement', ['movement', 'locomotion', 'dash', 'collision'], /\b(?:movement|locomotion|dash)\b/i],
  ['Inventory & Loot', ['inventory', 'loot', 'equipment', 'pickup', 'pickups', 'interaction', 'interactions'], /\b(?:inventory|loot|equipment|pickups?|interaction)\b/i],
  ['Networking & Sessions', ['networking', 'netcode', 'fusion', 'photon', 'runner', 'rpc', 'hostmigration', 'session', 'sessions', 'lobby', 'queue'], /\b(?:host migration|photon fusion|networking|netcode|lobby|session lifecycle)\b/i],
  ['UI', ['ui', 'hud', 'minimap', 'menu', 'menus', 'canvas', 'tooltip'], /\b(?:ui|hud|minimap|menus?|tooltips?)\b/i],
  ['World & Visibility', ['world', 'visibility', 'fog', 'tilemap', 'tiles', 'tilesets', 'dungeon', 'catacombs', 'room', 'rooms', 'raidgeneration'], /\b(?:visibility|fog|tilemaps?|dungeons?|catacombs|raid generation)\b/i],
  ['Persistence & Backend', ['persistence', 'save', 'saves', 'profile', 'profiles', 'backend', 'auth', 'authentication'], /\b(?:persistence|backend|authentication|save data)\b/i],
  ['Audio', ['audio', 'sfx', 'music', 'sound', 'sounds'], /\b(?:audio|sfx|music|sound effects?)\b/i],
  ['Art & Animation', ['art', 'sprites', 'sprite', 'animation', 'animations', 'animator', 'textures'], /\b(?:sprites?|animations?|textures?)\b/i],
  ['Input', ['input', 'inputactions', 'playerinput'], /\b(?:input system|input actions?|keybindings?)\b/i],
  ['Build & Tooling', ['tools', 'tooling', 'editor', 'agents', 'skills', 'github', 'ci', 'build', 'packages', 'projectsettings', 'changelog'], /\b(?:changelog|build pipeline|release workflow|ci pipeline)\b/i],
  ['Documentation', ['docs', 'documentation'], /\b(?:documentation|architecture docs?)\b/i],
  ['Tests', ['test', 'tests', 'testing', 'editmode', 'playmode'], /\b(?:tests?|test fixtures?)\b/i],
];
const historicalRules = [
  ['Architecture & Infrastructure', /^(?:add(?:ed)?|updat(?:e|ed)|creat(?:e|ed))\s+(?:[a-z0-9_-]+\s+)?(?:build\s+(?:profiles?|pipeline|configuration)|ci\s+workflow)\b/i],
  ['Documentation', /^document(?:ed)?\s+(.+)/i],
  ['Documentation', /^(?:add(?:ed)?|updat(?:e|ed)|writ(?:e|ten)|document(?:ed)?)\s+(?:the\s+)?(?:docs?|documentation|readme|architecture document)\b/i],
  ['Tests', /^(?:add(?:ed)?|updat(?:e|ed)|fix(?:ed)?|implement(?:ed)?)\s+(?:the\s+)?(?:tests?|test coverage|test fixtures?)\b/i],
  ['Bug Fixes', /^(?:fix(?:ed)?|repair(?:ed)?|resolv(?:e|ed)|correct(?:ed)?|arregl(?:a|ar|ado)|solucion(?:a|ar|ado)|corrig(?:e|ió))(?::)?\s+(.+)/i],
  ['Features', /^(?:add(?:ed)?|implement(?:ed)?|introduc(?:e|ed)|creat(?:e|ed)|agreg(?:a|ar|ado)|implement(?:a|ar|ado)|añad(?:e|ir|ido))\s+(.+)/i],
  ['Refactoring', /^(?:refactor(?:ed)?|reorganiz(?:e|ed)|restructur(?:e|ed)|refactoriz(?:a|ar|ado))\s+(.+)/i],
  ['Architecture & Infrastructure', /^(?:configur(?:e|ed)|set up|setup)\s+(.+)/i],
  ['Reverts', /^revert\s+["']?.+/i],
];

function tokens(value) {
  return String(value).replace(/([a-z0-9])([A-Z])/g, '$1 $2').toLowerCase().split(/[^a-z0-9]+/).filter(Boolean);
}

export function isGenericTitle(value) {
  const text = String(value || '').trim().replace(/[.!:]+$/, '').toLowerCase();
  return !text || /^(?:(?:more|some|minor|final|small)\s+)?(?:update[sd]?|changes?|fix(?:es|ed)?|bug\s*fix(?:es)?|cleanup|clean up|wip|work in progress|progress|test|initial commit|commit|misc(?:ellaneous)?|stuff)(?:\s+(?:changes?|fixes?|update|bugs?))?$/.test(text);
}

export function classifyCommit(commit) {
  const evidence = [];
  const diagnostics = [];
  const subject = String(commit.subject || '');
  const description = String(commit.description || subject);
  let section = typeSections[commit.type];
  let confidence = section ? 'explicit' : 'unknown';
  const legacyHeader = /^(feat|fix|perf|refactor|test|docs|build|ci|chore|revert)(?:\([^()]+\))?!?: (.+)$/i.exec(subject);
  if (!section && !commit.type && legacyHeader) {
    section = typeSections[legacyHeader[1].toLowerCase()]; confidence = 'inferred';
    evidence.push({ source: 'legacy-type', value: legacyHeader[0], supports: section });
  }
  const generic = isGenericTitle(description);
  if (typeSections[commit.type]) {
    evidence.push({ source: 'type', value: commit.type, supports: section });
  }
  const files = Array.isArray(commit.files) ? commit.files : [];
  // Artifact-only paths justify artifact categories, never added/fixed gameplay.
  if (!section && files.length) {
    const contentPaths = files.filter(file => !file.endsWith('.meta'));
    const allDocs = contentPaths.length && contentPaths.every(file => /\.(?:md|txt|pdf)$/i.test(file));
    const allTests = contentPaths.length && contentPaths.every(file => /(?:^|\/)(?:tests?|EditMode|PlayMode)(?:\/|\.)/i.test(file));
    if (allDocs || allTests) {
      section = allDocs ? 'Documentation' : 'Tests'; confidence = 'inferred';
      evidence.push({ source: 'artifact-paths', value: contentPaths.join(', '), supports: section });
    }
  }
  if (!section && !commit.type && !generic) {
    for (const [candidate, pattern] of historicalRules) {
      const match = pattern.exec(subject);
      if (!match || (match[1] && isGenericTitle(match[1]))) continue;
      section = candidate;
      confidence = 'inferred';
      evidence.push({ source: 'subject-action', value: match[0], supports: section });
      break;
    }
  }
  section ||= 'Other Changes';
  const systems = [];
  const scopeTokens = new Set(tokens(commit.scope || ''));
  // A conventional network scope is evidence; the word network in arbitrary prose is not.
  if (String(commit.scope || '').toLowerCase() === 'network') scopeTokens.add('networking');
  // Mentions in exclusions/future-work prose are not positive change evidence.
  // Paths/scopes remain independent evidence even when no new mechanic is claimed.
  const message = [subject, commit.body || ''].join('\n')
    .split(/\r?\n|(?<=[.!?])\s+/)
    .filter(line => !/\b(?:no|not|without|unchanged|unaffected|excluded|unrelated|deferred|future)\b|out[- ]of[- ]scope|doesn['’]t/i.test(line))
    .join('\n');
  for (const [system, names, messagePattern] of systemRules) {
    let matched = false;
    for (const file of files) {
      const pathTokens = new Set(tokens(file));
      if (names.some(name => pathTokens.has(name))) {
        evidence.push({ source: 'path', value: file, supports: system });
        matched = true;
      }
    }
    if (names.some(name => scopeTokens.has(name))) {
      evidence.push({ source: 'scope', value: commit.scope, supports: system });
      matched = true;
    }
    const match = messagePattern.exec(message);
    if (match) {
      evidence.push({ source: 'message-term', value: match[0], supports: system });
      matched = true;
    }
    if (matched) systems.push(system);
  }
  if (section === 'Other Changes') diagnostics.push({
    code: 'ambiguous-kind',
    message: 'No supported explicit type or anchored historical action. Inspect this commit diff; use an action and object or a Conventional Commit type in future messages.',
  });
  if (generic) diagnostics.push({
    code: 'generic-title',
    message: 'The subject does not identify a concrete change. Retained in the archive; omitted from summary headlines.',
  });
  if (!systems.length) diagnostics.push({
    code: 'unassigned-system',
    message: 'No recognized exact path/scope token or bounded system term. Inspect the listed paths before assigning a system.',
  });
  return { section, confidence, systems, evidence, diagnostics, generic, secondary: secondarySections.has(section) || /^(?:minor|small|trivial)\b/i.test(description) || (files.length > 0 && files.every(file => file.endsWith('.meta'))) };
}

export function classificationCounts(commits) {
  const sections = {};
  const systems = {};
  for (const commit of commits) {
    const classification = classifyCommit(commit);
    sections[classification.section] = (sections[classification.section] || 0) + 1;
    for (const system of classification.systems.length ? classification.systems : ['Unassigned']) {
      systems[system] = (systems[system] || 0) + 1;
    }
  }
  return { sections, systems };
}
