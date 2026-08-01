import { Card, Group, Text, ThemeIcon } from '@mantine/core';
import { IconFilter } from '@tabler/icons-react';
import { useTranslation } from 'react-i18next';
import { useLocation } from 'react-router-dom';
import { DepartmentPicker, EntityTypePicker, UserPicker } from '../../components/pickers';
import type { ReportFilters } from '../../lib/api';
import { activeNav } from '../../lib/navigation';

/** The same filter vocabulary every report accepts: owner, department, entity type. */
export function ReportFilterBar({
  value,
  onChange,
}: {
  value: ReportFilters;
  onChange: (next: ReportFilters) => void;
}) {
  const { t } = useTranslation();

  // The bar belongs to whichever screen it is on, and wears that screen's accent.
  const accent = activeNav(useLocation().pathname)?.color ?? 'everdue';

  return (
    <Card withBorder padding="sm" mb="md">
      <Group gap="xs" mb="xs">
        <ThemeIcon size="sm" radius="sm" variant="light" color={accent}>
          <IconFilter size={14} />
        </ThemeIcon>
        <Text size="xs" tt="uppercase" fw={700} c="dimmed">
          {t('common.filters')}
        </Text>
      </Group>

      <Group align="flex-end" gap="sm" wrap="wrap">
        <UserPicker
          label={t('reports.filterOwner')}
          clearable
          value={value.ownerId ?? null}
          onChange={(ownerId) => onChange({ ...value, ownerId: ownerId ?? undefined })}
          w={200}
        />
        <DepartmentPicker
          label={t('reports.filterDepartment')}
          value={value.departmentId ?? null}
          onChange={(departmentId) => onChange({ ...value, departmentId: departmentId ?? undefined })}
          w={200}
        />
        <EntityTypePicker
          label={t('reports.filterEntityType')}
          value={value.entityType ?? null}
          onChange={(entityType) => onChange({ ...value, entityType: entityType ?? undefined })}
          w={200}
        />
      </Group>
    </Card>
  );
}
