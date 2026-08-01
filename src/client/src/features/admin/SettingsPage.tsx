import { Button, Card, NumberInput, Select, Stack, Switch, TextInput } from '@mantine/core';
import { IconDeviceFloppy } from '@tabler/icons-react';
import { useForm } from '@mantine/form';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { PageHeader } from '../../components/PageHeader';
import { DemoModeCard } from './DemoModeCard';
import { api } from '../../lib/api';
import { notifyError, notifySaved } from '../../lib/notify';
import { useSession } from '../auth/session';
import { keys } from '../../lib/queryKeys';
import { useSupportedLanguages } from '../auth/languages';

export function SettingsPage() {
  const { t } = useTranslation();
  const languages = useSupportedLanguages();
  const { refresh } = useSession();

  const settings = useQuery({ queryKey: keys.settings.all, queryFn: api.settings.get });

  const form = useForm({
    initialValues: {
      name: '',
      timeZoneId: 'UTC',
      digestHourLocal: 7,
      defaultLanguage: 'es',
      reminderHourLocal: 8,
      canUseSystemChannels: true,
    },
    validate: {
      name: (value) => (value.trim().length === 0 ? t('common.required') : null),
      timeZoneId: (value) => (value.trim().length === 0 ? t('common.required') : null),
    },
  });

  useEffect(() => {
    if (settings.data) {
      form.setValues({
        name: settings.data.name,
        timeZoneId: settings.data.timeZoneId,
        digestHourLocal: settings.data.digestHourLocal,
        defaultLanguage: settings.data.defaultLanguage,
        reminderHourLocal: settings.data.reminderHourLocal,
        canUseSystemChannels: settings.data.canUseSystemChannels,
      });
      form.resetDirty();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [settings.data]);

  const save = useMutation({
    mutationFn: (values: typeof form.values) => api.settings.update(values),
    onSuccess: async () => {
      await Promise.all([settings.refetch(), refresh()]);
      notifySaved();
    },
    onError: notifyError,
  });

  return (
    <>
      <PageHeader title={t('settings.title')} />

      <Card withBorder maw={560}>
        <form onSubmit={form.onSubmit((values) => save.mutate(values))}>
          <Stack>
            <TextInput label={t('settings.name')} {...form.getInputProps('name')} />

            <TextInput
              label={t('settings.timeZone')}
              description={t('settings.timeZoneHint')}
              placeholder="America/Bogota"
              {...form.getInputProps('timeZoneId')}
            />

            <NumberInput
              label={t('settings.digestHour')}
              description={t('settings.digestHint')}
              min={0}
              max={23}
              {...form.getInputProps('digestHourLocal')}
            />

            {/* Later than the digest by default: managers read before the day starts, the people
                doing the work want it once they have. */}
            <NumberInput
              label={t('settings.reminderHour')}
              description={t('settings.reminderHint')}
              min={0}
              max={23}
              {...form.getInputProps('reminderHourLocal')}
            />

            <Select
              label={t('settings.defaultLanguage')}
              data={languages.map((language) => ({ value: language.code, label: language.nativeName }))}
              allowDeselect={false}
              {...form.getInputProps('defaultLanguage')}
            />

            <Switch
              label={t('settings.canUseSystemChannels')}
              description={t('settings.canUseSystemChannelsHint')}
              checked={form.values.canUseSystemChannels}
              onChange={(event) => form.setFieldValue('canUseSystemChannels', event.currentTarget.checked)}
            />

            <Button type="submit" loading={save.isPending} leftSection={<IconDeviceFloppy size={16} />}>
              {t('common.save')}
            </Button>
          </Stack>
        </form>
      </Card>

      {/* Last on the page, and in its own card: it is the only control here that destroys anything. */}
      <DemoModeCard />
    </>
  );
}
