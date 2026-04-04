/**
 * TestSiteBanner — shown on test/staging environments only.
 *
 * Rendered when the NEXT_PUBLIC_IS_TEST_SITE env var is set to "true".
 * The last git commit date is baked in at build time via NEXT_PUBLIC_BUILD_DATE
 * (set in next.config.ts using `git log -1 --format=%ci`).
 */
export function TestSiteBanner() {
  if (process.env.NEXT_PUBLIC_IS_TEST_SITE !== "true") return null;

  const rawDate = process.env.NEXT_PUBLIC_BUILD_DATE ?? "";

  // Parse the date and format it nicely, falling back to the raw string.
  let formattedDate = rawDate;
  try {
    const d = new Date(rawDate);
    if (!isNaN(d.getTime())) {
      formattedDate = d.toLocaleString("en-US", {
        year: "numeric",
        month: "short",
        day: "numeric",
        hour: "2-digit",
        minute: "2-digit",
        timeZoneName: "short",
      });
    }
  } catch {
    // keep rawDate
  }

  return (
    <div
      role="banner"
      aria-label="Test environment notice"
      className="w-full bg-amber-500 text-black text-center py-2 px-4 text-xs font-mono font-semibold tracking-wide z-[9999] sticky top-0"
    >
      ⚠️&nbsp; TEST SITE &nbsp;—&nbsp; This is a staging / test environment.
      &nbsp;|&nbsp; Source last updated:&nbsp;
      <span className="font-bold">{formattedDate || "unknown"}</span>
    </div>
  );
}
