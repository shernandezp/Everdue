import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { HoldReason, WorkItem } from '../../api/types';
import { api, type WorkItemFilters } from '../../lib/api';
import { notifyError } from '../../lib/notify';
import { keys } from '../../lib/queryKeys';

export function useWorkItems(filters: WorkItemFilters) {
  return useQuery({
    queryKey: keys.workItems.list(filters),
    queryFn: () => api.workItems.list(filters),
  });
}

export function useWorkItemDetail(id: string | null) {
  return useQuery({
    queryKey: keys.workItems.one(id),
    queryFn: () => api.workItems.get(id!),
    enabled: id !== null,
  });
}

export type WorkItemAction =
  | { kind: 'start' }
  | { kind: 'complete' }
  | { kind: 'reopen' }
  | { kind: 'cancel' }
  | { kind: 'hold'; reason: HoldReason; text?: string | null }
  | { kind: 'reschedule'; newDueDate: string; note?: string | null };

/**
 * Every mutation invalidates the whole work-item surface: the board, the list and the reports all
 * derive from the same rows, and a stale count on a dashboard is worse than a refetch.
 */
export function useWorkItemActions() {
  const queryClient = useQueryClient();

  const invalidate = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: keys.workItems.all }),
      queryClient.invalidateQueries({ queryKey: keys.workItems.detail }),
      queryClient.invalidateQueries({ queryKey: keys.reports.all }),
    ]);
  };

  return useMutation<WorkItem, unknown, { id: string; action: WorkItemAction }>({
    mutationFn: ({ id, action }) => {
      switch (action.kind) {
        case 'start':
          return api.workItems.start(id);
        case 'complete':
          return api.workItems.complete(id);
        case 'reopen':
          return api.workItems.reopen(id);
        case 'cancel':
          return api.workItems.cancel(id);
        case 'hold':
          return api.workItems.hold(id, action.reason, action.text);
        case 'reschedule':
          return api.workItems.reschedule(id, action.newDueDate, action.note);
      }
    },
    onSuccess: invalidate,
    // The server owns the transition matrix, so an invalid move comes back with its own reason.
    onError: notifyError,
  });
}

export function useComments(workItemId: string | null) {
  const queryClient = useQueryClient();

  const list = useQuery({
    queryKey: keys.comments.forItem(workItemId),
    queryFn: () => api.workItems.comments(workItemId!),
    enabled: workItemId !== null,
  });

  const add = useMutation({
    mutationFn: ({ body, mentionedUserIds }: { body: string; mentionedUserIds: string[] }) =>
      api.workItems.addComment(workItemId!, body, mentionedUserIds),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: keys.comments.forItem(workItemId) });
      await queryClient.invalidateQueries({ queryKey: keys.workItems.one(workItemId) });
    },
    onError: notifyError,
  });

  const remove = useMutation({
    mutationFn: (commentId: string) => api.workItems.deleteComment(commentId),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: keys.comments.forItem(workItemId) });
      await queryClient.invalidateQueries({ queryKey: keys.workItems.one(workItemId) });
    },
    onError: notifyError,
  });

  return { list, add, remove };
}
