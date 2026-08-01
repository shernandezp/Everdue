import { Alert, Button, Group, Stack, Table, Text } from '@mantine/core';
import { IconAlertTriangle, IconCheck, IconDownload, IconRefresh } from '@tabler/icons-react';
import { useTranslation } from 'react-i18next';
import type { ImportResult } from '../../api/types';

/**
 * What happened, per row.
 *
 * The failure CSV is built in the browser from the response: the server already returned everything it needs, so
 * a second endpoint (and a second place for the file to leak from) would buy nothing.
 */
export function ImportResultStep({ result, onDone }: { result: ImportResult; onDone: () => void }) {
  const { t } = useTranslation();

  const downloadFailures = () => {
    const header = `${t('imports.row')},${t('imports.problem')}\n`;
    const body = result.failures
      .map((failure) => `${failure.rowNumber},"${failure.message.replace(/"/g, '""')}"`)
      .join('\n');

    // The BOM is what makes Excel read accents correctly, exactly as the server's exports do.
    const blob = new Blob([`﻿${header}${body}`], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);

    const link = document.createElement('a');
    link.href = url;
    link.download = 'everdue-import-errors.csv';
    link.click();

    URL.revokeObjectURL(url);
  };

  return (
    <Stack>
      <Alert
        variant="light"
        color={result.failed > 0 ? 'orange' : 'teal'}
        // The icon follows the outcome: a run with failures is not a tick.
        icon={result.failed > 0 ? <IconAlertTriangle size={16} /> : <IconCheck size={16} />}
        title={t('imports.resultTitle')}
      >
        <Text size="sm">
          {t('imports.resultSummary', { created: result.created, skipped: result.skipped, failed: result.failed })}
        </Text>
      </Alert>

      {result.skipped > 0 && (
        <Text size="xs" c="dimmed">
          {t('imports.skippedNote')}
        </Text>
      )}

      {result.failures.length > 0 && (
        <>
          <Group justify="space-between">
            <Text size="sm" fw={500}>
              {t('imports.failuresTitle')}
            </Text>
            <Button size="xs" variant="light" leftSection={<IconDownload size={14} />} onClick={downloadFailures}>
              {t('imports.downloadErrors')}
            </Button>
          </Group>

          <Table.ScrollContainer minWidth={400}>
            <Table striped withTableBorder highlightOnHover>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>{t('imports.row')}</Table.Th>
                  <Table.Th>{t('imports.problem')}</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {result.failures.map((failure) => (
                  <Table.Tr key={failure.rowNumber}>
                    <Table.Td>{failure.rowNumber}</Table.Td>
                    <Table.Td>
                      <Text size="xs">{failure.message}</Text>
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        </>
      )}

      <Group justify="flex-end">
        <Button leftSection={<IconRefresh size={16} />} onClick={onDone}>
          {t('imports.startOver')}
        </Button>
      </Group>
    </Stack>
  );
}
