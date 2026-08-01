import { describe, expect, it } from 'vitest';
import { BUNDLED_LANGUAGES, DEFAULT_LANGUAGE } from './index';

/**
 * Acceptance criterion 14: adding a language is **data**.
 *
 * The claim rests on the locale list being derived by `import.meta.glob` over the folder rather than written out in a
 * `.ts` file. That is exactly the kind of claim that stays true until somebody "simplifies" the glob back into a
 * literal array, so the derivation is asserted rather than trusted: this test globs the folder independently and
 * expects the two to agree, which they cannot do if the exported list is hardcoded and a file is added.
 */
const globbed = Object.keys(import.meta.glob('./locales/*.json'))
  .map((path) => path.replace('./locales/', '').replace('.json', ''))
  .sort();

describe('bundled locales', () => {
  it('are derived from the locales folder, not from a list in the code', () => {
    expect([...BUNDLED_LANGUAGES].sort()).toEqual(globbed);
  });

  it('ship Spanish and English', () => {
    expect(BUNDLED_LANGUAGES).toContain('es');
    expect(BUNDLED_LANGUAGES).toContain('en');
  });

  it('default to Spanish, which is the product default', () => {
    expect(DEFAULT_LANGUAGE).toBe('es');
    expect(BUNDLED_LANGUAGES).toContain(DEFAULT_LANGUAGE);
  });
});
