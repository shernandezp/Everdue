import { describe as suite, expect, it } from 'vitest';
import { ApiError } from './http';
import { describe } from './notify';

suite('describe', () => {
  it('shows the field reasons of a validation refusal, not the generic summary', () => {
    const error = new ApiError(400, {
      code: 'validation_failed',
      detail: 'One or more validation errors occurred.',
      errors: { file: ["'.xls' files are not allowed. Allowed: .xlsx."] },
    });

    expect(describe(error)).toBe("'.xls' files are not allowed. Allowed: .xlsx.");
  });

  it('falls back to the detail when a validation refusal has no field reasons', () => {
    const error = new ApiError(400, { code: 'validation_failed', detail: 'The column mapping could not be read.' });

    expect(describe(error)).toBe('The column mapping could not be read.');
  });
});
