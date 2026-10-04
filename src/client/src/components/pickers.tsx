import { Select } from '@mantine/core';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { api } from '../lib/api';
import { ENTITY_TYPES, type DepartmentOption, type EntityOption } from '../api/types';
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

export function EntityPicker(props: PickerProps) {
  const { t } = useTranslation();
  const entities = useQuery({ queryKey: keys.entities.picker, queryFn: api.entities.options });

  const toOption = (entity: EntityOption) => ({
    value: entity.id,
    label: `${entity.name} · ${t(`entityType.${entity.type}`)}`,
  });

  const options = (entities.data ?? []).map(toOption);

  // Only active entities are offered, but an old task or a drill-through link can still point at an
  // inactive one — resolve it by id so a real value never renders as a blank control.
  const selectedMissing =
    entities.isSuccess && props.value !== null && !options.some((option) => option.value === props.value);
  const selected = useQuery({
    queryKey: keys.entities.one(props.value),
    queryFn: () => api.entities.get(props.value!),
    enabled: selectedMissing,
  });

  const data = selectedMissing && selected.data ? [toOption(selected.data), ...options] : options;

  return (
    <Select
      searchable
      nothingFoundMessage={t('common.noResults')}
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
  const departments = useQuery({ queryKey: keys.departments.picker, queryFn: api.departments.options });

  const toOption = (department: DepartmentOption) => ({ value: department.id, label: department.name });
  const options = (departments.data ?? []).map(toOption);

  // Same as entities: an inactive department is not offered, but one already set must still show.
  const selectedMissing =
    departments.isSuccess && props.value !== null && !options.some((option) => option.value === props.value);
  const selected = useQuery({
    queryKey: keys.departments.one(props.value),
    queryFn: () => api.departments.get(props.value!),
    enabled: selectedMissing,
  });

  const data = selectedMissing && selected.data ? [toOption(selected.data), ...options] : options;

  return (
    <Select
      searchable
      clearable={props.clearable ?? true}
      label={props.label ?? t('workItem.department')}
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
