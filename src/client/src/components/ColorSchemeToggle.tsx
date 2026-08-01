import { SegmentedControl, useMantineColorScheme } from '@mantine/core';
import { IconDeviceLaptop, IconMoon, IconSun } from '@tabler/icons-react';
import { useTranslation } from 'react-i18next';

/**
 * Light / dark / follow-the-system. "Auto" is the default, so a dark laptop gets a dark app without
 * anyone choosing anything; this exists for the people whose OS setting is not what they want in
 * this particular app. Mantine persists the choice in localStorage.
 */
export function ColorSchemeToggle() {
  const { t } = useTranslation();
  const { colorScheme, setColorScheme } = useMantineColorScheme();

  return (
    <SegmentedControl
      size="xs"
      fullWidth
      value={colorScheme}
      onChange={(value) => setColorScheme(value as 'light' | 'dark' | 'auto')}
      data={[
        { value: 'light', label: <IconSun size={16} aria-label={t('theme.light')} /> },
        { value: 'dark', label: <IconMoon size={16} aria-label={t('theme.dark')} /> },
        { value: 'auto', label: <IconDeviceLaptop size={16} aria-label={t('theme.auto')} /> },
      ]}
    />
  );
}
