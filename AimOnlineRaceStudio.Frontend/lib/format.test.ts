import { describe, it, expect } from 'vitest';
import {
  formatLapTime,
  formatDate,
  formatSessionDateTime,
  formatLapStartActual,
  formatMb,
} from './format';

describe('formatLapTime', () => {
  it('formats seconds as M:SS:mmm', () => {
    expect(formatLapTime(0)).toBe('0:00:000');
    expect(formatLapTime(1.5)).toBe('0:01:500');
    expect(formatLapTime(83.456)).toBe('1:23:456');
    expect(formatLapTime(90.001)).toBe('1:30:001');
  });

  it('rounds to nearest millisecond', () => {
    expect(formatLapTime(1.2345)).toBe('0:01:235');
  });
});

describe('formatDate', () => {
  it('formats valid ISO string to locale date/time', () => {
    const result = formatDate('2025-03-15T14:30:00.000Z');
    expect(typeof result).toBe('string');
    expect(result.length).toBeGreaterThan(0);
    expect(result).not.toBe('2025-03-15T14:30:00.000Z');
  });

  it('returns Invalid Date or raw string for unparseable input', () => {
    const result = formatDate('not-a-date');
    expect(result === 'not-a-date' || result === 'Invalid Date').toBe(true);
  });
});

describe('formatSessionDateTime', () => {
  it('returns null when libraryDate is missing', () => {
    expect(formatSessionDateTime(null, '12:00:00')).toBeNull();
    expect(formatSessionDateTime(undefined, '12:00:00')).toBeNull();
    expect(formatSessionDateTime('', '12:00:00')).toBeNull();
  });

  it('returns null when libraryTime is missing', () => {
    expect(formatSessionDateTime('2025-03-15', null)).toBeNull();
    expect(formatSessionDateTime('2025-03-15', '')).toBeNull();
  });

  it('returns locale string for valid date and time', () => {
    const result = formatSessionDateTime('2025-03-15', '14:30:00');
    expect(result).toBeTypeOf('string');
    expect(result).not.toBeNull();
  });

  it('returns null for unparseable date', () => {
    expect(formatSessionDateTime('invalid', '12:00:00')).toBeNull();
  });
});

describe('formatLapStartActual', () => {
  it('returns session-relative lap time when session date/time missing', () => {
    expect(formatLapStartActual(65.5, null, null)).toBe('1:05:500');
    expect(formatLapStartActual(65.5, undefined, '12:00:00')).toBe('1:05:500');
  });

  it('returns clock time when session date and time provided', () => {
    const result = formatLapStartActual(0, '2025-03-15', '14:30:00');
    expect(result).toBeTypeOf('string');
    expect(result).toMatch(/\d{1,2}:\d{2}:\d{2}/);
  });
});

describe('formatMb', () => {
  it('formats bytes as MB with 2 decimal places', () => {
    expect(formatMb(0)).toBe('0.00');
    expect(formatMb(1024 * 1024)).toBe('1.00');
    expect(formatMb(1536 * 1024)).toBe('1.50');
    expect(formatMb(120 * 1024 * 1024)).toBe('120.00');
  });
});
