import { Alert, Anchor, Button, Card, Code, CopyButton, Group, Stack, Text, ThemeIcon, Title } from '@mantine/core';
import { IconBrandTelegram, IconCheck, IconCopy, IconLink, IconUnlink } from '@tabler/icons-react';
import { useTranslation } from 'react-i18next';
import { useTelegramLink } from './hooks';

/**
 * Linking is deliberately a code the user carries to the bot, not the other way round: Everdue never
 * needs an inbound webhook, which is what lets this work on an install behind a home router.
 */
export function TelegramLinkCard({ linked }: { linked: boolean }) {
  const { t } = useTranslation();
  const { start, unlink } = useTelegramLink();

  const link = start.data;

  return (
    <Card withBorder padding="md">
      <Stack gap="sm">
        <Group gap="xs">
          <ThemeIcon size="md" radius="md" variant="light" color={linked ? 'teal' : 'blue'}>
            <IconBrandTelegram size={16} />
          </ThemeIcon>
          <Title order={5}>{t('notifications.telegram')}</Title>
        </Group>

        {linked ? (
          <>
            <Alert color="teal" icon={<IconCheck size={16} />}>
              {t('notifications.telegramLinked')}
            </Alert>
            <Group>
              <Button
                variant="light"
                color="red"
                leftSection={<IconUnlink size={16} />}
                onClick={() => unlink.mutate()}
                loading={unlink.isPending}
              >
                {t('notifications.telegramUnlink')}
              </Button>
            </Group>
          </>
        ) : (
          <>
            <Text size="sm" c="dimmed">
              {t('notifications.telegramHint')}
            </Text>

            {!link && (
              <Group>
                <Button leftSection={<IconLink size={16} />} onClick={() => start.mutate()} loading={start.isPending}>
                  {t('notifications.telegramLink')}
                </Button>
              </Group>
            )}

            {link && (
              <Stack gap="xs">
                {link.deepLink ? (
                  <Anchor href={link.deepLink} target="_blank" rel="noreferrer">
                    {t('notifications.telegramOpenBot')}
                  </Anchor>
                ) : (
                  <Text size="sm">{t('notifications.telegramNoBotUsername')}</Text>
                )}

                <Group gap="xs" align="center">
                  <Text size="sm">{t('notifications.telegramCode')}:</Text>
                  <Code>{`/start ${link.code}`}</Code>

                  <CopyButton value={`/start ${link.code}`}>
                    {({ copied, copy }) => (
                      <Button
                        size="compact-xs"
                        variant="subtle"
                        leftSection={copied ? <IconCheck size={14} /> : <IconCopy size={14} />}
                        onClick={copy}
                      >
                        {copied ? t('common.copied') : t('common.copy')}
                      </Button>
                    )}
                  </CopyButton>
                </Group>

                <Text size="xs" c="dimmed">
                  {t('notifications.telegramExpires')}
                </Text>
              </Stack>
            )}
          </>
        )}
      </Stack>
    </Card>
  );
}
