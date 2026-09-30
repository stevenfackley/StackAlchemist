# Qavren Auth Runbook

StackAlchemist is moving sign-in from Supabase Auth to Qavren Auth (Keycloak,
realm `stackalchemist`) through Auth.js. The switch is a runtime flag, so one
image serves both modes. This document covers what the flag does, the env
contract, the realm facts, a local recipe, the phase E flip, the traps and what
is deliberately left for phase F. Data moves separately; see
`qavren-db-migrations.md`. Supabase Auth stays the live mode until phase E.

## What the flag does

- **`QAVREN_AUTH_URL` set:** sign-in goes through the Keycloak realm with
  Auth.js (`next-auth` v5 plus `@qavren/auth-next`). `/login` and `/register`
  are one-button pages that hand off to the realm's own screen.
- **`QAVREN_AUTH_URL` unset or blank:** Supabase Auth, every existing path
  unchanged. Auth.js is never loaded in that mode (lazy imports behind
  `usesQavrenAuth()`).
- **Boot assert.** `assertAuthModeConsistent()` in `src/lib/runtime-config.ts`
  runs once per server start from `src/instrumentation.ts`. With
  `QAVREN_AUTH_URL` set it throws unless all of these hold:
  1. `DATABASE_URL` is set. A Keycloak `sub` cannot own a row where
     `profiles.id` references Supabase's `auth.users`, so auth without the
     Postgres store is refused by design.
  2. `QAVREN_AUTH_URL` is an absolute http(s) URL.
  3. `AUTH_SECRET` is not blank.

  A failure aborts **server start** (the container never turns healthy and the
  deploy's health gate fails), never the build: `next build` does not run the
  hook and the values exist only at runtime. The error messages never echo a
  value.
- **Every mode-branching page is `force-dynamic`.** The mode is a runtime secret
  that `next build` never sees; a prerendered page would freeze Supabase mode in
  at build time. Today that is `/login`, `/register`, `/forgot-password`,
  `/auth/reset-password` and `/privacy`. A new page that branches on
  `usesQavrenAuth()` must export `dynamic = "force-dynamic"`.
- **`/api/auth/*` answers 404 in Supabase mode** (the route handler returns 404
  before it imports Auth.js, so a stray request cannot surface a `MissingSecret`
  500). In Qavren mode it is Auth.js.
- **nginx sends `/api/auth/` to `sa-web`.** `docker/nginx.prod.conf` routes the
  rest of `/api/` to the Engine; without the `/api/auth/` location the OAuth
  callback would land on the Engine and 404. The rule ships inert ahead of the
  flip (the web answers the same 404 the Engine did).

## Env contract

Prod path: GitHub secret or variable -> `deploy-prod.yml` -> `setup-env` inputs
-> `.env` -> `docker-compose.prod.yml` `sa-web.environment`. Only `sa-web` gets
them; the Engine does not authenticate users.

| Variable | Prod | Local | Notes |
|---|---|---|---|
| `QAVREN_AUTH_URL` | `https://auth.stackalchemist.app` (secret `QAVREN_AUTH_URL`) | `http://localhost:8090` | Keycloak base URL, no realm path. The flag. |
| `QAVREN_REALM` | unset (repo variable `QAVREN_REALM`, optional); compose defaults it to `stackalchemist` | `stackalchemist-dev` | The OIDC client is `${realm}-web`. |
| `AUTH_SECRET` | secret `AUTH_SECRET` | any base64 string | At least 32 random bytes, base64: `openssl rand -base64 32`. Encrypts the session cookie. |
| `AUTH_URL` | derived in compose from `NEXT_PUBLIC_APP_URL` (`docker-compose.prod.yml:59`) | unset | The public **origin** only. See below. |
| `NEXT_PUBLIC_APP_URL` | `https://stackalchemist.app` (build arg, `docker-compose.prod.yml:33`; runtime env, `:44`) | `http://localhost:3000` | **Build-time** public origin. See below. |

Notes:

- **`AUTH_SECRET` is the only name the app honours.** Auth.js itself would fall
  back to the legacy `NEXTAUTH_SECRET`, but the boot check and the sign-out
  route read `AUTH_SECRET` only, so a deploy that sets just the legacy name
  fails the boot assert. Base64 has no `$`, so the compose interpolation hazard
  from `qavren-db-migrations.md` does not apply to it. Rotating it signs
  everyone out (the realm's SSO cookie usually makes the next sign-in silent).
- **`AUTH_URL` is mandatory behind the proxy and must be an origin, never a
  path.** The app sits behind Cloudflare, cloudflared and nginx. Inside the
  container `request.url` is the bind address (`http://0.0.0.0:3000`) and
  nginx's `X-Forwarded-Proto` is `$scheme` on a plain `:80` listener, i.e.
  `http` (TLS ends at Cloudflare). Without `AUTH_URL` Auth.js builds an
  `http://...` `redirect_uri`, which the realm rejects, and picks the non-secure
  cookie name. With it, Auth.js rewrites the request origin to the configured
  one, so the callback URL is `https://stackalchemist.app/api/auth/callback/keycloak`
  and the cookies get the `__Secure-` prefix. A path in it would change Auth.js's
  `basePath` and break `/api/auth`. Prod compose derives it from
  `NEXT_PUBLIC_APP_URL`, so the two cannot drift. It is harmless in Supabase
  mode (Auth.js is never loaded). Locally leave it unset: `http://localhost:3000`
  is what the browser and the server both see.
- **Sign-out follows the session cookie the browser sent.** Auth.js names the
  cookie from `AUTH_URL` / `X-Forwarded-Proto` (`__Secure-authjs.session-token`
  over https, `authjs.session-token` otherwise). `/auth/signout` needs the ID
  token on it for `id_token_hint`, so it calls `getToken` with `secureCookie`
  set by which of the two names the request carries, never by
  `NEXT_PUBLIC_APP_URL`. It then deletes every session cookie the request
  carried (chunks included) on its own 303, and the proxy skips
  `/auth/signout`, so no refreshed token rides on the same response and wins
  on header order. If the token cannot be read, sign-out degrades to
  `client_id`-only logout: Keycloak shows its own logout confirmation before
  it honours the redirect.
- **`NEXT_PUBLIC_APP_URL` is baked at build time and gates sign-out.**
  `/auth/signout` only accepts a POST whose `Origin` (or `Sec-Fetch-Site`) matches
  it, and sends `post_logout_redirect_uri = ${NEXT_PUBLIC_APP_URL}/`. An image
  built without the right value falls back to the container bind origin (or a
  wrong site) and **403s every sign-out behind the reverse proxy**. The
  `Dockerfile` defaults it on line 14 to `https://test.stackalchemist.app`, so a
  bare `docker build` (not through compose) bakes the TEST URL: every prod
  sign-out would 403 or redirect post-logout to the test site. Always build via
  `docker compose -f docker-compose.prod.yml build`, which passes the build arg
  (`docker-compose.prod.yml:33`, defaulting to `https://stackalchemist.app`).
- **One canonical host.** The same-origin check pins exactly one origin, and the
  realm's redirect and post-logout lists name only `https://stackalchemist.app`.
  `www.` or any other alias must redirect to the apex at Cloudflare; a
  sign-out POSTed from an alias is refused. `prod-ec2-runner-and-oidc.md` lists
  `www.stackalchemist.app` as an optional tunnel hostname that serves the app
  as-is: if it is configured, turn it into a redirect (or drop it) before
  phase E.
- **Local dev needs `NEXT_PUBLIC_DEMO_MODE=false` explicitly.** Demo mode
  auto-enables whenever `NEXT_PUBLIC_SUPABASE_URL` is unset outside production,
  and demo mode switches the proxy gate off and the app's demo code paths on.
  Without the explicit `false` a Keycloak-mode dev server never redirects you to
  sign in.

## Realm facts

Definitions live in the qavren-auth repo: `realms/apps/stackalchemist.yaml`
(prod) and `realms/apps/stackalchemist-dev.yaml` (local), added in qavren-auth
#164 (phase A).

| | `stackalchemist` (prod) | `stackalchemist-dev` (local) |
|---|---|---|
| Issuer | `https://auth.stackalchemist.app/realms/stackalchemist` (`frontendUrl` set) | `${QAVREN_AUTH_URL}/realms/stackalchemist-dev` |
| Client | `stackalchemist-web` | `stackalchemist-dev-web` |
| Redirect URI | `https://stackalchemist.app/api/auth/callback/keycloak` | `http://localhost:3000/api/auth/callback/keycloak` |
| Web origin | `https://stackalchemist.app` | `http://localhost:3000` |
| Post-logout allow-list | `https://stackalchemist.app/*` | `http://localhost:3000/*` |

- **Public client, PKCE (`S256`), no secret.** Standard flow only: no implicit,
  no direct grants, no service account. The redirect URI is the one exact
  callback Auth.js sends, not a wildcard.
- **The callback path is `/api/auth/callback/keycloak`** (Auth.js's provider id
  is `keycloak`).
- **Registration** is OIDC `prompt=create` on the authorization request, which
  the `/register` page sets. It needs Keycloak 26.1 or newer, and the realm has
  `registrationAllowed: true` (paired with `verifyEmail: true`, so sign-up
  sends a verification email through the fleet SMTP).
- **Google and password reset live on the Keycloak screen**, not in this app.
  Google needs the owner step in phase A task 6 (Google redirect URIs on the
  shared broker). Until it is done the Google button errors inside Keycloak.
- **Sessions:** 7-day JWT (Auth.js), the ID token lives on the encrypted JWT
  cookie only (never on the session object served by `/api/auth/session`).

## Local recipe

1. **Keycloak.** In the qavren-auth repo run the stock compose
   (`docker compose up -d --wait`). The stock file publishes `:8080`; the local
   setup here publishes `:8090` through a compose override kept outside that
   repo. Any port works: put it in `QAVREN_AUTH_URL`. The realm's redirect URIs
   are about the app (`localhost:3000`), not Keycloak's port.
2. **Dev realm.** From the qavren-auth repo run `tools/apply-realms.ps1`; it
   applies every realm in `realms/apps/`, including `stackalchemist-dev`. See
   that repo's README for the exact invocation and the env it needs (SMTP and
   Google ids are read at apply time).
3. **A test user** in the `stackalchemist-dev` realm (admin console): set first
   name and last name, email verified, a non-temporary password and no required
   actions. A user missing either name is stopped at a required-action screen
   and cannot finish signing in. (Self-registration needs working SMTP because
   of `verifyEmail`.)
4. **Postgres.** `DATABASE_URL` is required in this mode. Use the throwaway
   database from "Local database recipe" in `qavren-db-migrations.md` and run
   `npm run db:migrate` against it first.
5. **`src/StackAlchemist.Web/.env.local`** (gitignored; Next reads env files from
   the web project directory, not the repo-root `.env`):

   ```
   QAVREN_AUTH_URL=http://localhost:8090
   QAVREN_REALM=stackalchemist-dev
   AUTH_SECRET=<openssl rand -base64 32>
   DATABASE_URL=postgres://postgres:postgres@127.0.0.1:55440/stackalchemist_test
   NEXT_PUBLIC_DEMO_MODE=false
   NEXT_PUBLIC_APP_URL=http://localhost:3000
   ```

6. `npm run dev` from `src/StackAlchemist.Web`, then the click-path:
   1. Open `/dashboard` signed out. The page's own gate redirects to
      `/login?returnTo=/dashboard`. (The proxy gate covers only `/simple`,
      `/advanced` and `/generate`, which redirect as `?returnTo=%2Fsimple` and so
      on.)
   2. Click **Sign in**. The address bar moves to Keycloak.
   3. Sign in as the test user. You land back on `/dashboard`.
   4. Click **Sign Out**. You land on `/`, and opening `/dashboard` again
      redirects to `/login` (the realm session is gone too: **Sign in** shows
      the Keycloak form again instead of signing straight back in).

## Phase E flip order

1. The realm is applied to the prod Keycloak and `auth.stackalchemist.app`
   serves discovery
   (`https://auth.stackalchemist.app/realms/stackalchemist/.well-known/openid-configuration`
   answers 200). That is phase A tasks 5 to 7.
2. Set `DATABASE_URL`, `DATABASE_URL_MIGRATE`, `QAVREN_AUTH_URL` and
   `AUTH_SECRET` in the Prod environment, then deploy **once**. Auth without the
   Postgres store fails the boot assert by design, so they go together.
   `QAVREN_REALM` stays unset unless the realm is renamed.
3. The nginx `/api/auth/` route and `AUTH_URL` are already live from the phase C
   deploy; nothing else to ship. No rebuild is needed to flip: the web reads all
   of it at runtime. The Cloudflare tunnel maps the hostname to
   `http://sa-reverse-proxy:80` (per `prod-ec2-runner-and-oidc.md`), so it needs
   no path rule; if someone added path-based ingress rules in the Zero Trust
   dashboard, `/api/auth/*` must reach the same reverse proxy. Make sure no
   `www.` alias serves the app (see the canonical-host note above).
4. Existing Supabase users do **not** carry over: this is a fresh provision
   (decision recorded in the re-platform plan). Everyone registers again.
5. Verify: `https://stackalchemist.app/api/auth/session` answers 200 with `null`
   (it was 404 in Supabase mode), a signed-out `/dashboard` bounces to `/login`,
   and the full click-path above works against prod.
6. Rolling back is one deploy with the four secrets removed (no rebuild). Data
   written to qavren-db in the meantime does not flow back to Supabase.

## Troubleshooting

- **`/login?error=Configuration`** (copy: "Sign-in is temporarily unavailable"):
  `AUTH_SECRET` missing or wrong, or Auth.js cannot reach the issuer (check
  `auth.stackalchemist.app` from inside the container and the realm name).
- **Keycloak "Client not found":** the realm was not applied, or `QAVREN_REALM`
  names a realm whose client is not `${realm}-web`.
- **Keycloak "Invalid parameter: redirect_uri":** the callback URI is not on the
  client's allow-list. Almost always a public-origin mismatch: `AUTH_URL` unset
  or wrong (so the URI is `http://0.0.0.0:3000/...` or `http://...`), or the
  request came in on an alias host.
- **Sign-out answers 403:** `NEXT_PUBLIC_APP_URL` was baked wrong at build time
  (see the `Dockerfile` default above), or the POST came from a host that is not
  the canonical origin. Rebuild through compose; a runtime env change does not
  fix a baked value.
- **Redirect loop on a protected page:** the session cookie is not being set or
  not being read back. Check `AUTH_URL` (public origin, https, no path),
  `trustHost` (on in `src/auth.config.ts`) and the proxy's `X-Forwarded-*`
  headers. A browser that dropped the `__Secure-` cookie because the page
  loaded over http shows the same loop.
- **Auth.js errors** are redirected to `/login?error=<code>`; the page renders
  copy only for the closed set in `src/lib/auth-errors.ts` (`Configuration`,
  `AccessDenied`, `OAuthSignin`, `OAuthCallbackError`, `Callback`, `Default`,
  `session_expired`) and never echoes the parameter itself.
- **Server will not start after setting the secrets:** read the container log
  for the assert message (`DATABASE_URL` missing, not an absolute http(s) URL,
  or `AUTH_SECRET` blank).

## Session lifetime and sign-out

- The Auth.js session is a JWT cookie with a FIXED 7-day life from sign-in. There is
  no sliding refresh: the proxy strips the refreshed session cookie that Auth.js's
  middleware wrapper appends to every gated response (`stripSessionRefresh` in
  `src/proxy.ts`). Reason: with sliding refresh, any response still in flight when
  the user signs out (a Server Action, a router refresh, a prefetch) re-issued the
  cookie after the sign-out had deleted it and resurrected the session. Seen in the
  local smoke on 2026-09-30; the fix removes the writer instead of racing it.
- `POST /auth/signout` bypasses the gate for the same reason, and its 303 deletes
  every `authjs.session-token*` / `__Secure-authjs.session-token*` cookie the request
  carried with BOTH `Max-Age=0` and `Expires=Thu, 01 Jan 1970`: Next re-parses the
  response's Set-Cookie headers when it merges `cookies()` mutations and that round
  trip drops `Max-Age=0`, which would turn the deletion into an empty-value cookie.
- Disabling a user in Keycloak therefore ends their app session only at the JWT's
  expiry (up to 7 days), the same trade-off recharacter made. Shorten `maxAge` in
  `src/auth.config.ts` if that window is ever unacceptable.

## Known gaps and phase F

- The Qavren gate in `src/proxy.ts` decodes the path before the protected-prefix
  check (so `/%73imple` is guarded). The Supabase branch keeps its pre-existing
  gap for that encoding. Both are redirect conveniences: `getSessionUser()` in
  each action and page is the authorization boundary.
- In Supabase mode `returnTo` is unsanitised (pre-existing). Qavren mode runs it
  through `safeReturnTo`.
- Phase F, once prod has run on Qavren Auth, deletes: `LoginPageClient`,
  `RegisterPageClient`, `ForgotPasswordClient`, `ResetPasswordClient`,
  `oauth-buttons.tsx`, the Supabase branches of `proxy.ts`, `/auth/signout` and
  `/auth/callback`, `@supabase/ssr` and `@supabase/supabase-js`, the Supabase
  CSP entries, and `hasPublicSupabaseConfig` / `hasServerSupabaseConfig`. It
  also turns the `isDemoMode || !supabase` hard-navigation guards in
  `SimpleModePage` and `AdvancedModePage` into plain hard navigations.
