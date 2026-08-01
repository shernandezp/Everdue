import { Alert, Badge, Group, Select, Stack, Table, Text } from '@mantine/core';
import { IconInfoCircle } from '@tabler/icons-react';
import { useTranslation } from 'react-i18next';
import type { ImportPreview } from '../../api/types';

/**
 * Step two of three: the detected columns on the left, what we think each field is on the right, and the first
 * rows parsed through that guess so a wrong mapping is visible before anything is written.
 */
export function ColumnMappingStep({
  preview,
  mapping,
  onChange,
}: {
  preview: ImportPreview;
  mapping: Record<string, string>;
  onChange: (mapping: Record<string, string>) => void;
}) {
  const { t } = useTranslation();

  const options = [
    { value: '', label: t('imports.notMapped') },
    ...preview.headers.map((header) => ({ value: header, label: header })),
  ];

  const set = (fieldKey: string, header: string) => {
    const next = { ...mapping };

    if (header === '') {
      delete next[fieldKey];
    } else {
      next[fieldKey] = header;
    }

    onChange(next);
  };

  const failures = preview.rows.filter((row) => row.error);

  return (
    <Stack>
      <Alert variant="light" color="blue" icon={<IconInfoCircle size={16} />}>
        <Text size="sm">
          {t('imports.detected', {
            delimiter: preview.delimiter === ';' ? '";"' : '","',
            encoding: preview.encoding,
            rows: preview.totalRows,
          })}
        </Text>
      </Alert>

      <Stack gap="xs">
        {preview.fields.map((field) => (
          <Group key={field.key} gap="sm" wrap="nowrap" align="flex-end">
            <Stack gap={0} style={{ minWidth: 180 }}>
              <Group gap={4}>
                <Text size="sm" fw={500}>
                  {field.label}
                </Text>
                {field.required && (
                  <Badge size="xs" color="orange" variant="light">
                    {t('imports.required')}
                  </Badge>
                )}
              </Group>
              {field.hint && (
                <Text size="xs" c="dimmed">
                  {field.hint}
                </Text>
              )}
            </Stack>

            <Select
              style={{ flex: 1 }}
              size="xs"
              data={options}
              value={mapping[field.key] ?? ''}
              allowDeselect={false}
              onChange={(value) => set(field.key, value ?? '')}
            />
          </Group>
        ))}
      </Stack>

      {failures.length > 0 && (
        <Alert variant="light" color="orange">
          <Text size="sm">{t('imports.previewHasErrors', { count: failures.length })}</Text>
        </Alert>
      )}

      <Text size="sm" fw={500}>
        {t('imports.previewTitle', { count: preview.rows.length })}
      </Text>

      <Table.ScrollContainer minWidth={600}>
        <Table striped withTableBorder>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>{t('imports.row')}</Table.Th>
              {preview.fields.map((field) => (
                <Table.Th key={field.key}>{field.label}</Table.Th>
              ))}
              <Table.Th>{t('imports.problem')}</Table.Th>
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {preview.rows.map((row) => (
              <Table.Tr key={row.rowNumber}>
                <Table.Td>{row.rowNumber}</Table.Td>
                {preview.fields.map((field) => (
                  <Table.Td key={field.key}>
                    <Text size="xs">{row.values[field.key] ?? '—'}</Text>
                  </Table.Td>
                ))}
                <Table.Td>
                  {row.error ? (
                    <Text size="xs" c="red">
                      {row.error}
                    </Text>
                  ) : (
                    <Text size="xs" c="dimmed">
                      —
                    </Text>
                  )}
                </Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      </Table.ScrollContainer>
    </Stack>
  );
}
