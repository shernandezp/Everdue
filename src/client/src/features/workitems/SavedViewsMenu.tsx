import { ActionIcon, Button, Group, Menu, Modal, Stack, TextInput } from '@mantine/core';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { IconBookmark, IconBookmarkPlus, IconChevronDown, IconDeviceFloppy, IconTrash } from '@tabler/icons-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '../../lib/api';
import { notifyError, notifySaved } from '../../lib/notify';
import { keys } from '../../lib/queryKeys';

/**
 * A saved view is the query string, verbatim. The list screen is already URL-driven, so applying one
 * is handing the string back to the router — no serialization format to invent, and a filter added
 * in a later version keeps working in views saved today.
 */
export function SavedViewsMenu({
  route,
  currentQuery,
  onApply,
}: {
  route: 'work' | 'board';
  currentQuery: string;
  onApply: (queryString: string) => void;
}) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

  const [naming, setNaming] = useState(false);
  const [name, setName] = useState('');

  const views = useQuery({ queryKey: keys.savedViews.all, queryFn: () => api.savedViews.list() });
  const refresh = () => queryClient.invalidateQueries({ queryKey: keys.savedViews.all });

  const save = useMutation({
    mutationFn: () => api.savedViews.save({ name: name.trim(), route, queryString: currentQuery }),
    onSuccess: async () => {
      notifySaved();
      setNaming(false);
      setName('');
      await refresh();
    },
    onError: notifyError,
  });

  const remove = useMutation({
    mutationFn: (id: string) => api.savedViews.remove(id),
    onSuccess: refresh,
    onError: notifyError,
  });

  const mine = (views.data ?? []).filter((view) => view.route === route);

  return (
    <>
      <Menu position="bottom-end" withinPortal>
        <Menu.Target>
          <Button variant="subtle" size="compact-sm" leftSection={<IconBookmark size={14} />} rightSection={<IconChevronDown size={14} />}>
            {t('savedViews.title')}
          </Button>
        </Menu.Target>

        <Menu.Dropdown>
          {mine.map((view) => (
            <Menu.Item key={view.id} leftSection={<IconBookmark size={16} />} onClick={() => onApply(view.queryString)}>
              <Group justify="space-between" wrap="nowrap" gap="sm">
                <span>{view.name}</span>
                <ActionIcon
                  component="div"
                  variant="subtle"
                  color="red"
                  size="sm"
                  aria-label={t('common.delete')}
                  onClick={(event) => {
                    event.stopPropagation();
                    remove.mutate(view.id);
                  }}
                >
                  <IconTrash size={14} />
                </ActionIcon>
              </Group>
            </Menu.Item>
          ))}

          {mine.length === 0 && <Menu.Item disabled>{t('savedViews.none')}</Menu.Item>}

          <Menu.Divider />
          <Menu.Item leftSection={<IconBookmarkPlus size={16} />} onClick={() => setNaming(true)}>
            {t('savedViews.saveCurrent')}
          </Menu.Item>
        </Menu.Dropdown>
      </Menu>

      <Modal opened={naming} onClose={() => setNaming(false)} title={t('savedViews.saveCurrent')} centered>
        <Stack>
          <TextInput
            label={t('savedViews.name')}
            value={name}
            onChange={(event) => setName(event.currentTarget.value)}
            data-autofocus
          />
          <Group justify="flex-end">
            <Button variant="default" onClick={() => setNaming(false)}>
              {t('common.cancel')}
            </Button>
            <Button
              disabled={name.trim().length === 0}
              loading={save.isPending}
              leftSection={<IconDeviceFloppy size={16} />}
              onClick={() => save.mutate()}
            >
              {t('common.save')}
            </Button>
          </Group>
        </Stack>
      </Modal>
    </>
  );
}
