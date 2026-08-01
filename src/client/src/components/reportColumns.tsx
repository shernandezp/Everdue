import { Anchor, Group } from '@mantine/core';
import { IconChevronRight } from '@tabler/icons-react';
import type { DataTableColumn } from 'mantine-datatable';
import type { TFunction } from 'i18next';
import { Link } from 'react-router-dom';
import type { DrillThrough, EntityType } from '../api/types';
import { drillLink } from '../features/reports/drill';
import { EM_DASH, formatDate, formatDays } from '../lib/format';
import { EntityLink } from './EntityLink';

/**
 * The columns every report and insight table shares.
 *
 * Nine screens are the same shape: name it, type it, count it, and offer the list behind the number.
 * Each had grown its own copy — which is how one of them ended up rendering a null count as blank
 * while its neighbour showed an em dash. These are factories rather than components because
 * mantine-datatable wants column *descriptors*, not elements.
 */

/** A row that can be drilled into. Every report row the server returns carries one. */
type Drillable = { drillThrough: DrillThrough };

/**
 * "See the items behind this number." Deliberately last and unlabelled: the numbers are the content,
 * and a repeated header for a link column is nine words of noise per table.
 */
export function drillThroughColumn<T extends Drillable>(t: TFunction): DataTableColumn<T> {
  return {
    accessor: 'drillThrough',
    title: '',
    render: (row) => (
      <Anchor component={Link} to={drillLink(row.drillThrough)} size="xs">
        <Group gap={2} wrap="nowrap">
          {t('reports.drillThrough')}
          <IconChevronRight size={12} />
        </Group>
      </Anchor>
    ),
  };
}

/** An entity's name, linked to its timeline. */
export function entityNameColumn<T extends { entityId: string | null; entityName: string | null }>(
  t: TFunction,
  options: { title?: string; unlinkedLabel?: string } = {},
): DataTableColumn<T> {
  return {
    accessor: 'entityName',
    title: options.title ?? t('common.name'),
    render: (row) => (
      <EntityLink entityId={row.entityId} name={row.entityName} fallback={options.unlinkedLabel} />
    ),
  };
}

/** The entity's type, translated. The taxonomy is a closed enum, so the key always resolves. */
export function entityTypeColumn<T extends { entityType: EntityType | null }>(t: TFunction): DataTableColumn<T> {
  return {
    accessor: 'entityType',
    title: t('common.type'),
    render: (row) => (row.entityType ? t(`entityType.${row.entityType}`) : EM_DASH),
  };
}

/** A count. Right-aligned, because a column of numbers is only scannable when the digits line up. */
export function countColumn<T>(accessor: string, title: string, sortable = false): DataTableColumn<T> {
  return { accessor, title, sortable, textAlign: 'right' };
}

/** A wait measured in calendar days, to one decimal. */
export function daysColumn<T>(
  accessor: keyof T & string,
  title: string,
  sortable = false,
): DataTableColumn<T> {
  return {
    accessor,
    title,
    sortable,
    textAlign: 'right',
    render: (row) => formatDays(row[accessor] as number),
  };
}

/**
 * A nullable count. Absent is not zero: an entity with no recorded activity has not been neglected
 * for zero days, so the two render differently and the caller chooses the placeholder.
 */
export function nullableCountColumn<T>(
  accessor: keyof T & string,
  title: string,
  sortable = false,
  absent: string = EM_DASH,
): DataTableColumn<T> {
  return {
    accessor,
    title,
    sortable,
    textAlign: 'right',
    render: (row) => (row[accessor] as number | null | undefined) ?? absent,
  };
}

/** A timestamp, or the translated "never" when the thing has not happened yet. */
export function lastActivityColumn<T extends { lastActivityAt: string | null }>(
  t: TFunction,
  title: string,
): DataTableColumn<T> {
  return {
    accessor: 'lastActivityAt',
    title,
    render: (row) => (row.lastActivityAt ? formatDate(row.lastActivityAt) : t('common.never')),
  };
}
