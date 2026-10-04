import { NextResponse, type NextRequest } from "next/server";
import { usesQavrenAuth } from "@/lib/runtime-config";

// Without QAVREN_AUTH_URL (demo mode; production refuses to boot without it) there
// is no realm and no AUTH_SECRET. Auth.js is never loaded then, so a stray request
// cannot surface a MissingSecret 500.
export async function GET(req: NextRequest) {
  if (!usesQavrenAuth()) return new NextResponse(null, { status: 404 });
  const { handlers } = await import("@/auth");
  return handlers.GET(req);
}

export async function POST(req: NextRequest) {
  if (!usesQavrenAuth()) return new NextResponse(null, { status: 404 });
  const { handlers } = await import("@/auth");
  return handlers.POST(req);
}
