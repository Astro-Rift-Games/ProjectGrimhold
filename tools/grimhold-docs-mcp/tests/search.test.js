import test from 'node:test';
import assert from 'node:assert/strict';
import { searchDocuments } from '../src/search.js';

const documents = [
  {
    id: 'abilities',
    title: '11 - Diseño de Habilidades',
    markdown: '# Diseño de Habilidades\n\n## Equipamiento de Habilidades\nEl personaje dispone de dos Slots Universales.',
  },
  {
    id: 'loot',
    title: '07 - Sistema de Loot',
    markdown: '# Loot\n\nLos objetos conservan su identidad.',
  },
];

test('searchDocuments ranks title and content matches first', () => {
  const results = searchDocuments(documents, 'habilidades slots universales', 5);
  assert.equal(results.length, 1);
  assert.equal(results[0].id, 'abilities');
  assert.ok(results[0].score > 0);
  assert.ok(results[0].snippets.length > 0);
});

test('searchDocuments returns no result when nothing matches', () => {
  assert.deepEqual(searchDocuments(documents, 'host migration', 5), []);
});

test('exact multi-word phrase outranks documents matching only generic tokens', () => {
  const documents = [
    {
      id: 'equipment',
      title: 'Estructura de Equipamiento',
      markdown: '# Slots\nSlots de armadura.\nQuick Slots.\nOtros slots.',
    },
    {
      id: 'abilities',
      title: '11 - Diseño de Habilidades',
      markdown:
        '# Equipamiento de Habilidades\nEl personaje dispone de 2 Slots Universales de Habilidad.',
    },
  ];

  const results = searchDocuments(documents, 'slots universales', 5);

  assert.equal(results[0].id, 'abilities');
});