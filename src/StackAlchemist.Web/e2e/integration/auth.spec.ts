import { expect, test } from "@playwright/test";
import {
  FIXTURE_USER,
  deleteUserByEmail,
  endUserSessions,
  onKeycloak,
  signInViaKeycloak,
  userSessionCount,
} from "../helpers/keycloak";

// Needs Postgres + Keycloak (docker/docker-compose.test.yml) and the app in Qavren mode;
// see "Integration suite (Postgres + Keycloak)" in e2e/README.md.

const SESSION_COOKIE = /^(?:__Secure-)?authjs\.session-token(?:\.\d+)?$/;
const SESSION_SET_COOKIE = /(?:^|,\s*|\n)(?:__Secure-)?authjs\.session-token(?:\.\d+)?=[^;\s]/;

test.describe("Integration: anonymous routing", () => {
  test("dashboard redirects anonymous users to login with returnTo", async ({ page }) => {
    await page.goto("/dashboard");
    await page.waitForURL(/\/login/);
    await expect(page).toHaveURL(/returnTo/);
  });

  test("generation route redirects anonymous users to login with returnTo", async ({ page }) => {
    // /generate/* is auth-gated (middleware). A signed-out visitor never reaches
    // the page itself — including the not-found branch — so the contract here is
    // the redirect, not the rendered page. The UUID is a well-formed value that
    // cannot exist as a real generation ID.
    await page.goto("/generate/00000000-0000-0000-0000-000000000000");
    await page.waitForURL(/\/login/);
    await expect(page).toHaveURL(/returnTo/);
  });

  test("a protected page reached by a percent-encoded path is still gated", async ({ page }) => {
    // Next serves /%73imple as /simple but hands the proxy the raw path; the gate decodes it.
    const res = await page.goto("/%73imple");
    expect(res?.request().redirectedFrom()?.url()).toContain("/%73imple");
    await page.waitForURL(/\/login/);
    await expect(page).toHaveURL(/returnTo/);
  });

  test("Auth.js endpoints answer in Qavren mode", async ({ request }) => {
    const providers = await request.get("/api/auth/providers");
    expect(providers.status()).toBe(200);
    expect(await providers.json()).toHaveProperty("keycloak");
    const session = await request.get("/api/auth/session");
    expect(session.status()).toBe(200);
    expect(await session.json()).toBeNull();
  });
});

test.describe("Integration: Keycloak sign-in, dashboard, sign-out", () => {
  // Both tests count the fixture user's realm sessions: run them in order, one worker,
  // each starting from zero (earlier tests and runs leave SSO sessions behind).
  test.describe.configure({ mode: "default" });
  test.beforeEach(async ({ request }) => {
    await endUserSessions(request, FIXTURE_USER.email);
  });

  test("signs in through the realm and lands on the dashboard with the identity", async ({ page, request }) => {
    await signInViaKeycloak(page);
    await expect(page).toHaveURL(/\/dashboard/);
    await expect(page.getByText(FIXTURE_USER.email).first()).toBeVisible();
    const session = await page.request.get("/api/auth/session");
    expect((await session.json())?.user?.email).toBe(FIXTURE_USER.email);
    expect(await userSessionCount(request, FIXTURE_USER.email)).toBe(1);
  });

  test("sign-out ends both the app session and the realm session, even with a response still in flight", async ({
    page,
    request,
    baseURL,
  }) => {
    await signInViaKeycloak(page);
    expect(await userSessionCount(request, FIXTURE_USER.email)).toBe(1);
    const appOrigin = new URL(baseURL!).origin;

    // Phase C's race: Auth.js's middleware appends a refreshed session cookie to every gated
    // response, so a request that left the browser before sign-out and answers after it would
    // resurrect the session. Capture what such a request carries now and replay it once
    // sign-out has finished — the worst-case ordering, made deterministic (a fetch fired from
    // the page itself is cancelled when the sign-out navigation tears the document down).
    const inflightCookies = (await page.context().cookies(appOrigin)).map((c) => `${c.name}=${c.value}`).join("; ");
    expect(inflightCookies).toMatch(/authjs\.session-token/);

    await page.locator('form[action="/auth/signout"] button[type="submit"]').click();
    await page.waitForURL((u) => u.origin === appOrigin && u.pathname === "/");

    // page.request shares the browser's cookie jar: a Set-Cookie on this late response
    // lands exactly where the browser's own would.
    const late = await page.request.get("/dashboard", { headers: { cookie: inflightCookies }, maxRedirects: 0 });
    // Not vacuous: the stale JWT still authenticates (sessions are stateless until maxAge), so
    // this is a gated, signed-in response — exactly the one Auth.js appends a refresh to. If
    // it ever redirects instead, sign-out revokes server-side and this line should change.
    expect(late.status()).toBe(200);
    expect(late.headers()["set-cookie"] ?? "").not.toMatch(SESSION_SET_COOKIE);

    const cookies = await page.context().cookies(appOrigin);
    expect(cookies.filter((c) => SESSION_COOKIE.test(c.name) && c.value !== "")).toEqual([]);
    await page.goto("/dashboard");
    await expect(page).toHaveURL(/\/login\?returnTo=/);
    expect(await userSessionCount(request, FIXTURE_USER.email)).toBe(0);
  });
});

test.describe("Integration: registration through the realm", () => {
  const email = `e2e-reg-${Date.now()}@stackalchemist.test`;
  test.afterEach(async ({ request }) => {
    await deleteUserByEmail(request, email);
  });

  test("Create an account opens Keycloak's registration form and returns signed in", async ({ page }) => {
    await page.goto("/register?returnTo=%2Fdashboard");
    await page.locator("main form button[type=submit]").click();
    // prompt=create renders the registration form on the authorization URL itself.
    await page.waitForURL((u) => onKeycloak(u) && u.searchParams.get("prompt") === "create");
    await expect(page.getByRole("heading", { name: /^register$/i })).toBeVisible();
    // registrationEmailAsUsername: no separate username field.
    await page.getByRole("textbox", { name: /^email$/i }).fill(email);
    await page.getByRole("textbox", { name: /^password$/i }).fill("Reg-Ression-2026!");
    await page.getByRole("textbox", { name: /^confirm password$/i }).fill("Reg-Ression-2026!");
    await page.getByRole("textbox", { name: /^first name$/i }).fill("Reg");
    await page.getByRole("textbox", { name: /^last name$/i }).fill("Ression");
    await page.getByRole("button", { name: /^register$/i }).click();
    await page.waitForURL((u) => !onKeycloak(u));
    await expect(page).toHaveURL(/\/dashboard/);
    await expect(page.getByText(email).first()).toBeVisible();
  });
});
