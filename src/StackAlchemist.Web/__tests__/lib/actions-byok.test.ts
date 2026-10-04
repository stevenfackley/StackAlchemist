/**
 * actions.ts — getProfileSettings / saveProfileSettings (BYOK crypto).
 *
 * `encryptApiKeyOverride` / `getByokEncryptionSecret` are private to
 * actions.ts — there is no exported decrypt function anywhere in this file
 * (or, as far as this test file can see, anywhere else in the Next app).
 * Decryption of `api_key_override` presumably happens engine-side, reading
 * the same `BYOK_ENCRYPTION_KEY` / `ENGINE_SERVICE_KEY` fallback and the same
 * "stackalchemist-byok-v1" scrypt salt. `getProfileSettings` itself never
 * decrypts — it only reports `hasApiKeyOverride` (a presence boolean).
 *
 * To prove the round trip, these tests replicate the exact decrypt side of
 * the algorithm locally and use it to open the ciphertext captured from the
 * mocked `upsertProfile(...)` call. That's the only way to verify encryption
 * correctness without modifying production source.
 */
import { createDecipheriv, scryptSync } from "node:crypto";
import { revalidatePath } from "next/cache";
import { getDataStore } from "@/lib/data";
import { DataStoreError } from "@/lib/data/store";
import { getSessionUser } from "@/lib/session";
import { hasDataStoreConfig } from "@/lib/runtime-config";
import { getProfileSettings, saveProfileSettings } from "@/lib/actions";
import type { SaveProfileSettingsState } from "@/lib/types";
import { makeStore, type FakeStore } from "./actions-test-helpers";

vi.mock("@/lib/runtime-config", () => ({
  isDemoMode: false,
  hasEngineConfig: vi.fn(() => true),
  hasDataStoreConfig: vi.fn(() => true),
  hasStripeConfig: vi.fn(() => true),
  getEngineServiceKey: vi.fn(() => ""),
}));

vi.mock("@/lib/session", () => ({ getSessionUser: vi.fn() }));
vi.mock("@/lib/data", () => ({ getDataStore: vi.fn() }));
vi.mock("next/cache", () => ({ revalidatePath: vi.fn() }));

// Session ids are Keycloak subs, always UUIDs.
const USER = { id: "3f2b8c1a-5d4e-4f6a-9b7c-8d9e0f1a2b3c", email: "founder@example.com" };
const IDLE: SaveProfileSettingsState = { status: "idle", message: "" };

/** Mirrors `encryptApiKeyOverride`'s decrypt side exactly (see actions.ts). */
function decryptForTest(secret: string, ciphertext: string): string {
  const parts = ciphertext.split(":");
  const [version, ivB64, tagB64, dataB64] = parts;
  if (version !== "v1" || parts.length !== 4) {
    throw new Error(`Unexpected ciphertext format: ${ciphertext}`);
  }
  const key = scryptSync(secret, "stackalchemist-byok-v1", 32);
  const iv = Buffer.from(ivB64, "base64");
  const tag = Buffer.from(tagB64, "base64");
  const data = Buffer.from(dataB64, "base64");
  const decipher = createDecipheriv("aes-256-gcm", key, iv);
  decipher.setAuthTag(tag);
  return Buffer.concat([decipher.update(data), decipher.final()]).toString("utf8");
}

function makeFormData(fields: Record<string, string>): FormData {
  const fd = new FormData();
  for (const [key, value] of Object.entries(fields)) fd.set(key, value);
  return fd;
}

let consoleErrorSpy: ReturnType<typeof vi.spyOn>;
let store: FakeStore;

/** Fresh fake store per test, returned by every getDataStore() call. */
function resetStore() {
  store = makeStore();
  vi.mocked(getDataStore).mockReset();
  vi.mocked(getDataStore).mockReturnValue(store);
}

/** The profile object saveProfileSettings handed to the store. */
function upsertedProfile() {
  expect(store.upsertProfile).toHaveBeenCalledTimes(1);
  return store.upsertProfile.mock.calls[0][0];
}

