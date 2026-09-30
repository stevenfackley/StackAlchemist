import NextAuth from "next-auth";
import { authConfig } from "./auth.config";

// Imported lazily (await import("@/auth")) by every Qavren-mode branch so a
// Supabase-mode process never loads next-auth.
export const { handlers, auth, signIn, signOut } = NextAuth(authConfig);
export { authConfig };
