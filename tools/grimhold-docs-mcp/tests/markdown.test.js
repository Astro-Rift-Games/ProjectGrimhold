import test from 'node:test';
import assert from 'node:assert/strict';
import { extractHeadings, extractSection, normalizeForSearch } from '../src/markdown.js';

test('normalizeForSearch removes accents and normalizes whitespace', () => {
  assert.equal(normalizeForSearch('  Reanimación   Asistida  '), 'reanimacion asistida');
});

test('extractHeadings returns markdown heading metadata', () => {
  const headings = extractHeadings('# Uno\nTexto\n## Dos\nMás');
  assert.deepEqual(headings, [
    { level: 1, title: 'Uno', line: 1 },
    { level: 2, title: 'Dos', line: 3 },
  ]);
});

test('extractSection stops at the next heading of equal or higher level', () => {
  const markdown = '# A\nintro\n## Target\nbody\n### Child\nchild\n## Next\nnext';
  const section = extractSection(markdown, 'target');
  assert.equal(section.heading, 'Target');
  assert.equal(section.content, '## Target\nbody\n### Child\nchild');
});

test('Google Docs escaped bold headings can be selected by plain heading text', () => {
  const markdown = [
    '# **11 \\- Diseño de Habilidades**',
    '',
    '# **6\\. Equipamiento de Habilidades**',
    'El personaje dispone de 2 Slots Universales.',
    '',
    '# **7\\. Requisitos de Atributos**',
    'Contenido siguiente.',
  ].join('\n');

  const headings = extractHeadings(markdown);

  assert.equal(
    headings[1].title,
    '6. Equipamiento de Habilidades',
  );

  const section = extractSection(
    markdown,
    '6. Equipamiento de Habilidades',
  );

  assert.ok(section);
  assert.equal(
    section.heading,
    '6. Equipamiento de Habilidades',
  );
  assert.match(section.content, /2 Slots Universales/);
  assert.doesNotMatch(section.content, /Contenido siguiente/);
});
