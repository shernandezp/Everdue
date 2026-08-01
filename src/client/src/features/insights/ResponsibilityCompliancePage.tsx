import { Anchor, Badge, Card, Group, Loader, SimpleGrid, Stack, Text, ThemeIcon, Title } from '@mantine/core';
import { LineChart } from '@mantine/charts';
import { IconCalendarStats, IconChartLine, IconChevronRight, IconPlayerPause } from '@tabler/icons-react';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router-dom';
import { PageHeader } from '../../components/PageHeader';
import { api } from '../../lib/api';
import { drillLink } from '../reports/drill';
import { WorkItemDrawer } from '../workitems/WorkItemDrawer';
import { ComplianceStrip } from './ComplianceStrip';
import { InsightsWindowBar, type InsightWindow } from './InsightsWindowBar';
import { RateCell } from './RateCell';
import { keys } from '../../lib/queryKeys';

/** One responsibility: the rate, the trend, and the individual periods behind both. */
export function ResponsibilityCompliancePage() {
  const { t } = useTranslation();
  const { responsibilityId } = useParams<{ responsibilityId: string }>();
  const [window, setWindow] = useState<InsightWindow>({ bucket: 'Week', buckets: 12 });
  const [openId, setOpenId] = useState<string | null>(null);

  const page = useQuery({
    queryKey: keys.insights.responsibility(responsibilityId, window),
    queryFn: () => api.insights.responsibility(responsibilityId!, window),
    enabled: Boolean(responsibilityId),
  });

  const summary = page.data?.summary;

  return (
    <>
      <PageHeader
        title={page.data?.title ?? t('insights.complianceTitle')}
        description={page.data ? `${t('insights.owner')}: ${page.data.ownerName}` : undefined}
        actions={
          <Group gap="xs">
            {summary?.paused && (
              <Badge color="orange" variant="light" leftSection={<IconPlayerPause size={12} />}>
                {t('insights.paused')}
              </Badge>
            )}
            {summary && !summary.active && <Badge color="gray" variant="light">{t('insights.inactive')}</Badge>}
            {summary && (
              <Anchor component={Link} to={drillLink(summary.drillThrough)} size="sm">
                <Group gap={2} wrap="nowrap">
                  {t('reports.drillThrough')}
                  <IconChevronRight size={14} />
                </Group>
              </Anchor>
            )}
          </Group>
        }
      />

      <InsightsWindowBar value={window} onChange={setWindow} />

      {page.isLoading && <Loader />}

      {summary && (
        <Stack gap="lg">
          <SimpleGrid cols={{ base: 2, md: 5 }} spacing="sm">
            <Card withBorder padding="md">
              <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                {t('insights.rate')}
              </Text>
              <RateCell
                rate={summary.rate}
                suppressed={summary.rateSuppressed}
                onTime={summary.onTime}
                concluded={summary.concluded}
              />
            </Card>
            {/* The colours are the status colours from theme.ts, so the numbers agree with the badges. */}
            <Metric label={t('insights.onTime')} value={summary.onTime} color="teal" />
            <Metric label={t('insights.late')} value={summary.late} color="lime" />
            <Metric label={t('insights.missed')} value={summary.missed} color="red" />
            <Metric label={t('insights.inFlight')} value={summary.inFlight} color="indigo" />
          </SimpleGrid>

          <Card withBorder padding="md">
            <Group gap="xs" mb="sm">
              <ThemeIcon size="md" radius="md" variant="light" color="grape">
                <IconCalendarStats size={16} />
              </ThemeIcon>
              <Title order={5}>{t('insights.periods')}</Title>
            </Group>
            <ComplianceStrip points={page.data!.strip} onOpen={setOpenId} />
            {page.data!.strip.length === 0 && (
              <Text size="sm" c="dimmed">
                {t('insights.complianceEmpty')}
              </Text>
            )}
          </Card>

          <Card withBorder padding="md">
            <Group gap="xs" mb="sm">
              <ThemeIcon size="md" radius="md" variant="light" color="grape">
                <IconChartLine size={16} />
              </ThemeIcon>
              <Title order={5}>{t('insights.rateOverTime')}</Title>
            </Group>
            <LineChart
              h={220}
              data={page.data!.buckets.map((point) => ({
                label: point.label,
                rate: point.rate === null ? null : Math.round(point.rate * 100),
              }))}
              dataKey="label"
              series={[{ name: 'rate', label: t('insights.rate'), color: 'everdue.6' }]}
              yAxisProps={{ domain: [0, 100] }}
              connectNulls={false}
              valueFormatter={(value) => `${value}%`}
              curveType="linear"
            />
            <Text size="xs" c="dimmed" mt="xs">
              {t('insights.partialHint')}
            </Text>
          </Card>
        </Stack>
      )}

      {/* The same drawer the board and the entity timeline open — one way to look at an item. */}
      <WorkItemDrawer id={openId} onClose={() => setOpenId(null)} />
    </>
  );
}

function Metric({ label, value, color }: { label: string; value: number; color?: string }) {
  return (
    <Card withBorder padding="md">
      <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
        {label}
      </Text>
      <Text fz={28} fw={700} c={value > 0 ? color : undefined}>
        {value}
      </Text>
    </Card>
  );
}
