import { NextResponse, type NextRequest } from "next/server";
import { usesQavrenAuth } from "@/lib/runtime-config";

// Auth.js exists only in Qavren Auth mode. In Supabase mode the module is never
// loaded, so a stray request cannot surface a MissingSecret 500.
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
