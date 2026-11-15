const MARKS = /\p{M}/gu;

/** Mirrors the server's folding: lower case, no accents. One output character per input character. */
export function foldText(text: string): string {
  let folded = '';
  for (const char of text) {
    const base = char.normalize('NFD').replace(MARKS, '').toLowerCase();
    const lower = char.toLowerCase();
    folded += base.length === char.length ? base : lower.length === char.length ? lower : char;
  }
  return folded;
}

export function searchTerms(query: string): string[] {
  return [...new Set(foldText(query).split(/\s+/).filter(Boolean))];
}

export type TextPart = { text: string; hit: boolean };

/** Splits text into matched and unmatched runs, ignoring case and accents. */
export function markMatches(text: string, terms: string[]): TextPart[] {
  const folded = foldText(text);
  const hit = new Array<boolean>(text.length).fill(false);

  for (const term of terms) {
    for (let at = folded.indexOf(term); at !== -1; at = folded.indexOf(term, at + term.length)) {
      hit.fill(true, at, at + term.length);
    }
  }

  const parts: TextPart[] = [];
  for (let i = 0; i < text.length; i++) {
    const last = parts[parts.length - 1];
    if (last && last.hit === hit[i]) last.text += text[i];
    else parts.push({ text: text[i], hit: hit[i] });
  }
  return parts;
}

/** A window of a long text around its first match, or null when no term appears in it. */
export function excerptAround(text: string, terms: string[], radius = 48): string | null {
  const folded = foldText(text);
  const first = Math.min(...terms.map((term) => folded.indexOf(term)).filter((at) => at !== -1));
  if (!Number.isFinite(first)) return null;

  const start = Math.max(0, first - radius);
  const end = Math.min(text.length, first + radius * 2);
  return `${start > 0 ? '…' : ''}${text.slice(start, end).replace(/\s+/g, ' ').trim()}${end < text.length ? '…' : ''}`;
}
