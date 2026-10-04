import { useEffect } from 'react';
import type { Paged } from '../api/types';
import { PAGE_SIZE } from './api';

export function usePagination(data: Paged<unknown> | undefined, page: number, setPage: (page: number) => void) {
  const pageSize = data?.pageSize ?? PAGE_SIZE;
  const lastPage = Math.max(1, Math.ceil((data?.totalCount ?? 0) / pageSize));

  // Deactivating the only row on the last page would otherwise strand the table past the end.
  useEffect(() => {
    if (data && page > lastPage) setPage(lastPage);
  }, [data, page, lastPage, setPage]);

  return { totalRecords: data?.totalCount ?? 0, recordsPerPage: pageSize, page, onPageChange: setPage };
}
