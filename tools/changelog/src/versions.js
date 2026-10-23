import { git, readCommits } from './git.js';

function compareVersionTags(firstTag, secondTag) {
  const firstParts = firstTag.slice(1).split('.').map(BigInt);
  const secondParts = secondTag.slice(1).split('.').map(BigInt);

  for (let index = 0; index < 3; index += 1) {
    if (firstParts[index] !== secondParts[index]) {
      return firstParts[index] < secondParts[index] ? -1 : 1;
    }
  }

  return firstTag.localeCompare(secondTag);
}

function tagDate(root, tag) {
  const creatorDate = git(root, [
    'for-each-ref',
    `refs/tags/${tag}`,
    '--format=%(creatordate:iso-strict)',
  ]).trim();

  return new Date(creatorDate).toISOString().slice(0, 10);
}

function readVersion(root, tag, previousTag) {
  const range = previousTag ? `${previousTag}..${tag}` : tag;

  return {
    version: tag.slice(1),
    tag,
    commit: git(root, ['rev-list', '-n', '1', tag]).trim(),
    date: tagDate(root, tag),
    commits: readCommits(root, range),
  };
}

export function readVersions(root, ref) {
  git(root, ['rev-parse', '--verify', `${ref}^{commit}`]);

  const tags = git(root, ['tag', '--merged', ref])
    .trim()
    .split('\n')
    .filter(tag => /^v\d+\.\d+\.\d+$/.test(tag))
    .sort(compareVersionTags);
  const versions = tags.map((tag, index) => readVersion(root, tag, tags[index - 1]));
  const pendingRange = tags.length > 0 ? `${tags.at(-1)}..${ref}` : ref;

  versions.push({
    version: 'Unreleased',
    commits: readCommits(root, pendingRange),
  });

  return versions;
}

// Publication planning is stricter than the historical developer CLI. A tag on a
// merged feature branch must never silently become a public release boundary.
export function readPublicationVersions(root, ref) {
  const stableTag = /^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$/;
  const firstParent = git(root, ['rev-list', '--first-parent', '--reverse', ref])
    .trim().split('\n');
  const positions = new Map(firstParent.map((sha, index) => [sha, index]));
  const tags = git(root, ['tag', '--merged', ref]).trim().split('\n')
    .filter(tag => stableTag.test(tag))
    .map(tag => ({ tag, commit: git(root, ['rev-list', '-n', '1', tag]).trim() }));

  for (const { tag, commit } of tags) {
    if (!positions.has(commit)) {
      throw Error(`Invalid publication history: ${tag} is not on the first-parent history`);
    }
  }

  tags.sort((first, second) => positions.get(first.commit) - positions.get(second.commit));
  for (let index = 1; index < tags.length; index += 1) {
    if (tags[index].commit === tags[index - 1].commit
      || compareVersionTags(tags[index - 1].tag, tags[index].tag) >= 0) {
      throw Error('Invalid publication history: stable tags must increase on distinct commits');
    }
  }

  const versions = tags.map(({ tag }, index) => readVersion(root, tag, tags[index - 1]?.tag));
  const pendingRange = tags.length ? `${tags.at(-1).tag}..${ref}` : ref;
  versions.push({ version: 'Unreleased', commits: readCommits(root, pendingRange) });
  return versions;
}
