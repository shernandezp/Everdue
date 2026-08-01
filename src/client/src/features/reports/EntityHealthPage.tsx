import { Group, TextInput } from '@mantine/core';
import { useDebouncedValue } from '@mantine/hooks';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { EntityHealthRow } from '../../api/types';
import { ExportCsvButton } from '../../components/ExportCsvButton';
import { PageHeader } from '../../components/PageHeader';
import { ReportTable } from '../../components/ReportTable';
import { TruncationNotice } from '../../components/TruncationNotice';
import {
  countColumn,
  drillThroughColumn,
  entityNameColumn,
  entityTypeColumn,
  lastActivityColumn,
  nullableCountColumn,
} from '../../components/reportColumns';
import { api, type ReportFilters } from '../../lib/api';
import { keys } from '../../lib/queryKeys';
import { useServerSort } from '../../lib/useServerSort';
import { ReportFilterBar } from './ReportFilterBar';

const SORT_NAMES: Record<string, string> = {
  entityName: 'Name',
  open: 'Open',
  overdue: 'Overdue',
  missed30: 'Missed30',
  missed60: 'Missed60',
  missed90: 'Missed90',
  onHold: 'OnHold',
  daysSinceLastActivity: 'DaysSinceLastActivity',
};

/** The primary customer-service screen. Sorting is server-side on every column, including derived ones. */
export function EntityHealthPage() {
  const { t } = useTranslation();
  const [filters, setFilters] = useState<ReportFilters>({});
  const [search, setSearch] = useState('');
  // Debounced so typing costs one request, not one per keystroke.
  const [debouncedSearch] = useDebouncedValue(search, 250);
  const { sort, setSort, params } = useServerSort<EntityHealthRow>(SORT_NAMES, {
    column: 'entityName',
    direction: 'asc',
  });

  const report = useQuery({
    queryKey: keys.reports.entityHealth(filters, debouncedSearch, sort),
    queryFn: () => api.reports.entityHealth({ ...filters, search: debouncedSearch || undefined, ...params }),
  });

  return (
    <>
      <PageHeader
        title={t('reports.entityHealthTitle')}
        actions={<ExportCsvButton href={api.exports.report('entity-health', filters)} />}
      />
      <ReportFilterBar value={filters} onChange={setFilters} />

      <Group mb="sm">
        <TextInput
          placeholder={t('common.search')}
          value={search}
          onChange={(event) => setSearch(event.currentTarget.value)}
          w={240}
        />
      </Group>

      <ReportTable
        fetching={report.isFetching}
        records={report.data?.items ?? []}
        idAccessor="entityId"
        sort={sort}
        onSortChange={setSort}
        columns={[
          { ...entityNameColumn(t), sortable: true },
          entityTypeColumn(t),
          countColumn('open', t('reports.open'), true),
          countColumn('overdue', t('reports.overdue'), true),
          countColumn('missed30', t('reports.missed30'), true),
          countColumn('missed60', t('reports.missed60'), true),
          countColumn('missed90', t('reports.missed90'), true),
          countColumn('onHold', t('reports.onHold'), true),
          lastActivityColumn(t, t('reports.lastActivity')),
          nullableCountColumn('daysSinceLastActivity', t('reports.daysSince'), true),
          drillThroughColumn(t),
        ]}
      />

      <TruncationNotice
        shown={report.data?.items.length ?? 0}
        total={report.data?.totalCount ?? 0}
      />
    </>
  );
}
