import { SegmentedControl } from '@mantine/core';
import { useTranslation } from 'react-i18next';
import { useSupportedLanguages } from './languages';
import { applyLanguage, DEFAULT_LANGUAGE } from '../../i18n';

/**
 * Pre-sign-in language switch. Once signed in, the language comes from the user's stored preference (falling back
 * to the tenant default), so this only affects the login screen itself.
 *
 * The options come from the server, not from a list in this file: a locale bundled into the client but absent from
 * `Languages.Supported` would otherwise be offered here and then arrive in English in every notification.
 */
export function LanguageToggle() {
  const { i18n } = useTranslation();
  const languages = useSupportedLanguages();

  if (languages.length < 2) return null;

  return (
    <SegmentedControl
      size="xs"
      value={languages.some((language) => language.code === i18n.language) ? i18n.language : DEFAULT_LANGUAGE}
      onChange={applyLanguage}
      data={languages.map((language) => ({ value: language.code, label: language.nativeName }))}
    />
  );
}
