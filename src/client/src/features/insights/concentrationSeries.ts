import type { BucketAxis, ConcentrationRow } from '../../api/types';

export type StackedPoint = Record<string, string | number>;

/** The bar colours, in the order entities are stacked. Same hues the rest of the app already uses. */
export const SERIES_PALETTE = ['everdue.6', 'teal.6', 'orange.6', 'grape.6', 'lime.7', 'cyan.6', 'red.6', 'indigo.6'];

/**
 * Turns the server's per-entity series into the per-bucket rows a stacked bar chart wants.
 *
 * A bucket the server sent with no work still becomes a point with zeros: a chart that skips quiet
 * months tells a different story from the one the ledger supports.
 */
export function toStackedSeries(buckets: BucketAxis[], rows: ConcentrationRow[]): StackedPoint[] {
  return buckets.map((bucket) => {
    const point: StackedPoint = { label: bucket.label };

    for (const row of rows) {
      point[row.entityName] = row.points.find((candidate) => candidate.bucketKey === bucket.key)?.total ?? 0;
    }

    return point;
  });
}

export function seriesFor(rows: ConcentrationRow[]) {
  return rows
    .slice(0, SERIES_PALETTE.length)
    .map((row, index) => ({ name: row.entityName, color: SERIES_PALETTE[index] }));
}
