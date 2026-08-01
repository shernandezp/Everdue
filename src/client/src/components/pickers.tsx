import { Select } from '@mantine/core';
import { useDebouncedValue } from '@mantine/hooks';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '../lib/api';
import { ENTITY_TYPES, type EntityDto } from '../api/types';
import { keys } from '../lib/queryKeys';

type PickerProps = {
  value: string | null;
  onChange: (value: string | null) => void;
  label?: string;
  placeholder?: string;
  clearable?: boolean;
  required?: boolean;
  disabled?: boolean;
  error?: string;
  w?: number | string;
};

/**
 * Entities are searched on the server. The old picker loaded one page of 100 and filtered it in the
 * browser, which meant a business with 150 customers could not link work to customer #120 at all —
 * the option simply was not there to find.
 */
export function EntityPicker(props: PickerProps) {
  const { t } = useTranslation();
  const [search, setSearch] = useState('');
  const [debounced] = useDebouncedValue(search.trim(), 250);

  const entities = useQuery({
    queryKey: keys.entities.pickerSearch(debounced),
    queryFn: () => api.entities.list(debounced ? { search: debounced } : {}),
    // Keep the previous page on screen while the next keystroke's result arrives.
    placeholderData: keepPreviousData,
  });

  const toOption = (entity: EntityDto) => ({
    value: entity.id,
    label: `${entity.name} · ${t(`entityType.${entity.type}`)}`,
  });

  const options = (entities.data?.items ?? []).map(toOption);

  // The selected entity may not be in the page being shown (a drill-through link, an old task, a
  // different search) — resolve it by id so a real value never renders as a blank control.
  const selectedMissing = props.value !== null && !options.some((option) => option.value === props.value);
  const selected = useQuery({
    queryKey: keys.entities.one(props.value),
    queryFn: () => api.entities.get(props.value!),
    enabled: selectedMissing,
  });

  const data = selectedMissing && selected.data ? [toOption(selected.data), ...options] : options;

  return (
    <Select
      searchable
      searchValue={search}
      onSearchChange={setSearch}
      // The server already filtered; filtering again here would drop the resolved selected option.
      filter={({ options: current }) => current}
      nothingFoundMessage={entities.isFetching ? undefined : t('common.noResults')}
      clearable={props.clearable ?? true}
      label={props.label ?? t('workItem.entity')}
      placeholder={props.placeholder ?? t('common.none')}
      data={data}
      value={props.value}
      onChange={props.onChange}
      required={props.required}
      disabled={props.disabled}
      error={props.error}
      w={props.w}
    />
  );
}

export function DepartmentPicker(props: PickerProps) {
  const { t } = useTranslation();
  const departments = useQuery({ queryKey: keys.departments.picker, queryFn: () => api.departments.list() });

  return (
    <Select
      searchable
      clearable={props.clearable ?? true}
      label={props.label ?? t('workItem.department')}
      placeholder={props.placeholder ?? t('common.none')}
      data={(departments.data?.items ?? []).map((department) => ({ value: department.id, label: department.name }))}
      value={props.value}
      onChange={props.onChange}
      required={props.required}
      disabled={props.disabled}
      error={props.error}
      w={props.w}
    />
  );
}

/** Only active users can be assigned new work; the server enforces the same rule. */
export function UserPicker(props: PickerProps) {
  const { t } = useTranslation();
  const users = useQuery({ queryKey: keys.users.all, queryFn: api.users.list });

  return (
    <Select
      searchable
      clearable={props.clearable ?? false}
      label={props.label ?? t('workItem.owner')}
      placeholder={props.placeholder ?? t('common.none')}
      data={(users.data ?? []).filter((user) => user.active).map((user) => ({ value: user.id, label: user.displayName }))}
      value={props.value}
      onChange={props.onChange}
      required={props.required}
      disabled={props.disabled}
      error={props.error}
      w={props.w}
    />
  );
}

export function EntityTypePicker(props: PickerProps) {
  const { t } = useTranslation();

  return (
    <Select
      clearable={props.clearable ?? true}
      label={props.label ?? t('common.type')}
      placeholder={props.placeholder ?? t('common.all')}
      data={ENTITY_TYPES.map((type) => ({ value: type, label: t(`entityType.${type}`) }))}
      value={props.value}
      onChange={props.onChange}
      required={props.required}
      disabled={props.disabled}
      error={props.error}
      w={props.w}
    />
  );
}
