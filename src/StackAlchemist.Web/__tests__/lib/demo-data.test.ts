import { describe, expect, it } from 'vitest';
import { buildDemoGeneration, demoPreviewFiles, demoSchema } from '../../src/lib/demo-data';

describe('demo-data', () => {
  it('buildDemoGeneration returns spark preview without download url', () => {
    const generation = buildDemoGeneration('demo-simple-123', 0);

    expect(generation.id).toBe('demo-simple-123');
    expect(generation.tier).toBe(0);
    expect(generation.status).toBe('success');
    expect(generation.preview_files_json).toEqual(demoPreviewFiles);
    expect(generation.download_url).toBeNull();
  });

  it('buildDemoGeneration returns paid download placeholder for non-spark tiers', () => {
    const generation = buildDemoGeneration('demo-advanced-456', 2);

    expect(generation.mode).toBe('advanced');
    expect(generation.tier).toBe(2);
    expect(generation.download_url).toBe('/demo-download.zip');
    expect(generation.schema_json).toEqual(demoSchema);
  });
});
