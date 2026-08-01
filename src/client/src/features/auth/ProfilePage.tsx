import { Button, Card, Select, Stack, TextInput } from '@mantine/core';
import { IconDeviceFloppy } from '@tabler/icons-react';
import { useForm } from '@mantine/form';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { PageHeader } from '../../components/PageHeader';
import { api } from '../../lib/api';
import { notifyError, notifySaved } from '../../lib/notify';
import { DigestSubscriptionCard } from '../notifications/DigestSubscriptionCard';
import { NotificationPreferencesCard } from '../notifications/NotificationPreferencesCard';
import { useSession } from './session';
import { useSupportedLanguages } from './languages';

export function ProfilePage() {
  const { t } = useTranslation();
  const languages = useSupportedLanguages();
  const { user, refresh } = useSession();
  const [busy, setBusy] = useState(false);

  const form = useForm({
    initialValues: {
      displayName: user?.displayName ?? '',
      preferredLanguage: user?.tenant.defaultLanguage === user?.language ? '' : (user?.language ?? ''),
    },
    validate: { displayName: (value) => (value.trim().length === 0 ? t('common.required') : null) },
  });

  const submit = form.onSubmit(async ({ displayName, preferredLanguage }) => {
    setBusy(true);
    try {
      await api.auth.updateProfile(displayName.trim(), preferredLanguage === '' ? null : preferredLanguage);
      await refresh();
      notifySaved(t('auth.profileSaved'));
    } catch (e) {
      notifyError(e);
    } finally {
      setBusy(false);
    }
  });

  return (
    <>
      <PageHeader title={t('nav.profile')} description={user?.email} />

      <Stack maw={560} gap="md">
        <Card withBorder>
          <form onSubmit={submit}>
            <Stack>
              <TextInput label={t('auth.displayName')} {...form.getInputProps('displayName')} />

              <Select
                label={t('auth.preferredLanguage')}
                data={[
                  { value: '', label: t('auth.useTenantDefault') },
                  ...languages.map((language) => ({ value: language.code, label: language.nativeName })),
                ]}
                allowDeselect={false}
                {...form.getInputProps('preferredLanguage')}
              />

              <Button type="submit" loading={busy} leftSection={<IconDeviceFloppy size={16} />}>
                {t('common.save')}
              </Button>
            </Stack>
          </form>
        </Card>

        {/* How this person wants to be reached, and how often — both belong to them, not to an admin. */}
        <NotificationPreferencesCard />
        <DigestSubscriptionCard />
      </Stack>
    </>
  );
}
