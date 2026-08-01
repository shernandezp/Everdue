import { ActionIcon, Button, Checkbox, Group, Stack, Text, TextInput } from '@mantine/core';
import { IconArrowDown, IconArrowUp, IconPlus, IconTrash } from '@tabler/icons-react';
import { useTranslation } from 'react-i18next';

export type TemplateLine = { text: string; required: boolean };

/**
 * The ordered checklist a responsibility hands to every occurrence it spawns.
 *
 * Edited as a whole list and saved as one replace, because reordering, renaming and deleting always arrive
 * together from a form. Editing it never touches occurrences that already exist: their checklist is a snapshot
 * taken at spawn, which is the whole reason it is a copy.
 */
export function ChecklistTemplateEditor({
  items,
  onChange,
  max,
}: {
  items: TemplateLine[];
  onChange: (items: TemplateLine[]) => void;
  max: number;
}) {
  const { t } = useTranslation();

  const update = (index: number, patch: Partial<TemplateLine>) =>
    onChange(items.map((item, i) => (i === index ? { ...item, ...patch } : item)));

  const move = (index: number, delta: number) => {
    const target = index + delta;
    if (target < 0 || target >= items.length) return;

    const next = [...items];
    [next[index], next[target]] = [next[target], next[index]];
    onChange(next);
  };

  return (
    <Stack gap="xs">
      <Text size="sm" fw={500}>
        {t('checklist.template')}
      </Text>
      <Text size="xs" c="dimmed">
        {t('checklist.templateHint')}
      </Text>

      {items.map((item, index) => (
        <Group key={index} gap="xs" wrap="nowrap" align="center">
          <TextInput
            style={{ flex: 1 }}
            size="xs"
            value={item.text}
            maxLength={300}
            placeholder={t('checklist.itemPlaceholder')}
            onChange={(event) => update(index, { text: event.currentTarget.value })}
          />

          <Checkbox
            size="xs"
            checked={item.required}
            label={t('checklist.required')}
            onChange={(event) => update(index, { required: event.currentTarget.checked })}
          />

          <ActionIcon variant="subtle" size="sm" aria-label={t('checklist.moveUp')} onClick={() => move(index, -1)}>
            <IconArrowUp size={14} />
          </ActionIcon>
          <ActionIcon variant="subtle" size="sm" aria-label={t('checklist.moveDown')} onClick={() => move(index, 1)}>
            <IconArrowDown size={14} />
          </ActionIcon>
          <ActionIcon
            variant="subtle"
            size="sm"
            color="red"
            aria-label={t('common.delete')}
            onClick={() => onChange(items.filter((_, i) => i !== index))}
          >
            <IconTrash size={14} />
          </ActionIcon>
        </Group>
      ))}

      <Group>
        <Button
          size="xs"
          variant="light"
          leftSection={<IconPlus size={14} />}
          disabled={items.length >= max}
          onClick={() => onChange([...items, { text: '', required: false }])}
        >
          {t('checklist.addItem')}
        </Button>

        {items.length >= max && (
          <Text size="xs" c="dimmed">
            {t('checklist.maxItems', { max })}
          </Text>
        )}
      </Group>
    </Stack>
  );
}
