import { describe, it, expect, vi, afterEach } from 'vitest';
import {
  hasEngineConfig,
  hasPublicSupabaseConfig,
  hasServerSupabaseConfig,
  hasStripeConfig,
} from '../../src/lib/runtime-config';

describe('runtime-config', () => {
  afterEach(() => {
    vi.unstubAllEnvs();
  });

  it('hasPublicSupabaseConfig returns true only when public URL and a real anon key are set', () => {
    vi.stubEnv('NEXT_PUBLIC_SUPABASE_URL', 'https://example.supabase.co');
    vi.stubEnv('NEXT_PUBLIC_SUPABASE_ANON_KEY', 'sb_publishable_example');
    expect(hasPublicSupabaseConfig()).toBe(true);

    // A truthy-but-bogus placeholder must be rejected, not silently accepted.
    vi.stubEnv('NEXT_PUBLIC_SUPABASE_ANON_KEY', 'anon-key');
    expect(hasPublicSupabaseConfig()).toBe(false);

    vi.stubEnv('NEXT_PUBLIC_SUPABASE_ANON_KEY', '');
    expect(hasPublicSupabaseConfig()).toBe(false);
  });

  it('hasServerSupabaseConfig returns true only when public URL and service role are set', () => {
    vi.stubEnv('NEXT_PUBLIC_SUPABASE_URL', 'https://example.supabase.co');
    vi.stubEnv('SUPABASE_SERVICE_ROLE_KEY', 'service-role-key');
    expect(hasServerSupabaseConfig()).toBe(true);

    vi.stubEnv('SUPABASE_SERVICE_ROLE_KEY', '');
    expect(hasServerSupabaseConfig()).toBe(false);
  });

  it('hasStripeConfig and hasEngineConfig follow env presence', () => {
    vi.stubEnv('STRIPE_SECRET_KEY', 'sk_test_123');
    expect(hasStripeConfig()).toBe(true);

    vi.stubEnv('STRIPE_SECRET_KEY', '');
    expect(hasStripeConfig()).toBe(false);

    vi.stubEnv('ENGINE_API_URL', 'http://localhost:5000');
    expect(hasEngineConfig()).toBe(true);
  });
});
