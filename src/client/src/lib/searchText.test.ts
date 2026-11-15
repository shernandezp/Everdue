import { describe, expect, it } from 'vitest';
import { excerptAround, foldText, markMatches, searchTerms } from './searchText';

describe('searchText', () => {
  it('folds case and accents without changing length', () => {
    expect(foldText('Guía de RECEPCIÓN')).toBe('guia de recepcion');
  });

  it('splits a query into distinct folded terms', () => {
    expect(searchTerms('  Caja  CAJA diaria ')).toEqual(['caja', 'diaria']);
  });

  it('marks matches in the original text, accents intact', () => {
    expect(markMatches('Guía de recepción', ['recepcion'])).toEqual([
      { text: 'Guía de ', hit: false },
      { text: 'recepción', hit: true },
    ]);
  });

  it('cuts an excerpt around the first match and returns null without one', () => {
    const text = `${'a '.repeat(60)}llamar al proveedor ${'b '.repeat(80)}`;
    expect(excerptAround(text, ['proveedor'])).toMatch(/^….*proveedor.*…$/);
    expect(excerptAround(text, ['zzz'])).toBeNull();
  });
});
