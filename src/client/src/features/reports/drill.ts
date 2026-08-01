import { routes } from '../../lib/routes';
import type { DrillThrough } from '../../api/types';

/**
 * Turns a report's drill-through into the work-list URL it describes. The client never invents a
 * filter: the server hands back the exact query set it counted with, so the list behind a number
 * always totals that number.
 */
export function drillLink(drill: DrillThrough): string {
  const search = new URLSearchParams(drill.workItemQuery as Record<string, string>);
  return `${routes.work}?${search.toString()}`;
}
