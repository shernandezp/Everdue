import { Button, Modal, Stack, Text, Textarea } from '@mantine/core';
import { IconPlayerPause } from '@tabler/icons-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { HOLD_REASONS, type HoldReason } from '../../api/types';

/**
 * The reason is mandatory and free text is mandatory for "Other" — the same two rules the server
 * enforces. Two taps is the whole point: open, tap the reason, done. The four fixed reasons confirm
 * immediately (a dropdown-plus-confirm here once made it five clicks, and a taxonomy staff will not
 * use records nothing); only "Other" — the one reason that explains nothing on its own — asks for
 * anything more. A note can be typed before tapping a reason; it rides along.
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
  const [otherOpen, setOtherOpen] = useState(false);
  const [text, setText] = useState('');
  const [touched, setTouched] = useState(false);

  const note = text.trim() === '' ? null : text.trim();

  const close = () => {
    setOtherOpen(false);
    setText('');
    setTouched(false);
    onClose();
  };

  const confirm = (reason: HoldReason) => {
    onConfirm(reason, note);
    close();
  };

  return (
    <Modal opened={opened} onClose={close} title={t('workItem.hold')} centered>
      <Stack gap="xs">
        <Text size="sm" c="dimmed">
          {t('workItem.holdReasonRequired')}
        </Text>

        {HOLD_REASONS.filter((reason) => reason !== 'Other').map((reason) => (
          <Button
            key={reason}
            variant="light"
            color="orange"
            fullWidth
            justify="flex-start"
            leftSection={<IconPlayerPause size={16} />}
            loading={busy}
            onClick={() => confirm(reason)}
          >
            {t(`holdReason.${reason}`)}
          </Button>
        ))}

        <Button
          variant={otherOpen ? 'filled' : 'light'}
          color="orange"
          fullWidth
          justify="flex-start"
          leftSection={<IconPlayerPause size={16} />}
          disabled={busy}
          onClick={() => setOtherOpen((open) => !open)}
        >
          {t('holdReason.Other')}
        </Button>

        <Textarea
          label={t('workItem.holdReasonText')}
          placeholder={otherOpen ? t('workItem.holdTextRequired') : ''}
          minRows={2}
          autosize
          value={text}
          onChange={(event) => setText(event.currentTarget.value)}
          error={otherOpen && touched && note === null ? t('workItem.holdTextRequired') : undefined}
          withAsterisk={otherOpen}
          data-autofocus={otherOpen || undefined}
        />

        {otherOpen && (
          <Button
            color="orange"
            leftSection={<IconPlayerPause size={16} />}
            loading={busy}
            onClick={() => {
              setTouched(true);
              if (note === null) return;
              confirm('Other');
            }}
          >
            {t('common.confirm')}
          </Button>
        )}

        <Button variant="default" onClick={close}>
          {t('common.cancel')}
        </Button>
      </Stack>
    </Modal>
  );
}
