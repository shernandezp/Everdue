import { Anchor, Group, Text } from '@mantine/core';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { api } from '../lib/api';
import { keys } from '../lib/queryKeys';

/**
 * The AGPL's "Appropriate Legal Notices" (§5d), displayed rather than filed away.
 *
 * Section 5d requires an interactive interface to show a copyright notice, say there is no warranty, and tell the
 * user how to see the licence; §13 requires that somebody interacting with a modified version over a network can
 * get its source. Almost nobody does this, and doing it costs one line at the bottom of the page.
 *
 * <strong>A fork that changes the code should change the source URL</strong> — it is served from
 * <c>GET /api/v1/about</c>, so there is one place to change it.
 */
export function LegalNotice() {
  const { t } = useTranslation();

  const about = useQuery({
    queryKey: keys.meta.about,
    queryFn: () => api.meta.about(),

    // It cannot change while the tab is open.
    staleTime: Infinity,
  });

  if (!about.data) return null;

  const { product, version, license, licenseUrl, sourceUrl } = about.data;

  return (
    <Group gap={6} justify="center" py="xs" wrap="wrap">
      <Text size="xs" c="dimmed">
        {product} {version} · {t('legal.noWarranty')} ·
      </Text>
      <Anchor size="xs" c="dimmed" href={licenseUrl} target="_blank" rel="noreferrer noopener">
        {license}
      </Anchor>
      <Text size="xs" c="dimmed">
        ·
      </Text>
      <Anchor size="xs" c="dimmed" href={sourceUrl} target="_blank" rel="noreferrer noopener">
        {t('legal.source')}
      </Anchor>
    </Group>
  );
}
