import { createServerClient } from "@/lib/supabase";
import type { Generation } from "@/lib/types";
import { DataStoreError, type DataStore, type NewGeneration, type ProfileSettingsRow, type ProfileUpsert } from "./store";

type Client = ReturnType<typeof createServerClient>;

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** Supabase-JS over PostgREST with the service role. Retired in phase F. */
export class SupabaseStore implements DataStore {
  readonly kind = "supabase" as const;
  private db?: Client;

  private client(): Client {
    // Lazy: createServerClient() throws when the service-role env is missing.
    // That throw reaches the caller, and each action in actions.ts maps it to
    // its generic failure message (or a count of 0 for the quota check).
    return (this.db ??= createServerClient());
  }

  async getProfile(userId: string): Promise<ProfileSettingsRow | null> {
    // Same guard as DrizzleStore: a malformed id is simply not found.
    if (!UUID.test(userId)) return null;
    const { data, error } = await this.client()
      .from("profiles").select("email, api_key_override, preferred_model").eq("id", userId).maybeSingle();
    if (error) throw new DataStoreError("profiles select failed", error);
    return data ?? null;
  }

  async upsertProfile(profile: ProfileUpsert): Promise<void> {
    const { api_key_override, ...rest } = profile;
    const row = { ...rest, ...(api_key_override !== undefined ? { api_key_override } : {}) };
    const { error } = await this.client().from("profiles").upsert(row, { onConflict: "id" });
    if (error) throw new DataStoreError("profiles upsert failed", error);
  }

  async ensureProfile({ id, email }: { id: string; email: string }): Promise<void> {
    // ON CONFLICT (id) DO NOTHING: an existing row keeps its settings.
    const { error } = await this.client()
      .from("profiles").upsert({ id, email }, { onConflict: "id", ignoreDuplicates: true });
    if (error) throw new DataStoreError("profiles insert failed", error);
  }

  async countFreeGenerationsThisMonth(userId: string, monthStartUtc: Date): Promise<number> {
    const { count, error } = await this.client()
      .from("generations").select("id", { count: "exact", head: true })
      .eq("user_id", userId).eq("tier", 0).neq("status", "failed")
      .gte("created_at", monthStartUtc.toISOString());
    if (error) throw new DataStoreError("generations count failed", error);
    return count ?? 0;
  }

  async insertGeneration(gen: NewGeneration): Promise<Generation> {
    const { data, error } = await this.client()
      .from("generations")
      .insert({ ...gen, status: "pending", download_url: null, error_message: null, attempt_count: 0, completed_at: null })
      .select().single();
    if (error || !data) throw new DataStoreError("generations insert failed", error);
    return data as Generation;
  }

  async getGenerationById(id: string): Promise<Generation | null> {
    // A malformed id would make Postgres raise 22P02; it is simply not found.
    if (!UUID.test(id)) return null;
    const { data, error } = await this.client().from("generations").select("*").eq("id", id).maybeSingle();
    if (error) throw new DataStoreError("generations select failed", error);
    return (data as Generation | null) ?? null;
  }

  async resetForRetry(id: string, userId: string): Promise<boolean> {
    if (!UUID.test(id) || !UUID.test(userId)) return false;
    const { data, error } = await this.client()
      .from("generations").update({ status: "pending", error_message: null })
      .eq("id", id).eq("user_id", userId).eq("status", "failed").select("id");
    if (error) throw new DataStoreError("generations update failed", error);
    return (data?.length ?? 0) === 1;
  }

  async listMyGenerations(userId: string, offset: number, limit: number) {
    const { data, error, count } = await this.client()
      .from("generations").select("*", { count: "exact" })
      .eq("user_id", userId).order("created_at", { ascending: false }).order("id", { ascending: false })
      .range(offset, offset + limit - 1);
    if (error) throw new DataStoreError("generations list failed", error);
    return { generations: (data ?? []) as Generation[], total: count ?? 0 };
  }

  async generationStats(userId: string) {
    const base = () => this.client().from("generations").select("*", { count: "exact", head: true }).eq("user_id", userId);
    const [t, c, p] = await Promise.all([base(), base().eq("status", "success"), base().not("status", "in", "(success,failed)")]);
    if (t.error || c.error || p.error) throw new DataStoreError("generations stats failed", t.error ?? c.error ?? p.error);
    return { total: t.count ?? 0, completed: c.count ?? 0, inProgress: p.count ?? 0 };
  }
}
