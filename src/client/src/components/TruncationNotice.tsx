import { Text } from '@mantine/core';
import { useTranslation } from 'react-i18next';

/**
 * Says out loud that a table stopped at one page. Every capped list must carry this: a report that
 * silently shows 100 of 214 rows is worse than one that refuses — the whole product rests on the
 * numbers being trustworthy.
 */
export function TruncationNotice({ shown, total }: { shown: number; total: number }) {
  const { t } = useTranslation();

  if (total <= shown) return null;

  return (
    <Text size="xs" c="orange" mt={4}>
      {t('common.showingOf', { shown, total })}
    </Text>
  );
}
