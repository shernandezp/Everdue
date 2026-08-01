import { Text } from '@mantine/core';
import { Sparkline } from '@mantine/charts';
import type { BucketPoint } from '../../api/types';

/** The rates of the periods that were actually judged, in order. Buckets with nothing due have none. */
export function judgedRates(points: BucketPoint[]): number[] {
  return points.filter((point) => point.rate !== null).map((point) => Math.round(point.rate! * 100));
}

/**
 * The compliance trend for one row.
 *
 * Only judged periods are plotted. A bucket where nothing was due has no rate, and drawing it as 0%
 * would show a quiet fortnight as a total failure — a sparkline has no axis to explain itself with, so
 * leaving the empty periods out is the honest shape. Fewer than two points is not a trend, and says so.
 */
export function TrendSparkline({ points }: { points: BucketPoint[] }) {
  const rates = judgedRates(points);

  if (rates.length < 2) {
    return (
      <Text size="xs" c="dimmed">
        —
      </Text>
    );
  }

  return (
    <Sparkline
      w={110}
      h={28}
      data={rates}
      curveType="linear"
      color="everdue"
      fillOpacity={0.2}
      strokeWidth={1.5}
    />
  );
}
