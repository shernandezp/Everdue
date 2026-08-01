import { Alert } from '@mantine/core';
import { IconInfoCircle } from '@tabler/icons-react';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { ReliabilityRow } from '../../api/types';
import { ExportCsvButton } from '../../components/ExportCsvButton';
import { PageHeader } from '../../components/PageHeader';
import { ReportTable } from '../../components/ReportTable';
import { countColumn, daysColumn, drillThroughColumn } from '../../components/reportColumns';
import { api, type ReportFilters } from '../../lib/api';
import { keys } from '../../lib/queryKeys';
import { useServerSort } from '../../lib/useServerSort';
import { ReportFilterBar } from '../reports/ReportFilterBar';
import { InsightsWindowBar, type InsightWindow } from './InsightsWindowBar';
import { RateCell } from './RateCell';

const SORT_NAMES: Record<string, string> = {
  displayName: 'Name',
  onTime: 'OnTime',
  late: 'Late',
  missed: 'Missed',
  concluded: 'Concluded',
  rate: 'Rate',
  externallyBlocked: 'ExternallyBlocked',
  blockedDays: 'BlockedDays',
};

/**
 * The one screen that measures people rather than work, so the rules are on the screen: no rank, no
 * target, volume beside every rate, and the external waits in the same row as the misses. It is here to
 * decide where to help, not to sort a team.
 */
export function ReliabilityPage() {
  const { t } = useTranslation();
  const [filters, setFilters] = useState<ReportFilters>({});
  const [window, setWindow] = useState<InsightWindow>({ bucket: 'Week', buckets: 12 });
  const { sort, setSort, params } = useServerSort<ReliabilityRow>(SORT_NAMES, { column: 'missed' });

  const query = { ...filters, ...window };
  const report = useQuery({
    queryKey: keys.insights.reliability(query, sort),
    queryFn: () => api.insights.reliability({ ...query, ...params }),
  });

  return (
    <>
      <PageHeader
        title={t('insights.reliabilityTitle')}
        description={t('insights.reliabilityIntro')}
        actions={<ExportCsvButton href={api.exports.insight('reliability', { ...filters, ...window })} />}
      />

      <Alert variant="light" color="gray" icon={<IconInfoCircle size={18} />} mb="md">
        {t('insights.reliabilityAttribution')} {t('insights.reliabilityExternal')}
      </Alert>

      <ReportFilterBar value={filters} onChange={setFilters} />
      <InsightsWindowBar value={window} onChange={setWindow} />

      <ReportTable
        fetching={report.isFetching}
        records={report.data ?? []}
        idAccessor="userId"
        emptyText={t('insights.reliabilityEmpty')}
        sort={sort}
        onSortChange={setSort}
        columns={[
          { accessor: 'displayName', title: t('insights.person'), sortable: true },
          {
            accessor: 'rate',
            title: t('insights.rate'),
            sortable: true,
            textAlign: 'right',
            render: (row) => (
              <RateCell
                rate={row.rate}
                suppressed={row.rateSuppressed}
                onTime={row.onTime}
                concluded={row.concluded}
              />
            ),
          },
          countColumn('onTime', t('insights.onTime'), true),
          countColumn('late', t('insights.late'), true),
          countColumn('missed', t('insights.missed'), true),
          countColumn('externallyBlocked', t('insights.externallyBlocked'), true),
          daysColumn<ReliabilityRow>('blockedDays', t('insights.blockedDays'), true),
          countColumn('oneOffCompleted', t('insights.oneOffCompleted')),
          countColumn('handedOverInWindow', t('insights.handedOver')),
          drillThroughColumn(t),
        ]}
      />
    </>
  );
}
