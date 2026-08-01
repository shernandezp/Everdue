import {
  Button,
  Chip,
  Divider,
  Group,
  Modal,
  NumberInput,
  Select,
  Stack,
  Switch,
  Text,
  Textarea,
  TextInput,
} from '@mantine/core';
import { DatePickerInput } from '@mantine/dates';
import { IconDeviceFloppy } from '@tabler/icons-react';
import { useForm } from '@mantine/form';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { RECURRENCE_KINDS, type RecurrenceKind, type ResponsibilityDto } from '../../api/types';
import { DepartmentPicker, EntityPicker, UserPicker } from '../../components/pickers';
import { api } from '../../lib/api';
import { toDateInputValue } from '../../lib/format';
import { notifyError, notifySaved } from '../../lib/notify';
import { keys } from '../../lib/queryKeys';
import { ChecklistTemplateEditor, type TemplateLine } from './ChecklistTemplateEditor';
import { CompletionRulesFields } from './CompletionRulesFields';

const WEEKDAYS = [0, 1, 2, 3, 4, 5, 6];

/**
 * Mirrors `Checklists:MaxItemsPerTemplate`. The server is the limit that matters; this one only stops the form
 * offering an "add" that would be refused.
 */
const MAX_TEMPLATE_ITEMS = 50;

/**
 * Create and edit a responsibility, including its checklist template and the two completion rules.
 *
 * Its own file rather than another 250 lines on the list page: the recurrence fields, the template editor and the
 * completion rules are three separate concerns that happen to share one form.
 */
