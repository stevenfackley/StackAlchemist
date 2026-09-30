import { getServerUser } from "./supabase-server";
import { usesQavrenAuth } from "./runtime-config";

export type SessionUser = { id: string; email: string | null };

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * The one place session identity is read.
 * Qavren Auth mode: the Keycloak `sub` — it becomes `user_id` on every owned
 * row, so anything that is not a UUID is treated as no session at all.
 * Supabase mode: today's `getServerUser()`, unchanged, collapsed to the same shape.
 */
export async function getSessionUser(): Promise<SessionUser | null> {
  if (!usesQavrenAuth()) {
    const user = await getServerUser();
    return user ? { id: user.id, email: user.email ?? null } : null;
  }
  // Lazy: keeps next-auth out of Supabase-mode processes and unit tests.
  const { auth } = await import("@/auth");
  const session = await auth();
  const id = session?.user?.id;
  if (!id || !UUID.test(id)) return null;
  return { id, email: session!.user!.email ?? null };
}
