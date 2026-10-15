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
