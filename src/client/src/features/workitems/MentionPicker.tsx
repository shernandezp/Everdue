import { MultiSelect } from '@mantine/core';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { api } from '../../lib/api';
import { keys } from '../../lib/queryKeys';

/**
 * Mentions are **picked, not parsed**. The comment body stays plain text for humans; the ids travel
 * beside it so the server never has to guess which "María" was meant — and display names never have
 * to be unique, stable or escapable for the feature to work.
 *
 * Anyone active can be mentioned: a fifteen-person company mentions across teams.
 */
export function MentionPicker({
  value,
  onChange,
  onInsert,
}: {
  value: string[];
  onChange: (ids: string[]) => void;
  onInsert: (displayName: string) => void;
}) {
  const { t } = useTranslation();
  const users = useQuery({ queryKey: keys.users.all, queryFn: api.users.list });

  const active = (users.data ?? []).filter((user) => user.active);

  return (
    <MultiSelect
      searchable
      clearable
      size="xs"
      label={t('workItem.mention')}
      placeholder={t('workItem.mentionPlaceholder')}
      data={active.map((user) => ({ value: user.id, label: user.displayName }))}
      value={value}
      onChange={(ids) => {
        // Writing the name into the body is a convenience; the ids are what the server acts on.
        const added = ids.find((id) => !value.includes(id));
        const name = active.find((user) => user.id === added)?.displayName;
        if (name) onInsert(name);

        onChange(ids);
      }}
    />
  );
}
