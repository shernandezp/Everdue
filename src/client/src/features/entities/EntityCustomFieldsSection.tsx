import { NumberInput, Select, Stack, Text, TextInput } from '@mantine/core';
import { useTranslation } from 'react-i18next';
import type { EntityCustomFieldValue } from '../../api/types';

/**
 * The custom fields defined for an entity's type.
 *
 * <strong>Reference information, not business data.</strong> Nothing filters, sorts or reports on these — the
 * guardrails say an entity that grows credit limits and contact persons has stopped being a thin reference — and
 * the note under the heading says so where somebody is about to fill them in.
 */
export function EntityCustomFieldsSection({
  fields,
  values,
  onChange,
}: {
  fields: EntityCustomFieldValue[];
  values: Record<string, string>;
  onChange: (values: Record<string, string>) => void;
}) {
  const { t } = useTranslation();

  if (fields.length === 0) return null;

  const set = (definitionId: string, value: string) => onChange({ ...values, [definitionId]: value });

  return (
    <Stack gap="xs">
      <Text size="sm" fw={500}>
        {t('entityFields.sectionTitle')}
      </Text>
      <Text size="xs" c="dimmed">
        {t('entityFields.sectionHint')}
      </Text>

      {fields.map((field) => {
        const value = values[field.definitionId] ?? '';

        if (field.fieldType === 'Select') {
          return (
            <Select
              key={field.definitionId}
              label={field.name}
              data={field.options}
              value={value === '' ? null : value}
              clearable
              onChange={(next) => set(field.definitionId, next ?? '')}
            />
          );
        }

        if (field.fieldType === 'Number') {
          return (
            <NumberInput
              key={field.definitionId}
              label={field.name}
              value={value === '' ? '' : Number(value)}
              onChange={(next) => set(field.definitionId, next === '' ? '' : String(next))}
            />
          );
        }

        return (
          <TextInput
            key={field.definitionId}
            label={field.name}
            type={field.fieldType === 'Date' ? 'date' : 'text'}
            maxLength={field.fieldType === 'Date' ? undefined : 200}
            value={value}
            onChange={(event) => set(field.definitionId, event.currentTarget.value)}
          />
        );
      })}
    </Stack>
  );
}

/** The stored values of an entity, as the form holds them. */
export function customFieldValues(fields: EntityCustomFieldValue[] | undefined): Record<string, string> {
  return Object.fromEntries((fields ?? []).map((field) => [field.definitionId, field.value ?? '']));
}
