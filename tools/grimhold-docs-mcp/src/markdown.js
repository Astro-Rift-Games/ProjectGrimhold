const HEADING_PATTERN = /^(#{1,6})\s+(.+?)\s*$/;

export function cleanHeadingTitle(value) {
  return (value ?? '')
    // Google Docs Markdown export escapes punctuation such as "\." and "\-".
    .replace(/\\([^\s])/g, '$1')
    // Heading text is often exported entirely in bold.
    .replace(/\*\*/g, '')
    .replace(/__/g, '')
    .replace(/`/g, '')
    .trim();
}

export function normalizeForSearch(value) {
  return cleanHeadingTitle(value)
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
    .toLocaleLowerCase('es')
    // Treat punctuation as separators so "8.1" and "8\\.1" normalize equally.
    .replace(/[^\p{L}\p{N}]+/gu, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

export function extractHeadings(markdown) {
  const headings = [];
  const lines = (markdown ?? '').split(/\r?\n/);

  for (let index = 0; index < lines.length; index += 1) {
    const match = HEADING_PATTERN.exec(lines[index]);
    if (!match) {
      continue;
    }

    headings.push({
      level: match[1].length,
      title: cleanHeadingTitle(match[2]),
      line: index + 1,
    });
  }

  return headings;
}

export function extractSection(markdown, headingQuery) {
  const lines = (markdown ?? '').split(/\r?\n/);
  const normalizedQuery = normalizeForSearch(headingQuery);

  if (!normalizedQuery) {
    return null;
  }

  for (let startIndex = 0; startIndex < lines.length; startIndex += 1) {
    const startMatch = HEADING_PATTERN.exec(lines[startIndex]);
    if (!startMatch) {
      continue;
    }

    const title = cleanHeadingTitle(startMatch[2]);

    if (!normalizeForSearch(title).includes(normalizedQuery)) {
      continue;
    }

    const level = startMatch[1].length;
    let endIndex = lines.length;

    for (let index = startIndex + 1; index < lines.length; index += 1) {
      const candidate = HEADING_PATTERN.exec(lines[index]);

      if (candidate && candidate[1].length <= level) {
        endIndex = index;
        break;
      }
    }

    return {
      heading: title,
      level,
      content: lines.slice(startIndex, endIndex).join('\n').trim(),
    };
  }

  return null;
}