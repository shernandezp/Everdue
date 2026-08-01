import { Anchor, Badge, Card, Group, Loader, Stack, Text, Title } from '@mantine/core';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { ExportCsvButton } from '../../components/ExportCsvButton';
import { PageHeader } from '../../components/PageHeader';
import { api, type ReportFilters } from '../../lib/api';
import { formatDate } from '../../lib/format';
import { routes } from '../../lib/routes';
import { drillLink } from './drill';
import { ReportFilterBar } from './ReportFilterBar';
import { keys } from '../../lib/queryKeys';

/** Which entity is the bottleneck, and why — the evidence staff need in a dispute. */
export function BlockedByEntityPage() {
  const { t } = useTranslation();
  const [filters, setFilters] = useState<ReportFilters>({});

  const report = useQuery({
    queryKey: keys.reports.blocked(filters),
    queryFn: () => api.reports.blockedByEntity(filters),
  });

  return (
    <>
      <PageHeader
        title={t('reports.blockedTitle')}
        actions={<ExportCsvButton href={api.exports.report('blocked-by-entity', filters)} />}
      />
      <ReportFilterBar value={filters} onChange={setFilters} />

      {report.isLoading && <Loader />}

      {report.data?.length === 0 && (
        <Text size="sm" c="dimmed">
          {t('reports.blockedEmpty')}
        </Text>
      )}

      <Stack gap="sm">
        {(report.data ?? []).map((group) => (
          <Card key={group.entityId ?? 'none'} withBorder padding="md">
            <Group justify="space-between" mb="xs" wrap="wrap">
              <Group gap="xs">
                <Title order={5}>
                  {group.entityId ? (
                    <Anchor component={Link} to={routes.entityTimeline(group.entityId)}>
                      {group.entityName}
                    </Anchor>
                  ) : (
                    t('common.none')
                  )}
                </Title>
                {group.entityType && (
                  <Badge variant="light" size="sm">
                    {t(`entityType.${group.entityType}`)}
                  </Badge>
                )}
              </Group>

              <Group gap="md">
                <Text size="xs" c="dimmed">
                  {t('reports.oldestHold')}: {formatDate(group.oldestHoldAt)}
                </Text>
                <Anchor component={Link} to={drillLink(group.drillThrough)} size="sm" fw={600}>
                  {t('reports.total')}: {group.total}
                </Anchor>
              </Group>
            </Group>

            <Stack gap={4}>
              {group.reasons.map((reason) => (
                <Group key={reason.reason} justify="space-between">
                  <Anchor component={Link} to={drillLink(reason.drillThrough)} size="sm">
                    {t(`holdReason.${reason.reason}`)}
                  </Anchor>
                  <Group gap="md">
                    <Text size="xs" c="dimmed">
                      {formatDate(reason.oldestHoldAt)}
                    </Text>
                    <Text size="sm" fw={600}>
                      {reason.count}
                    </Text>
                  </Group>
                </Group>
              ))}
            </Stack>
          </Card>
        ))}
      </Stack>
    </>
  );
}
