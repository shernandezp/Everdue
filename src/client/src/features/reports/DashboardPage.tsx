import { Card, Group, Loader, SimpleGrid, Stack, Text, ThemeIcon, Title, UnstyledButton } from '@mantine/core';
import {
  IconAlertTriangle,
  IconCalendarDue,
  IconChevronRight,
  IconCircleCheck,
  IconClockExclamation,
  IconPlayerPause,
  type TablerIcon,
} from '@tabler/icons-react';
import { useQuery } from '@tanstack/react-query';
import { useState, type CSSProperties } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import type { Metric } from '../../api/types';
import { PageHeader } from '../../components/PageHeader';
import { api, type ReportFilters } from '../../lib/api';
import { formatDate, formatDateTime, formatNumber } from '../../lib/format';
import { ChronicDelayCard } from '../insights/ChronicDelayCard';
import { drillLink } from './drill';
import { ReportFilterBar } from './ReportFilterBar';
import { keys } from '../../lib/queryKeys';

/**
 * The manager's home screen: what needs attention, never a list of 847 completed tasks. Every card
 * is a link, so the number and the work behind it are one click apart.
 */
export function DashboardPage() {
  const { t } = useTranslation();
  const [filters, setFilters] = useState<ReportFilters>({});

  const report = useQuery({
    queryKey: keys.reports.exceptions(filters),
    queryFn: () => api.reports.exceptions(filters),
  });

  return (
    <>
      <PageHeader
        title={t('reports.dashboardTitle')}
        description={report.data ? `${t('reports.generatedAt')} ${formatDate(report.data.generatedAt)}` : undefined}
      />

      <ReportFilterBar value={filters} onChange={setFilters} />

      {report.isLoading && <Loader />}

      {report.data && (
        <Stack gap="lg">
          <SimpleGrid cols={{ base: 1, xs: 2, md: 5 }} spacing="sm">
            <MetricCard label={t('reports.dueToday')} metric={report.data.dueToday} color="blue" icon={IconCalendarDue} />
            <MetricCard
              label={t('reports.completedToday')}
              metric={report.data.completedToday}
              color="teal"
              icon={IconCircleCheck}
            />
            <MetricCard
              label={t('reports.overdue')}
              metric={report.data.overdue}
              color="red"
              icon={IconClockExclamation}
            />
            <MetricCard
              label={t('reports.missed')}
              metric={report.data.missedInRange}
              color="red"
              icon={IconAlertTriangle}
            />
            <MetricCard
              label={t('reports.onHold')}
              metric={report.data.onHold}
              color="orange"
              icon={IconPlayerPause}
            />
          </SimpleGrid>

          <Card withBorder padding="md">
            <Title order={5} mb="sm">
              {t('reports.byReason')}
            </Title>

            {report.data.onHoldByReason.length === 0 ? (
              <Text size="sm" c="dimmed">
                {t('reports.blockedEmpty')}
              </Text>
            ) : (
              <Stack gap="xs">
                {report.data.onHoldByReason.map((group) => (
                  <UnstyledButton
                    key={group.reason}
                    component={Link}
                    to={drillLink(group.drillThrough)}
                    className="everdue-row"
                  >
                    <Group justify="space-between">
                      <Group gap={6} wrap="nowrap">
                        <IconPlayerPause size={14} style={{ color: 'var(--mantine-color-orange-6)' }} />
                        <Text size="sm">{t(`holdReason.${group.reason}`)}</Text>
                      </Group>
                      <Group gap="md">
                        <Text size="xs" c="dimmed">
                          {t('reports.oldestHold')}: {formatDateTime(group.oldestHoldAt)}
                        </Text>
                        <Text fw={600}>{formatNumber(group.count)}</Text>
                      </Group>
                    </Group>
                  </UnstyledButton>
                ))}
              </Stack>
            )}
          </Card>

          {/*
            What keeps happening, next to what happened today. Its own endpoint, so this screen's query
            stays exactly what it was — the exception dashboard is about today, and adding a trend to it
            would make it about neither.
          */}
          <ChronicDelayCard />

          {/*
            Visibility inside an existing screen, not a new report. The "counting since" line is the
            honest part: owner changes used to be recorded as ordinary edits, so this number starts
            when that changed rather than covering all of history.
          */}
          <Card withBorder padding="md">
            <Group justify="space-between" align="flex-start">
              <Stack gap={2}>
                <Text size="sm" c="dimmed">
                  {t('reports.reassigned')}
                </Text>
                <Text size="xs" c="dimmed">
                  {report.data.reassigned.countingSince
                    ? t('reports.reassignedSince', { date: formatDate(report.data.reassigned.countingSince) })
                    : t('reports.reassignedNone')}
                </Text>
              </Stack>

              <Text fw={700} fz={28}>
                {formatNumber(report.data.reassigned.count)}
              </Text>
            </Group>
          </Card>
        </Stack>
      )}
    </>
  );
}

function MetricCard({
  label,
  metric,
  color,
  icon: Icon,
}: {
  label: string;
  metric: Metric;
  color: string;
  icon: TablerIcon;
}) {
  const { t } = useTranslation();

  return (
    <Card
      withBorder
      padding="md"
      component={Link}
      to={drillLink(metric.drillThrough)}
      className="everdue-interactive"
      style={{ '--everdue-accent': `var(--mantine-color-${color}-6)` } as CSSProperties}
    >
      <Group justify="space-between" align="flex-start" wrap="nowrap" mb={2}>
        <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
          {label}
        </Text>
        <ThemeIcon size="md" radius="md" variant="light" color={color}>
          <Icon size={16} />
        </ThemeIcon>
      </Group>

      <Stack gap={2}>
        {/* A zero is not news: it stays in the text colour, and only a number above zero is coloured. */}
        <Text fz={32} fw={700} c={metric.count > 0 ? color : undefined}>
          {formatNumber(metric.count)}
        </Text>
        <Group gap={2} wrap="nowrap">
          <Text size="xs" c="dimmed">
            {t('reports.drillThrough')}
          </Text>
          <IconChevronRight size={12} style={{ color: 'var(--mantine-color-dimmed)' }} />
        </Group>
      </Stack>
    </Card>
  );
}
