export const config = {
  repoUrl: 'https://github.com/Astro-Rift-Games/ProjectGrimhold',
  developmentBranch: 'New-Testing',
  devFile: 'CHANGELOG_DEV.md',
  historyDirectory: 'docs/changelog',
  playersFile: 'CHANGELOG.md',
  jsonFile: 'changelog-players.json',
  overridesPath: 'tools/changelog/player-overrides.json',
};

export const categories = [
  ['added', 'Added'],
  ['improved', 'Improved'],
  ['fixed', 'Fixed'],
  ['balanced', 'Balanced'],
];

export function normalize(text) {
  return text.replace(/\s+/g, ' ').trim();
}

export function cleanOutput(text) {
  const lines = text.split('\n').map(line => line.trimEnd());

  return `${lines.join('\n').trimEnd()}\n`;
}
