import { Alert, Button, Group, Modal, Stack, Switch, Text } from '@mantine/core';
import { IconArrowsExchange, IconInfoCircle } from '@tabler/icons-react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { UserDto } from '../../api/types';
import { UserPicker } from '../../components/pickers';
import { api } from '../../lib/api';
import { notifyError, notifySaved } from '../../lib/notify';
import { keys } from '../../lib/queryKeys';

/**
 * The departure path. This is the screen somebody opens under time pressure — the person has left
 * and their responsibilities are still spawning occurrences every morning — so it does the whole
 * thing in one action rather than asking for a list of items.
 */
export function ReassignUserWorkModal({
  user,
  opened,
  onClose,
}: {
  user: UserDto | null;
  opened: boolean;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

  const [toUserId, setToUserId] = useState<string | null>(null);
  const [includeResponsibilities, setIncludeResponsibilities] = useState(true);
  const [includeWorkableItems, setIncludeWorkableItems] = useState(true);

  const handOver = useMutation({
    mutationFn: () =>
      api.users.reassignAll(user!.id, { toUserId: toUserId!, includeResponsibilities, includeWorkableItems }),
    onSuccess: async (result) => {
      notifySaved(t('reassign.handedOver', { responsibilities: result.responsibilities, items: result.workItems }));

      await Promise.all([
        queryClient.invalidateQueries({ queryKey: keys.workItems.all }),
        queryClient.invalidateQueries({ queryKey: keys.responsibilities.all }),
      ]);

      onClose();
    },
    onError: notifyError,
  });

  return (
    <Modal opened={opened} onClose={onClose} title={t('reassign.handOver')} centered>
      <Stack>
        {user && (
          <Text size="sm" c="dimmed">
            {t('reassign.handOverHint', { name: user.displayName })}
          </Text>
        )}

        <UserPicker label={t('reassign.newOwner')} value={toUserId} onChange={setToUserId} clearable={false} />

        <Switch
          label={t('reassign.includeResponsibilities')}
          checked={includeResponsibilities}
          onChange={(event) => setIncludeResponsibilities(event.currentTarget.checked)}
        />
        <Switch
          label={t('reassign.includeWorkableItems')}
          checked={includeWorkableItems}
          onChange={(event) => setIncludeWorkableItems(event.currentTarget.checked)}
        />

        <Alert color="gray" icon={<IconInfoCircle size={16} />}>
          {t('reassign.historyKept')}
        </Alert>

        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>
            {t('common.cancel')}
          </Button>
          <Button
            disabled={!toUserId || toUserId === user?.id}
            loading={handOver.isPending}
            leftSection={<IconArrowsExchange size={16} />}
            onClick={() => handOver.mutate()}
          >
            {t('common.confirm')}
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}
