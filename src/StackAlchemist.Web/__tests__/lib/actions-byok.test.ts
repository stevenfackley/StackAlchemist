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
 * mocked `upsert(...)` call. That's the only way to verify encryption
 * correctness without modifying production source.
 */
import { createDecipheriv, scryptSync } from "node:crypto";
import { revalidatePath } from "next/cache";
import { getServerUser } from "@/lib/supabase-server";
import { createServerClient } from "@/lib/supabase";
import { hasServerSupabaseConfig } from "@/lib/runtime-config";
import { getProfileSettings, saveProfileSettings } from "@/lib/actions";
import type { SaveProfileSettingsState } from "@/lib/types";
import { makeDb } from "./actions-test-helpers";

vi.mock("@/lib/runtime-config", () => ({
  isDemoMode: false,
  hasEngineConfig: vi.fn(() => true),
  hasServerSupabaseConfig: vi.fn(() => true),
  hasStripeConfig: vi.fn(() => true),
  getEngineServiceKey: vi.fn(() => ""),
}));

vi.mock("@/lib/supabase-server", () => ({ getServerUser: vi.fn() }));
vi.mock("@/lib/supabase", () => ({ createServerClient: vi.fn() }));
vi.mock("next/cache", () => ({ revalidatePath: vi.fn() }));

const USER = { id: "user-1", email: "founder@example.com" };
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

