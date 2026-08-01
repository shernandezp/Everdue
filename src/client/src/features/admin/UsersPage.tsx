import { ActionIcon, Badge, Button, Group, Modal, PasswordInput, Select, Stack, Switch, TextInput } from '@mantine/core';
import { useForm } from '@mantine/form';
import { IconArrowsExchange, IconDeviceFloppy, IconKey, IconPencil, IconPlus } from '@tabler/icons-react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { DataTable } from 'mantine-datatable';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { USER_ROLES, type UserDto } from '../../api/types';
import { PageHeader } from '../../components/PageHeader';
import { api } from '../../lib/api';
import { notifyError, notifySaved } from '../../lib/notify';
import { ReassignUserWorkModal } from './ReassignUserWorkModal';
import { keys } from '../../lib/queryKeys';
import { useSupportedLanguages } from '../auth/languages';

export function UsersPage() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState<UserDto | null>(null);
  const [creating, setCreating] = useState(false);
  const [resetting, setResetting] = useState<UserDto | null>(null);
  const [handingOver, setHandingOver] = useState<UserDto | null>(null);

  const users = useQuery({ queryKey: keys.users.all, queryFn: api.users.list });
  const invalidate = () => queryClient.invalidateQueries({ queryKey: keys.users.all });

  return (
    <>
      <PageHeader
        title={t('users.title')}
        description={t('users.noSelfService')}
        actions={
          <Button leftSection={<IconPlus size={16} />} onClick={() => setCreating(true)}>
            {t('users.new')}
          </Button>
        }
      />

      <DataTable
        highlightOnHover
        withTableBorder
        minHeight={200}
        fetching={users.isFetching}
        records={users.data ?? []}
        idAccessor="id"
        noRecordsText={t('common.noResults')}
        columns={[
          { accessor: 'displayName', title: t('auth.displayName') },
          { accessor: 'email', title: t('auth.email') },
          {
            accessor: 'role',
            title: t('common.type'),
            // Who can see the manager's screens is worth telling apart at a glance.
            render: (user) => (
              <Badge size="sm" variant="light" color={user.role === 'Admin' ? 'grape' : 'gray'}>
                {t(`role.${user.role}`)}
              </Badge>
            ),
          },
          {
            accessor: 'preferredLanguage',
            title: t('common.language'),
            render: (user) => user.preferredLanguage ?? t('auth.useTenantDefault'),
          },
          {
            accessor: 'active',
            title: t('common.status'),
            render: (user) => (
              <Group gap={4}>
                <Badge size="sm" variant="light" color={user.active ? 'teal' : 'gray'}>
                  {user.active ? t('common.active') : t('common.inactive')}
                </Badge>
                {user.mustChangePassword && (
                  <Badge size="sm" variant="outline" color="orange">
                    {t('users.mustChangePassword')}
                  </Badge>
                )}
              </Group>
            ),
          },
          {
            accessor: 'actions',
            title: '',
            textAlign: 'right',
            render: (user) => (
              <Group gap={4} justify="flex-end">
                <ActionIcon variant="subtle" aria-label={t('common.edit')} onClick={() => setEditing(user)}>
                  <IconPencil size={16} />
                </ActionIcon>
                <ActionIcon variant="subtle" aria-label={t('users.resetPassword')} onClick={() => setResetting(user)}>
                  <IconKey size={16} />
                </ActionIcon>

                {/* The departure path, where somebody will look for it: on the person who is leaving. */}
                <ActionIcon variant="subtle" aria-label={t('reassign.handOver')} onClick={() => setHandingOver(user)}>
                  <IconArrowsExchange size={16} />
                </ActionIcon>
              </Group>
            ),
          },
        ]}
      />

      <UserModal
        user={editing}
        opened={creating || editing !== null}
        onClose={() => {
          setCreating(false);
          setEditing(null);
        }}
        onSaved={invalidate}
      />

      <ResetPasswordModal user={resetting} onClose={() => setResetting(null)} onSaved={invalidate} />

      <ReassignUserWorkModal
        user={handingOver}
        opened={handingOver !== null}
        onClose={() => setHandingOver(null)}
      />
    </>
  );
}

