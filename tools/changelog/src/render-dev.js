import path from 'node:path';
import { projectHighlightRecords, synthesizeAdvance } from './dev-highlight-outcomes.js';
import { config, cleanOutput } from './config.js';
import { classifyCommit, classificationCounts } from './classify.js';

const summaryPeriodLimit = 6;
const headlineLimit = 3;
const sectionOrder = ['Features', 'Bug Fixes', 'Performance', 'Refactoring', 'Architecture & Infrastructure', 'Reverts', 'Documentation', 'Tests', 'Maintenance', 'Other Changes'];

function markdown(value) {
  return String(value ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
    .replace(/([\\`*_[\]{}|])/g, '\\$1').replace(/\r?\n/g, ' ');
}
function commitLink(sha, repoUrl, full = false) {
  return `[${full ? sha : sha.slice(0, 7)}](${repoUrl}/commit/${encodeURIComponent(sha)})`;
}
function relativeLink(from, to) {
  return path.posix.relative(path.posix.dirname(from), to).split('/').map(part => encodeURIComponent(part)).join('/');
}
function fenced(value) {
  const text = String(value || '');
  const runs = text.match(/`+/g) || [];
  const delimiter = '`'.repeat(Math.max(3, ...runs.map(run => run.length + 1)));
  return `${delimiter}text\n${text}${text.endsWith('\n') ? '' : '\n'}${delimiter}`;
}
function monthOf(commit) {
  const date = Date.parse(commit.date);
  return Number.isNaN(date) ? 'undated' : new Date(date).toISOString().slice(0, 7);
}
function periodKey(value) {
  return String(value).replace(/[^a-zA-Z0-9.-]/g, '-');
}
function eligible(commits) {
  // Bot-generated history must not feed itself on the next regeneration.
  return (commits || []).filter(commit => !commit.excluded);
}
function historyPeriods(versions) {
  const periods = [];
  for (const [index, version] of versions.entries()) {
    const commits = eligible(version.commits || version.entries);
    const entries = eligible(version.entries || version.commits);
    const base = { version: version.version, tag: version.tag || null, date: version.date || null, releaseCommit: version.commit || null, order: index };
    if (index === 0 && commits.length) {
      const months = [...new Set(commits.map(monthOf))].sort().reverse();
      for (const month of months) {
        const monthlyCommits = commits.filter(commit => monthOf(commit) === month);
        const shas = new Set(monthlyCommits.map(commit => commit.sha));
        periods.push({ ...base, key: `history-${month}`,
          title: `${month} — ${version.tag ? `initial history through ${version.tag}` : 'Unreleased history'}`,
          month, commits: monthlyCommits, entries: entries.filter(entry => shas.has(entry.sha)) });
      }
    } else {
      periods.push({ ...base, key: version.tag ? periodKey(version.tag) : 'unreleased',
        title: version.tag ? `${version.tag}${version.date ? ` — ${version.date}` : ''}` : 'Unreleased',
        commits, entries });
    }
  }
  return periods.sort((first, second) => second.order - first.order || (second.month || '').localeCompare(first.month || ''));
}
function rowsForSystems(entries) {
  const rows = new Map();
  for (const entry of entries) {
    const classified = classifyCommit(entry);
    for (const system of classified.systems.length ? classified.systems : ['Unassigned']) {
      const counts = rows.get(system) || {};
      counts[classified.section] = (counts[classified.section] || 0) + 1;
      rows.set(system, counts);
    }
  }
  return [...rows].sort(([first], [second]) => first.localeCompare(second));
}
function systemTable(entries) {
  const lines = ['| System | Features | Fixes | Refactors / perf | Infrastructure | Secondary | Reverts / other |',
    '| --- | ---: | ---: | ---: | ---: | ---: | ---: |'];
  for (const [system, counts] of rowsForSystems(entries)) {
    const n = section => counts[section] || 0;
    lines.push(`| ${system} | ${n('Features')} | ${n('Bug Fixes')} | ${n('Refactoring') + n('Performance')} | ${n('Architecture & Infrastructure')} | ${n('Documentation') + n('Tests') + n('Maintenance')} | ${n('Reverts') + n('Other Changes')} |`);
  }
  if (!entries.length) lines.push('| No canonical changes | 0 | 0 | 0 | 0 | 0 | 0 |');
  return lines;
}
function categorySummary(entries) {
  const counts = classificationCounts(entries).sections;
  return sectionOrder.filter(section => counts[section]).map(section => `${counts[section]} ${section.toLowerCase()}`).join('; ') || 'No canonical changes';
}
// Summary policy is deliberately separate from classification, canonical identity,
// archive periods and release planning. It only chooses which recorded changes to show.
const supportingSystems = new Set(['Build & Tooling', 'Documentation', 'Tests']);
const changePriority = { Features: 12, 'Bug Fixes': 10, Performance: 9, 'Architecture & Infrastructure': 5, Refactoring: 7, Reverts: 6 };
const routineWords = new Set('add added implement implemented fix fixed validate prevent refactor simplify update support introduce remove improve rules state system player network manager controller changes change with from into through without'.split(' '));

function summaryRecord(entry, position, lifecycle = false) {
  const classified = classifyCommit(entry);
  const description = entry.description || entry.subject || '';
  const files = (entry.files || []).map(file => file.replace(/\\/g, '/')).filter(file => !file.endsWith('.meta'));
  const referencePath = file => /(?:^|\/)(?:tests?|docs?|documentation)(?:\/|$)|\.(?:md|rst|adoc|txt)$/i.test(file);
  const referenceOnly = files.length > 0 && files.every(referencePath);
  const toolingOnly = files.length > 0 && files.every(file => referencePath(file) || /(?:^|\/)(?:tools|editor|\.github|packages|projectsettings)(?:\/|$)/i.test(file));
  if (referenceOnly || (!lifecycle && (classified.secondary || classified.generic || classified.section === 'Other Changes' || description.length > 240))) return null;
  const systems = classified.systems.filter(system => !supportingSystems.has(system));
  const scopedSystem = classified.evidence.find(item => item.source === 'scope' && systems.includes(item.supports));
  const pathWeight = system => new Set(classified.evidence.filter(item => item.source === 'path' && item.supports === system).map(item => item.value)).size;
  const byPath = [...systems].sort((first, second) => pathWeight(second) - pathWeight(first));
  const system = scopedSystem?.supports || byPath[0] || classified.systems[0] || 'Unassigned';
  const architectural = (classified.section === 'Architecture & Infrastructure'
      && /\b(?:architecture|boundaries|boundary|ownership|authority|authoritative|composition|dependencies|recovery|migration|snapshots?|replication|persistence|transactions?|concurrency)\b/i.test(description)
      && !/^(?:configure|prepare)\b/i.test(description))
    || (classified.section === 'Refactoring'
      && /^(?:decouple|separate|extract|isolate|replace)\b/i.test(description)
      && /\b(?:publication|transport|simulation|presentation|composition|dependencies|ownership|persistence|authority|recovery)\b/i.test(description));
  const routineInfrastructure = classified.section === 'Architecture & Infrastructure' && !architectural;
  const tier = routineInfrastructure || (toolingOnly && !architectural) ? 1 : systems.length || architectural ? 3 : classified.systems.length ? 1 : 2;
  const taskLines = [entry.subject, ...(entry.body || '').split('\n').filter(line => /^\s*(?:refs|fixes|closes):?\s/i.test(line))]
    .filter(line => !/\b(?:no|not|future|deferred|follow-up|exclude)\b/i.test(line || ''));
  const tasks = new Set(taskLines.flatMap(line => (line || '').toUpperCase().match(/\bTASK-\d+\b/g) || []));
  const sources = new Set((entry.files || []).map(file => file.replace(/\\/g, '/')).filter(file =>
    /\.(?:cs|js|mjs|cjs|ts|tsx|jsx|shader|hlsl)$/i.test(file)
    && !/(?:^|\/)(?:tests?|docs?|documentation|node_modules)(?:\/|$)|\.generated\.|PlayerInputActions\.cs$/i.test(file)));
  const words = new Set((description.toLowerCase().match(/[a-z][a-z0-9]+/g) || []).filter(word => word.length >= 4 && !routineWords.has(word)));
  return { entry, position, description, section: classified.section, system, tasks, sources, words,
    priority: tier * 100 + (architectural ? 12 : changePriority[classified.section] || 0) + (architectural ? 3 : 0) + (entry.breaking ? 3 : 0) };
}

function relationship(first, second) {
  const task = [...first.tasks].sort().find(id => second.tasks.has(id));
  if (task) return task;
  if (!first.entry.scope || first.entry.scope.toLowerCase() !== second.entry.scope?.toLowerCase()) return null;
  if (![...first.sources].some(file => second.sources.has(file))) return null;
  return [...first.words].filter(word => second.words.has(word)).length >= 2 ? 'scope + source + subject' : null;
}

function selectAdvances(entries, commits) {
  const groups = [];
  for (const record of projectHighlightRecords(entries, commits, summaryRecord)) {
    // Pairwise support prevents a shared helper from chaining unrelated advances.
    const existing = groups.find(group => group.records.length < 3 && group.records.every(other => !record.operation && !record.restored && !other.operation && !other.restored && relationship(record, other)));
    if (existing) existing.records.push(record);
    else groups.push({ records: [record] });
  }
  const selected = [];
  const systems = new Set();
  const kinds = new Set();
  const score = group => Math.max(...group.records.map(record => record.priority))
    + (group.records.some(record => !systems.has(record.system)) ? 8 : 0)
    + (group.records.some(record => !kinds.has(record.section)) ? 3 : 0)
    + group.records.length - 1;
  while (groups.length && selected.length < headlineLimit) {
    groups.sort((first, second) => score(second) - score(first)
      || first.records[0].position - second.records[0].position
      || first.records[0].entry.sha.localeCompare(second.records[0].entry.sha));
    const group = groups.shift();
    selected.push(group);
    for (const record of group.records) { systems.add(record.system); kinds.add(record.section); }
  }
  return selected;
}

function advanceLine(group, settings) {
  const records = [...group.records].sort((first, second) => (changePriority[second.section] || 0) - (changePriority[first.section] || 0) || first.position - second.position);
  const systems = [...new Set(records.map(record => record.system))].join(' / ');
  const summary = synthesizeAdvance(records);
  const scopes = [...new Set(records.map(record => record.entry.scope).filter(Boolean))];
  const scope = scopes.length === 1 ? `**${markdown(scopes[0])}:** ` : '';
  const breaking = records.some(record => record.entry.breaking) ? '**BREAKING** ' : '';
  const refs = [...new Set(records.flatMap(record => record.trace || record.entry.refs || [record.entry.sha]))];
  return `- **${systems}** — ${breaking}${scope}${markdown(summary)} (${refs.map(sha => commitLink(sha, settings.repoUrl)).join(', ')}).`;
}
function summaryPeriods(periods, versions) {
  const projected = [];
  for (const period of periods) {
    const existing = period.tag && projected.find(item => item.tag === period.tag);
    if (existing) { existing.entries.push(...period.entries); continue; }
    projected.push({ ...period, entries: [...period.entries],
      commits: period.tag ? eligible(versions.find(version => version.tag === period.tag)?.commits || period.commits) : period.commits,
      title: period.tag ? `${period.tag}${period.date ? ` — ${period.date}` : ''}` : period.month ? `${period.month} — before the first release` : 'Unreleased' });
  }
  return projected;
}

function renderMain(versions, periods, settings, directory) {
  const mainFile = settings.devFile;
  const archive = relativeLink(mainFile, `${directory}/README.md`);
  const latestRelease = versions.filter(version => version.tag).at(-1);
  const pendingPath = latestRelease ? `compare/${encodeURIComponent(latestRelease.tag)}...${encodeURIComponent(settings.developmentBranch)}` : `commits/${encodeURIComponent(settings.developmentBranch)}`;
  const lines = ['# Developer Changelog', '',
    'Development advances recorded in Git, organized by period and system. Generated by `tools/changelog`; do not edit manually.', '',
    `[Full archive](${archive}) · [Classification report](${relativeLink(mainFile, `${directory}/report.json`)}) · [${latestRelease ? 'Unreleased comparison' : 'Development history'}](${settings.repoUrl}/${pendingPath})`, '',
    `At most ${summaryPeriodLimit} periods and ${headlineLimit} selected advances per period. Selection favors concrete functional changes and architectural boundaries, with system and change-kind diversity. Each highlight is a brief synthesis backed by commit links, reconciled within its period. Original messages, types and evidence remain in the archive; summaries do not establish runtime validation.`, ''];
  if (!latestRelease) lines.push('## Unreleased', '',
    'No release baseline exists yet. The monthly history below is initial development, not one pending release; a distinct post-release change range cannot be inferred until the first release.', '',
    '## Initial development history', '');
  else lines.push('## Recent progress', '');
  for (const period of summaryPeriods(periods, versions).slice(0, summaryPeriodLimit)) {
    const destination = period.tag ? `${settings.repoUrl}/releases/tag/${encodeURIComponent(period.tag)}` : relativeLink(mainFile, `${directory}/${period.key}.md`);
    lines.push(`### [${markdown(period.title)}](${destination})`, '');
    const advances = selectAdvances(period.entries, period.commits);
    lines.push(...advances.map(group => advanceLine(group, settings)), '');
    if (!advances.length) lines.push(period.entries.length
      ? '_No eligible concrete advances selected; inspect the original records in the archive._'
      : '_No changes recorded in this range._', '');
    const detail = period.tag && period.month ? archive : relativeLink(mainFile, `${directory}/${period.key}.md`);
    lines.push(`[Full period history](${detail})`, '');
  }
  lines.push(`Older periods, full metrics, secondary work and every original record remain in the [full archive index](${archive}) and classification report.`, '');
  return cleanOutput(lines.join('\n'));
}

function renderArchiveIndex(periods, settings, directory) {
  const lines = ['# Developer history archive', '',
    `[Technical summary](${relativeLink(`${directory}/README.md`, settings.devFile)}) · [Machine-readable evidence and diagnostics](report.json)`, '',
    'Every eligible original commit is retained, including equivalent/cherry-picked commits omitted from canonical summaries. Changelog automation is excluded to prevent generated-history feedback loops.', '',
    'The initial history is partitioned by UTC author month; subsequent releases retain their Git version boundaries. Missing dates are kept in an undated period. This index lists every generated period.', '',
    '| Release / period | Original commits | Canonical entries |', '| --- | ---: | ---: |'];
  for (const period of periods) lines.push(`| [${markdown(period.title)}](${period.key}.md) | ${period.commits.length} | ${period.entries.length} |`);
  lines.push('', '## Reading the evidence', '',
    '- Explicit Conventional Commit types take precedence over conservative anchored action-word inference.',
    '- Systems are overlapping labels supported by exact path/scope tokens or bounded system terms. They do not establish behavior.',
    '- Other Changes means the kind is not justified by the available message; recognized systems do not silently promote it.',
    '- Secondary documentation, tests and maintenance are separate from primary change headlines.',
    '- Original subjects, bodies, paths, all commit links and actionable diagnostics appear below each historical record.', '');
  return cleanOutput(lines.join('\n'));
}
function renderPeriod(period, settings, directory) {
  const lines = [`# ${markdown(period.title)}`, '', '[Archive index](README.md)', '',
    `Release: ${markdown(period.version)}${period.tag ? ` · [${markdown(period.tag)}](${settings.repoUrl}/releases/tag/${encodeURIComponent(period.tag)})` : ''}`,
    ...(period.releaseCommit ? [`Release commit: ${commitLink(period.releaseCommit, settings.repoUrl, true)}`] : []), '',
    `${period.commits.length} original commits; ${period.entries.length} canonical entries.`, '',
    '## Canonical classification', '', categorySummary(period.entries), '', ...systemTable(period.entries), '',
    '## Original commit records', ''];
  for (const commit of period.commits) {
    const classified = classifyCommit(commit);
    lines.push(`### ${commit.sha}`, '', commitLink(commit.sha, settings.repoUrl, true), '',
      `Date: ${markdown(commit.date || 'unknown')} · Classification: ${classified.section} (${classified.confidence}) · Systems: ${classified.systems.join(', ') || 'Unassigned'}`, '',
      `Flags: ${commit.breaking ? 'breaking; ' : ''}${classified.secondary ? 'secondary' : 'primary or unclassified'}${commit.reverts ? `; reverts ${markdown(commit.reverts)}` : ''}`, '',
      '#### Original subject', '', fenced(commit.subject), '', '#### Original body', '', fenced(commit.body || ''), '',
      '#### Changed paths', '', fenced((commit.files || []).join('\n')), '', '#### Classification evidence', '');
    if (classified.evidence.length) {
      lines.push(...classified.evidence.map(item => `- ${item.source} → ${markdown(item.supports)}: ${markdown(item.value)}`));
    } else lines.push('- No sufficient classification evidence.');
    lines.push('', '#### Diagnostics', '');
    if (classified.diagnostics.length) lines.push(...classified.diagnostics.map(item => `- **${item.code}:** ${item.message}`));
    else lines.push('- No classification ambiguity detected.');
    lines.push('');
  }
  return `${lines.join('\n')}\n`;
}
function classificationReport(periods, settings) {
  const raw = periods.flatMap(period => period.commits);
  const entries = periods.flatMap(period => period.entries);
  const before = {};
  for (const commit of raw) before[commit.section || 'Other Changes'] = (before[commit.section || 'Other Changes'] || 0) + 1;
  const counts = classificationCounts(raw);
  const records = raw.map(commit => ({ sha: commit.sha, url: `${settings.repoUrl}/commit/${commit.sha}`,
    subject: commit.subject, body: commit.body || '', date: commit.date || null, files: commit.files || [],
    originalType: commit.type || null, originalScope: commit.scope || null, originalSection: commit.section || 'Other Changes',
    breaking: Boolean(commit.breaking), reverts: commit.reverts || null, ...classifyCommit(commit) }));
  return { schemaVersion: 1, rawCommits: raw.length, canonicalEntries: entries.length, before, after: counts.sections,
    systemCountsOverlap: true, systems: counts.systems, canonical: classificationCounts(entries),
    unknownEntries: records.filter(record => record.section === 'Other Changes'),
    examples: records.filter(record => record.confidence === 'inferred').slice(0, 12),
    periods: periods.map(period => ({ key: period.key, title: period.title, version: period.version, tag: period.tag,
      originalCommits: period.commits.length, canonicalEntries: period.entries.length })), commits: records };
}

export function developerArtifacts(versions, options = config) {
  const settings = { ...config, ...options };
  const directory = (settings.historyDirectory || 'docs/changelog').replace(/\\/g, '/').replace(/\/$/, '');
  const periods = historyPeriods(versions);
  const artifacts = [
    { name: settings.devFile, text: renderMain(versions, periods, settings, directory), type: 'dev' },
    { name: `${directory}/README.md`, text: renderArchiveIndex(periods, settings, directory), type: 'dev' },
    ...periods.map(period => ({ name: `${directory}/${period.key}.md`, text: renderPeriod(period, settings, directory), type: 'dev' })),
    { name: `${directory}/report.json`, text: `${JSON.stringify(classificationReport(periods, settings), null, 2)}\n`, type: 'dev' },
  ];
  const inventory = directory + '/generated-files.json';
  const files = [...artifacts.filter(file => file.name.startsWith(directory + '/')).map(file => file.name), inventory].sort();
  return [...artifacts, { name: inventory, type: 'dev', text: JSON.stringify({ schemaVersion: 1, generator: 'tools/changelog', files }, null, 2) + '\n' }];
}

export function renderDev(versions, settings = config) {
  return developerArtifacts(versions, settings)[0].text;
}
