import { describe, it, expect } from 'vitest';
import { groupFilesByTrack } from './grouping';
import type { XrkFileSummary } from '@/types/api';

function file(
  overrides: Partial<XrkFileSummary> & { id: string }
): XrkFileSummary {
  return {
    id: overrides.id,
    file_hash: overrides.file_hash ?? '',
    filename: overrides.filename ?? null,
    vehicle: overrides.vehicle ?? null,
    track: overrides.track ?? null,
    created_at: overrides.created_at ?? '',
    lap_count: overrides.lap_count ?? 0,
    ...overrides,
  };
}

describe('groupFilesByTrack', () => {
  it('returns empty array for empty input', () => {
    expect(groupFilesByTrack([])).toEqual([]);
  });

  it('groups files by track', () => {
    const files: XrkFileSummary[] = [
      file({ id: '1', track: 'Silverstone' }),
      file({ id: '2', track: 'Silverstone' }),
      file({ id: '3', track: 'Monza' }),
    ];
    const result = groupFilesByTrack(files);
    expect(result).toHaveLength(2);
    expect(result[0].track).toBe('Monza');
    expect(result[0].files).toHaveLength(1);
    expect(result[0].files[0].id).toBe('3');
    expect(result[1].track).toBe('Silverstone');
    expect(result[1].files).toHaveLength(2);
  });

  it('sorts track names alphabetically with — last', () => {
    const files: XrkFileSummary[] = [
      file({ id: '1', track: '—' }),
      file({ id: '2', track: 'Monza' }),
      file({ id: '3', track: 'Silverstone' }),
    ];
    const result = groupFilesByTrack(files);
    expect(result[0].track).toBe('Monza');
    expect(result[1].track).toBe('Silverstone');
    expect(result[2].track).toBe('—');
  });

  it('sorts files within group by shortest_middle_lap_sec ascending', () => {
    const files: XrkFileSummary[] = [
      file({ id: '1', track: 'Silverstone', shortest_middle_lap_sec: 95.5 }),
      file({ id: '2', track: 'Silverstone', shortest_middle_lap_sec: 93.1 }),
      file({ id: '3', track: 'Silverstone', shortest_middle_lap_sec: 94.0 }),
    ];
    const result = groupFilesByTrack(files);
    expect(result[0].files.map((f) => f.id)).toEqual(['2', '3', '1']);
  });

  it('treats null/undefined shortest_middle_lap_sec as infinity', () => {
    const files: XrkFileSummary[] = [
      file({ id: '1', track: 'Silverstone' }),
      file({ id: '2', track: 'Silverstone', shortest_middle_lap_sec: 90 }),
    ];
    const result = groupFilesByTrack(files);
    expect(result[0].files[0].id).toBe('2');
    expect(result[0].files[1].id).toBe('1');
  });

  it('uses — for missing or whitespace track', () => {
    const files: XrkFileSummary[] = [
      file({ id: '1', track: null }),
      file({ id: '2', track: '  ' }),
    ];
    const result = groupFilesByTrack(files);
    expect(result).toHaveLength(1);
    expect(result[0].track).toBe('—');
    expect(result[0].files).toHaveLength(2);
  });
});