export function ResponsibilityModal({
  responsibility,
  opened,
  onClose,
  onSaved,
}: {
  responsibility: ResponsibilityDto | null;
  opened: boolean;
  onClose: () => void;
  onSaved: () => Promise<unknown>;
}) {
  const { t } = useTranslation();

  // Loaded alongside the responsibility rather than folded into its DTO: the template is a list, and the list page
  // only needs to know how long it is.
  const template = useQuery({
    queryKey: keys.checklists.template(responsibility?.id ?? null),
    queryFn: () => api.checklists.template(responsibility!.id),
    enabled: opened && responsibility !== null,
  });

  const [checklist, setChecklist] = useState<TemplateLine[] | null>(null);

  // The fetched template becomes the form's value the first time it arrives; after that the local edit wins.
  const lines: TemplateLine[] =
    checklist ?? (template.data ?? []).map((item) => ({ text: item.text, required: item.required }));

  const form = useForm({
    initialValues: {
      title: responsibility?.title ?? '',
      description: responsibility?.description ?? '',
      ownerUserId: responsibility?.ownerUserId ?? null,
      departmentId: responsibility?.departmentId ?? null,
      entityId: responsibility?.entityId ?? null,
      recurrenceKind: (responsibility?.recurrenceKind ?? 'WeeklyOnDays') as RecurrenceKind,
      daysOfWeekMask: responsibility?.daysOfWeekMask ?? 0,
      dayOfMonth: responsibility?.dayOfMonth ?? 1,
      monthOfYear: responsibility?.monthOfYear ?? 1,
      startDate: responsibility?.startDate ?? toDateInputValue(new Date()),
      active: responsibility?.active ?? true,
      requireChecklistToComplete: responsibility?.requireChecklistToComplete ?? false,
      requireAttachmentToComplete: responsibility?.requireAttachmentToComplete ?? false,
    },
    validate: {
      title: (value) => (value.trim().length === 0 ? t('common.required') : null),
      ownerUserId: (value) => (value ? null : t('common.required')),
      daysOfWeekMask: (value, values) =>
        values.recurrenceKind === 'WeeklyOnDays' && value === 0 ? t('common.required') : null,
      startDate: (value) => (value ? null : t('common.required')),
    },
  });

  const save = useMutation({
    mutationFn: async (values: typeof form.values) => {
      const usable = lines.filter((line) => line.text.trim().length > 0);

      const body = {
        title: values.title.trim(),
        description: values.description.trim() === '' ? null : values.description.trim(),
        ownerUserId: values.ownerUserId,
        departmentId: values.departmentId,
        entityId: values.entityId,
        recurrenceKind: values.recurrenceKind,
        daysOfWeekMask: values.recurrenceKind === 'WeeklyOnDays' ? values.daysOfWeekMask : null,
        dayOfMonth:
          values.recurrenceKind === 'MonthlyOnDay' || values.recurrenceKind === 'Yearly' ? values.dayOfMonth : null,
        monthOfYear: values.recurrenceKind === 'Yearly' ? values.monthOfYear : null,
        startDate: values.startDate,

        // The rule cannot be on without steps to enforce, so the switch and the list agree here rather than the
        // server having to guess which of the two the user meant.
        requireChecklistToComplete: values.requireChecklistToComplete && usable.length > 0,
        requireAttachmentToComplete: values.requireAttachmentToComplete,
      };

      const saved = responsibility
        ? await api.responsibilities.update(responsibility.id, { ...body, active: values.active })
        : await api.responsibilities.create(body);

      // Second call on purpose: the template is its own resource, and the responsibility has to exist before it can
      // have one. Nothing is lost if this fails — the responsibility saved, and the form reports the error.
      await api.checklists.saveTemplate(
        saved.id,
        usable.map((line) => ({ text: line.text.trim(), required: line.required })),
      );

      return saved;
    },
    onSuccess: async () => {
      await onSaved();
      notifySaved();
      setChecklist(null);
      onClose();
    },
    onError: notifyError,
  });

  const toggleDay = (day: number) => {
    const mask = form.values.daysOfWeekMask ^ (1 << day);
    form.setFieldValue('daysOfWeekMask', mask);
  };

  return (
    <Modal
      opened={opened}
      onClose={onClose}
      title={responsibility ? t('responsibility.edit') : t('responsibility.new')}
      centered
      size="lg"
      key={responsibility?.id ?? 'new'}
    >
      <form onSubmit={form.onSubmit((values) => save.mutate(values))}>
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

          <Select
            label={t('recurrence.kind')}
            data={RECURRENCE_KINDS.map((kind) => ({ value: kind, label: t(`recurrence.${kind}`) }))}
            allowDeselect={false}
            {...form.getInputProps('recurrenceKind')}
          />

          {form.values.recurrenceKind === 'WeeklyOnDays' && (
            <Stack gap={4}>
              <Text size="sm" fw={500}>
                {t('recurrence.weekdays')}
              </Text>
              <Group gap={6}>
                {WEEKDAYS.map((day) => (
                  <Chip
                    key={day}
                    checked={(form.values.daysOfWeekMask & (1 << day)) !== 0}
                    onChange={() => toggleDay(day)}
                    size="sm"
                  >
                    {t(`weekday.${day}`)}
                  </Chip>
                ))}
              </Group>
              {form.errors.daysOfWeekMask && (
                <Text size="xs" c="red">
                  {form.errors.daysOfWeekMask}
                </Text>
              )}
            </Stack>
          )}

          {(form.values.recurrenceKind === 'MonthlyOnDay' || form.values.recurrenceKind === 'Yearly') && (
            <Group grow>
              <NumberInput
                label={t('recurrence.dayOfMonth')}
                min={1}
                max={31}
                description={t('recurrence.clampNote')}
                {...form.getInputProps('dayOfMonth')}
              />
              {form.values.recurrenceKind === 'Yearly' && (
                <NumberInput label={t('recurrence.monthOfYear')} min={1} max={12} {...form.getInputProps('monthOfYear')} />
              )}
            </Group>
          )}

          <DatePickerInput
            label={t('recurrence.startDate')}
            value={form.values.startDate}
            onChange={(value) => form.setFieldValue('startDate', value ?? '')}
            error={form.errors.startDate as string | undefined}
          />

          <Divider />

          <ChecklistTemplateEditor items={lines} onChange={setChecklist} max={MAX_TEMPLATE_ITEMS} />

          <CompletionRulesFields
            requireChecklist={form.values.requireChecklistToComplete}
            requireAttachment={form.values.requireAttachmentToComplete}
            hasChecklistItems={lines.some((line) => line.text.trim().length > 0)}
            onChange={(patch) => {
              if (patch.requireChecklist !== undefined) {
                form.setFieldValue('requireChecklistToComplete', patch.requireChecklist);
              }

              if (patch.requireAttachment !== undefined) {
                form.setFieldValue('requireAttachmentToComplete', patch.requireAttachment);
              }
            }}
          />

          {responsibility && (
            <Switch
              label={t('common.active')}
              checked={form.values.active}
              onChange={(event) => form.setFieldValue('active', event.currentTarget.checked)}
            />
          )}

          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>
              {t('common.cancel')}
            </Button>
            <Button type="submit" loading={save.isPending} leftSection={<IconDeviceFloppy size={16} />}>
              {t('common.save')}
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
