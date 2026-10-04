import "server-only";
import { usesQavrenAuth } from "./runtime-config";

export type SessionUser = { id: string; email: string | null };

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * The one place session identity is read: the Auth.js session's Keycloak `sub`.
 * It becomes `user_id` on every owned row, so anything that is not a UUID is
 * treated as no session at all.
 */
export async function getSessionUser(): Promise<SessionUser | null> {
  // Without QAVREN_AUTH_URL (demo mode; production refuses to boot without it) there is
  // no realm and no AUTH_SECRET, so auth() would throw MissingSecret: nobody is signed in.
  if (!usesQavrenAuth()) return null;
  // Lazy: a demo-mode process never loads Auth.js.
  const { auth } = await import("@/auth");
  const session = await auth();
  const u = session?.user;
  if (!u?.id || !UUID.test(u.id)) return null;
  return { id: u.id, email: u.email ?? null };
}
