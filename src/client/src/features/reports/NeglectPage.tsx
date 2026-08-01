import { Group, NumberInput } from '@mantine/core';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ExportCsvButton } from '../../components/ExportCsvButton';
import { PageHeader } from '../../components/PageHeader';
import { ReportTable } from '../../components/ReportTable';
import {
  countColumn,
  drillThroughColumn,
  entityNameColumn,
  entityTypeColumn,
  lastActivityColumn,
  nullableCountColumn,
} from '../../components/reportColumns';
import { api, type ReportFilters } from '../../lib/api';
import { INFINITY_SIGN } from '../../lib/format';
import { keys } from '../../lib/queryKeys';
import { ReportFilterBar } from './ReportFilterBar';

const DEFAULT_DAYS = 90;

/**
 * "Last activity" is the last completed occurrence and nothing else — which is what makes this list
 * trustworthy where a CRM's activity log, polluted by auto-logged e-mail, is not.
 */
export function NeglectPage() {
  const { t } = useTranslation();
  const [filters, setFilters] = useState<ReportFilters>({});
  const [days, setDays] = useState<number>(DEFAULT_DAYS);

  const report = useQuery({
    queryKey: keys.reports.neglect(filters, days),
    queryFn: () => api.reports.neglect({ ...filters, days }),
  });

  return (
    <>
      <PageHeader
        title={t('reports.neglectTitle')}
        actions={<ExportCsvButton href={api.exports.report('neglect', { ...filters, days })} />}
      />
      <ReportFilterBar value={filters} onChange={setFilters} />

      <Group mb="sm">
        <NumberInput
          label={t('reports.neglectDays')}
          min={1}
          max={3650}
          value={days}
          onChange={(value) => setDays(typeof value === 'number' ? value : DEFAULT_DAYS)}
          w={240}
        />
      </Group>

      <ReportTable
        fetching={report.isFetching}
        records={report.data ?? []}
        idAccessor="entityId"
        emptyText={t('reports.neglectEmpty')}
        columns={[
          entityNameColumn(t),
          entityTypeColumn(t),
          lastActivityColumn(t, t('reports.lastActivity')),

          // No last activity means the wait is unbounded, not unknown — hence ∞ rather than a dash.
          nullableCountColumn('daysSinceLastActivity', t('reports.daysSince'), false, INFINITY_SIGN),
          countColumn('openCount', t('reports.open')),
          drillThroughColumn(t),
        ]}
      />
    </>
  );
}
