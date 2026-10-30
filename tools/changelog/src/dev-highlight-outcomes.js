import { activeCommits, commitIdentities } from './commits.js';

const normalized = value => String(value || '').replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase().replace(/[^a-z0-9]+/g, ' ').trim();
const topic = value => String(value || '').replace(/\s*\(TASK-\d+\)/gi, '').replace(/^(?:add|implement|introduce|restore|reintroduce|fix|remove|drop|delete|replace)\s+/i, '').trim();
const references = record => record.trace || record.entry.refs || [record.entry.sha];

function operation(entry) {
  const clauses = [entry.description || entry.subject, ...(entry.body || '').split(/\r?\n|[.!?]\s+/)];
  const positive = [];
  let result;
  for (const clause of clauses) {
    const statement = String(clause || '').replace(/^\s*[-*]\s*/, '').split(/\b(?:while|but|retaining|preserving|keeping)\b/i)[0].trim();
    if (/\b(?:no|not|never|without|future|deferred|example)\b/i.test(statement)) continue;
    const match = /^(remove[sd]?|drop(?:ped)?|delete[sd]?|retire[sd]?|eliminate[sd]?|replace[sd]?)\s+(.+?)[.!]?$/i.exec(statement);
    if (!match) continue;
    const replacement = /^replace/i.test(match[1]) && /^(.+?)\s+(?:with|by)\s+(.+)$/i.exec(match[2]);
    // A bare "replace" without an identified successor is not a final-state claim.
    if (/^replace/i.test(match[1]) && !replacement) continue;
    positive.push(replacement ? replacement[1] : match[2]);
    result ||= { kind: replacement ? 'replaced' : 'removed', old: replacement ? replacement[1] : match[2],
      next: replacement?.[2], sentence: replacement ? `Replaced ${match[2]}` : `Removed ${match[2]}` };
  }
  return result && { ...result, positive };
}

function affects(change, record, actor) {
  const sharedSource = [...actor.sources].some(file => record.sources.has(file));
  const sameScope = actor.entry.scope && actor.entry.scope.toLowerCase() === record.entry.scope?.toLowerCase();
  if (!sharedSource && !sameScope) return false;
  const target = normalized(record.currentTopic || topic(record.description));
  const specificTopic = target.split(' ').filter(word => word.length >= 4).length >= 2;
  return change.positive.some(value => {
    const objects = [value, ...value.split(/,|\s+and\s+/i)].map(part => normalized(part).replace(/^(?:the|an?|earlier) /, ''));
    if (specificTopic && objects.includes(target)) return true;
    // A shared filename is not deletion evidence. Its named mechanism must occur
    // in an explicit positive removal/replacement object, not in a retained clause.
    return sharedSource && [...record.sources].some(file => {
      const name = file.split('/').at(-1).replace(/\.[^.]+$/, '');
      return name.length >= 6 && /[A-Z]/.test(name) && objects.includes(normalized(name));
    });
  });
}