describe("actions.ts — saveProfileSettings", () => {
  beforeEach(() => {
    vi.mocked(getServerUser).mockReset();
    vi.mocked(createServerClient).mockReset();
    vi.mocked(hasServerSupabaseConfig).mockReturnValue(true);
    vi.mocked(revalidatePath).mockClear();
    consoleErrorSpy = vi.spyOn(console, "error").mockImplementation(() => {});
  });

  afterEach(() => {
    vi.unstubAllEnvs();
    consoleErrorSpy.mockRestore();
  });

  it("rejects when the caller isn't authenticated", async () => {
    vi.mocked(getServerUser).mockResolvedValue(null);

    const result = await saveProfileSettings(IDLE, makeFormData({ preferredModel: "claude-sonnet-4-6" }));
    expect(result).toEqual({ status: "error", message: "Sign in before saving API settings." });
  });

  it("rejects when Supabase server config is incomplete", async () => {
    vi.mocked(getServerUser).mockResolvedValue(USER as never);
    vi.mocked(hasServerSupabaseConfig).mockReturnValue(false);

    const result = await saveProfileSettings(IDLE, makeFormData({ preferredModel: "claude-sonnet-4-6" }));
    expect(result).toEqual({ status: "error", message: "Supabase server configuration is incomplete." });
  });

  it("rejects an unsupported preferredModel value", async () => {
    vi.mocked(getServerUser).mockResolvedValue(USER as never);

    const result = await saveProfileSettings(IDLE, makeFormData({ preferredModel: "gpt-5-turbo-nope" }));
    expect(result).toEqual({ status: "error", message: "Choose a supported model before saving." });
  });

  it("rejects an API key that's obviously too short", async () => {
    vi.mocked(getServerUser).mockResolvedValue(USER as never);
    vi.stubEnv("BYOK_ENCRYPTION_KEY", "a".repeat(32));

    const result = await saveProfileSettings(
      IDLE,
      makeFormData({ preferredModel: "claude-sonnet-4-6", apiKeyOverride: "short-key" })
    );
    expect(result).toEqual({
      status: "error",
      message: "API key looks too short. Paste the full provider key.",
    });
    expect(createServerClient).not.toHaveBeenCalled();
  });

  it("fails closed with a config error when neither BYOK_ENCRYPTION_KEY nor ENGINE_SERVICE_KEY is set", async () => {
    vi.mocked(getServerUser).mockResolvedValue(USER as never);
    vi.stubEnv("BYOK_ENCRYPTION_KEY", undefined);
    vi.stubEnv("ENGINE_SERVICE_KEY", undefined);

    const result = await saveProfileSettings(
      IDLE,
      makeFormData({ preferredModel: "claude-sonnet-4-6", apiKeyOverride: "sk-ant-fake-key-1234567890" })
    );
    expect(result).toEqual({
      status: "error",
      message: "BYOK encryption is not configured. Set BYOK_ENCRYPTION_KEY before storing keys.",
    });
    expect(createServerClient).not.toHaveBeenCalled();
  });

  it("also fails closed when BYOK_ENCRYPTION_KEY is set but shorter than 32 chars (no fallback rescue)", async () => {
    vi.mocked(getServerUser).mockResolvedValue(USER as never);
    vi.stubEnv("BYOK_ENCRYPTION_KEY", "too-short");
    vi.stubEnv("ENGINE_SERVICE_KEY", "e".repeat(40)); // would be long enough, but nullish-coalescing never reaches it

    const result = await saveProfileSettings(
      IDLE,
      makeFormData({ preferredModel: "claude-sonnet-4-6", apiKeyOverride: "sk-ant-fake-key-1234567890" })
    );
    expect(result.status).toBe("error");
  });

  it("requires a distinct BYOK_ENCRYPTION_KEY on save and does NOT fall back to ENGINE_SERVICE_KEY (finding I3)", async () => {
    vi.mocked(getServerUser).mockResolvedValue(USER as never);
    // `getByokEncryptionSecret()` no longer falls back to ENGINE_SERVICE_KEY for saves: a stored
    // key decrypted engine-side with the engine key would be stranded if that key rotates. So a
    // save with ONLY ENGINE_SERVICE_KEY set must fail closed, not silently encrypt with it.
    vi.stubEnv("BYOK_ENCRYPTION_KEY", undefined);
    vi.stubEnv("ENGINE_SERVICE_KEY", "engine-service-key-that-is-at-least-32-chars-long");

    const result = await saveProfileSettings(
      IDLE,
      makeFormData({ preferredModel: "claude-sonnet-4-6", apiKeyOverride: "sk-ant-super-secret-real-looking-key-000111" })
    );

    expect(result).toEqual({
      status: "error",
      message: "BYOK encryption is not configured. Set BYOK_ENCRYPTION_KEY before storing keys.",
    });
    expect(createServerClient).not.toHaveBeenCalled();
  });

  it("encrypts + round-trips the key when BYOK_ENCRYPTION_KEY is set", async () => {
    vi.mocked(getServerUser).mockResolvedValue(USER as never);
    const byokKey = "byok-encryption-key-that-is-32-chars-plus!!";
    vi.stubEnv("BYOK_ENCRYPTION_KEY", byokKey);

    const dbMock = makeDb([{ error: null }]);
    vi.mocked(createServerClient).mockReturnValue(dbMock as never);

    const plaintext = "sk-ant-super-secret-real-looking-key-000111";
    const result = await saveProfileSettings(
      IDLE,
      makeFormData({ preferredModel: "claude-sonnet-4-6", apiKeyOverride: plaintext })
    );

    expect(result).toEqual({
      status: "success",
      message: "API settings saved. The key was encrypted before storage.",
    });

    const upsertCall = (dbMock.from.mock.results[0].value as { upsert: ReturnType<typeof vi.fn> }).upsert;
    const [profile] = upsertCall.mock.calls[0];
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
    vi.mocked(getServerUser).mockResolvedValue(USER as never);
    const byokKey = "byok-encryption-key-thats-32-chars-plus";
    const engineKey = "engine-service-key-thats-also-32-chars-plus";
    vi.stubEnv("BYOK_ENCRYPTION_KEY", byokKey);
    vi.stubEnv("ENGINE_SERVICE_KEY", engineKey);

    const dbMock = makeDb([{ error: null }]);
    vi.mocked(createServerClient).mockReturnValue(dbMock as never);

    const plaintext = "sk-ant-another-realistic-looking-secret-key";
    await saveProfileSettings(
      IDLE,
      makeFormData({ preferredModel: "claude-sonnet-4-6", apiKeyOverride: plaintext })
    );

    const upsertCall = (dbMock.from.mock.results[0].value as { upsert: ReturnType<typeof vi.fn> }).upsert;
    const [profile] = upsertCall.mock.calls[0];
    const ciphertext = profile.api_key_override as string;

    expect(decryptForTest(byokKey, ciphertext)).toBe(plaintext);
    expect(() => decryptForTest(engineKey, ciphertext)).toThrow();
  });

  it("omits api_key_override entirely from the upsert when neither a new key nor clearApiKey is provided", async () => {
    vi.mocked(getServerUser).mockResolvedValue(USER as never);
    const dbMock = makeDb([{ error: null }]);
    vi.mocked(createServerClient).mockReturnValue(dbMock as never);

    const result = await saveProfileSettings(
      IDLE,
      makeFormData({ preferredModel: "claude-3-5-haiku-20241022" })
    );

    expect(result).toEqual({ status: "success", message: "Preferred model saved." });
    const upsertCall = (dbMock.from.mock.results[0].value as { upsert: ReturnType<typeof vi.fn> }).upsert;
    const [profile] = upsertCall.mock.calls[0];
    expect(profile).not.toHaveProperty("api_key_override");
    expect(revalidatePath).toHaveBeenCalledWith("/dashboard");
  });

  it("clears the stored key when clearApiKey=true", async () => {
    vi.mocked(getServerUser).mockResolvedValue(USER as never);
    const dbMock = makeDb([{ error: null }]);
    vi.mocked(createServerClient).mockReturnValue(dbMock as never);

    const result = await saveProfileSettings(
      IDLE,
      makeFormData({ preferredModel: "claude-sonnet-4-6", clearApiKey: "true" })
    );

    expect(result).toEqual({
      status: "success",
      message: "API settings saved. Stored API key cleared.",
    });
    const upsertCall = (dbMock.from.mock.results[0].value as { upsert: ReturnType<typeof vi.fn> }).upsert;
    const [profile] = upsertCall.mock.calls[0];
    expect(profile.api_key_override).toBeNull();
  });

  it("reports a generic failure when the upsert errors", async () => {
    vi.mocked(getServerUser).mockResolvedValue(USER as never);
    vi.mocked(createServerClient).mockReturnValue(
      makeDb([{ error: { message: "constraint violation" } }]) as never
    );

    const result = await saveProfileSettings(
      IDLE,
      makeFormData({ preferredModel: "claude-sonnet-4-6" })
    );
    expect(result).toEqual({ status: "error", message: "Failed to save API settings." });
    expect(revalidatePath).not.toHaveBeenCalled();
  });
});

