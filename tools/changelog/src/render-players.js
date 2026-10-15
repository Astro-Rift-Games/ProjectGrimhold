import { config, categories, cleanOutput } from './config.js';

function releaseCategories(notes) {
  return categories.map(([id, title]) => ({
    id,
    title,
    entries: notes
      .filter(note => note.category === id)
      .map(({ id: entryId, text }) => ({ id: entryId, text })),
  }));
}

function releaseDocument(version, settings) {
  return {
    version: version.version,
    tag: version.tag,
    date: version.date,
    commit: version.commit,
    url: `${settings.repoUrl}/releases/tag/${version.tag}`,
    categories: releaseCategories(version.notes),
  };
}

export function playersDocument(versions, settings = config) {
  return {
    schemaVersion: 1,
    game: 'Project Grimhold',
    locale: 'en',
    releases: versions
      .filter(version => version.tag)
      .reverse()
      .map(version => releaseDocument(version, settings)),
  };
}

export function categoryMarkdown(
  releaseCategories,
  emptyMessage = '_No player-facing changes in this release._',
) {
  const lines = [];

  for (const category of releaseCategories) {
    if (category.entries.length === 0) {
      continue;
    }

    lines.push(`### ${category.title}`, '');
    lines.push(...category.entries.map(entry => `- ${entry.text}`));
    lines.push('');
  }

  return cleanOutput(lines.length > 0 ? lines.join('\n') : emptyMessage);
}

export function renderPlayers(document) {
  const introduction = "What's new in Project Grimhold. Only changes included in published "
    + 'releases are listed, newest first.';
  const lines = ['# Changelog', '', introduction, ''];

  if (document.releases.length === 0) {
    lines.push('_No releases yet._');
  }

  for (const release of document.releases) {
    lines.push(
      `## [${release.version}](${release.url}) - ${release.date}`,
      '',
      categoryMarkdown(release.categories),
      '',
    );
  }

  return cleanOutput(lines.join('\n'));
}

export function renderReleaseNotes(version, settings = config) {
  const developerUrl = `${settings.repoUrl}/blob/${settings.developmentBranch}/CHANGELOG_DEV.md`;

  return cleanOutput(
    `${categoryMarkdown(version.categories)}\nDeveloper changes: ${developerUrl}`,
  );
}

function isObject(value) {
  return value && typeof value === 'object' && !Array.isArray(value);
}

function validateReleaseMetadata(release, seenVersions, problems) {
  const validVersion = typeof release.version === 'string'
    && /^\d+\.\d+\.\d+$/.test(release.version)
    && release.tag === `v${release.version}`
    && !seenVersions.has(release.version);

  if (!validVersion) {
    problems.push('Invalid or duplicate version');
  }
  seenVersions.add(release.version);

  const validDate = typeof release.date === 'string'
    && /^\d{4}-\d{2}-\d{2}$/.test(release.date)
    && Number.isFinite(Date.parse(release.date));

  if (!validDate) {
    problems.push('Invalid release date');
  }

  const validCommit = typeof release.commit === 'string'
    && /^[a-f0-9]{40}$/.test(release.commit);
  const validUrl = typeof release.url === 'string' && /^https?:\/\//.test(release.url);

  if (!validCommit || !validUrl) {
    problems.push('Invalid release commit or URL');
  }
}

function validateEntry(entry, seenIds, problems) {
  if (!isObject(entry)) {
    problems.push('Invalid entry');
    return;
  }

  const validId = typeof entry.id === 'string'
    && /^[a-f0-9]{12}-[1-9]\d*$/.test(entry.id)
    && !seenIds.has(entry.id);

  if (!validId) {
    problems.push('Invalid or duplicate entry id');
  }
  seenIds.add(entry.id);

  if (typeof entry.text !== 'string' || !entry.text.trim() || entry.text.length > 240) {
    problems.push('Invalid entry text');
  }
}

function validateCategories(releaseCategories, seenIds, problems) {
  if (!Array.isArray(releaseCategories) || releaseCategories.length !== 4) {
    problems.push('Invalid categories');
    return;
  }

  releaseCategories.forEach((category, index) => {
    const validCategory = isObject(category)
      && category.id === categories[index][0]
      && category.title === categories[index][1]
      && Array.isArray(category.entries);

    if (!validCategory) {
      problems.push('Invalid category order or shape');
      return;
    }

    for (const entry of category.entries) {
      validateEntry(entry, seenIds, problems);
    }
  });
}

export function validatePlayersDocument(document) {
  if (!isObject(document)) {
    return ['Document must be an object'];
  }

  const problems = [];
  const validMetadata = document.schemaVersion === 1
    && document.game === 'Project Grimhold'
    && document.locale === 'en';

  if (!validMetadata) {
    problems.push('Invalid document metadata');
  }

  if (!Array.isArray(document.releases)) {
    return [...problems, 'releases must be an array'];
  }

  const seenIds = new Set();
  const seenVersions = new Set();

  for (const release of document.releases) {
    if (!isObject(release)) {
      problems.push('Invalid release');
      continue;
    }

    validateReleaseMetadata(release, seenVersions, problems);
    validateCategories(release.categories, seenIds, problems);
  }

  return problems;
}
