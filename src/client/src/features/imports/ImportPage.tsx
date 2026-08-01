import { Button, FileInput, Group, SegmentedControl, Stack, Stepper, Text } from '@mantine/core';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { IconArrowRight, IconFileImport, IconFileSpreadsheet } from '@tabler/icons-react';
import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import type { ImportPreview, ImportResult } from '../../api/types';
import { PageHeader } from '../../components/PageHeader';
import { api } from '../../lib/api';
import { notifyError } from '../../lib/notify';
import { keys } from '../../lib/queryKeys';
import { ColumnMappingStep } from './ColumnMappingStep';
import { ImportResultStep } from './ImportResultStep';

type ImportKindValue = 'entities' | 'workitems';

/**
 * The on-ramp off a spreadsheet: upload, confirm the mapping against real rows, commit.
 *
 * The file is held in component state and <strong>posted twice</strong> — once to preview, once to commit. That
 * is deliberate: the alternative is a temp file, a token table, an expiry sweeper and a leak when somebody closes
 * the tab, which is a whole subsystem for a 200 KB spreadsheet.
 */
export function ImportPage() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [params] = useSearchParams();

  const initialKind: ImportKindValue = params.get('kind') === 'workitems' ? 'workitems' : 'entities';

  const [kind, setKind] = useState<ImportKindValue>(initialKind);
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<ImportPreview | null>(null);
  const [mapping, setMapping] = useState<Record<string, string>>({});
  const [result, setResult] = useState<ImportResult | null>(null);

  const step = result ? 2 : preview ? 1 : 0;

  const startPreview = useMutation({
    mutationFn: (selected: File) => api.imports.preview(kind, selected),
    onSuccess: (data) => {
      setPreview(data);
      setMapping({ ...data.suggestedMapping });
    },
    onError: notifyError,
  });

  const commit = useMutation({
    mutationFn: () => api.imports.commit(kind, file!, mapping),
    onSuccess: async (data) => {
      setResult(data);

      // Whatever was created has to appear on the screens that list it.
      await queryClient.invalidateQueries({ queryKey: keys.entities.all });
      await queryClient.invalidateQueries({ queryKey: keys.workItems.all });
    },
    onError: notifyError,
  });

  const reset = () => {
    setFile(null);
    setPreview(null);
    setMapping({});
    setResult(null);
  };

  return (
    <Stack>
      <PageHeader title={t('imports.title')} description={t('imports.description')} />

      <Stepper active={step} size="sm">
        <Stepper.Step label={t('imports.stepFile')} />
        <Stepper.Step label={t('imports.stepMapping')} />
        <Stepper.Step label={t('imports.stepResult')} />
      </Stepper>

      {step === 0 && (
        <Stack maw={520}>
          <SegmentedControl
            value={kind}
            data={[
              { value: 'entities', label: t('imports.kinds.entities') },
              { value: 'workitems', label: t('imports.kinds.workItems') },
            ]}
            onChange={(value) => {
              setKind(value as ImportKindValue);
              reset();
            }}
          />

          <Text size="sm" c="dimmed">
            {kind === 'entities' ? t('imports.entitiesHint') : t('imports.workItemsHint')}
          </Text>

          <FileInput
            label={t('imports.file')}
            description={t('imports.fileHint')}
            accept=".csv,text/csv"
            leftSection={<IconFileSpreadsheet size={16} />}
            value={file}
            clearable
            onChange={setFile}
          />

          <Group>
            <Button
              disabled={!file}
              loading={startPreview.isPending}
              rightSection={<IconArrowRight size={16} />}
              onClick={() => file && startPreview.mutate(file)}
            >
              {t('imports.continue')}
            </Button>
          </Group>
        </Stack>
      )}

      {step === 1 && preview && (
        <Stack>
          <ColumnMappingStep preview={preview} mapping={mapping} onChange={setMapping} />

          <Group justify="flex-end">
            <Button variant="default" onClick={reset}>
              {t('common.cancel')}
            </Button>
            <Button
              loading={commit.isPending}
              leftSection={<IconFileImport size={16} />}
              onClick={() => commit.mutate()}
            >
              {t('imports.import', { count: preview.totalRows })}
            </Button>
          </Group>
        </Stack>
      )}

      {step === 2 && result && <ImportResultStep result={result} onDone={reset} />}
    </Stack>
  );
}
