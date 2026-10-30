import { existsSync, mkdirSync, readFileSync, readdirSync, unlinkSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { config } from './config.js';

// Inventory ownership is explicit: never delete arbitrary files in docs/.
export function obsoleteDeveloperFiles(root, files, settings = config) {
  const directory = settings.historyDirectory || config.historyDirectory;
  const prefix = directory + '/';
  const expected = new Set(files.filter(file => file.type === 'dev' && file.name.startsWith(prefix)).map(file => file.name));
  if (!expected.size) return [];
  const inventoryPath = resolve(root, directory, 'generated-files.json');
  let previous = [];
  if (existsSync(inventoryPath)) {
    const document = JSON.parse(readFileSync(inventoryPath, 'utf8'));
    const validName = name => typeof name === 'string' && name.startsWith(prefix)
      && /^(?:history-(?:[0-9]{4}-[0-9]{2}|undated)|v[0-9]+\.[0-9]+\.[0-9]+|unreleased|README)\.md$|^(?:report|generated-files)\.json$/.test(name.slice(prefix.length));
    if (document.schemaVersion !== 1 || document.generator !== 'tools/changelog'
      || !Array.isArray(document.files) || !document.files.every(validName)
      || new Set(document.files).size !== document.files.length) {
      throw Error('Invalid generated history inventory; no files were changed');
    }
    previous = document.files;
  }
  const owned = new Set(previous);
  const folder = resolve(root, directory);
  if (existsSync(folder)) {
    for (const entry of readdirSync(folder, { withFileTypes: true })) {
      const name = prefix + entry.name;
      if (!entry.isFile() || (!expected.has(name) && !owned.has(name))) {
        throw Error('Unmanaged file in generated history: ' + name + '; inspect or move it explicitly before regenerating');
      }
    }
  }
  return previous.filter(name => !expected.has(name) && existsSync(resolve(root, name)));
}

export function writeGeneratedFiles(root, files, settings = config) {
  const obsolete = obsoleteDeveloperFiles(root, files, settings);
  for (const file of files) {
    const path = resolve(root, file.name);
    if (existsSync(path) && readFileSync(path, 'utf8') === file.text) continue;
    mkdirSync(dirname(path), { recursive: true });
    writeFileSync(path, file.text, 'utf8');
  }
  // All replacement history exists before obsolete owned files are retired.
  for (const name of obsolete) unlinkSync(resolve(root, name));
}
