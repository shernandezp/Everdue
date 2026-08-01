import { ActionIcon, Anchor, Button, FileButton, Group, Stack, Text } from '@mantine/core';
import { useMediaQuery } from '@mantine/hooks';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { IconCamera, IconPaperclip, IconTrash } from '@tabler/icons-react';
import { useTranslation } from 'react-i18next';
import { api } from '../../lib/api';
import { notifyError } from '../../lib/notify';
import { useSession } from '../auth/session';
import { keys } from '../../lib/queryKeys';

/** Bytes, in the unit a person would say out loud. */
function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export function AttachmentsPanel({ workItemId }: { workItemId: string }) {
  const { t } = useTranslation();
  const { user, isAdmin } = useSession();
  const queryClient = useQueryClient();

  // `capture` is a hint only mobile browsers honour: a desktop one ignores it and opens the ordinary file
  // picker, so "Take a photo" there is a second button that does what the first one does. Asked by pointer
  // rather than by width, because the question is "does this device have a camera to hand" and not "is the
  // window narrow" — a tablet qualifies, a small desktop window does not.
  //
  // Read synchronously rather than in an effect (the hook's default): this is a client-rendered SPA with no
  // server pass to disagree with, and deferring it makes the button visibly pop in on the very devices it
  // exists for.
  const canTakeAPhoto = useMediaQuery('(pointer: coarse)', false, { getInitialValueInEffect: false });

  const attachments = useQuery({
    queryKey: keys.attachments.forItem(workItemId),
    queryFn: () => api.workItems.attachments(workItemId),
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: keys.attachments.forItem(workItemId) });

  const upload = useMutation({
    mutationFn: (file: File) => api.attachments.upload(workItemId, file),
    onSuccess: refresh,

    // Size and type limits live on the server; its message names the actual limit, so it is shown.
    onError: notifyError,
  });

  const remove = useMutation({
    mutationFn: (id: string) => api.attachments.remove(id),
    onSuccess: refresh,
    onError: notifyError,
  });

  const items = attachments.data ?? [];

  return (
    <Stack gap="xs">
      {items.map((attachment) => (
        <Group key={attachment.id} justify="space-between" wrap="nowrap">
          <Group gap="xs" wrap="nowrap" style={{ minWidth: 0 }}>
            <IconPaperclip size={16} style={{ color: 'var(--mantine-color-dimmed)', flexShrink: 0 }} />
            <Anchor href={api.attachments.downloadUrl(attachment.id)} size="sm" lineClamp={1}>
              {attachment.fileName}
            </Anchor>
            <Text size="xs" c="dimmed">
              {formatSize(attachment.sizeBytes)}
            </Text>
          </Group>

          {(isAdmin || attachment.uploadedByUserId === user?.id) && (
            <ActionIcon
              variant="subtle"
              color="red"
              aria-label={t('common.delete')}
              onClick={() => remove.mutate(attachment.id)}
            >
              <IconTrash size={16} />
            </ActionIcon>
          )}
        </Group>
      ))}

      {items.length === 0 && (
        <Text size="sm" c="dimmed">
          {t('attachments.none')}
        </Text>
      )}

      <Group>
        <FileButton onChange={(file) => file && upload.mutate(file)}>
          {(props) => (
            <Button
              {...props}
              size="xs"
              variant="light"
              leftSection={<IconPaperclip size={14} />}
              loading={upload.isPending}
            >
              {t('attachments.add')}
            </Button>
          )}
        </FileButton>

        {/*
          Proof of completion, on the device the work happens on. `capture="environment"` opens the rear camera
          directly — no native app, no new endpoint, and the same upload path as any other file.
          `accept="image/*"` is what makes a phone offer the camera at all.

          Nothing is lost by hiding it elsewhere: a desktop user attaches the same photo through the button
          beside it, which is all "Take a photo" could ever have done for them anyway.
        */}
        {canTakeAPhoto && (
          <FileButton onChange={(file) => file && upload.mutate(file)} accept="image/*" capture="environment">
            {(props) => (
              <Button
                {...props}
                size="xs"
                variant="light"
                leftSection={<IconCamera size={14} />}
                loading={upload.isPending}
              >
                {t('attachments.takePhoto')}
              </Button>
            )}
          </FileButton>
        )}
      </Group>
    </Stack>
  );
}
