import { resolve } from 'node:path';
import { repositoryRoot } from './git.js';
import { createReleasePlan, writePlannedRelease } from './release-plan.js';

const usage = 'Usage: node tools/changelog/src/release-cli.js <plan|prepare> [--root R] [--ref REF]';

function argumentsFor(args) {
  const command = args.shift();
  const options = {};
  if (!['plan', 'prepare'].includes(command)) {
    throw Error('usage');
  }
  while (args.length) {
    const key = args.shift();
    const value = args.shift();
    if (!['--root', '--ref'].includes(key) || key in options || !value || value.startsWith('-')) {
      throw Error('usage');
    }
    options[key] = value;
  }
  return { command, options };
}

let parsed;
try {
  parsed = argumentsFor(process.argv.slice(2));
} catch {
  console.error(usage);
  process.exitCode = 2;
}
if (parsed) {
  try {
    const root = resolve(parsed.options['--root'] || repositoryRoot());
    const plan = createReleasePlan(root, parsed.options['--ref'] || 'HEAD');
    if (parsed.command === 'prepare' && plan.version) {
      writePlannedRelease(root, plan);
    }
    console.log(JSON.stringify(plan));
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}
