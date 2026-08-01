import { Card, Group, Text, Title } from '@mantine/core';
import { BarChart } from '@mantine/charts';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ExportCsvButton } from '../../components/ExportCsvButton';
import { PageHeader } from '../../components/PageHeader';
import { ReportTable } from '../../components/ReportTable';
import { countColumn, drillThroughColumn, entityNameColumn, entityTypeColumn } from '../../components/reportColumns';
import { api, type ReportFilters } from '../../lib/api';
import { keys } from '../../lib/queryKeys';
import { ReportFilterBar } from '../reports/ReportFilterBar';
import { SERIES_PALETTE, seriesFor, toStackedSeries } from './concentrationSeries';
import { InsightsWindowBar, type InsightWindow } from './InsightsWindowBar';

/**
 * Which entities the team's completed work goes to, month by month.
 *
 * It counts work items, not hours — the ledger holds no time — so the wording is "completed work"
 * everywhere, and the amount of work nobody linked to an entity is stated rather than hidden.
 */
export function ConcentrationPage() {
  const { t } = useTranslation();
  const [filters, setFilters] = useState<ReportFilters>({});
  const [window, setWindow] = useState<InsightWindow>({ bucket: 'Month', buckets: 12 });

  const query = { ...filters, ...window };
  const report = useQuery({
    queryKey: keys.insights.concentration(query),
    queryFn: () => api.insights.concentration(query),
  });

  const rows = report.data?.rows ?? [];
  const buckets = report.data?.buckets ?? [];

  return (
    <>
      <PageHeader
        title={t('insights.concentrationTitle')}
        description={t('insights.concentrationIntro')}
        actions={<ExportCsvButton href={api.exports.insight('concentration', { ...filters, ...window })} />}
      />
      <ReportFilterBar value={filters} onChange={setFilters} />
      <InsightsWindowBar value={window} onChange={setWindow} />

      <Card withBorder padding="md" mb="md">
        <Group justify="space-between" mb="sm" wrap="wrap">
          <Title order={5}>{t('insights.completedWorkPerBucket')}</Title>
          <Group gap="md">
            {report.data && report.data.unlinkedTotal > 0 && (
              <Text size="xs" c="dimmed">
                {t('insights.unlinkedTotal', { count: report.data.unlinkedTotal })}
              </Text>
            )}
            {report.data && report.data.omittedEntities > 0 && (
              <Text size="xs" c="dimmed">
                {t('insights.omittedEntities', { count: report.data.omittedEntities })}
              </Text>
            )}

            {/* The chart has as many colours as it has, and the table below carries the rest. */}
            {rows.length > SERIES_PALETTE.length && (
              <Text size="xs" c="dimmed">
                {t('insights.chartTopOnly', { count: SERIES_PALETTE.length })}
              </Text>
            )}
          </Group>
        </Group>

        {rows.length === 0 ? (
          <Text size="sm" c="dimmed">
            {t('insights.concentrationEmpty')}
          </Text>
        ) : (
          <BarChart
            h={280}
            data={toStackedSeries(buckets, rows)}
            dataKey="label"
            type="stacked"
            withLegend
            series={seriesFor(rows)}
          />
        )}
      </Card>

      <ReportTable
        compact
        fetching={report.isFetching}
        records={rows}
        idAccessor="entityId"
        emptyText={t('insights.concentrationEmpty')}
        columns={[
          entityNameColumn(t),
          entityTypeColumn(t),
          countColumn('total', t('insights.completedWork')),
          {
            accessor: 'occurrences',
            title: t('insights.occurrences'),
            textAlign: 'right',
            render: (row) => row.points.reduce((sum, point) => sum + point.occurrences, 0),
          },
          {
            accessor: 'oneOffs',
            title: t('insights.oneOffs'),
            textAlign: 'right',
            render: (row) => row.points.reduce((sum, point) => sum + point.oneOffs, 0),
          },
          drillThroughColumn(t),
        ]}
      />
    </>
  );
}
