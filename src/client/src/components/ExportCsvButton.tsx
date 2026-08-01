import { Button } from '@mantine/core';
import { IconDownload } from '@tabler/icons-react';
import { useTranslation } from 'react-i18next';

/**
 * The button *is* the promise that the file matches the screen: it is a plain link to an export URL built from the
 * page's own filters, and the server answers it by dispatching the same query the page dispatched.
 *
 * A link rather than a fetch, so the browser handles the download and nothing has to hold a blob in memory. An export
 * over the row limit comes back as a 400, which the browser shows as a failed download — the alternative was a file
 * that looked complete and was not.
 */
export function ExportCsvButton({ href }: { href: string }) {
  const { t } = useTranslation();

  return (
    <Button component="a" href={href} download variant="default" size="xs" leftSection={<IconDownload size={14} />}>
      {t('exports.csv')}
    </Button>
  );
}
