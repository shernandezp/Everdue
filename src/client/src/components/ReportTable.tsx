import { DataTable, type DataTableColumn, type DataTableProps, type DataTableSortStatus } from 'mantine-datatable';
import { useTranslation } from 'react-i18next';

/**
 * The table every report and insight screen renders.
 *
 * Eleven screens passed the same five presentation props — bordered, a minimum height so a loading
 * table does not collapse, a fetching flag, an id accessor, an empty message — and the ones that
 * disagreed disagreed by accident. Only `minHeight` is worth varying (a card-embedded table is
 * shorter), so that is the one prop with a second value.
 *
 * Sorting is server-side everywhere: `sortStatus` is passed through untouched, and the page maps the
 * accessor to the API's sort name via `useServerSort`.
 */
export function ReportTable<T>({
  records,
  columns,
  idAccessor,
  fetching,
  emptyText,
  sort,
  onSortChange,
  compact = false,
}: {
  records: T[];
  columns: DataTableColumn<T>[];
  idAccessor: string | ((row: T) => string);
  fetching?: boolean;
  /** Defaults to the shared "no results" string; pass one when a screen can say something better. */
  emptyText?: string;
  sort?: DataTableSortStatus<T>;
  onSortChange?: (sort: DataTableSortStatus<T>) => void;
  /** Shorter minimum height, for a table that sits inside a card beside others. */
  compact?: boolean;
}) {
  const { t } = useTranslation();

  // DataTableProps is a discriminated union over the pagination and sorting variants. A wrapper that
  // makes sorting optional cannot satisfy that union structurally, so the assembled props are asserted
  // once here — which is the whole reason this cast is in one file instead of eleven.
  const props = {
    withTableBorder: true,
    highlightOnHover: true,
    minHeight: compact ? 160 : 200,
    fetching,
    records,
    idAccessor,
    noRecordsText: emptyText ?? t('common.noResults'),
    columns,
    ...(sort && onSortChange ? { sortStatus: sort, onSortStatusChange: onSortChange } : {}),
  } as DataTableProps<T>;

  return <DataTable {...props} />;
}
