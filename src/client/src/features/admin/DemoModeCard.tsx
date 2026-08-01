import { Alert, Badge, Button, Card, Code, Group, List, Modal, PasswordInput, Stack, Text, TextInput, Title } from '@mantine/core';
import { IconAlertTriangle, IconFlask, IconTrash } from '@tabler/icons-react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '../../lib/api';
import { ApiError } from '../../lib/http';
import { notifyError, notifySaved } from '../../lib/notify';
import { keys } from '../../lib/queryKeys';
import { useSession } from '../auth/session';

/**
 * The one control in Everdue that destroys data on purpose.
 *
 * Both buttons wipe the workspace — on means "wipe, then fill with invented history", off means "wipe, and
 * leave it empty for real use" — so the dialog says the same sentence for both and asks for the same two
 * things. It deliberately does *not* have a switch: a switch reads as a setting, and settings are the kind of
 * thing people flip to see what happens.
 */
export function DemoModeCard() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const { refresh } = useSession();

  const [target, setTarget] = useState<boolean | null>(null);
  const [confirmation, setConfirmation] = useState('');
  const [password, setPassword] = useState('');
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});

  const status = useQuery({ queryKey: keys.demo.all, queryFn: api.demo.status });

  function close() {
    setTarget(null);
    setConfirmation('');
    setPassword('');
    setFieldErrors({});
  }

  const apply = useMutation({
    mutationFn: () => api.demo.set({ enabled: target!, confirmation, password }),
    onSuccess: async (result) => {
      notifySaved(
        result.status.enabled
          ? t('demo.turnedOn', {
              responsibilities: result.seeded?.responsibilities ?? 0,
              occurrences: result.seeded?.occurrences ?? 0,
            })
          : t('demo.turnedOff', { workItems: result.deleted.workItems }),
      );

      close();

      // Every cached list, report and insight on the client describes rows that no longer exist. Selective
      // invalidation would be a list of every key in the app and one bug away from a screen showing a work
      // item the server has never heard of, so the cache goes wholesale.
      //
      // Everything EXCEPT the session, though. `Protected` navigates to the login screen the moment `user` is
      // null and the session query is not loading, and a bare queryClient.clear() drops that data before the
      // refetch it triggers has started — which bounces the administrator to a login screen the instant their
      // wipe succeeds. Keeping ['me'] means the session never blinks; `refresh()` re-reads it in place, which
      // it must, because the tenant's demo flag has just changed and the header badge is drawn from it.
      queryClient.removeQueries({ predicate: (cached) => cached.queryKey[0] !== keys.session.me[0] });
      await refresh();
    },
    onError: (error) => {
      // 400s carry the field that was wrong, and both of them are boxes in this dialog — showing them inline
      // beats a toast the user has to read and then map back onto a form.
      if (error instanceof ApiError && error.status === 400 && Object.keys(error.fieldErrors).length > 0) {
        setFieldErrors(error.fieldErrors);
        return;
      }

      notifyError(error);
    },
  });

  // Demo:AllowReset is off: this install does not have the feature at all, so nothing is rendered rather
  // than a disabled control that invites somebody to go looking for the setting that would enable it.
  if (!status.data?.resetAllowed) {
    return null;
  }

  const enabled = status.data.enabled;
  const phrase = status.data.confirmationPhrase;

  return (
    <>
      <Card withBorder maw={560} mt="lg" className="everdue-danger">
        <Stack>
          <Group justify="space-between" wrap="nowrap">
            <Title order={4}>{t('demo.title')}</Title>
            {enabled && (
              <Badge color="orange" variant="light" leftSection={<IconFlask size={12} />}>
                {t('demo.badge')}
              </Badge>
            )}
          </Group>

          <Text size="sm" c="dimmed">
            {enabled ? t('demo.stateOn') : t('demo.stateOff')}
          </Text>

          <Alert color="red" icon={<IconAlertTriangle size={16} />}>
            {t('demo.warning')}
          </Alert>

          {enabled ? (
            <Button color="red" leftSection={<IconTrash size={16} />} onClick={() => setTarget(false)}>
              {t('demo.turnOff')}
            </Button>
          ) : (
            <Button color="red" leftSection={<IconFlask size={16} />} onClick={() => setTarget(true)}>
              {t('demo.turnOn')}
            </Button>
          )}
        </Stack>
      </Card>

      <Modal
        opened={target !== null}
        onClose={close}
        title={target ? t('demo.turnOn') : t('demo.turnOff')}
        centered
        closeOnClickOutside={false}
      >
        <Stack>
          <Alert color="red" icon={<IconAlertTriangle size={16} />} title={t('demo.confirmTitle')}>
            <List size="sm" spacing={4}>
              <List.Item>{t('demo.confirmLedger')}</List.Item>
              <List.Item>{t('demo.confirmUsers')}</List.Item>
              <List.Item>{t('demo.confirmFiles')}</List.Item>
              <List.Item>{t('demo.confirmIrreversible')}</List.Item>
            </List>
          </Alert>

          {target && (
            <Text size="sm">
              {t('demo.willSeed')} <Code>{status.data.demoPassword}</Code>
            </Text>
          )}

          <TextInput
            label={t('demo.confirmationLabel')}
            description={t('demo.confirmationHint', { name: phrase })}
            placeholder={phrase}
            value={confirmation}
            error={fieldErrors.confirmation?.[0]}
            // Cleared on edit, or the server's "type it exactly" sits under a box that now says the right
            // thing and reads as the app being broken.
            onChange={(event) => {
              setConfirmation(event.currentTarget.value);
              setFieldErrors((current) => ({ ...current, confirmation: [] }));
            }}
          />

          <PasswordInput
            label={t('demo.passwordLabel')}
            description={t('demo.passwordHint')}
            value={password}
            error={fieldErrors.password?.[0]}
            onChange={(event) => {
              setPassword(event.currentTarget.value);
              setFieldErrors((current) => ({ ...current, password: [] }));
            }}
          />

          <Group justify="flex-end">
            <Button variant="default" onClick={close}>
              {t('common.cancel')}
            </Button>
            <Button
              color="red"
              // The server checks both anyway; this only stops the request that was never going to work.
              disabled={confirmation.trim() !== phrase.trim() || password.length === 0}
              loading={apply.isPending}
              leftSection={<IconTrash size={16} />}
              onClick={() => apply.mutate()}
            >
              {t('demo.confirmButton')}
            </Button>
          </Group>
        </Stack>
      </Modal>
    </>
  );
}
