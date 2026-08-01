import { useQuery } from '@tanstack/react-query';
import type { Language } from '../../api/types';
import { BUNDLED_LANGUAGES } from '../../i18n';
import { api } from '../../lib/api';
import { keys } from '../../lib/queryKeys';

/**
 * The languages this installation actually offers: the intersection of what the server supports and what this
 * bundle can render.
 *
 * The intersection is the honest answer. A code the server knows but the client has no locale for would render as
 * raw keys; a locale the client has but the server does not would send notifications and digests in English while
 * the screens were translated. Both are worse than not offering it.
 */
export function useSupportedLanguages(): Language[] {
  const languages = useQuery({
    queryKey: keys.meta.languages,
    queryFn: () => api.meta.languages(),

    // It cannot change without a deploy.
    staleTime: Infinity,
  });

  return (languages.data ?? []).filter((language) => BUNDLED_LANGUAGES.includes(language.code));
}