/** Summary-only lifecycle projection. Raw order is newest-first topology, never author dates. */
export function projectHighlightRecords(entries, commits, makeRecord) {
  const raw = (commits || entries).filter(entry => !entry.excluded);
  const bySha = new Map(raw.map(entry => [entry.sha.toLowerCase(), entry]));
  const canonical = new Map();
  const identities = new Map(commitIdentities(raw));
  for (const entry of entries) {
    for (const ref of entry.refs || [entry.sha]) {
      canonical.set(ref.toLowerCase(), entry);
      // Reuse proven canonical aliases, without changing any identity or input object.
      identities.set(ref.toLowerCase(), entry.identity || `sha:${entry.sha.toLowerCase()}`);
    }
  }
  const active = activeCommits(raw, identities);
  const surviving = new Set(active.map(entry => entry.sha.toLowerCase()));
  const records = [];
  const emitted = new Set();
  for (const entry of [...active].reverse()) {
    if (entry.reverts) continue;
    const origin = canonical.get(entry.sha.toLowerCase());
    if (origin && emitted.has(origin)) continue;
    if (origin) emitted.add(origin);
    const change = operation(entry);
    const record = makeRecord(origin || entry, raw.indexOf(entry), Boolean(change));
    if (!record || (!origin && !change)) continue;
    record.trace = [...new Set([...(origin?.refs || [entry.sha])])];
    record.currentTopic = change?.next || topic(record.description);
    if (change) {
      const affected = records.filter(previous => affects(change, previous, record));
      for (const previous of affected) {
        record.trace.push(...references(previous));
        record.priority = Math.max(record.priority, previous.priority);
        records.splice(records.indexOf(previous), 1);
      }
      record.operation = change;
    } else if (record.section === 'Features') {
      const previous = records.filter(other => other.operation && affects(other.operation, record, other));
      for (const other of previous) {
        record.trace.push(...references(other)); records.splice(records.indexOf(other), 1);
        record.restored = true;
      }
    }
    records.push(record);
  }
  // Resolve complete same-period revert chains, including a revert of a removal.
  // Unknown/out-of-period targets cannot invalidate unrelated current candidates.
  const chains = new Map();
  for (const entry of raw.filter(item => item.reverts)) {
    const chain = [entry]; const seen = new Set([entry.sha]); let base = entry;
    while (base.reverts && bySha.has(base.reverts.toLowerCase()) && !seen.has(base.reverts)) {
      base = bySha.get(base.reverts.toLowerCase()); chain.push(base); seen.add(base.sha);
    }
    if (base.reverts) continue;
    const origin = canonical.get(base.sha.toLowerCase()) || base;
    const group = chains.get(origin) || { base, chain: [] };
    group.chain.push(...chain); chains.set(origin, group);
  }
  for (const [origin, { base, chain }] of chains) {
    const refs = [...new Set([...(origin.refs || [base.sha]), ...chain.map(entry => entry.sha)])];
    const alive = (origin.refs || [base.sha]).some(ref => surviving.has(ref.toLowerCase()));
    if (alive) {
      const current = records.find(record => record.entry === origin && !record.operation);
      if (current) { current.trace.push(...refs); current.restored = true; }
    } else {
      const change = operation(base);
      const record = makeRecord(origin, Math.min(...chain.map(entry => raw.indexOf(entry))), true);
      if (!record) continue;
      if (change) {
        for (const current of records.filter(item => !item.operation && affects(change, item, record))) {
          current.trace.push(...refs); current.restored = true;
        }
      } else {
        const restored = records.find(current => !current.operation && current.section === 'Features'
          && current.position < record.position && affects({ positive: [topic(record.description)] }, current, record));
        if (restored) {
          restored.trace.push(...refs); restored.restored = true; continue;
        }
        record.trace = refs;
        record.operation = { kind: 'reverted', sentence: `Reverted the earlier implementation of ${topic(record.description)}` };
        records.push(record);
      }
    }
  }
  return records.sort((first, second) => first.position - second.position);
}

function sentence(description) {
  const text = description.replace(/\s*\(TASK-\d+\)/gi, '').replace(/[.!]+$/, '').trim();
  const verbs = { add: 'Added', implement: 'Implemented', introduce: 'Introduced', fix: 'Fixed', prevent: 'Prevented',
    validate: 'Added validation for', inspect: 'Enabled inspection of', show: 'Added presentation of',
    project: 'Added projection of', share: 'Shared', migrate: 'Migrated', collect: 'Enabled collection of',
    open: 'Enabled opening of', extend: 'Extended', decouple: 'Decoupled', simplify: 'Simplified', network: 'Networked' };
  const match = /^(\w+)\s+(.+)$/.exec(text);
  return match && verbs[match[1].toLowerCase()] ? `${verbs[match[1].toLowerCase()]} ${match[2]}` : text.charAt(0).toUpperCase() + text.slice(1);
}

export function synthesizeAdvance(records) {
  const lead = records[0];
  if (lead.operation) {
    if (lead.operation.sentence.length > 220) {
      const outcome = { removed: 'Removal', replaced: 'Replacement', reverted: 'Reversal' }[lead.operation.kind];
      return `${outcome} recorded; see the linked history for affected mechanisms.`;
    }
    return `${lead.operation.sentence.replace(/[.!]+$/, '')}.`;
  }
  if (lead.restored) return `Restored the earlier implementation of ${topic(lead.description)} after a same-period reversal.`;
  const migrations = records.map(record => /^migrate (.+?) to (.+?)[.!]?$/i.exec(record.description));
  if (records.length > 1 && migrations.every(Boolean)) {
    const targets = migrations.map(match => match[2].split(/\s+/));
    const suffix = [];
    for (let offset = 1; offset <= Math.min(...targets.map(words => words.length)); offset++) {
      const word = targets[0].at(-offset);
      if (!targets.every(words => words.at(-offset).toLowerCase() === word.toLowerCase())) break;
      suffix.unshift(word);
    }
    if (suffix.length >= 2) {
      const subjects = [...new Set(migrations.map(match => match[1]))];
      const combined = `Migrated ${subjects.join(', ')} to ${suffix.join(' ')}.`;
      if (combined.length <= 220) return combined;
    }
  }
  let result = sentence(lead.description);
  if (records.length > 1 && result.length < 180) {
    const other = records.slice(1);
    result += other.some(record => record.section === 'Bug Fixes') ? ', with related fixes'
      : other.some(record => record.section === 'Refactoring') ? ', with supporting refactoring' : '';
  }
  return `${result}.`;
}
