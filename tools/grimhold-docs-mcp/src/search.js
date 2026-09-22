import { extractHeadings, normalizeForSearch } from './markdown.js';

const MAX_TOKEN_OCCURRENCES = 12;
const DEFAULT_SNIPPET_LENGTH = 420;

function tokenize(query) {
  return [...new Set(
    normalizeForSearch(query)
      .split(/[^\p{L}\p{N}]+/u)
      .filter(token => token.length >= 2),
  )];
}

function countOccurrences(haystack, needle) {
  if (!needle) {
    return 0;
  }

  let count = 0;
  let cursor = 0;

  while (count < MAX_TOKEN_OCCURRENCES) {
    const index = haystack.indexOf(needle, cursor);
    if (index < 0) {
      break;
    }

    count += 1;
    cursor = index + needle.length;
  }

  return count;
}

function paragraphScore(paragraph, normalizedQuery, tokens) {
  const normalizedParagraph = normalizeForSearch(paragraph);
  let score = normalizedParagraph.includes(normalizedQuery) ? 12 : 0;

  for (const token of tokens) {
    score += countOccurrences(normalizedParagraph, token) * 2;
  }

  return score;
}

function buildSnippets(markdown, normalizedQuery, tokens, maxSnippets = 2) {
  return (markdown ?? '')
    .split(/\n\s*\n/)
    .map(paragraph => paragraph.trim())
    .filter(Boolean)
    .map(paragraph => ({
      paragraph,
      score: paragraphScore(paragraph, normalizedQuery, tokens),
    }))
    .filter(item => item.score > 0)
    .sort((a, b) => b.score - a.score)
    .slice(0, maxSnippets)
    .map(item => item.paragraph.length > DEFAULT_SNIPPET_LENGTH
      ? `${item.paragraph.slice(0, DEFAULT_SNIPPET_LENGTH - 1).trimEnd()}…`
      : item.paragraph);
}

export function rankDocument(document, query) {
  const normalizedQuery = normalizeForSearch(query);
  const tokens = tokenize(query);

  if (!normalizedQuery || tokens.length === 0) {
    return null;
  }

  const normalizedTitle = normalizeForSearch(document.title);
  const normalizedContent = normalizeForSearch(document.markdown);
  const headings = extractHeadings(document.markdown);
  const normalizedHeadings = headings.map(item => normalizeForSearch(item.title));
  const hasExactHeadingMatch = normalizedHeadings.some(
  heading => heading.includes(normalizedQuery),
);

  let score = 0;
  if (normalizedTitle.includes(normalizedQuery)) {
    score += 30;
  }
  if (hasExactHeadingMatch) {
  score += 30;
}
if (normalizedContent.includes(normalizedQuery)) {
  // An exact multi-word phrase is much stronger evidence than repeated
  // occurrences of individual generic tokens such as "slots".
  score += 40;
}

  for (const token of tokens) {
    score += countOccurrences(normalizedTitle, token) * 10;
    score += normalizedHeadings.reduce(
      (total, heading) => total + (heading.includes(token) ? 6 : 0),
      0,
    );
    score += countOccurrences(normalizedContent, token);
  }

  if (score <= 0) {
    return null;
  }

  return {
    ...document,
    score,
    matchingHeadings: headings
      .filter(item => {
        const normalized = normalizeForSearch(item.title);
        return normalized.includes(normalizedQuery) || tokens.some(token => normalized.includes(token));
      })
      .slice(0, 8),
    snippets: buildSnippets(document.markdown, normalizedQuery, tokens),
  };
}

export function searchDocuments(documents, query, limit = 5) {
  return documents
    .map(document => rankDocument(document, query))
    .filter(Boolean)
    .sort((a, b) => b.score - a.score || a.title.localeCompare(b.title, 'es'))
    .slice(0, limit);
}
