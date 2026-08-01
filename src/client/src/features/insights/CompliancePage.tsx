import { Anchor, Badge, Group } from '@mantine/core';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import type { ComplianceRow } from '../../api/types';
import { ExportCsvButton } from '../../components/ExportCsvButton';
import { PageHeader } from '../../components/PageHeader';
import { ReportTable } from '../../components/ReportTable';
import { TruncationNotice } from '../../components/TruncationNotice';
import { countColumn, drillThroughColumn } from '../../components/reportColumns';
import { api, type ReportFilters } from '../../lib/api';
import { EM_DASH } from '../../lib/format';
import { keys } from '../../lib/queryKeys';
import { routes } from '../../lib/routes';
import { useServerSort } from '../../lib/useServerSort';
import { ReportFilterBar } from '../reports/ReportFilterBar';
import { InsightsWindowBar, type InsightWindow } from './InsightsWindowBar';
import { RateCell } from './RateCell';
import { TrendSparkline } from './TrendSparkline';

const SORT_NAMES: Record<string, string> = {
  title: 'Title',
  onTime: 'OnTime',
  late: 'Late',
  missed: 'Missed',
  concluded: 'Concluded',
  rate: 'Rate',
};

/**
 * The "Week 29 ✅ Week 30 ❌" series as a rate, per responsibility. A table, not a chart — the sparkline
 * carries the shape and the numbers carry the meaning.
 */
export function CompliancePage() {
  const { t } = useTranslation();
  const [filters, setFilters] = useState<ReportFilters>({});
  const [window, setWindow] = useState<InsightWindow>({ bucket: 'Week', buckets: 12 });
  const { sort, setSort, params } = useServerSort<ComplianceRow>(SORT_NAMES, { column: 'missed' });

  const query = { ...filters, ...window };
  const report = useQuery({
    queryKey: keys.insights.compliance(query, sort),
    queryFn: () => api.insights.compliance({ ...query, ...params }),
  });

  return (
    <>
      <PageHeader
        title={t('insights.complianceTitle')}
        description={t('insights.complianceIntro')}
        actions={<ExportCsvButton href={api.exports.insight('compliance', { ...filters, ...window })} />}
      />
      <ReportFilterBar value={filters} onChange={setFilters} />
      <InsightsWindowBar value={window} onChange={setWindow} />

      <ReportTable
        fetching={report.isFetching}
        records={report.data?.items ?? []}
        idAccessor="responsibilityId"
        emptyText={t('insights.complianceEmpty')}
        sort={sort}
        onSortChange={setSort}
        columns={[
          {
            accessor: 'title',
            title: t('insights.responsibility'),
            sortable: true,
            render: (row) => (
              <Group gap="xs" wrap="nowrap">
                <Anchor component={Link} to={routes.responsibilityCompliance(row.responsibilityId)} size="sm">
                  {row.title}
                </Anchor>

                {/* History is still reported for these; the badge is what stops somebody chasing them. */}
                {!row.active && (
                  <Badge color="gray" variant="light" size="xs">
                    {t('insights.inactive')}
                  </Badge>
                )}
                {row.active && row.paused && (
                  <Badge color="orange" variant="light" size="xs">
                    {t('insights.paused')}
                  </Badge>
                )}
              </Group>
            ),
          },
          { accessor: 'ownerName', title: t('insights.owner') },
          { accessor: 'entityName', title: t('workItem.entity'), render: (row) => row.entityName ?? EM_DASH },
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
          countColumn('inFlight', t('insights.inFlight')),
          {
            accessor: 'trend',
            title: t('insights.trend'),
            render: (row) => <TrendSparkline points={row.trend} />,
          },
          drillThroughColumn(t),
        ]}
      />

      <TruncationNotice shown={report.data?.items.length ?? 0} total={report.data?.totalCount ?? 0} />
    </>
  );
}
