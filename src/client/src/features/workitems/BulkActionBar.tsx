import { Badge, Button, Card, Group, Modal, Stack, Text, Textarea } from '@mantine/core';
import { DatePickerInput } from '@mantine/dates';
import {
  IconArrowsExchange,
  IconCalendarEvent,
  IconCheck,
  IconCircleCheck,
  IconSquareCheck,
  IconX,
} from '@tabler/icons-react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { BulkResult } from '../../api/types';
import { UserPicker } from '../../components/pickers';
import { api } from '../../lib/api';
import { notifyError, notifySaved } from '../../lib/notify';
import { keys } from '../../lib/queryKeys';

type Pending = { kind: 'reassign' } | { kind: 'reschedule' } | null;

/**
 * Appears only when something is selected. Every action goes through one endpoint that runs each id
 * through the ordinary single-item command, so a partial result is reported rather than leaving the
 * user to guess which half of their selection went through.
 */
export function BulkActionBar({ ids, onDone }: { ids: string[]; onDone: () => void }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

  const [pending, setPending] = useState<Pending>(null);
  const [ownerUserId, setOwnerUserId] = useState<string | null>(null);
  const [newDueDate, setNewDueDate] = useState<string | null>(null);
  const [note, setNote] = useState('');

  const run = useMutation({
    mutationFn: (body: Parameters<typeof api.workItems.bulk>[0]) => api.workItems.bulk(body),
    onSuccess: async (result: BulkResult) => {
      report(result);

      await Promise.all([
        queryClient.invalidateQueries({ queryKey: keys.workItems.all }),
        queryClient.invalidateQueries({ queryKey: keys.workItems.detail }),
        queryClient.invalidateQueries({ queryKey: keys.reports.all }),
      ]);

      setPending(null);
      onDone();
    },
    onError: notifyError,
  });

  /** Names what failed and why: "28 done, 2 refused" is the honest summary, not "done". */
  const report = (result: BulkResult) => {
    if (result.failed.length === 0) {
      notifySaved(t('bulk.allSucceeded', { count: result.succeeded.length }));
      return;
    }

    notifyError(
      new Error(
        t('bulk.partial', {
          succeeded: result.succeeded.length,
          failed: result.failed.length,
          reason: result.failed[0].error,
        }),
      ),
    );
  };

  if (ids.length === 0) return null;

  return (
    <>
      {/* The bar only exists while a selection does, so it may as well say so in the primary colour. */}
      <Card withBorder mb="sm" padding="xs" bg="var(--mantine-color-everdue-light)">
        <Group justify="space-between" wrap="wrap" gap="xs">
          <Group gap={8} wrap="nowrap">
            <IconSquareCheck size={18} style={{ color: 'var(--mantine-primary-color-filled)' }} />
            <Text size="sm" fw={500}>
              {t('bulk.selected', { count: ids.length })}
            </Text>
            <Badge size="sm" variant="filled">
              {ids.length}
            </Badge>
          </Group>

          <Group gap="xs">
            <Button
              size="xs"
              color="teal"
              leftSection={<IconCircleCheck size={14} />}
              onClick={() => run.mutate({ ids, action: 'Complete' })}
              loading={run.isPending}
            >
              {t('workItem.complete')}
            </Button>
            <Button
              size="xs"
              variant="light"
              leftSection={<IconArrowsExchange size={14} />}
              onClick={() => setPending({ kind: 'reassign' })}
            >
              {t('bulk.reassign')}
            </Button>
            <Button
              size="xs"
              variant="light"
              color="grape"
              leftSection={<IconCalendarEvent size={14} />}
              onClick={() => setPending({ kind: 'reschedule' })}
            >
              {t('workItem.reschedule')}
            </Button>
            <Button size="xs" variant="subtle" color="gray" leftSection={<IconX size={14} />} onClick={onDone}>
              {t('common.clear')}
            </Button>
          </Group>
        </Group>
      </Card>

      <Modal opened={pending?.kind === 'reassign'} onClose={() => setPending(null)} title={t('bulk.reassign')} centered>
        <Stack>
          <UserPicker value={ownerUserId} onChange={setOwnerUserId} clearable={false} />
          <Group justify="flex-end">
            <Button variant="default" onClick={() => setPending(null)}>
              {t('common.cancel')}
            </Button>
            <Button
              disabled={!ownerUserId}
              loading={run.isPending}
              leftSection={<IconCheck size={16} />}
              onClick={() => run.mutate({ ids, action: 'Reassign', ownerUserId })}
            >
              {t('common.confirm')}
            </Button>
          </Group>
        </Stack>
      </Modal>

      <Modal opened={pending?.kind === 'reschedule'} onClose={() => setPending(null)} title={t('workItem.reschedule')} centered>
        <Stack>
          <DatePickerInput label={t('workItem.newDueDate')} value={newDueDate} onChange={setNewDueDate} />
          <Textarea label={t('workItem.note')} autosize minRows={2} value={note} onChange={(e) => setNote(e.currentTarget.value)} />

          <Group justify="flex-end">
            <Button variant="default" onClick={() => setPending(null)}>
              {t('common.cancel')}
            </Button>
            <Button
              disabled={!newDueDate}
              loading={run.isPending}
              leftSection={<IconCheck size={16} />}
              onClick={() =>
                run.mutate({
                  ids,
                  action: 'Reschedule',
                  newDueDate: new Date(`${newDueDate}T23:59:59`).toISOString(),
                  note: note.trim() || null,
                })
              }
            >
              {t('common.confirm')}
            </Button>
          </Group>
        </Stack>
      </Modal>
    </>
  );
}
