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
  // BEFORE signOut clears it. getToken derives the cookie name from secureCookie and reassembles chunks.
  const jwt = await getToken({
    req: request,
    secret: process.env.AUTH_SECRET ?? "",
    secureCookie: appBaseUrl.startsWith("https:"),
  });
  const idToken = typeof jwt?.idToken === "string" ? jwt.idToken : undefined;

  await signOut({ redirect: false });

  const realm = getQavrenRealm();
  const end = new URL(`${issuerFor(realm, getQavrenAuthUrl())}/protocol/openid-connect/logout`);
  end.searchParams.set("post_logout_redirect_uri", `${appBaseUrl}/`);
  if (idToken) end.searchParams.set("id_token_hint", idToken);
  // Keycloak needs one of the two to honour the redirect; without either it asks the user to confirm.
  else end.searchParams.set("client_id", `${realm}-web`);

  return NextResponse.redirect(end, 303);
}
