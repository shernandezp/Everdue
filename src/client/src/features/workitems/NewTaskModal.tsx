import { Button, Group, Modal, Stack, Textarea, TextInput } from '@mantine/core';
import { IconPlus } from '@tabler/icons-react';
import { DatePickerInput } from '@mantine/dates';
import { useForm } from '@mantine/form';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { DepartmentPicker, EntityPicker, UserPicker } from '../../components/pickers';
import { api } from '../../lib/api';
import { endOfLocalDay, toDateInputValue } from '../../lib/format';
import { notifyError, notifySaved } from '../../lib/notify';
import { useSession } from '../auth/session';
import { keys } from '../../lib/queryKeys';

/** One-off tasks only. Occurrences are engine-created; there is deliberately no way to post one. */
export function NewTaskModal({ opened, onClose }: { opened: boolean; onClose: () => void }) {
  const { t } = useTranslation();
  const { user } = useSession();
  const queryClient = useQueryClient();

  const form = useForm<{
    title: string;
    description: string;
    ownerUserId: string | null;
    entityId: string | null;
    departmentId: string | null;
    dueDate: string | null;
  }>({
    initialValues: {
      title: '',
      description: '',
      ownerUserId: user?.id ?? null,
      entityId: null,
      departmentId: null,
      dueDate: toDateInputValue(new Date()),
    },
    validate: {
      title: (value) => (value.trim().length === 0 ? t('common.required') : null),
      ownerUserId: (value) => (value ? null : t('common.required')),
      dueDate: (value) => (value ? null : t('common.required')),
    },
  });

  const create = useMutation({
    mutationFn: (values: typeof form.values) =>
      api.workItems.create({
        title: values.title.trim(),
        description: values.description.trim() === '' ? null : values.description.trim(),
        ownerUserId: values.ownerUserId!,
        entityId: values.entityId,
        departmentId: values.departmentId,
        // A one-off is due at the end of the chosen local day, matching how occurrences read.
        dueDate: endOfLocalDay(new Date(`${values.dueDate!}T00:00:00`)),
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: keys.workItems.all });
      notifySaved();
      form.reset();
      onClose();
    },
    onError: notifyError,
  });

  return (
    <Modal opened={opened} onClose={onClose} title={t('workItem.newTaskTitle')} centered size="lg">
      <form onSubmit={form.onSubmit((values) => create.mutate(values))}>
        <Stack>
          <TextInput label={t('workItem.title')} data-autofocus {...form.getInputProps('title')} />
          <Textarea label={t('workItem.description')} autosize minRows={2} {...form.getInputProps('description')} />

          <UserPicker
            required
            value={form.values.ownerUserId}
            onChange={(value) => form.setFieldValue('ownerUserId', value)}
            error={form.errors.ownerUserId as string | undefined}
          />

          <Group grow align="flex-start">
            <EntityPicker value={form.values.entityId} onChange={(value) => form.setFieldValue('entityId', value)} />
            <DepartmentPicker
              value={form.values.departmentId}
              onChange={(value) => form.setFieldValue('departmentId', value)}
            />
          </Group>

          <DatePickerInput
            label={t('workItem.dueDate')}
            value={form.values.dueDate}
            onChange={(value) => form.setFieldValue('dueDate', value)}
            error={form.errors.dueDate as string | undefined}
          />

          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>
              {t('common.cancel')}
            </Button>
            <Button type="submit" loading={create.isPending} leftSection={<IconPlus size={16} />}>
              {t('common.create')}
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
