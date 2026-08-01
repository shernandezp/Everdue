import { Card, Group, Select, Text, ThemeIcon } from '@mantine/core';
import { IconCalendarStats } from '@tabler/icons-react';
import { useTranslation } from 'react-i18next';
import type { BucketKind } from '../../lib/api';

export type InsightWindow = { bucket: BucketKind; buckets: number };

const COUNTS = [8, 12, 26, 52];

/**
 * The trend window, beside the report filter bar rather than inside it: the filters say *which* work,
 * this says *over what*. Both are URL-free on purpose — an insight screen is read, not shared as a link.
 */
export function InsightsWindowBar({
  value,
  onChange,
}: {
  value: InsightWindow;
  onChange: (next: InsightWindow) => void;
}) {
  const { t } = useTranslation();

  return (
    <Card withBorder padding="sm" mb="md">
      <Group gap="xs" mb="xs">
        <ThemeIcon size="sm" radius="sm" variant="light" color="grape">
          <IconCalendarStats size={14} />
        </ThemeIcon>
        <Text size="xs" tt="uppercase" fw={700} c="dimmed">
          {t('insights.window')}
        </Text>
      </Group>

      <Group align="flex-end" gap="sm" wrap="wrap">
        <Select
          label={t('insights.bucket')}
          w={160}
          allowDeselect={false}
          data={[
            { value: 'Week', label: t('insights.bucketWeek') },
            { value: 'Month', label: t('insights.bucketMonth') },
          ]}
          value={value.bucket}
          onChange={(bucket) => onChange({ ...value, bucket: (bucket as BucketKind) ?? 'Week' })}
        />

        <Select
          label={t('insights.window')}
          w={200}
          allowDeselect={false}
          data={COUNTS.map((count) => ({
            value: String(count),
            label: t(value.bucket === 'Month' ? 'insights.lastMonths' : 'insights.lastWeeks', { count }),
          }))}
          value={String(value.buckets)}
          onChange={(buckets) => onChange({ ...value, buckets: Number(buckets) || 12 })}
        />

        <Text size="xs" c="dimmed" maw={320}>
          {t('insights.windowHint')}
        </Text>
      </Group>
    </Card>
  );
}
