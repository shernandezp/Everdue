import i18n from '../i18n';

/**
 * Stands in for a value that is absent rather than zero — an unlinked entity, an owner who left. Named
 * because it is a typographic em dash, not a hyphen, and the two are indistinguishable in a diff.
 */
export const EM_DASH = '—';

/** An unbounded count, used where "days since" has no last activity to count from. */
export const INFINITY_SIGN = '∞';

/**
 * Dates and numbers are formatted per language; the instants themselves are already in the tenant's
 * time zone as far as the user is concerned, because the server anchors every period to it.
 */
function locale(): string {
  return i18n.language === 'en' ? 'en-GB' : 'es-ES';
}

export function formatDate(value: string | null | undefined): string {
  if (!value) return EM_DASH;
  return new Date(value).toLocaleDateString(locale(), { day: '2-digit', month: 'short', year: 'numeric' });
}

export function formatDateTime(value: string | null | undefined): string {
  if (!value) return EM_DASH;
  return new Date(value).toLocaleString(locale(), {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

/** Due dates land on 23:59:59; showing the clock there is noise, so occurrences show the day. */
export function formatDueDate(value: string, isOccurrence: boolean): string {
  return isOccurrence ? formatDate(value) : formatDateTime(value);
}

export function formatNumber(value: number): string {
  return value.toLocaleString(locale());
}

/**
 * A duration in days, to one decimal. Waits are calendar days including nights and weekends — the
 * insight screens say so — and one decimal is as precise as that claim can honestly be.
 */
export function formatDays(value: number): string {
  return value.toFixed(1);
}

/** A value, or the em dash when there is nothing to show. Zero is a value; null is not. */
export function orDash(value: string | number | null | undefined): string {
  return value === null || value === undefined || value === '' ? EM_DASH : String(value);
}

export function toDateInputValue(value: Date): string {
  const year = value.getFullYear();
  const month = `${value.getMonth() + 1}`.padStart(2, '0');
  const day = `${value.getDate()}`.padStart(2, '0');
  return `${year}-${month}-${day}`;
}

/** Local end-of-day, as the ISO instant the API expects for a one-off due date. */
export function endOfLocalDay(date: Date): string {
  const copy = new Date(date);
  copy.setHours(23, 59, 59, 0);
  return copy.toISOString();
}

export function daysBetween(from: string, to: Date = new Date()): number {
  return Math.floor((to.getTime() - new Date(from).getTime()) / 86_400_000);
}
