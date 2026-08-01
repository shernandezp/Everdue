import { Button, Group, Modal, Select, Stack, Textarea } from '@mantine/core';
import { IconPlayerPause } from '@tabler/icons-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { HOLD_REASONS, type HoldReason } from '../../api/types';

/**
 * The reason is mandatory and free text is mandatory for "Other" — the same two rules the server
 * enforces. Two clicks is the whole point: a taxonomy staff will not use records nothing.
 */
export function HoldDialog({
  opened,
  onClose,
  onConfirm,
  busy,
}: {
  opened: boolean;
  onClose: () => void;
  onConfirm: (reason: HoldReason, text: string | null) => void;
  busy?: boolean;
}) {
  const { t } = useTranslation();
  const [reason, setReason] = useState<HoldReason | null>(null);
  const [text, setText] = useState('');
  const [touched, setTouched] = useState(false);

  const textRequired = reason === 'Other';
  const textMissing = textRequired && text.trim().length === 0;

  const close = () => {
    setReason(null);
    setText('');
    setTouched(false);
    onClose();
  };

  return (
    <Modal opened={opened} onClose={close} title={t('workItem.hold')} centered>
      <Stack>
        <Select
          data={HOLD_REASONS.map((value) => ({ value, label: t(`holdReason.${value}`) }))}
          label={t('workItem.holdReason')}
          placeholder={t('workItem.holdReasonRequired')}
          value={reason}
          onChange={(value) => setReason(value as HoldReason | null)}
          error={touched && !reason ? t('workItem.holdReasonRequired') : undefined}
          allowDeselect={false}
          data-autofocus
        />

        <Textarea
          label={t('workItem.holdReasonText')}
          placeholder={textRequired ? t('workItem.holdTextRequired') : ''}
          minRows={2}
          autosize
          value={text}
          onChange={(event) => setText(event.currentTarget.value)}
          error={touched && textMissing ? t('workItem.holdTextRequired') : undefined}
          withAsterisk={textRequired}
        />

        <Group justify="flex-end">
          <Button variant="default" onClick={close}>
            {t('common.cancel')}
          </Button>
          <Button
            color="orange"
            leftSection={<IconPlayerPause size={16} />}
            loading={busy}
            onClick={() => {
              setTouched(true);
              if (!reason || textMissing) return;
              onConfirm(reason, text.trim() === '' ? null : text.trim());
              close();
            }}
          >
            {t('common.confirm')}
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}
