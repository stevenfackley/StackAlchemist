import { describe, it, expect, vi, afterEach } from 'vitest';
import {
  assertAuthModeConsistent,
  getQavrenAuthUrl,
  getQavrenRealm,
  hasDataStoreConfig,
  hasEngineConfig,
  hasPublicSupabaseConfig,
  hasServerSupabaseConfig,
  hasStripeConfig,
  usesQavrenAuth,
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

  it('hasDataStoreConfig is true with DATABASE_URL alone, or with the Supabase service pair', () => {
    vi.stubEnv('DATABASE_URL', '');
    vi.stubEnv('NEXT_PUBLIC_SUPABASE_URL', '');
    vi.stubEnv('SUPABASE_SERVICE_ROLE_KEY', '');
    expect(hasDataStoreConfig()).toBe(false);

    vi.stubEnv('DATABASE_URL', 'postgres://u:p@localhost:5432/db');
    expect(hasDataStoreConfig()).toBe(true);

    vi.stubEnv('DATABASE_URL', '');
    vi.stubEnv('NEXT_PUBLIC_SUPABASE_URL', 'https://example.supabase.co');
    vi.stubEnv('SUPABASE_SERVICE_ROLE_KEY', 'service-role-key');
    expect(hasDataStoreConfig()).toBe(true);
  });

  it('hasStripeConfig and hasEngineConfig follow env presence', () => {
    vi.stubEnv('STRIPE_SECRET_KEY', 'sk_test_123');
    expect(hasStripeConfig()).toBe(true);

    vi.stubEnv('STRIPE_SECRET_KEY', '');
    expect(hasStripeConfig()).toBe(false);

    vi.stubEnv('ENGINE_API_URL', 'http://localhost:5000');
    expect(hasEngineConfig()).toBe(true);
  });

  it('usesQavrenAuth follows QAVREN_AUTH_URL presence and ignores whitespace', () => {
    vi.stubEnv('QAVREN_AUTH_URL', '');
    expect(usesQavrenAuth()).toBe(false);
    vi.stubEnv('QAVREN_AUTH_URL', '   ');
    expect(usesQavrenAuth()).toBe(false);
    vi.stubEnv('QAVREN_AUTH_URL', 'http://localhost:8090');
    expect(usesQavrenAuth()).toBe(true);
  });

  it('getQavrenAuthUrl strips a trailing slash and getQavrenRealm defaults to stackalchemist', () => {
    vi.stubEnv('QAVREN_AUTH_URL', 'https://auth.stackalchemist.app/');
    vi.stubEnv('QAVREN_REALM', '');
    expect(getQavrenAuthUrl()).toBe('https://auth.stackalchemist.app');
    expect(getQavrenRealm()).toBe('stackalchemist');
    vi.stubEnv('QAVREN_REALM', 'stackalchemist-dev');
    expect(getQavrenRealm()).toBe('stackalchemist-dev');
  });

  it('assertAuthModeConsistent refuses Qavren Auth without the qavren-db store', () => {
    vi.stubEnv('QAVREN_AUTH_URL', 'http://localhost:8090');
    vi.stubEnv('DATABASE_URL', '');
    expect(() => assertAuthModeConsistent()).toThrow(/DATABASE_URL/);
    vi.stubEnv('DATABASE_URL', 'postgres://u:p@localhost:5432/db');
    expect(() => assertAuthModeConsistent()).not.toThrow();
    vi.stubEnv('QAVREN_AUTH_URL', '');
    vi.stubEnv('DATABASE_URL', '');
    expect(() => assertAuthModeConsistent()).not.toThrow();
  });

  it('assertAuthModeConsistent rejects a QAVREN_AUTH_URL that is not an absolute http(s) URL', () => {
    vi.stubEnv('DATABASE_URL', 'postgres://u:p@localhost:5432/db');
    for (const bad of ['/', 'auth.stackalchemist.app']) {
      vi.stubEnv('QAVREN_AUTH_URL', bad);
      expect(() => assertAuthModeConsistent()).toThrow(/http\(s\)/);
    }
    vi.stubEnv('QAVREN_AUTH_URL', 'http://localhost:8090');
    expect(() => assertAuthModeConsistent()).not.toThrow();
  });
});
