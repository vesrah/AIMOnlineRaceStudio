import { describe, it, expect } from 'vitest';
import { getErrorMessage } from './utils';

describe('getErrorMessage', () => {
  it('returns message from Error instance', () => {
    expect(getErrorMessage(new Error('Something failed'), 'Fallback')).toBe('Something failed');
  });

  it('returns fallback for non-Error values', () => {
    expect(getErrorMessage('string', 'Fallback')).toBe('Fallback');
    expect(getErrorMessage(42, 'Fallback')).toBe('Fallback');
    expect(getErrorMessage(null, 'Fallback')).toBe('Fallback');
    expect(getErrorMessage(undefined, 'Fallback')).toBe('Fallback');
  });
});
