import { config, cleanOutput, normalize } from './config.js';
import { sectionOrder } from './commits.js';

function commitLink(sha, repoUrl) {
  return `[${sha.slice(0, 7)}](${repoUrl}/commit/${sha})`;
}

function renderEntry(entry, repoUrl) {
  const breaking = entry.breaking ? '**BREAKING** ' : '';
  const scope = entry.scope ? `**${normalize(entry.scope)}:** ` : '';
  const references = entry.refs.map(sha => commitLink(sha, repoUrl)).join(', ');

  return `- ${breaking}${scope}${entry.description} (${references})`;
}

function versionHeading(version, latestRelease, settings) {
  const repoUrl = settings.repoUrl;

  if (version.tag) {
    return [
      `## [${version.version}](${repoUrl}/releases/tag/${version.tag}) - ${version.date}`,
      '',
      `Commit: ${commitLink(version.commit, repoUrl)}`,
      '',
    ];
  }

  const pendingPath = latestRelease
    ? `compare/${latestRelease.tag}...${settings.developmentBranch}`
    : `commits/${settings.developmentBranch}`;

  return [`## [Unreleased](${repoUrl}/${pendingPath})`, ''];
}

function renderSections(entries, repoUrl) {
  const lines = [];

  for (const section of sectionOrder) {
    const sectionEntries = entries.filter(entry => entry.section === section);
    if (sectionEntries.length === 0) {
      continue;
    }

    lines.push(`### ${section}`, '');

    for (const entry of sectionEntries) {
      lines.push(renderEntry(entry, repoUrl));
    }

    lines.push('');
  }

  return lines;
}

export function renderDev(versions, settings = config) {
  const latestRelease = versions.filter(version => version.tag).at(-1);
  const introduction = 'Technical history of Project Grimhold generated from Git commits '
    + 'by `tools/changelog`. Do not edit manually; see `tools/changelog/README.md`.';
  const lines = ['# Developer Changelog', '', introduction, ''];

  for (const version of [...versions].reverse()) {
    lines.push(...versionHeading(version, latestRelease, settings));

    if (!version.tag && version.entries.length === 0) {
      lines.push('_No unreleased changes._', '');
    }

    lines.push(...renderSections(version.entries, settings.repoUrl));
  }

  return cleanOutput(lines.join('\n'));
}
