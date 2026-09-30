import { expect, type APIRequestContext, type Page } from "@playwright/test";

/**
 * Keycloak helpers for the integration and nightly suites. They drive the realm's stock
 * login theme (keycloak.v2), so the selectors are Keycloak's own labels, not our
 * data-testids. The fixture user and the admin/admin bootstrap account come from
 * docker/keycloak/stackalchemist-ci-realm.json and docker-compose.ci.yml: public CI
 * constants for a throwaway realm, never real credentials.
 */
export const KC_URL = (process.env.E2E_KEYCLOAK_URL ?? "http://localhost:8080").replace(/\/+$/, "");
export const KC_REALM = process.env.QAVREN_REALM ?? "stackalchemist-ci";
export const FIXTURE_USER = { email: "e2e@stackalchemist.test", password: "E2e-Fixture-2026!" };

/** True while the browser is on the realm's own pages (login, registration, logout). */
export const onKeycloak = (u: URL) => u.href.startsWith(`${KC_URL}/realms/${KC_REALM}/`);

/**
 * Sign in through the real Keycloak login page. `startAt` must be a gated app page:
 * the gate bounces it to /login?returnTo=…, and the flow ends back on it, signed in.
 */
export async function signInViaKeycloak(page: Page, user = FIXTURE_USER, startAt = "/dashboard") {
  await page.goto(startAt);
  await page.waitForURL(/\/login/);
  // `main form` is the Qavren login page's Server Action hand-off, not Keycloak's form.
  await page.locator("main form button[type=submit]").click();
  await page.waitForURL(onKeycloak);
  // registrationEmailAsUsername relabels "Username or email" to plain "Email".
  await page.getByRole("textbox", { name: /^(username or )?email$/i }).fill(user.email);
  // Anchored so it never matches a neighbouring field or the "Show password" toggle.
  await page.getByRole("textbox", { name: /^password$/i }).fill(user.password);
  await page.getByRole("button", { name: /^sign in$/i }).click();
  await page.waitForURL((u) => !onKeycloak(u));
}

export async function adminToken(request: APIRequestContext): Promise<string> {
  const res = await request.post(`${KC_URL}/realms/master/protocol/openid-connect/token`, {
    form: { client_id: "admin-cli", grant_type: "password", username: "admin", password: "admin" },
  });
  expect(res.ok()).toBeTruthy();
  return (await res.json()).access_token as string;
}

export async function findUserId(request: APIRequestContext, email: string): Promise<string | null> {
  const token = await adminToken(request);
  const res = await request.get(`${KC_URL}/admin/realms/${KC_REALM}/users`, {
    params: { username: email, exact: "true" },
    headers: { authorization: `Bearer ${token}` },
  });
  expect(res.ok()).toBeTruthy();
  const users = (await res.json()) as Array<{ id: string }>;
  return users[0]?.id ?? null;
}

/** Active realm sessions for a user — the honest check that RP-initiated logout worked. */
export async function userSessionCount(request: APIRequestContext, email: string): Promise<number> {
  const token = await adminToken(request);
  const id = await findUserId(request, email);
  if (!id) return 0;
  const res = await request.get(`${KC_URL}/admin/realms/${KC_REALM}/users/${id}/sessions`, {
    headers: { authorization: `Bearer ${token}` },
  });
  expect(res.ok()).toBeTruthy();
  return ((await res.json()) as unknown[]).length;
}

/**
 * End every realm session a user holds. Closing a browser context leaves its SSO session
 * alive on the server, so earlier tests and runs leave sessions behind; clear them first
 * when a test counts sessions.
 */
export async function endUserSessions(request: APIRequestContext, email: string): Promise<void> {
  const token = await adminToken(request);
  const id = await findUserId(request, email);
  if (!id) return;
  const res = await request.post(`${KC_URL}/admin/realms/${KC_REALM}/users/${id}/logout`, {
    headers: { authorization: `Bearer ${token}` },
  });
  expect(res.ok()).toBeTruthy();
}

export async function deleteUserByEmail(request: APIRequestContext, email: string): Promise<void> {
  const token = await adminToken(request);
  const id = await findUserId(request, email);
  if (!id) return;
  const res = await request.delete(`${KC_URL}/admin/realms/${KC_REALM}/users/${id}`, {
    headers: { authorization: `Bearer ${token}` },
  });
  expect(res.ok()).toBeTruthy();
}
