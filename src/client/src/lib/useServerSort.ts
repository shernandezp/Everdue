import { useState } from 'react';
import type { DataTableSortStatus } from 'mantine-datatable';

/**
 * Sort state for a table the *server* sorts.
 *
 * Sorting has to happen server-side because these tables are paged and their interesting columns are
 * derived — a client cannot sort by "days since last activity" over rows it has not fetched. That
 * means every screen carried the same three-part translation: a map from column accessor to the API's
 * sort name, a fallback for an accessor that has none, and `descending: direction === 'desc'`.
 *
 * `params` is what the API call spreads in.
 */
export function useServerSort<T>(
  columnToSortName: Record<string, string>,
  initial: { column: keyof T & string; direction?: 'asc' | 'desc' },
) {
  const [sort, setSort] = useState<DataTableSortStatus<T>>({
    columnAccessor: initial.column,
    direction: initial.direction ?? 'desc',
  });

  const name = columnToSortName[String(sort.columnAccessor)] ?? columnToSortName[initial.column];

  return {
    sort,
    setSort,
    params: { sort: name, descending: sort.direction === 'desc' },
  };
}
