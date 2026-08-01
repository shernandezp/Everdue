import { describe, expect, it } from 'vitest';
import type { WorkItem, WorkItemStatus } from '../../api/types';
import { columnOf, resolveDrop, type BoardColumnId } from './boardColumns';

const item = (status: WorkItemStatus) => ({ status }) as WorkItem;

describe('columnOf', () => {
  it('folds both completed states into Done', () => {
    expect(columnOf(item('Completed'))).toBe('done');
    expect(columnOf(item('CompletedLate'))).toBe('done');
  });

  it('keeps the actionable states apart', () => {
    expect(columnOf(item('Open'))).toBe('open');
    expect(columnOf(item('InProgress'))).toBe('inProgress');
    expect(columnOf(item('OnHold'))).toBe('onHold');
    expect(columnOf(item('Missed'))).toBe('missed');
  });
});

describe('picking work up', () => {
  it('starts from the queue or from a hold', () => {
    expect(resolveDrop('Open', 'inProgress')).toEqual({ kind: 'start' });
    expect(resolveDrop('OnHold', 'inProgress')).toEqual({ kind: 'start' });
  });

  it('never lets a missed item slip back into progress', () => {
    // Compliance counts a miss until it is completed late; moving it back in flight would drop it
    // out of the 30/60/90-day numbers while someone worked on it.
    expect(resolveDrop('Missed', 'inProgress')).toEqual({ kind: 'rejected' });
  });

  it('cannot be reached from a finished item', () => {
    expect(resolveDrop('Completed', 'inProgress')).toEqual({ kind: 'rejected' });
    expect(resolveDrop('CompletedLate', 'inProgress')).toEqual({ kind: 'rejected' });
    expect(resolveDrop('Cancelled', 'inProgress')).toEqual({ kind: 'rejected' });
  });

  it('can be put back down, held, or finished', () => {
    expect(resolveDrop('InProgress', 'open')).toEqual({ kind: 'reopen' });
    expect(resolveDrop('InProgress', 'onHold')).toEqual({ kind: 'hold' });
    expect(resolveDrop('InProgress', 'done')).toEqual({ kind: 'complete' });
  });
});

describe('resolveDrop', () => {
  it('completes from every workable state', () => {
    expect(resolveDrop('Open', 'done')).toEqual({ kind: 'complete' });
    expect(resolveDrop('InProgress', 'done')).toEqual({ kind: 'complete' });
    expect(resolveDrop('OnHold', 'done')).toEqual({ kind: 'complete' });

    // From Missed this is CompletedLate on the server; the miss is never erased.
    expect(resolveDrop('Missed', 'done')).toEqual({ kind: 'complete' });
  });

  it('opens the reason dialog instead of guessing one', () => {
    expect(resolveDrop('Open', 'onHold')).toEqual({ kind: 'hold' });
  });

  it('reopens from on-hold and from either completed state', () => {
    expect(resolveDrop('OnHold', 'open')).toEqual({ kind: 'reopen' });
    expect(resolveDrop('Completed', 'open')).toEqual({ kind: 'reopen' });
    expect(resolveDrop('CompletedLate', 'open')).toEqual({ kind: 'reopen' });
  });

  it('never lets a user record a miss — that is the engine’s job alone', () => {
    const columns: BoardColumnId[] = ['open', 'inProgress', 'onHold', 'missed', 'done'];
    const statuses: WorkItemStatus[] = ['Open', 'InProgress', 'OnHold', 'Completed', 'CompletedLate', 'Cancelled'];

    for (const status of statuses) {
      expect(resolveDrop(status, 'missed')).toEqual({ kind: 'rejected' });
    }

    // And dropping a missed item back onto Missed is simply a no-op, not an error.
    expect(columns).toContain('missed');
    expect(resolveDrop('Missed', 'missed')).toEqual({ kind: 'noop' });
  });

  it('rejects the moves the API has no endpoint for', () => {
    expect(resolveDrop('Missed', 'onHold')).toEqual({ kind: 'rejected' });
    expect(resolveDrop('Completed', 'onHold')).toEqual({ kind: 'rejected' });
    expect(resolveDrop('Cancelled', 'open')).toEqual({ kind: 'rejected' });
    expect(resolveDrop('Cancelled', 'done')).toEqual({ kind: 'rejected' });
  });

  it('treats a drop back onto the same column as nothing at all', () => {
    expect(resolveDrop('Open', 'open')).toEqual({ kind: 'noop' });
    expect(resolveDrop('OnHold', 'onHold')).toEqual({ kind: 'noop' });
    expect(resolveDrop('InProgress', 'inProgress')).toEqual({ kind: 'noop' });
    expect(resolveDrop('Completed', 'done')).toEqual({ kind: 'noop' });
    expect(resolveDrop('CompletedLate', 'done')).toEqual({ kind: 'noop' });
  });
});