describe("actions.ts — saveProfileSettings", () => {
  beforeEach(() => {
    vi.mocked(getSessionUser).mockReset();
    resetStore();
    vi.mocked(hasDataStoreConfig).mockReturnValue(true);
    vi.mocked(revalidatePath).mockClear();
    consoleErrorSpy = vi.spyOn(console, "error").mockImplementation(() => {});
  });

  afterEach(() => {
    vi.unstubAllEnvs();
    consoleErrorSpy.mockRestore();
  });

  it("rejects when the caller isn't authenticated", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(null);

    const result = await saveProfileSettings(IDLE, makeFormData({ preferredModel: "claude-sonnet-5-5" }));
    expect(result).toEqual({ status: "error", message: "Sign in before saving API settings." });
  });

  it("rejects when no data store is configured", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(USER as never);
    vi.mocked(hasDataStoreConfig).mockReturnValue(false);

    const result = await saveProfileSettings(IDLE, makeFormData({ preferredModel: "claude-sonnet-5-5" }));
    expect(result).toEqual({ status: "error", message: "Server database configuration is incomplete." });
    expect(getDataStore).not.toHaveBeenCalled();
  });

  it("rejects an unsupported preferredModel value", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(USER as never);

    const result = await saveProfileSettings(IDLE, makeFormData({ preferredModel: "gpt-5-turbo-nope" }));
    expect(result).toEqual({ status: "error", message: "Choose a supported model before saving." });
  });

  it.each(["claude-sonnet-4-6", "claude-3-5-sonnet-20241022", "openai/gpt-4o-mini"])(
    "rejects the superseded model id %s",
    async (retired) => {
      vi.mocked(getSessionUser).mockResolvedValue(USER as never);

      const result = await saveProfileSettings(IDLE, makeFormData({ preferredModel: retired }));
      expect(result).toEqual({ status: "error", message: "Choose a supported model before saving." });
    },
  );

  it("rejects an API key that's obviously too short", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(USER as never);
    vi.stubEnv("BYOK_ENCRYPTION_KEY", "a".repeat(32));

    const result = await saveProfileSettings(
      IDLE,
      makeFormData({ preferredModel: "claude-sonnet-5-5", apiKeyOverride: "short-key" })
    );
    expect(result).toEqual({
      status: "error",
      message: "API key looks too short. Paste the full provider key.",
    });
    expect(getDataStore).not.toHaveBeenCalled();
  });

  it("fails closed with a config error when neither BYOK_ENCRYPTION_KEY nor ENGINE_SERVICE_KEY is set", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(USER as never);
    vi.stubEnv("BYOK_ENCRYPTION_KEY", undefined);
    vi.stubEnv("ENGINE_SERVICE_KEY", undefined);

    const result = await saveProfileSettings(
      IDLE,
      makeFormData({ preferredModel: "claude-sonnet-5-5", apiKeyOverride: "sk-ant-fake-key-1234567890" })
    );
    expect(result).toEqual({
      status: "error",
      message: "BYOK encryption is not configured. Set BYOK_ENCRYPTION_KEY before storing keys.",
    });
    expect(getDataStore).not.toHaveBeenCalled();
  });

  it("also fails closed when BYOK_ENCRYPTION_KEY is set but shorter than 32 chars (no fallback rescue)", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(USER as never);
    vi.stubEnv("BYOK_ENCRYPTION_KEY", "too-short");
    vi.stubEnv("ENGINE_SERVICE_KEY", "e".repeat(40)); // would be long enough, but nullish-coalescing never reaches it

    const result = await saveProfileSettings(
      IDLE,
      makeFormData({ preferredModel: "claude-sonnet-5-5", apiKeyOverride: "sk-ant-fake-key-1234567890" })
    );
    expect(result.status).toBe("error");
  });

  it("requires a distinct BYOK_ENCRYPTION_KEY on save and does NOT fall back to ENGINE_SERVICE_KEY (finding I3)", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(USER as never);
    // `getByokEncryptionSecret()` no longer falls back to ENGINE_SERVICE_KEY for saves: a stored
    // key decrypted engine-side with the engine key would be stranded if that key rotates. So a
    // save with ONLY ENGINE_SERVICE_KEY set must fail closed, not silently encrypt with it.
    vi.stubEnv("BYOK_ENCRYPTION_KEY", undefined);
    vi.stubEnv("ENGINE_SERVICE_KEY", "engine-service-key-that-is-at-least-32-chars-long");

    const result = await saveProfileSettings(
      IDLE,
      makeFormData({ preferredModel: "claude-sonnet-5-5", apiKeyOverride: "sk-ant-super-secret-real-looking-key-000111" })
    );

    expect(result).toEqual({
      status: "error",
      message: "BYOK encryption is not configured. Set BYOK_ENCRYPTION_KEY before storing keys.",
    });
    expect(getDataStore).not.toHaveBeenCalled();
  });

  it("encrypts + round-trips the key when BYOK_ENCRYPTION_KEY is set", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(USER as never);
    const byokKey = "byok-encryption-key-that-is-32-chars-plus!!";
    vi.stubEnv("BYOK_ENCRYPTION_KEY", byokKey);

    const plaintext = "sk-ant-super-secret-real-looking-key-000111";
    const result = await saveProfileSettings(
      IDLE,
      makeFormData({ preferredModel: "claude-sonnet-5-5", apiKeyOverride: plaintext })
    );

    expect(result).toEqual({
      status: "success",
      message: "API settings saved. The key was encrypted before storage.",
    });

    const profile = upsertedProfile();
    // Written for the session's own id and email.
    expect(profile).toMatchObject({ id: USER.id, email: USER.email, preferred_model: "claude-sonnet-5-5" });
    const ciphertext = profile.api_key_override as string;

    expect(ciphertext).not.toBe(plaintext);
    expect(ciphertext.startsWith("v1:")).toBe(true);
    expect(ciphertext.split(":")).toHaveLength(4);

    // Round trip: decrypting with the BYOK secret recovers the original (this is the exact scheme
    // the Engine's ByokKeyProtector reproduces in C#).
    expect(decryptForTest(byokKey, ciphertext)).toBe(plaintext);
    expect(() => decryptForTest("a-completely-different-32-char-secret!!", ciphertext)).toThrow();
  });

  it("prefers BYOK_ENCRYPTION_KEY over ENGINE_SERVICE_KEY when both are set", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(USER as never);
    const byokKey = "byok-encryption-key-thats-32-chars-plus";
    const engineKey = "engine-service-key-thats-also-32-chars-plus";
    vi.stubEnv("BYOK_ENCRYPTION_KEY", byokKey);
    vi.stubEnv("ENGINE_SERVICE_KEY", engineKey);

    const plaintext = "sk-ant-another-realistic-looking-secret-key";
    await saveProfileSettings(
      IDLE,
      makeFormData({ preferredModel: "claude-sonnet-5-5", apiKeyOverride: plaintext })
    );

    const ciphertext = upsertedProfile().api_key_override as string;

    expect(decryptForTest(byokKey, ciphertext)).toBe(plaintext);
    expect(() => decryptForTest(engineKey, ciphertext)).toThrow();
  });

  it("leaves api_key_override undefined (keep the stored key) when neither a new key nor clearApiKey is provided", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(USER as never);

    const result = await saveProfileSettings(
      IDLE,
      makeFormData({ preferredModel: "claude-haiku-4-5" })
    );

    expect(result).toEqual({ status: "success", message: "Preferred model saved." });
    // ProfileUpsert's contract: undefined = leave the stored key alone; null = clear it.
    const profile = upsertedProfile();
    expect(profile.api_key_override).toBeUndefined();
    expect(profile).toEqual({ id: USER.id, email: USER.email, preferred_model: "claude-haiku-4-5" });
    expect(revalidatePath).toHaveBeenCalledWith("/dashboard");
  });

  it("clears the stored key when clearApiKey=true", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(USER as never);

    const result = await saveProfileSettings(
      IDLE,
      makeFormData({ preferredModel: "claude-sonnet-5-5", clearApiKey: "true" })
    );

    expect(result).toEqual({
      status: "success",
      message: "API settings saved. Stored API key cleared.",
    });
    expect(upsertedProfile().api_key_override).toBeNull();
  });

  it("reports a generic failure when the upsert errors", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(USER as never);
    store.upsertProfile.mockRejectedValue(new DataStoreError("profiles upsert failed"));

    const result = await saveProfileSettings(
      IDLE,
      makeFormData({ preferredModel: "claude-sonnet-5-5" })
    );
    expect(result).toEqual({ status: "error", message: "Failed to save API settings." });
    expect(revalidatePath).not.toHaveBeenCalled();
  });
});

