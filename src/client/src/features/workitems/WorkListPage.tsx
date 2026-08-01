import { Button, Card, Group, MultiSelect, Pill, Select, Switch, Text, TextInput, ThemeIcon } from '@mantine/core';
import { useQuery } from '@tanstack/react-query';
import { IconFilter, IconFilterOff, IconPlus, IconSearch } from '@tabler/icons-react';
import { DataTable } from 'mantine-datatable';
import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router-dom';
import { HOLD_REASONS, WORK_ITEM_STATUSES, type WorkItem } from '../../api/types';
import { ExportCsvButton } from '../../components/ExportCsvButton';
import { PageHeader } from '../../components/PageHeader';
import { StatusBadge } from '../../components/StatusBadge';
import { DepartmentPicker, EntityPicker, UserPicker } from '../../components/pickers';
import { formatDate, formatDueDate } from '../../lib/format';
import { api, type WorkItemFilters } from '../../lib/api';
import { keys } from '../../lib/queryKeys';
import { useServerSort } from '../../lib/useServerSort';
import { BulkActionBar } from './BulkActionBar';
import { ChecklistProgress } from './ChecklistProgress';
import { NewTaskModal } from './NewTaskModal';
import { SavedViewsMenu } from './SavedViewsMenu';
import { WorkItemDrawer } from './WorkItemDrawer';
import { useWorkItems } from './hooks';

const PAGE_SIZE = 50;

const SORT_NAMES: Record<string, string> = {
  title: 'Title',
  status: 'Status',
  entityName: 'Entity',
  dueDate: 'DueDate',
};

/**
 * URL parameters a drill-through can set that have no control on the filter bar. Each one renders
 * as a dismissible chip — without them, "Completed today: 43" landed on a list whose date window
 * was invisible, and changing any visible filter silently kept it.
 */
const HIDDEN_FILTER_KEYS = [
  'dueFrom',
  'dueTo',
  'completedFrom',
  'completedTo',
  'responsibilityId',
  'occurrences',
  'entityType',
  'view',
] as const;

/**
 * The list is the drill-through target: every report number links here with the exact filter set
 * that produced it, which is why the filters live in the URL rather than in component state.
 */