describe("actions.ts — getProfileSettings", () => {
  beforeEach(() => {
    vi.mocked(getServerUser).mockReset();
    vi.mocked(createServerClient).mockReset();
    vi.mocked(hasServerSupabaseConfig).mockReturnValue(true);
    consoleErrorSpy = vi.spyOn(console, "error").mockImplementation(() => {});
  });

  afterEach(() => {
    consoleErrorSpy.mockRestore();
  });

  it("returns an anonymous fallback when unauthenticated", async () => {
    vi.mocked(getServerUser).mockResolvedValue(null);
    const settings = await getProfileSettings();
    expect(settings).toEqual({
      email: "",
      hasApiKeyOverride: false,
      preferredModel: "claude-sonnet-4-6",
    });
  });

  it("returns a fallback (using the user's email) when Supabase server config is incomplete", async () => {
    vi.mocked(getServerUser).mockResolvedValue(USER as never);
    vi.mocked(hasServerSupabaseConfig).mockReturnValue(false);

    const settings = await getProfileSettings();
    expect(settings).toEqual({
      email: USER.email,
      hasApiKeyOverride: false,
      preferredModel: "claude-sonnet-4-6",
    });
  });

  it("returns hasApiKeyOverride=true when a stored key is present, and passes through a known preferredModel", async () => {
    vi.mocked(getServerUser).mockResolvedValue(USER as never);
    vi.mocked(createServerClient).mockReturnValue(
      makeDb([
        {
          data: { email: "db@example.com", api_key_override: "v1:aa:bb:cc", preferred_model: "claude-3-5-haiku-20241022" },
          error: null,
        },
      ]) as never
    );

    const settings = await getProfileSettings();
    expect(settings).toEqual({
      email: "db@example.com",
      hasApiKeyOverride: true,
      preferredModel: "claude-3-5-haiku-20241022",
    });
  });

  it("normalizes an unrecognized stored preferredModel back to the default", async () => {
    vi.mocked(getServerUser).mockResolvedValue(USER as never);
    vi.mocked(createServerClient).mockReturnValue(
      makeDb([
        { data: { email: "db@example.com", api_key_override: null, preferred_model: "some-retired-model" }, error: null },
      ]) as never
    );

    const settings = await getProfileSettings();
    expect(settings.preferredModel).toBe("claude-sonnet-4-6");
    expect(settings.hasApiKeyOverride).toBe(false);
  });

  it("falls back gracefully when the profile query errors", async () => {
    vi.mocked(getServerUser).mockResolvedValue(USER as never);
    vi.mocked(createServerClient).mockReturnValue(
      makeDb([{ data: null, error: { message: "boom" } }]) as never
    );

    const settings = await getProfileSettings();
    expect(settings).toEqual({
      email: USER.email,
      hasApiKeyOverride: false,
      preferredModel: "claude-sonnet-4-6",
    });
  });
});
