import { Center, Loader } from '@mantine/core';
import { useEffect, useState } from 'react';
import { Navigate, useSearchParams } from 'react-router-dom';
import { routes } from '../../lib/routes';
import { useSession } from './session';

/**
 * Where the Google callback lands.
 *
 * It exists so the strict cookie posture survives external sign-in. The request immediately after a
 * cross-site navigation does not carry a SameSite=Strict cookie, so redirecting straight into the
 * app would show the login screen again with a perfectly good session in the jar. This route is the
 * static shell, which needs no cookie; the session refresh below is a same-origin fetch, which does
 * carry it.
 *
 * The `settled` flag is load-bearing. The session query has already run and 401'd during app boot,
 * so `user` is null and `isLoading` false the moment this mounts — deciding on that would bounce a
 * *successful* sign-in to the error page before the refresh had a chance to answer.
 */
export function ExternalLoginCompletePage() {
  const [params] = useSearchParams();
  const { user, refresh } = useSession();
  const [settled, setSettled] = useState(false);

  useEffect(() => {
    let cancelled = false;

    void refresh().finally(() => {
      if (!cancelled) setSettled(true);
    });

    return () => {
      cancelled = true;
    };
  }, [refresh]);

  if (!settled) {
    return (
      <Center h="100vh">
        <Loader />
      </Center>
    );
  }

  const returnUrl = params.get('returnUrl');
  const target = returnUrl && returnUrl.startsWith('/') && !returnUrl.startsWith('//') ? returnUrl : routes.board;

  return <Navigate to={user ? target : '/login?error=external_login_failed'} replace />;
}
