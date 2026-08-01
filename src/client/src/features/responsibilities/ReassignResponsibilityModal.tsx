import { Alert, Button, Group, Modal, Stack, Switch } from '@mantine/core';
import { IconArrowsExchange, IconInfoCircle } from '@tabler/icons-react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { ResponsibilityDto } from '../../api/types';
import { UserPicker } from '../../components/pickers';
import { api } from '../../lib/api';
import { notifyError, notifySaved } from '../../lib/notify';
import { keys } from '../../lib/queryKeys';

/**
 * Handing over a responsibility. Future occurrences follow the new owner automatically — the engine
 * copies the owner when it spawns them — so the only decision is what to do about the work already
 * on somebody's plate.
 */
export function ReassignResponsibilityModal({
  responsibility,
  opened,
  onClose,
}: {
  responsibility: ResponsibilityDto | null;
  opened: boolean;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

  const [newOwnerUserId, setNewOwnerUserId] = useState<string | null>(null);
  const [applyToWorkable, setApplyToWorkable] = useState(true);

  const reassign = useMutation({
    mutationFn: () =>
      api.responsibilities.reassign(responsibility!.id, {
        newOwnerUserId: newOwnerUserId!,
        applyToWorkableOccurrences: applyToWorkable,
      }),
    onSuccess: async (result) => {
      notifySaved(t('reassign.done', { count: result.workItems }));

      await Promise.all([
        queryClient.invalidateQueries({ queryKey: keys.responsibilities.all }),
        queryClient.invalidateQueries({ queryKey: keys.workItems.all }),
      ]);

      onClose();
    },
    onError: notifyError,
  });

  return (
    <Modal opened={opened} onClose={onClose} title={t('reassign.responsibility')} centered>
      <Stack>
        <Alert color="blue" icon={<IconInfoCircle size={16} />}>
          {t('reassign.futureHint')}
        </Alert>

        <UserPicker
          label={t('reassign.newOwner')}
          value={newOwnerUserId}
          onChange={setNewOwnerUserId}
          clearable={false}
        />

        <Switch
          label={t('reassign.applyToWorkable')}
          description={t('reassign.applyToWorkableHint')}
          checked={applyToWorkable}
          onChange={(event) => setApplyToWorkable(event.currentTarget.checked)}
        />

        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>
            {t('common.cancel')}
          </Button>
          <Button
            disabled={!newOwnerUserId}
            loading={reassign.isPending}
            leftSection={<IconArrowsExchange size={16} />}
            onClick={() => reassign.mutate()}
          >
            {t('common.confirm')}
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}
