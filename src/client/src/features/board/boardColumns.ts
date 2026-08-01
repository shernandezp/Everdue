import type { WorkItem, WorkItemStatus } from '../../api/types';

export type BoardColumnId = 'open' | 'inProgress' | 'onHold' | 'missed' | 'done';

export const BOARD_COLUMNS: BoardColumnId[] = ['open', 'inProgress', 'onHold', 'missed', 'done'];

export function columnOf(item: WorkItem): BoardColumnId {
  switch (item.status) {
    case 'InProgress':
      return 'inProgress';
    case 'OnHold':
      return 'onHold';
    case 'Missed':
      return 'missed';
    case 'Completed':
    case 'CompletedLate':
      return 'done';
    default:
      return 'open';
  }
}

export type DropOutcome =
  | { kind: 'complete' }
  | { kind: 'reopen' }
  | { kind: 'start' }
  | { kind: 'hold' }
  | { kind: 'noop' }
  | { kind: 'rejected' };

/**
 * The board's drag map is a projection of the server's transition matrix, not a second copy of it:
 * anything this returns is still validated server-side, and anything it rejects is a move the API
 * has no endpoint for at all (only the engine may record a miss).
 */
export function resolveDrop(status: WorkItemStatus, target: BoardColumnId): DropOutcome {
  // Cancelled is terminal — no transition leaves it, and the board does not show it in the first
  // place. Guarding here keeps it from falling through to the Open column's rules.
  if (status === 'Cancelled') return { kind: 'rejected' };

  const current = columnOf({ status } as WorkItem);
  if (current === target) return { kind: 'noop' };

  switch (target) {
    case 'done':
      // From Missed this completes late, and the miss stays on the record. So does an occurrence
      // whose period has already ended — the server decides that, not the column.
      return status === 'Open' || status === 'InProgress' || status === 'OnHold' || status === 'Missed'
        ? { kind: 'complete' }
        : { kind: 'rejected' };

    case 'inProgress':
      // Deliberately not reachable from Missed: compliance counts a miss until it is completed
      // late, so a missed item must not slip back into an in-flight state.
      return status === 'Open' || status === 'OnHold' ? { kind: 'start' } : { kind: 'rejected' };

    case 'onHold':
      return status === 'Open' || status === 'InProgress' ? { kind: 'hold' } : { kind: 'rejected' };

    case 'open':
      // CompletedLate is deliberately absent: a late completion always sits on a closed period, and
      // reopening it would drop the item out of the miss counts. The miss is final; so is the late
      // completion that answered it.
      return status === 'InProgress' || status === 'OnHold' || status === 'Completed'
        ? { kind: 'reopen' }
        : { kind: 'rejected' };

    case 'missed':
      // Missed is recorded by the occurrence engine at the period boundary and by nothing else.
      return { kind: 'rejected' };
  }
}