function UserModal({
  user,
  opened,
  onClose,
  onSaved,
}: {
  user: UserDto | null;
  opened: boolean;
  onClose: () => void;
  onSaved: () => Promise<unknown>;
}) {
  const { t } = useTranslation();
  const languages = useSupportedLanguages();

  const form = useForm({
    initialValues: {
      email: user?.email ?? '',
      password: '',
      displayName: user?.displayName ?? '',
      role: user?.role ?? 'Member',
      preferredLanguage: user?.preferredLanguage ?? '',
      active: user?.active ?? true,
      whatsAppPhoneE164: user?.whatsAppPhoneE164 ?? '',
    },
    validate: {
      email: (value) => (user || value.trim().length > 0 ? null : t('common.required')),
      password: (value) => (user || value.length >= 10 ? null : t('common.required')),
      displayName: (value) => (value.trim().length === 0 ? t('common.required') : null),
    },
  });

  const save = useMutation({
    mutationFn: (values: typeof form.values) =>
      user
        ? api.users.update(user.id, {
            displayName: values.displayName.trim(),
            role: values.role,
            preferredLanguage: values.preferredLanguage === '' ? null : values.preferredLanguage,
            active: values.active,
            whatsAppPhoneE164: values.whatsAppPhoneE164.trim() === '' ? null : values.whatsAppPhoneE164.trim(),
          })
        : api.users.create({
            email: values.email.trim(),
            password: values.password,
            displayName: values.displayName.trim(),
            role: values.role,
            preferredLanguage: values.preferredLanguage === '' ? null : values.preferredLanguage,
          }),
    onSuccess: async () => {
      await onSaved();
      notifySaved();
      onClose();
    },
    onError: notifyError,
  });

  return (
    <Modal opened={opened} onClose={onClose} title={user ? t('users.edit') : t('users.new')} centered key={user?.id ?? 'new'}>
      <form onSubmit={form.onSubmit((values) => save.mutate(values))}>
        <Stack>
          <TextInput label={t('auth.email')} disabled={user !== null} {...form.getInputProps('email')} />
          {!user && <PasswordInput label={t('auth.newPassword')} {...form.getInputProps('password')} />}
          <TextInput label={t('auth.displayName')} {...form.getInputProps('displayName')} />

          <Select
            label={t('common.type')}
            data={USER_ROLES.map((role) => ({ value: role, label: t(`role.${role}`) }))}
            allowDeselect={false}
            {...form.getInputProps('role')}
          />

          <Select
            label={t('auth.preferredLanguage')}
            data={[
              { value: '', label: t('auth.useTenantDefault') },
              ...languages.map((language) => ({ value: language.code, label: language.nativeName })),
            ]}
            allowDeselect={false}
            {...form.getInputProps('preferredLanguage')}
          />

          {/*
            WhatsApp has no linking flow — without a public webhook there is nothing for a user to
            confirm against — so an administrator types the number and takes responsibility for
            having asked first. That is what the description says, in as many words.
          */}
          {user && (
            <TextInput
              label={t('users.whatsAppNumber')}
              description={t('users.whatsAppNumberHint')}
              placeholder="+573001112233"
              {...form.getInputProps('whatsAppPhoneE164')}
            />
          )}

          {user && (
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

function ResetPasswordModal({
  user,
  onClose,
  onSaved,
}: {
  user: UserDto | null;
  onClose: () => void;
  onSaved: () => Promise<unknown>;
}) {
  const { t } = useTranslation();
  const [password, setPassword] = useState('');

  const reset = useMutation({
    mutationFn: () => api.users.resetPassword(user!.id, password),
    onSuccess: async () => {
      await onSaved();
      notifySaved();
      setPassword('');
      onClose();
    },
    onError: notifyError,
  });

  return (
    <Modal opened={user !== null} onClose={onClose} title={t('users.resetPassword')} centered>
      <Stack>
        <PasswordInput
          label={t('auth.newPassword')}
          description={t('users.resetPasswordHint')}
          value={password}
          onChange={(event) => setPassword(event.currentTarget.value)}
          data-autofocus
        />
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>
            {t('common.cancel')}
          </Button>
          <Button
            loading={reset.isPending}
            disabled={password.length < 10}
            leftSection={<IconKey size={16} />}
            onClick={() => reset.mutate()}
          >
            {t('common.save')}
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}
