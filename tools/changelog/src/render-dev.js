import path from 'node:path';
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
function headline(entry, settings) {
  const breaking = entry.breaking ? '**BREAKING** ' : '';
  const scope = entry.scope ? `**${markdown(entry.scope)}:** ` : '';
  const refs = (entry.refs || [entry.sha]).slice(0, 3).map(sha => commitLink(sha, settings.repoUrl)).join(', ');
  return `- ${breaking}${scope}${markdown(entry.description || entry.subject)} (${refs})`;
}
function renderMain(versions, periods, settings, directory) {
  const mainFile = settings.devFile;
  const archive = relativeLink(mainFile, `${directory}/README.md`);
  const latestRelease = versions.filter(version => version.tag).at(-1);
  const pendingPath = latestRelease ? `compare/${encodeURIComponent(latestRelease.tag)}...${encodeURIComponent(settings.developmentBranch)}` : `commits/${encodeURIComponent(settings.developmentBranch)}`;
  const entries = periods.flatMap(period => period.entries);
  const lines = ['# Developer Changelog', '',
    'Technical history generated from Git by `tools/changelog`. Do not edit generated files manually.', '',
    `[Full archive](${archive}) · [Classification report](${relativeLink(mainFile, `${directory}/report.json`)}) · [Unreleased comparison](${settings.repoUrl}/${pendingPath})`, '',
    '## Systems at a glance', '',
    `${entries.length} canonical entries. Counts overlap when a commit has evidence for multiple systems; they are not additive.`, '',
    ...systemTable(entries), '',
    '## Recent periods', '',
    `At most ${summaryPeriodLimit} periods and ${headlineLimit} concrete primary headlines per period. Documentation, tests, maintenance, ambiguous messages and all original bodies remain in the archive.`, ''];
  for (const period of periods.slice(0, summaryPeriodLimit)) {
    lines.push(`### [${markdown(period.title)}](${relativeLink(mainFile, `${directory}/${period.key}.md`)})`, '',
      `${categorySummary(period.entries)}. ${period.commits.length} original commits in detail.`, '');
    const headlines = period.entries.filter(entry => {
      const classified = classifyCommit(entry);
      return !classified.secondary && !classified.generic && classified.section !== 'Other Changes';
    }).slice(0, headlineLimit);
    lines.push(...headlines.map(entry => headline(entry, settings)));
    if (headlines.length) lines.push('');
    if (!period.tag && !period.entries.length) lines.push('_No unreleased changes._', '');
  }
  lines.push(`Older periods and every release are available in the [full archive index](${archive}).`, '');
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
