import { Anchor, Card, Text, Title } from '@mantine/core';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { ExportCsvButton } from '../../components/ExportCsvButton';
import { PageHeader } from '../../components/PageHeader';
import { ReportTable } from '../../components/ReportTable';
import { countColumn, daysColumn, entityNameColumn, entityTypeColumn } from '../../components/reportColumns';
import { api, type ReportFilters } from '../../lib/api';
import { formatDate } from '../../lib/format';
import { keys } from '../../lib/queryKeys';
import { drillLink } from '../reports/drill';
import { ReportFilterBar } from '../reports/ReportFilterBar';
import { InsightsWindowBar, type InsightWindow } from './InsightsWindowBar';

/**
 * Where waiting time goes — customers, suppliers, or our own approvals.
 *
 * The days are calendar days, and the screen says so: nights and weekends are inside them. Only the
 * holds still running can be opened as a list; a hold that has ended leaves nothing on the work item to
 * filter by, so those numbers are deliberately not links.
 */
export function HoldAgingPage() {
  const { t } = useTranslation();
  const [filters, setFilters] = useState<ReportFilters>({});
  const [window, setWindow] = useState<InsightWindow>({ bucket: 'Month', buckets: 12 });

  const query = { ...filters, ...window };
  const report = useQuery({
    queryKey: keys.insights.holdAging(query),
    queryFn: () => api.insights.holdAging(query),
  });

  const waitColumns = [
    daysColumn<{ totalWaitDays: number }>('totalWaitDays', t('insights.totalWait')),
    daysColumn<{ averageWaitDays: number }>('averageWaitDays', t('insights.averageWait')),
    daysColumn<{ longestWaitDays: number }>('longestWaitDays', t('insights.longestWait')),
  ];

  return (
    <>
      <PageHeader
        title={t('insights.holdAgingTitle')}
        description={
          report.data
            ? `${t('insights.calendarDays')} · ${formatDate(report.data.from)} – ${formatDate(report.data.to)}`
            : t('insights.calendarDays')
        }
        actions={<ExportCsvButton href={api.exports.insight('hold-aging', { ...filters, ...window })} />}
      />

      <ReportFilterBar value={filters} onChange={setFilters} />
      <InsightsWindowBar value={window} onChange={setWindow} />

      <Card withBorder padding="md" mb="md">
        <Title order={5} mb="sm">
          {t('insights.byReason')}
        </Title>

        <ReportTable
          compact
          fetching={report.isFetching}
          records={report.data?.byReason ?? []}
          idAccessor="reason"
          emptyText={t('insights.holdAgingEmpty')}
          columns={[
            { accessor: 'reason', title: t('insights.reason'), render: (row) => t(`holdReason.${row.reason}`) },
            ...waitColumns,
            countColumn('holds', t('insights.holds')),
            countColumn('items', t('insights.items')),
            {
              accessor: 'stillOnHold',
              title: t('insights.stillOnHold'),
              textAlign: 'right',
              render: (row) =>
                row.currentDrillThrough ? (
                  <Anchor component={Link} to={drillLink(row.currentDrillThrough)} size="sm">
                    {row.stillOnHold}
                  </Anchor>
                ) : (
                  row.stillOnHold
                ),
            },
          ]}
        />
      </Card>

      <Card withBorder padding="md">
        <Title order={5} mb="sm">
          {t('insights.byEntity')}
        </Title>

        <ReportTable
          compact
          fetching={report.isFetching}
          records={report.data?.byEntity ?? []}
          idAccessor={(row) => row.entityId ?? 'unlinked'}
          emptyText={t('insights.holdAgingEmpty')}
          columns={[
            entityNameColumn(t, { unlinkedLabel: t('insights.unlinked') }),
            entityTypeColumn(t),
            ...waitColumns,
            countColumn('holds', t('insights.holds')),
            countColumn('stillOnHold', t('insights.stillOnHold')),
          ]}
        />

        {report.data && report.data.omittedEntities > 0 && (
          <Text size="xs" c="dimmed" mt="xs">
            {t('insights.omittedEntities', { count: report.data.omittedEntities })}
          </Text>
        )}
      </Card>
    </>
  );
}
