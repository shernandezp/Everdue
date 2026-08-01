import { Button, Card, Group, Loader, Select, Stack, Switch, Text, ThemeIcon, Title } from '@mantine/core';
import { IconDeviceFloppy, IconMailForward } from '@tabler/icons-react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { DigestFrequency } from '../../api/types';
import { DIGEST_FREQUENCIES } from '../../api/types';
import { DepartmentPicker } from '../../components/pickers';
import { api } from '../../lib/api';
import { notifyError, notifySaved } from '../../lib/notify';
import { useSession } from '../auth/session';
import { keys } from '../../lib/queryKeys';

const DAYS = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];

/**
 * The manager digest, per person. An administrator with no row here is still a daily subscriber —
 * saving anything at all is what turns that implicit arrangement into a recorded preference.
 */
export function DigestSubscriptionCard() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const { user, isAdmin } = useSession();

  const subscriptions = useQuery({ queryKey: keys.notifications.digestSubscriptions, queryFn: () => api.digestSubscriptions.list() });
  const mine = subscriptions.data?.find((s) => s.userId === user?.id);

  const [frequency, setFrequency] = useState<DigestFrequency>('Daily');
  const [weeklyDay, setWeeklyDay] = useState('Monday');
  const [departmentId, setDepartmentId] = useState<string | null>(null);
  const [active, setActive] = useState(isAdmin);

  useEffect(() => {
    if (!mine) return;
    setFrequency(mine.frequency);
    setWeeklyDay(mine.weeklyDayOfWeek);
    setDepartmentId(mine.departmentId ?? null);
    setActive(mine.active);
  }, [mine]);

  const save = useMutation({
    mutationFn: () =>
      api.digestSubscriptions.save({ frequency, weeklyDayOfWeek: weeklyDay, departmentId, active }),
    onSuccess: async () => {
      notifySaved();
      await queryClient.invalidateQueries({ queryKey: keys.notifications.digestSubscriptions });
    },
    onError: notifyError,
  });

  if (subscriptions.isLoading) return <Loader />;

  return (
    <Card withBorder padding="md">
      <Stack gap="sm">
        <Group gap="xs">
          <ThemeIcon size="md" radius="md" variant="light" color={active ? 'everdue' : 'gray'}>
            <IconMailForward size={16} />
          </ThemeIcon>
          <Title order={5}>{t('digest.title')}</Title>
        </Group>

        <Text size="sm" c="dimmed">
          {t('digest.hint')}
        </Text>

        <Switch
          label={t('digest.active')}
          checked={active}
          onChange={(event) => setActive(event.currentTarget.checked)}
        />

        <Group align="flex-end" gap="sm" wrap="wrap">
          <Select
            label={t('digest.frequency')}
            data={DIGEST_FREQUENCIES.map((value) => ({ value, label: t(`digest.frequencies.${value}`) }))}
            value={frequency}
            onChange={(value) => setFrequency((value as DigestFrequency) ?? 'Daily')}
            allowDeselect={false}
            w={160}
          />

          {frequency === 'Weekly' && (
            <Select
              label={t('digest.weeklyDay')}
              data={DAYS.map((value) => ({ value, label: t(`digest.days.${value}`) }))}
              value={weeklyDay}
              onChange={(value) => setWeeklyDay(value ?? 'Monday')}
              allowDeselect={false}
              w={180}
            />
          )}

          <DepartmentPicker value={departmentId} onChange={setDepartmentId} w={200} />
        </Group>

        <Group justify="flex-end">
          <Button leftSection={<IconDeviceFloppy size={16} />} onClick={() => save.mutate()} loading={save.isPending}>
            {t('common.save')}
          </Button>
        </Group>
      </Stack>
    </Card>
  );
}
