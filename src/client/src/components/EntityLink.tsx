import { Anchor } from '@mantine/core';
import { Link } from 'react-router-dom';
import { routes } from '../lib/routes';

/**
 * An entity's name, linked to its timeline.
 *
 * Seven screens show an entity name and every one of them should get you to the same place, because
 * "why is this customer in the neglect list" is answered by the timeline and nowhere else. Before this
 * each screen built the URL itself, and the one that forgot was the one nobody clicked.
 *
 * An unlinked row (work with no entity) renders as plain text: there is nothing to navigate to, and a
 * link that goes nowhere is worse than no link.
 */
export function EntityLink({
  entityId,
  name,
  fallback,
  size = 'sm',
}: {
  entityId: string | null | undefined;
  name: string | null | undefined;
  /** Shown when there is no entity — usually a translated "unlinked". */
  fallback?: string;
  size?: string;
}) {
  if (!entityId) {
    return <>{fallback ?? name ?? ''}</>;
  }

  return (
    <Anchor component={Link} to={routes.entityTimeline(entityId)} size={size}>
      {name}
    </Anchor>
  );
}