describe("actions.ts — getProfileSettings", () => {
  beforeEach(() => {
    vi.mocked(getSessionUser).mockReset();
    resetStore();
    vi.mocked(hasDataStoreConfig).mockReturnValue(true);
    consoleErrorSpy = vi.spyOn(console, "error").mockImplementation(() => {});
  });

  afterEach(() => {
    consoleErrorSpy.mockRestore();
  });

  it("returns an anonymous fallback when unauthenticated", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(null);
    const settings = await getProfileSettings();
    expect(settings).toEqual({
      email: "",
      hasApiKeyOverride: false,
      preferredModel: "claude-sonnet-5-5",
    });
  });

  it("returns a fallback (using the user's email) when no data store is configured", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(USER as never);
    vi.mocked(hasDataStoreConfig).mockReturnValue(false);

    const settings = await getProfileSettings();
    expect(settings).toEqual({
      email: USER.email,
      hasApiKeyOverride: false,
      preferredModel: "claude-sonnet-5-5",
    });
    expect(getDataStore).not.toHaveBeenCalled();
  });

  it("returns hasApiKeyOverride=true when a stored key is present, and passes through a known preferredModel", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(USER as never);
    store.getProfile.mockResolvedValue({
      email: "db@example.com",
      api_key_override: "v1:aa:bb:cc",
      preferred_model: "claude-haiku-4-5",
    });

    const settings = await getProfileSettings();
    expect(settings).toEqual({
      email: "db@example.com",
      hasApiKeyOverride: true,
      preferredModel: "claude-haiku-4-5",
    });
    // Read for the session's own id only.
    expect(store.getProfile).toHaveBeenCalledWith(USER.id);
  });

  it("normalizes an unrecognized stored preferredModel back to the default", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(USER as never);
    store.getProfile.mockResolvedValue({
      email: "db@example.com",
      api_key_override: null,
      preferred_model: "some-retired-model",
    });

    const settings = await getProfileSettings();
    expect(settings.preferredModel).toBe("claude-sonnet-5-5");
    expect(settings.hasApiKeyOverride).toBe(false);
  });

  it("falls back gracefully when the profile query errors", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(USER as never);
    store.getProfile.mockRejectedValue(new DataStoreError("profiles select failed"));

    const settings = await getProfileSettings();
    expect(settings).toEqual({
      email: USER.email,
      hasApiKeyOverride: false,
      preferredModel: "claude-sonnet-5-5",
    });
  });
});
