import { createServerClient, type CookieOptions } from "@supabase/ssr";
import { cookies } from "next/headers";
import { NextResponse, type NextRequest } from "next/server";
import { getQavrenAuthUrl, getQavrenRealm, usesQavrenAuth } from "@/lib/runtime-config";

/**
 * Phase 6 — Sign-Out Route Handler
 *
 * Called via a POST form submission from the navbar's "Sign Out" button.
 * Using a Route Handler (not a Server Action) keeps the sign-out logic server-
 * side and avoids shipping Supabase credentials to the browser bundle.
 */
export async function POST(request: NextRequest) {
  if (usesQavrenAuth()) return qavrenSignOut(request);

  const cookieStore = await cookies();

  const supabase = createServerClient(
    process.env.NEXT_PUBLIC_SUPABASE_URL!,
    process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY!,
    {
      cookies: {
        getAll() {
          return cookieStore.getAll();
        },
        setAll(cookiesToSet: { name: string; value: string; options: CookieOptions }[]) {
          cookiesToSet.forEach(({ name, value, options }) =>
            cookieStore.set(name, value, options)
          );
        },
      },
    }
  );

  await supabase.auth.signOut();

  // Redirect to home after sign-out.
  return NextResponse.redirect(new URL("/", request.url));
}

/**
 * Sign out of StackAlchemist AND of the realm. Dropping our cookie alone leaves
 * Keycloak's SSO cookie alive, so the next "sign in" on a shared machine would
 * hand the account straight back. RP-initiated logout ends the realm session.
 */
async function qavrenSignOut(request: NextRequest) {
  const appBaseUrl = (process.env.NEXT_PUBLIC_APP_URL?.trim() || new URL(request.url).origin).replace(/\/+$/, "");
  const appOrigin = new URL(appBaseUrl).origin;

  // POST-only plus two cross-site checks: a cross-site form must not be able to log someone out.
  // A same-origin form navigation sends no Origin (hence `origin &&`); Sec-Fetch-Site closes that
  // gap on browsers that send it (`none` = typed URL/bookmark, `same-origin` = our own page).
  const origin = request.headers.get("origin");
  const site = request.headers.get("sec-fetch-site");
  if ((origin && origin !== appOrigin) || (site && site !== "same-origin" && site !== "none")) {
    return NextResponse.json({ error: "forbidden" }, { status: 403 });
  }

  // Lazy: a Supabase-mode process never loads Auth.js.
  const [{ getToken }, { signOut }, { issuerFor }] = await Promise.all([
    import("next-auth/jwt"),
    import("@/auth"),
    import("@qavren/auth-next"),
  ]);

  // The ID token lives only on the JWT cookie (never on the session object) and must be read
  // BEFORE signOut clears it. getToken derives the cookie name from secureCookie and reassembles
  // chunks. Auth.js picks the `__Secure-` name from AUTH_URL / x-forwarded-proto, not from
  // NEXT_PUBLIC_APP_URL, so follow the cookie the browser actually sent.
  const sessionCookies = request.cookies.getAll().filter((c) => isSessionCookie(c.name));
  const secureCookie = sessionCookies.some((c) => c.name.startsWith(SECURE_PREFIX));
  const jwt = await getToken({ req: request, secret: process.env.AUTH_SECRET ?? "", secureCookie });
  const idToken = typeof jwt?.idToken === "string" ? jwt.idToken : undefined;

  await signOut({ redirect: false });

  const realm = getQavrenRealm();
  const end = new URL(`${issuerFor(realm, getQavrenAuthUrl())}/protocol/openid-connect/logout`);
  end.searchParams.set("post_logout_redirect_uri", `${appBaseUrl}/`);
  if (idToken) end.searchParams.set("id_token_hint", idToken);
  // Without id_token_hint Keycloak rejects post_logout_redirect_uri unless client_id names the
  // client; with client_id alone it still shows its own logout confirmation first.
  else end.searchParams.set("client_id", `${realm}-web`);

  const res = NextResponse.redirect(end, 303);
  // Belt and braces: Auth.js's own deletion rides on cookies(); a refreshed token appended by any
  // middleware would race it. Delete what the request actually carried (chunked cookies included).
  // Both Max-Age=0 AND a past Expires: Next re-parses the response's Set-Cookie headers when it
  // merges cookies() mutations (appendMutableCookies) and that round trip drops Max-Age=0, which
  // would turn this deletion into an empty-value cookie and displace Auth.js's own deletion.
  for (const c of sessionCookies) {
    res.cookies.set(c.name, "", {
      path: "/",
      maxAge: 0,
      expires: new Date(0),
      httpOnly: true,
      sameSite: "lax",
      secure: c.name.startsWith(SECURE_PREFIX),
    });
  }
  return res;
}

const SESSION_COOKIE = "authjs.session-token";
const SECURE_PREFIX = "__Secure-";

/** Auth.js's session cookie, either name, whole or as one of its `.0`, `.1`, … chunks. */
function isSessionCookie(name: string) {
  const bare = name.startsWith(SECURE_PREFIX) ? name.slice(SECURE_PREFIX.length) : name;
  return bare === SESSION_COOKIE || bare.startsWith(`${SESSION_COOKIE}.`);
}
