import Link from "next/link";

export default function ErrorPage() {
  return (
    <div
      style={{
        minHeight: "100vh",
        display: "grid",
        placeItems: "center",
        background: "#0f172a",
        color: "white",
        padding: "2rem",
        textAlign: "center",
      }}
    >
      <div>
        <p style={{ letterSpacing: ".3em", textTransform: "uppercase", color: "#60a5fa", fontSize: 12 }}>
          StackAlchemist
        </p>
        <h1 style={{ fontSize: 32, margin: "12px 0" }}>Something went wrong</h1>
        <p style={{ color: "#94a3b8", maxWidth: 560, lineHeight: 1.6 }}>
          This fallback page exists to keep the app buildable in isolated environments like Bolt.new.
        </p>
        <div style={{ marginTop: 24 }}>
          <Link href="/" style={{ color: "white", background: "#3b82f6", padding: "12px 20px", borderRadius: 9999 }}>
            Return Home
          </Link>
        </div>
      </div>
    </div>
  );
}