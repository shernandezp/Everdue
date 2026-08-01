import { createContext, useContext, useEffect, useMemo, type ReactNode } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { CurrentUser } from '../../api/types';
import { api } from '../../lib/api';
import { ApiError } from '../../lib/http';
import { applyLanguage } from '../../i18n';
import { keys } from '../../lib/queryKeys';

type Session = {
  user: CurrentUser | null;
  isLoading: boolean;
  isAdmin: boolean;
  mustChangePassword: boolean;
  signIn: (email: string, password: string) => Promise<CurrentUser>;
  signOut: () => Promise<void>;
  refresh: () => Promise<unknown>;
};

const SessionContext = createContext<Session | null>(null);

export function SessionProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();

  const me = useQuery({
    queryKey: keys.session.me,
    queryFn: api.auth.me,
    // 401 simply means "not signed in": it is an answer, not a failure worth retrying.
    retry: (_count, error) => !(error instanceof ApiError && error.status === 401),
    staleTime: 30_000,
  });

  const user = me.data ?? null;

  // The language belongs to the person, not the browser: preference first, tenant default second.
  useEffect(() => {
    applyLanguage(user?.language);
  }, [user?.language]);

  const signIn = useMutation({
    mutationFn: ({ email, password }: { email: string; password: string }) => api.auth.login(email, password),
    onSuccess: (data) => queryClient.setQueryData(['me'], data),
  });

  const signOut = useMutation({
    mutationFn: api.auth.logout,
    onSuccess: () => queryClient.clear(),
  });

  const value = useMemo<Session>(
    () => ({
      user,
      isLoading: me.isLoading,
      isAdmin: user?.role === 'Admin',
      mustChangePassword: user?.mustChangePassword === true,
      signIn: (email, password) => signIn.mutateAsync({ email, password }),
      signOut: async () => {
        await signOut.mutateAsync();
      },
      refresh: () => queryClient.invalidateQueries({ queryKey: keys.session.me }),
    }),
    [user, me.isLoading, signIn, signOut, queryClient],
  );

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}

export function useSession(): Session {
  const session = useContext(SessionContext);
  if (!session) {
    throw new Error('useSession must be used inside a SessionProvider.');
  }
  return session;
}
