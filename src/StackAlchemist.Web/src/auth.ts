import NextAuth from "next-auth";
import { authConfig } from "./auth.config";

// Imported lazily (await import("@/auth")), behind usesQavrenAuth() or the proxy's
// demo-mode check, so a demo-mode process never loads next-auth (this applies to
// @/auth.config too).
export const { handlers, auth, signIn, signOut } = NextAuth(authConfig);
export { authConfig };
