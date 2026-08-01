import { ActionIcon, Badge, Button, Checkbox, Group, Stack, Text, TextInput } from '@mantine/core';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { IconPlus, IconTrash } from '@tabler/icons-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '../../lib/api';
import { formatDateTime } from '../../lib/format';
import { notifyError } from '../../lib/notify';
import { keys } from '../../lib/queryKeys';

/**
 * The checklist on one work item.
 *
 * Ticking a line writes no event — the row's own who-and-when is the record, and fifteen events per inspection
 * would bury the status history the timeline exists for. Required lines come from the responsibility's template
 * and cannot be deleted from a single occurrence; ad-hoc lines can, and are never required.
 */
export function ChecklistPanel({ workItemId, editable }: { workItemId: string; editable: boolean }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [text, setText] = useState('');

  const checklist = useQuery({
    queryKey: keys.checklists.forItem(workItemId),
    queryFn: () => api.checklists.forItem(workItemId),
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: keys.checklists.forItem(workItemId) });

    // The completion gate and the progress badge both live outside this panel.
    await queryClient.invalidateQueries({ queryKey: keys.workItems.detail });
    await queryClient.invalidateQueries({ queryKey: keys.workItems.all });
  };

  const setChecked = useMutation({
    mutationFn: ({ id, checked }: { id: string; checked: boolean }) =>
      api.checklists.setChecked(workItemId, id, checked),
    onSuccess: refresh,
    onError: notifyError,
  });

  const add = useMutation({
    mutationFn: (value: string) => api.checklists.add(workItemId, value),
    onSuccess: async () => {
      setText('');
      await refresh();
    },
    onError: notifyError,
  });

  const remove = useMutation({
    mutationFn: (id: string) => api.checklists.remove(workItemId, id),
    onSuccess: refresh,
    onError: notifyError,
  });

  const items = checklist.data ?? [];

  if (items.length === 0 && !editable) {
    return (
      <Text size="sm" c="dimmed">
        {t('checklist.empty')}
      </Text>
    );
  }

  return (
    <Stack gap="xs">
      {items.map((item) => (
        <Group key={item.id} justify="space-between" wrap="nowrap" gap="xs">
          <Checkbox
            checked={Boolean(item.checkedAt)}
            disabled={!editable || setChecked.isPending}
            onChange={(event) => setChecked.mutate({ id: item.id, checked: event.currentTarget.checked })}
            label={
              <Group gap={6} wrap="wrap">
                <Text size="sm" td={item.checkedAt ? 'line-through' : undefined} c={item.checkedAt ? 'dimmed' : undefined}>
                  {item.text}
                </Text>
                {item.required && (
                  <Badge size="xs" variant="light" color="orange">
                    {t('checklist.required')}
                  </Badge>
                )}
                {item.checkedAt && (
                  <Text size="xs" c="dimmed">
                    {t('checklist.checkedBy', {
                      name: item.checkedByDisplayName ?? '—',
                      when: formatDateTime(item.checkedAt),
                    })}
                  </Text>
                )}
              </Group>
            }
          />

          {editable && !item.required && (
            <ActionIcon
              variant="subtle"
              color="red"
              aria-label={t('common.delete')}
              onClick={() => remove.mutate(item.id)}
              loading={remove.isPending}
            >
              <IconTrash size={16} />
            </ActionIcon>
          )}
        </Group>
      ))}

      {editable && (
        <Group gap="xs" wrap="nowrap" align="flex-start">
          <TextInput
            style={{ flex: 1 }}
            size="xs"
            placeholder={t('checklist.addPlaceholder')}
            value={text}
            maxLength={300}
            onChange={(event) => setText(event.currentTarget.value)}
            onKeyDown={(event) => {
              if (event.key === 'Enter' && text.trim().length > 0) {
                event.preventDefault();
                add.mutate(text.trim());
              }
            }}
          />
          <Button
            size="xs"
            variant="light"
            leftSection={<IconPlus size={14} />}
            disabled={text.trim().length === 0}
            loading={add.isPending}
            onClick={() => add.mutate(text.trim())}
          >
            {t('common.add')}
          </Button>
        </Group>
      )}
    </Stack>
  );
}
