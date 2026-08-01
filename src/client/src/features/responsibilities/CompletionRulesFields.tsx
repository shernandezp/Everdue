import { Alert, Stack, Switch, Text } from '@mantine/core';
import { IconInfoCircle } from '@tabler/icons-react';
import { useTranslation } from 'react-i18next';

/**
 * The two server-enforced completion rules.
 *
 * Both apply from the next completion attempt: nothing already completed is reopened, and an open occurrence is
 * not retroactively blocked until somebody tries to finish it. That is the first question an administrator asks,
 * so the form answers it rather than leaving it to be discovered.
 */
export function CompletionRulesFields({
  requireChecklist,
  requireAttachment,
  hasChecklistItems,
  onChange,
}: {
  requireChecklist: boolean;
  requireAttachment: boolean;
  hasChecklistItems: boolean;
  onChange: (patch: { requireChecklist?: boolean; requireAttachment?: boolean }) => void;
}) {
  const { t } = useTranslation();

  return (
    <Stack gap="xs">
      <Switch
        checked={requireChecklist}
        disabled={!hasChecklistItems}
        label={t('responsibility.requireChecklist')}
        description={
          hasChecklistItems ? t('responsibility.requireChecklistHint') : t('responsibility.requireChecklistNeedsItems')
        }
        onChange={(event) => onChange({ requireChecklist: event.currentTarget.checked })}
      />

      <Switch
        checked={requireAttachment}
        label={t('responsibility.requireAttachment')}
        description={t('responsibility.requireAttachmentHint')}
        onChange={(event) => onChange({ requireAttachment: event.currentTarget.checked })}
      />

      {(requireChecklist || requireAttachment) && (
        <Alert variant="light" color="blue" icon={<IconInfoCircle size={16} />} p="xs">
          <Text size="xs">{t('responsibility.completionRulesNotRetroactive')}</Text>
        </Alert>
      )}
    </Stack>
  );
}
