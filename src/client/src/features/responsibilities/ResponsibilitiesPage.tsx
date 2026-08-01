import { ActionIcon, Badge, Button, Group, Modal, Stack, Text } from '@mantine/core';
import { DatePickerInput } from '@mantine/dates';
import {
  IconArrowsExchange,
  IconHistory,
  IconListCheck,
  IconPencil,
  IconPlayerPause,
  IconPlayerPlay,
  IconPlus,
  IconTrash,
} from '@tabler/icons-react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { DataTable } from 'mantine-datatable';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { ResponsibilityDto } from '../../api/types';
import { PageHeader } from '../../components/PageHeader';
import { TruncationNotice } from '../../components/TruncationNotice';
import { api } from '../../lib/api';
import { formatDate, toDateInputValue } from '../../lib/format';
import { notifyError, notifySaved } from '../../lib/notify';
import { ReassignResponsibilityModal } from './ReassignResponsibilityModal';
import { ResponsibilityHistoryModal } from './ResponsibilityHistoryModal';
import { ResponsibilityModal } from './ResponsibilityModal';
import { keys } from '../../lib/queryKeys';

const WEEKDAYS = [0, 1, 2, 3, 4, 5, 6];

type TranslateFn = ReturnType<typeof useTranslation>['t'];

export function ResponsibilitiesPage() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState<ResponsibilityDto | null>(null);
  const [creating, setCreating] = useState(false);
  const [pausing, setPausing] = useState<ResponsibilityDto | null>(null);
  const [reassigning, setReassigning] = useState<ResponsibilityDto | null>(null);
  const [historyFor, setHistoryFor] = useState<ResponsibilityDto | null>(null);

  const list = useQuery({
    queryKey: keys.responsibilities.all,
    queryFn: () => api.responsibilities.list({ includeInactive: true }),
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: keys.responsibilities.all });

  const mutate = (fn: (id: string) => Promise<unknown>) =>
    async function run(id: string) {
      try {
        await fn(id);
        await invalidate();
        notifySaved();
      } catch (error) {
        notifyError(error);
      }
    };

  const resume = mutate((id) => api.responsibilities.resume(id));
  const deactivate = mutate((id) => api.responsibilities.deactivate(id));

  return (
    <>
      <PageHeader
        title={t('responsibility.title')}
        actions={
          <Button leftSection={<IconPlus size={16} />} onClick={() => setCreating(true)}>
            {t('responsibility.new')}
          </Button>
        }
      />

      <DataTable
        highlightOnHover
        withTableBorder
        minHeight={200}
        fetching={list.isFetching}
        records={list.data?.items ?? []}
        idAccessor="id"
        noRecordsText={t('responsibility.empty')}
        columns={[
          {
            accessor: 'title',
            title: t('workItem.title'),
            render: (row) => (
              <Stack gap={2}>
                <Text size="sm" fw={500}>
                  {row.title}
                </Text>
                <Group gap={4}>
                  {row.pausedUntil && new Date(row.pausedUntil) > new Date() && (
                    <Badge size="xs" color="orange" variant="light">
                      {t('responsibility.paused', { date: formatDate(row.pausedUntil) })}
                    </Badge>
                  )}

                  {/* Which responsibilities carry a checklist, and which of those actually enforce it. */}
                  {row.checklistItemCount > 0 && (
                    <Badge size="xs" variant="light" leftSection={<IconListCheck size={11} />}>
                      {t('responsibility.checklistItems', { count: row.checklistItemCount })}
                    </Badge>
                  )}
                  {row.requireChecklistToComplete && (
                    <Badge size="xs" variant="light" color="teal">
                      {t('responsibility.gateChecklist')}
                    </Badge>
                  )}
                  {row.requireAttachmentToComplete && (
                    <Badge size="xs" variant="light" color="teal">
                      {t('responsibility.gateProof')}
                    </Badge>
                  )}
                </Group>
              </Stack>
            ),
          },
          { accessor: 'recurrence', title: t('recurrence.kind'), render: (row) => describeRecurrence(row, t) },
          { accessor: 'ownerDisplayName', title: t('workItem.owner') },
          {
            accessor: 'entityName',
            title: t('workItem.entity'),
            render: (row) => row.entityName ?? t('common.none'),
          },
          {
            accessor: 'nextScheduledDate',
            title: t('recurrence.nextDate'),
            render: (row) => (row.nextScheduledDate ? formatDate(row.nextScheduledDate) : t('common.none')),
          },
          {
            accessor: 'active',
            title: t('common.status'),
            render: (row) => (row.active ? t('common.active') : t('common.inactive')),
          },
          {
            accessor: 'actions',
            title: '',
            textAlign: 'right',
            render: (row) => (
              <Group gap={4} justify="flex-end">
                <ActionIcon variant="subtle" aria-label={t('common.edit')} onClick={() => setEditing(row)}>
                  <IconPencil size={16} />
                </ActionIcon>

                {/* Future occurrences follow the owner automatically; existing ones on request. */}
                <ActionIcon variant="subtle" aria-label={t('reassign.responsibility')} onClick={() => setReassigning(row)}>
                  <IconArrowsExchange size={16} />
                </ActionIcon>

                <ActionIcon variant="subtle" aria-label={t('respHistory.open')} onClick={() => setHistoryFor(row)}>
                  <IconHistory size={16} />
                </ActionIcon>

                {row.pausedUntil && new Date(row.pausedUntil) > new Date() ? (
                  <ActionIcon variant="subtle" aria-label={t('responsibility.resume')} onClick={() => void resume(row.id)}>
                    <IconPlayerPlay size={16} />
                  </ActionIcon>
                ) : (
                  <ActionIcon variant="subtle" aria-label={t('responsibility.pause')} onClick={() => setPausing(row)}>
                    <IconPlayerPause size={16} />
                  </ActionIcon>
                )}

                {row.active && (
                  <ActionIcon
                    variant="subtle"
                    color="red"
                    aria-label={t('common.deactivate')}
                    onClick={() => {
                      if (window.confirm(t('responsibility.deactivateConfirm'))) {
                        void deactivate(row.id);
                      }
                    }}
                  >
                    <IconTrash size={16} />
                  </ActionIcon>
                )}
              </Group>
            ),
          },
        ]}
      />

      <TruncationNotice shown={list.data?.items.length ?? 0} total={list.data?.totalCount ?? 0} />

      <ResponsibilityModal
        responsibility={editing}
        opened={creating || editing !== null}
        onClose={() => {
          setCreating(false);
          setEditing(null);
        }}
        onSaved={invalidate}
      />

      <PauseModal responsibility={pausing} onClose={() => setPausing(null)} onSaved={invalidate} />

      <ReassignResponsibilityModal
        responsibility={reassigning}
        opened={reassigning !== null}
        onClose={() => setReassigning(null)}
      />

      <ResponsibilityHistoryModal responsibility={historyFor} onClose={() => setHistoryFor(null)} />
    </>
  );
}