export function WorkListPage() {
  const { t } = useTranslation();
  const [params, setParams] = useSearchParams();
  const [openId, setOpenId] = useState<string | null>(null);
  const [newTaskOpen, setNewTaskOpen] = useState(false);
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<WorkItem[]>([]);
  const { sort, setSort, params: sortParams } = useServerSort<WorkItem>(SORT_NAMES, {
    column: 'dueDate',
    direction: 'asc',
  });

  // A notification links straight to the item it is about; opening the drawer is the whole point.
  useEffect(() => {
    const target = params.get('workItemId');
    if (target) setOpenId(target);
  }, [params]);

  const filters = useMemo<WorkItemFilters>(() => {
    const value: WorkItemFilters = { pageSize: PAGE_SIZE, page, ...sortParams };

    for (const [key, raw] of params.entries()) {
      if (raw === '' || key === 'workItemId') continue;
      if (key === 'overdue' || key === 'includeCancelled') {
        (value as Record<string, unknown>)[key] = raw === 'true';
      } else {
        (value as Record<string, unknown>)[key] = raw;
      }
    }

    return value;
  }, [params, page, sortParams.sort, sortParams.descending]);

  const items = useWorkItems(filters);

  // Only to name the responsibility chip: a bare id would tell the reader nothing.
  const chipResponsibilityId = params.get('responsibilityId');
  const chipResponsibility = useQuery({
    queryKey: keys.responsibilities.one(chipResponsibilityId),
    queryFn: () => api.responsibilities.get(chipResponsibilityId!),
    enabled: chipResponsibilityId !== null,
  });

  const hiddenChips = HIDDEN_FILTER_KEYS.flatMap((key) => {
    const raw = params.get(key);
    if (raw === null || raw === '') return [];

    let label: string;
    switch (key) {
      case 'dueFrom':
      case 'dueTo':
      case 'completedFrom':
      case 'completedTo':
        label = t(`list.chip.${key}`, { value: formatDate(raw) });
        break;
      case 'responsibilityId':
        label = t('list.chip.responsibility', { title: chipResponsibility.data?.title ?? '…' });
        break;
      case 'occurrences':
        label = raw === 'true' ? t('list.chip.occurrencesOnly') : t('list.chip.oneOffOnly');
        break;
      case 'entityType':
        label = t('list.chip.entityType', { value: t(`entityType.${raw}`) });
        break;
      case 'view':
        label = t('list.chip.boardView');
        break;
      default:
        label = `${key}: ${raw}`;
    }

    return [{ key, label }];
  });

  const set = (key: string, value: string | boolean | null) => {
    const next = new URLSearchParams(params);
    if (value === null || value === '' || value === false) {
      next.delete(key);
    } else {
      next.set(key, String(value));
    }
    setParams(next, { replace: true });
    setPage(1);
  };

  return (
    <>
      <PageHeader
        title={t('list.title')}
        description={items.data ? t('list.results', { count: items.data.totalCount }) : undefined}
        actions={
          <Group gap="xs">
            <Button leftSection={<IconPlus size={16} />} onClick={() => setNewTaskOpen(true)}>
              {t('board.newTask')}
            </Button>

            {/* Built from the page's own filters, minus paging — an export is the whole result or a refusal. */}
            <ExportCsvButton href={api.exports.workItems({ ...filters, page: undefined, pageSize: undefined })} />

            <SavedViewsMenu
              route="work"
              currentQuery={params.toString()}
              onApply={(queryString) => {
                setParams(new URLSearchParams(queryString), { replace: true });
                setPage(1);
              }}
            />

            {params.size > 0 && (
              <Button
                variant="subtle"
                color="gray"
                leftSection={<IconFilterOff size={16} />}
                onClick={() => setParams(new URLSearchParams(), { replace: true })}
              >
                {t('common.clear')}
              </Button>
            )}
          </Group>
        }
      />

      <Card withBorder mb="md" padding="sm">
        <Group gap="xs" mb="xs">
          <ThemeIcon size="sm" radius="sm" variant="light" color="everdue">
            <IconFilter size={14} />
          </ThemeIcon>
          <Text size="xs" tt="uppercase" fw={700} c="dimmed">
            {t('common.filters')}
          </Text>
        </Group>

        <Group align="flex-end" gap="sm" wrap="wrap">
          <TextInput
            label={t('common.search')}
            leftSection={<IconSearch size={16} />}
            value={params.get('search') ?? ''}
            onChange={(event) => set('search', event.currentTarget.value)}
            w={200}
          />
          <UserPicker
            label={t('reports.filterOwner')}
            clearable
            value={params.get('ownerId')}
            onChange={(value) => set('ownerId', value)}
            w={190}
          />
          <EntityPicker value={params.get('entityId')} onChange={(value) => set('entityId', value)} w={220} />
          <DepartmentPicker
            value={params.get('departmentId')}
            onChange={(value) => set('departmentId', value)}
            w={190}
          />
          {/* The API has always accepted these; they were simply never on the bar. */}
          <MultiSelect
            label={t('common.status')}
            placeholder={t('common.all')}
            data={WORK_ITEM_STATUSES.map((status) => ({ value: status, label: t(`status.${status}`) }))}
            value={params.get('status')?.split(',').filter(Boolean) ?? []}
            onChange={(values) => set('status', values.join(','))}
            clearable
            w={220}
          />

          <Select
            label={t('workItem.holdReason')}
            placeholder={t('common.all')}
            data={HOLD_REASONS.map((reason) => ({ value: reason, label: t(`holdReason.${reason}`) }))}
            value={params.get('holdReason')}
            onChange={(value) => set('holdReason', value)}
            clearable
            w={190}
          />

          <Switch
            label={t('list.overdueOnly')}
            checked={params.get('overdue') === 'true'}
            onChange={(event) => set('overdue', event.currentTarget.checked)}
          />
          <Switch
            label={t('list.showCancelled')}
            checked={params.get('includeCancelled') === 'true'}
            onChange={(event) => set('includeCancelled', event.currentTarget.checked)}
          />
        </Group>

        {/* Filters a drill-through set that have no control above. Dismissible, never invisible. */}
        {hiddenChips.length > 0 && (
          <Group gap={6} mt="xs">
            {hiddenChips.map((chip) => (
              <Pill key={chip.key} withRemoveButton onRemove={() => set(chip.key, null)} size="sm">
                {chip.label}
              </Pill>
            ))}
          </Group>
        )}
      </Card>

      <BulkActionBar ids={selected.map((item) => item.id)} onDone={() => setSelected([])} />

      <DataTable
        highlightOnHover
        withTableBorder
        minHeight={200}
        fetching={items.isFetching}
        records={items.data?.items ?? []}
        idAccessor="id"
        selectedRecords={selected}
        onSelectedRecordsChange={setSelected}
        noRecordsText={t('common.noResults')}
        totalRecords={items.data?.totalCount ?? 0}
        recordsPerPage={PAGE_SIZE}
        page={page}
        onPageChange={setPage}
        onRowClick={({ record }) => setOpenId(record.id)}
        sortStatus={sort}
        onSortStatusChange={setSort}
        columns={[
          {
            accessor: 'title',
            title: t('workItem.title'),
            ellipsis: true,
            sortable: true,
            render: (item) => (
              <Group gap={6} wrap="nowrap">
                <Text size="sm" lineClamp={1}>
                  {item.title}
                </Text>
                <ChecklistProgress checked={item.checklistChecked} total={item.checklistTotal} />
              </Group>
            ),
          },
          {
            accessor: 'status',
            title: t('common.status'),
            sortable: true,
            render: (item) => (
              <StatusBadge
                status={item.status}
                isOverdue={item.isOverdue}
                holdReason={item.holdReason}
                holdReasonText={item.holdReasonText}
                size="xs"
              />
            ),
          },
          {
            accessor: 'entityName',
            title: t('workItem.entity'),
            sortable: true,
            render: (item) => <Text size="sm">{item.entityName ?? t('common.none')}</Text>,
          },
          { accessor: 'ownerDisplayName', title: t('workItem.owner') },
          {
            accessor: 'dueDate',
            title: t('workItem.dueDate'),
            sortable: true,
            render: (item) => (
              <Text size="sm" c={item.isOverdue ? 'red' : undefined}>
                {formatDueDate(item.dueDate, item.responsibilityId !== null)}
              </Text>
            ),
          },
        ]}
      />

      <NewTaskModal opened={newTaskOpen} onClose={() => setNewTaskOpen(false)} />

      <WorkItemDrawer
        id={openId}
        onClose={() => {
          setOpenId(null);

          // A notification's deep link has done its job. Leaving it in the URL would re-open the
          // drawer the next time any filter changed.
          if (params.has('workItemId')) {
            const next = new URLSearchParams(params);
            next.delete('workItemId');
            setParams(next, { replace: true });
          }
        }}
      />
    </>
  );
}
