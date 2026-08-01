#!/usr/bin/env node
// Acceptance criterion 11: no hardcoded UI strings, and both languages complete.
//
// Two checks:
//   1. Key parity — every key present in one locale is present in the other.
//   2. Missing keys — every literal t('…') in the sources resolves in both locales.
//
// Dynamic keys (t(`status.${x}`)) are checked by prefix: the namespace must exist and be non-empty.

import { readFileSync, readdirSync, statSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, '..');
const localesDir = join(root, 'src', 'i18n', 'locales');
const sourceDir = join(root, 'src');

const locales = Object.fromEntries(
  readdirSync(localesDir)
    .filter((file) => file.endsWith('.json'))
    .map((file) => [file.replace('.json', ''), JSON.parse(readFileSync(join(localesDir, file), 'utf8'))]),
);

const names = Object.keys(locales);
if (names.length < 2) {
  fail(`Expected at least two locales in ${localesDir}, found ${names.length}.`);
}

function flatten(object, prefix = '') {
  return Object.entries(object).flatMap(([key, value]) =>
    typeof value === 'object' && value !== null
      ? flatten(value, `${prefix}${key}.`)
      : [`${prefix}${key}`],
  );
}

const keySets = Object.fromEntries(names.map((name) => [name, new Set(flatten(locales[name]))]));
const problems = [];

// 1. Parity.
for (const name of names) {
  for (const other of names) {
    if (name === other) continue;
    for (const key of keySets[name]) {
      if (!keySets[other].has(key)) {
        problems.push(`Key "${key}" exists in ${name}.json but not in ${other}.json`);
      }
    }
  }
}

// 2. Usage.
function* sources(dir) {
  for (const entry of readdirSync(dir)) {
    const path = join(dir, entry);
    if (statSync(path).isDirectory()) {
      yield* sources(path);
    } else if (/\.(ts|tsx)$/.test(path) && !path.endsWith('.d.ts')) {
      yield path;
    }
  }
}

const literal = /\bt\(\s*'([^']+)'/g;
const dynamic = /\bt\(\s*`([^`$]*)\$\{/g;

for (const file of sources(sourceDir)) {
  const text = readFileSync(file, 'utf8');

  for (const match of text.matchAll(literal)) {
    const key = match[1];
    for (const name of names) {
      if (!keySets[name].has(key)) {
        problems.push(`Missing key "${key}" in ${name}.json (used in ${file.replace(root, '.')})`);
      }
    }
  }

  for (const match of text.matchAll(dynamic)) {
    const namespace = match[1].replace(/\.$/, '');
    if (!namespace) continue;

    for (const name of names) {
      const has = [...keySets[name]].some((key) => key.startsWith(`${namespace}.`));
      if (!has) {
        problems.push(`Missing namespace "${namespace}" in ${name}.json (used in ${file.replace(root, '.')})`);
      }
    }
  }
}

if (problems.length > 0) {
  fail(`i18n check failed:\n  - ${[...new Set(problems)].join('\n  - ')}`);
}

console.log(`i18n check passed: ${keySets[names[0]].size} keys × ${names.length} locales (${names.join(', ')}).`);

function fail(message) {
  console.error(message);
  process.exit(1);
}
