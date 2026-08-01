import { notifications } from '@mantine/notifications';
import i18n from '../i18n';
import { ApiError } from './http';

export function notifySaved(message?: string): void {
  notifications.show({
    color: 'teal',
    message: message ?? i18n.t('common.saved'),
    autoClose: 2200,
  });
}

/**
 * Refusals are shown with the server's own reason. A rejected board drop must say *why* the move is
 * not allowed, and only the server knows that — the client never re-derives the transition matrix.
 */
export function notifyError(error: unknown): void {
  notifications.show({
    color: 'red',
    title: i18n.t('common.error'),
    message: describe(error),
    autoClose: 6000,
  });
}

export function describe(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.problem.detail) {
      return error.problem.detail;
    }

    switch (error.code) {
      case 'conflict':
        return i18n.t('errors.conflict');
      case 'not_found':
        return i18n.t('errors.notFound');
      case 'forbidden':
        return i18n.t('errors.forbidden');
      case 'unauthenticated':
        return i18n.t('errors.unauthenticated');
      default:
        return error.message;
    }
  }

  if (error instanceof Error) {
    return error.message;
  }

  return i18n.t('errors.network');
}
