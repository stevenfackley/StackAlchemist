import type { Generation, GenerationSchema, Tier } from "./types";

export const demoSchema: GenerationSchema = {
  entities: [
    {
      name: "Project",
      fields: [
        { name: "id", type: "UUID", pk: true },
        { name: "name", type: "String", pk: false },
        { name: "status", type: "String", pk: false },
        { name: "created_at", type: "Timestamp", pk: false },
      ],
    },
    {
      name: "Task",
      fields: [
        { name: "id", type: "UUID", pk: true },
        { name: "project_id", type: "UUID", pk: false },
        { name: "title", type: "String", pk: false },
        { name: "priority", type: "String", pk: false },
      ],
    },
    {
      name: "Member",
      fields: [
        { name: "id", type: "UUID", pk: true },
        { name: "project_id", type: "UUID", pk: false },
        { name: "email", type: "String", pk: false },
        { name: "role", type: "String", pk: false },
      ],
    },
  ],
  relationships: [
    { from: "Project", type: "Has Many", to: "Task" },
    { from: "Project", type: "Has Many", to: "Member" },
  ],
  endpoints: [
    { method: "GET", path: "/api/projects", entity: "Project" },
    { method: "GET", path: "/api/tasks", entity: "Task" },
    { method: "POST", path: "/api/projects", entity: "Project" },
  ],
};

export const demoPreviewFiles: Record<string, string> = {
  "package.json": JSON.stringify(
    {
      name: "stackalchemist-demo-preview",
      private: true,
      scripts: {
        dev: "next dev",
        build: "next build",
      },
      dependencies: {
        next: "15.5.14",
        react: "19.0.0",
        "react-dom": "19.0.0",
      },
    },
    null,
    2
  ),
  "src/app/page.tsx": `export default function Page() {
  return (
    <main style={{minHeight:'100vh',display:'grid',placeItems:'center',background:'#020617',color:'white',fontFamily:'sans-serif'}}>
      <div style={{maxWidth:720,padding:32}}>
        <p style={{color:'#60a5fa',textTransform:'uppercase',letterSpacing:'0.2em',fontSize:12}}>StackAlchemist Demo</p>
        <h1 style={{fontSize:42,margin:'12px 0'}}>Bolt-friendly preview scaffold</h1>
        <p style={{color:'#94a3b8',lineHeight:1.6}}>This preview is intentionally self-contained so you can tweak the UI in Bolt without requiring the .NET engine, Supabase, or background workers.</p>
      </div>
    </main>
  );
}`,
};

export function buildDemoGeneration(id: string, tier: Tier = 0): Generation {
  const now = new Date().toISOString();

  return {
    id,
    user_id: null,
    transaction_id: null,
    status: "success",
    mode: id.includes("advanced") ? "advanced" : "simple",
    tier,
    prompt: "Demo workspace for UI iteration in Bolt.new",
    schema_json: demoSchema,
    download_url: tier === 0 ? null : "/demo-download.zip",
    preview_files_json: demoPreviewFiles,
    build_log: "Demo mode active: backend services are mocked for Bolt.new compatibility.",
    error_message: null,
    attempt_count: 0,
    created_at: now,
    updated_at: now,
    completed_at: now,
  };
}