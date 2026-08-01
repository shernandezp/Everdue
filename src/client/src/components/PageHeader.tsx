import { Group, Stack, Text, ThemeIcon, Title } from '@mantine/core';
import type { TablerIcon } from '@tabler/icons-react';
import type { ReactNode } from 'react';
import { useLocation } from 'react-router-dom';
import { activeNav } from '../lib/navigation';

/**
 * Every screen's title, and the icon and colour that place it.
 *
 * Neither is passed in: both are read from the navigation entry that the current path belongs to, so
 * the tile beside "Compliance" is the same chart glyph in the same violet as the link that got here.
 * The props exist for the few screens that are nobody's navigation destination.
 */
export function PageHeader({
  title,
  description,
  actions,
  icon,
  color,
}: {
  title: string;
  description?: string;
  actions?: ReactNode;
  icon?: TablerIcon;
  color?: string;
}) {
  const location = useLocation();
  const match = activeNav(location.pathname);

  const Icon = icon ?? match?.icon;
  const accent = color ?? match?.color ?? 'everdue';

  return (
    <Group justify="space-between" align="flex-end" wrap="wrap" mb="md" gap="sm">
      <Group gap="sm" wrap="nowrap" align="center">
        {Icon && (
          <ThemeIcon size={38} radius="md" variant="light" color={accent}>
            <Icon size={22} />
          </ThemeIcon>
        )}
        <Stack gap={2}>
          <Title order={2}>{title}</Title>
          {description && (
            <Text size="sm" c="dimmed">
              {description}
            </Text>
          )}
        </Stack>
      </Group>
      {actions && <Group gap="xs">{actions}</Group>}
    </Group>
  );
}
