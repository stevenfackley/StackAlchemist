// @vitest-environment node
import { describe, it, expect, vi, afterEach } from 'vitest';
import {
  assertProductionConfig,
  getQavrenAuthUrl,
  getQavrenRealm,
  hasDataStoreConfig,
  hasEngineConfig,
  hasStripeConfig,
  usesQavrenAuth,
} from '../../src/lib/runtime-config';

describe('runtime-config', () => {
  afterEach(() => {
    vi.unstubAllEnvs();
  });

  it('hasDataStoreConfig follows DATABASE_URL and ignores whitespace', () => {
    vi.stubEnv('DATABASE_URL', '');
    expect(hasDataStoreConfig()).toBe(false);

    vi.stubEnv('DATABASE_URL', '   ');
    expect(hasDataStoreConfig()).toBe(false);

    vi.stubEnv('DATABASE_URL', 'postgres://u:p@localhost:5432/db');
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

  it('assertProductionConfig refuses Qavren Auth without the qavren-db store', () => {
    vi.stubEnv('AUTH_SECRET', 'x'.repeat(44));
    vi.stubEnv('QAVREN_AUTH_URL', 'http://localhost:8090');
    vi.stubEnv('DATABASE_URL', '');
    expect(() => assertProductionConfig()).toThrow(/DATABASE_URL/);
    vi.stubEnv('DATABASE_URL', 'postgres://u:p@localhost:5432/db');
    expect(() => assertProductionConfig()).not.toThrow();
    // Outside production, neither set is demo mode.
    vi.stubEnv('QAVREN_AUTH_URL', '');
    vi.stubEnv('DATABASE_URL', '');
    expect(() => assertProductionConfig()).not.toThrow();
  });

  it('assertProductionConfig rejects a QAVREN_AUTH_URL that is not an absolute http(s) URL', () => {
    vi.stubEnv('AUTH_SECRET', 'x'.repeat(44));
    vi.stubEnv('DATABASE_URL', 'postgres://u:p@localhost:5432/db');
    for (const bad of ['/', 'auth.stackalchemist.app', 'http://a b']) {
      vi.stubEnv('QAVREN_AUTH_URL', bad);
      expect(() => assertProductionConfig()).toThrow(/http\(s\)/);
    }
    vi.stubEnv('QAVREN_AUTH_URL', 'http://localhost:8090');
    expect(() => assertProductionConfig()).not.toThrow();
  });

  it('assertProductionConfig requires AUTH_SECRET with QAVREN_AUTH_URL, and only then outside production', () => {
    vi.stubEnv('QAVREN_AUTH_URL', 'http://localhost:8090');
    vi.stubEnv('DATABASE_URL', 'postgres://u:p@localhost:5432/db');
    vi.stubEnv('AUTH_SECRET', '');
    expect(() => assertProductionConfig()).toThrow(/AUTH_SECRET/);
    vi.stubEnv('AUTH_SECRET', '   ');
    expect(() => assertProductionConfig()).toThrow(/AUTH_SECRET/);
    vi.stubEnv('AUTH_SECRET', 'x'.repeat(44));
    expect(() => assertProductionConfig()).not.toThrow();

    // Demo mode never needs it.
    vi.stubEnv('QAVREN_AUTH_URL', '');
    vi.stubEnv('DATABASE_URL', '');
    vi.stubEnv('AUTH_SECRET', '');
    expect(() => assertProductionConfig()).not.toThrow();
  });

  it('assertProductionConfig in production requires DATABASE_URL, QAVREN_AUTH_URL and AUTH_SECRET together', () => {
    vi.stubEnv('NODE_ENV', 'production');
    const full = {
      DATABASE_URL: 'postgres://u:p@db.example:6543/postgres?sslmode=require',
      QAVREN_AUTH_URL: 'https://auth.stackalchemist.app',
      AUTH_SECRET: 'x'.repeat(44),
    };
    const stubAll = (env: Record<string, string>) => {
      for (const [k, v] of Object.entries(env)) vi.stubEnv(k, v);
    };

    stubAll(full);
    expect(() => assertProductionConfig()).not.toThrow();

    for (const [missing, message] of [
      ['DATABASE_URL', /DATABASE_URL/],
      ['QAVREN_AUTH_URL', /QAVREN_AUTH_URL/],
      ['AUTH_SECRET', /AUTH_SECRET/],
    ] as const) {
      stubAll({ ...full, [missing]: '' });
      expect(() => assertProductionConfig(), missing).toThrow(message);
    }

    // Nothing set at all is not demo mode in production: it is a boot failure.
    stubAll({ DATABASE_URL: '', QAVREN_AUTH_URL: '', AUTH_SECRET: '' });
    expect(() => assertProductionConfig()).toThrow(/DATABASE_URL/);

    // The URL shape is enforced in production too.
    stubAll({ ...full, QAVREN_AUTH_URL: 'auth.stackalchemist.app' });
    expect(() => assertProductionConfig()).toThrow(/http\(s\)/);
  });

  it('error messages never echo a configured value', () => {
    vi.stubEnv('NODE_ENV', 'production');
    vi.stubEnv('DATABASE_URL', 'postgres://user:hunter2@db.example:6543/postgres?sslmode=require');
    vi.stubEnv('AUTH_SECRET', 'x'.repeat(44));
    vi.stubEnv('QAVREN_AUTH_URL', 'not-a-url-hunter2');
    expect(() => assertProductionConfig()).toThrow(expect.objectContaining({ message: expect.not.stringContaining('hunter2') }));
  });
});

describe('isDemoMode (explicit, or local outside production)', () => {
  afterEach(() => {
    vi.unstubAllEnvs();
    vi.resetModules();
  });

  async function demoModeWith(env: Record<string, string>) {
    for (const [k, v] of Object.entries(env)) vi.stubEnv(k, v);
    vi.resetModules();
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    try {
      const { isDemoMode } = await import('../../src/lib/runtime-config');
      return { isDemoMode, warned: warn.mock.calls.length > 0 };
    } finally {
      warn.mockRestore();
    }
  }

  it('auto-enables outside production when NEXT_PUBLIC_DEMO_MODE is unset, with a warning', async () => {
    await expect(demoModeWith({ NODE_ENV: 'development', NEXT_PUBLIC_DEMO_MODE: '' })).resolves.toEqual({
      isDemoMode: true,
      warned: true,
    });
  });

  it('never auto-enables in production', async () => {
    await expect(demoModeWith({ NODE_ENV: 'production', NEXT_PUBLIC_DEMO_MODE: '' })).resolves.toEqual({
      isDemoMode: false,
      warned: false,
    });
  });

  it('follows an explicit NEXT_PUBLIC_DEMO_MODE in any environment, without the warning', async () => {
    for (const NODE_ENV of ['development', 'production']) {
      await expect(demoModeWith({ NODE_ENV, NEXT_PUBLIC_DEMO_MODE: 'true' })).resolves.toEqual({ isDemoMode: true, warned: false });
      await expect(demoModeWith({ NODE_ENV, NEXT_PUBLIC_DEMO_MODE: 'false' })).resolves.toEqual({ isDemoMode: false, warned: false });
    }
  });
});
