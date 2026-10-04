# StackAlchemist: Troubleshooting & FAQ

## Common Generation Issues

### Build Failure During Generation
If the "Compile Guarantee" log shows a persistent build failure after 3 retries:
- **Possible Cause:** The schema you defined is too complex for a single generation pass or contains circular dependencies. A full refund is issued automatically on a paid tier.
- **Resolution:** Try simplifying your entity relationships in the Advanced Mode editor and re-running the generation.

### Missing API Endpoints
If you expected an endpoint that wasn't generated:
- **Possible Cause:** The LLM prioritized core CRUD operations over custom logic.
- **Resolution:** All generated code is 100% human-readable. You can easily add custom endpoints by following the patterns established in the existing controllers.

## Platform Errors

### Payment Succeeded but Generation Didn't Start
- **Possible Cause:** Stripe webhook delay or processing error.
- **Resolution:** Please contact support@stackalchemist.app with your Stripe Session ID, and we will manually trigger the generation for you.

### Download Link Expired
- **Possible Cause:** Cloudflare R2 presigned URLs expire after 7 days.
- **Resolution:** Email support@stackalchemist.app with your generation ID and we will issue a fresh link. It costs nothing extra. Save the ZIP somewhere safe once it downloads.

### Status Page Looks Stuck
- **Possible Cause:** The page refreshes every few seconds, but only while its browser tab is visible.
- **Resolution:** Bring the tab to the foreground, or reload it. If the build has failed, the page offers a retry (up to 3 per generation).

### Sign-in Problems
- Sign-in runs on `auth.stackalchemist.app`. Use **Forgot password** on that page to reset a password, and check spam for the verification email.
- Sessions last 7 days; after that you sign in again.

## Local Development Issues

### .NET Build Errors Locally
- **Issue:** `The framework 'Microsoft.AspNetCore.App', version '10.0.0' was not found.`
- **Resolution:** Ensure you have the .NET 10 SDK installed. You can check your version with `dotnet --version`.

### Next.js Connection Refused
- **Issue:** The frontend cannot connect to the backend API.
- **Resolution:** Verify that your `NEXT_PUBLIC_API_URL` in `.env.local` points to the correct local port (usually `https://localhost:5001` or `http://localhost:5000`).