function describeRecurrence(row: ResponsibilityDto, t: TranslateFn): string {
  switch (row.recurrenceKind) {
    case 'WeeklyOnDays': {
      const days = WEEKDAYS.filter((day) => ((row.daysOfWeekMask ?? 0) & (1 << day)) !== 0)
        .map((day) => t(`weekday.${day}`))
        .join(', ');
      return `${t('recurrence.WeeklyOnDays')} · ${days}`;
    }
    case 'MonthlyOnDay':
      return `${t('recurrence.MonthlyOnDay')} · ${row.dayOfMonth}`;
    case 'Yearly':
      return `${t('recurrence.Yearly')} · ${row.dayOfMonth}/${row.monthOfYear}`;
    default:
      return t('recurrence.Daily');
  }
}

function PauseModal({
  responsibility,
  onClose,
  onSaved,
}: {
  responsibility: ResponsibilityDto | null;
  onClose: () => void;
  onSaved: () => Promise<unknown>;
}) {
  const { t } = useTranslation();
  const [until, setUntil] = useState<string | null>(toDateInputValue(new Date()));

  const pause = useMutation({
    mutationFn: () => api.responsibilities.pause(responsibility!.id, until!),
    onSuccess: async () => {
      await onSaved();
      notifySaved();
      onClose();
    },
    onError: notifyError,
  });

  return (
    <Modal opened={responsibility !== null} onClose={onClose} title={t('responsibility.pause')} centered>
      <Stack>
        <Text size="sm" c="dimmed">
          {t('responsibility.pauseHint')}
        </Text>
        <DatePickerInput label={t('responsibility.pauseUntil')} value={until} onChange={setUntil} />
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>
            {t('common.cancel')}
          </Button>
          <Button
            color="orange"
            loading={pause.isPending}
            disabled={!until}
            leftSection={<IconPlayerPause size={16} />}
            onClick={() => pause.mutate()}
          >
            {t('common.confirm')}
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}
