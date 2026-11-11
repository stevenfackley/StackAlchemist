// @vitest-environment node
/**
 * In production nginx sends every /api/ path to the Engine unless a rule above
 * that one names it (docker/nginx.prod.conf). A web route handler added under
 * src/app/api without such a rule works in tests and on the test site, which
 * has no nginx, and answers 401 from the Engine in production. This has
 * happened twice (csp-report, and a download route caught in review).
 */
import { readdirSync, readFileSync } from "node:fs";
import path from "node:path";

const WEB_ROOT = path.resolve(__dirname, "../..");
const NGINX_CONF = path.resolve(WEB_ROOT, "../../docker/nginx.prod.conf");

/** The /api locations nginx hands to the web container, as { path, exact }. */
function webApiLocations(): Array<{ path: string; exact: boolean }> {
  const conf = readFileSync(NGINX_CONF, "utf8");
  return [...conf.matchAll(/location\s+(=\s+)?(\/api\/\S*)\s*\{([^}]*)\}/g)]
    .filter(([, , , body]) => body.includes("sa-web"))
    .map(([, exact, location]) => ({ path: location, exact: Boolean(exact) }));
}

/** The URL of every route handler under src/app/api, with dynamic segments left as written. */
function webApiRoutes(dir = path.join(WEB_ROOT, "src/app/api"), url = "/api"): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    if (entry.isDirectory()) return webApiRoutes(path.join(dir, entry.name), `${url}/${entry.name}`);
    return /^route\.(ts|tsx|js)$/.test(entry.name) ? [url] : [];
  });
}

describe("production nginx and the web app's /api routes", () => {
  it("finds the rules and the routes it is meant to compare", () => {
    expect(webApiLocations().map((l) => l.path)).toEqual(
      expect.arrayContaining(["/api/healthz", "/api/auth/", "/api/csp-report"])
    );
    expect(webApiRoutes()).toEqual(expect.arrayContaining(["/api/healthz", "/api/csp-report"]));
  });

  it("sends every web route under /api to the web container, not the Engine", () => {
    const locations = webApiLocations();
    const unrouted = webApiRoutes().filter(
      (route) =>
        !locations.some((l) => (l.exact ? route === l.path : `${route}/`.startsWith(l.path) || route.startsWith(l.path)))
    );

    // A route listed here needs a `location` rule above `location /api/` in
    // docker/nginx.prod.conf, or a home outside /api (see src/app/download).
    expect(unrouted).toEqual([]);
  });
});
