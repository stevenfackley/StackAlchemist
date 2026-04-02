# StackAlchemist: Troubleshooting & FAQ

## Common Generation Issues

### Build Failure During Generation
If the "Compile Guarantee" log shows a persistent build failure after 3 retries:
- **Possible Cause:** The schema you defined is too complex for a single generation pass or contains circular dependencies.
- **Resolution:** Try simplifying your entity relationships in the Advanced Mode editor and re-running the generation.

### Missing API Endpoints
If you expected an endpoint that wasn't generated:
- **Possible Cause:** The LLM prioritized core CRUD operations over custom logic.
- **Resolution:** All generated code is 100% human-readable. You can easily add custom endpoints by following the patterns established in the existing controllers.

## Platform Errors

### Payment Succeeded but Generation Didn't Start
- **Possible Cause:** Stripe webhook delay or processing error.
- **Resolution:** Please contact support@stackalchemist.com with your Stripe Session ID, and we will manually trigger the generation for you.

### Download Link Expired
- **Possible Cause:** For security reasons, Cloudflare R2 presigned URLs expire after 24 hours.
- **Resolution:** Go to your **Profile > History** and click "Regenerate Download Link." This will not cost any additional credits.

## Local Development Issues

### .NET Build Errors Locally
- **Issue:** `The framework 'Microsoft.AspNetCore.App', version '10.0.0' was not found.`
- **Resolution:** Ensure you have the .NET 10 SDK installed. You can check your version with `dotnet --version`.

### Next.js Connection Refused
- **Issue:** The frontend cannot connect to the backend API.
- **Resolution:** Verify that your `NEXT_PUBLIC_API_URL` in `.env.local` points to the correct local port (usually `https://localhost:5001` or `http://localhost:5000`).
