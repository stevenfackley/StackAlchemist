import Link from "next/link";
import { get{{EntityName}}s } from "@/lib/api";

export default async function {{EntityName}}sPage() {
  const items = await get{{EntityName}}s();

  return (
    <main className="min-h-screen p-8">
      <div className="mb-6">
        <Link href="/" className="text-sm text-gray-500 hover:underline">← Home</Link>
      </div>

      <h1 className="text-2xl font-bold">{{EntityName}}s</h1>

      [[LLM_INJECTION_START: ListContent]]
      {/*
        LLM fills: a table or grid rendering the `items` array. Use the entity's
        non-pk Field names as columns. Each row links to `/{{EntityNameLower}}s/${id}`
        for a future detail page. Use Tailwind utility classes; keep it minimal —
        one table, no fancy filtering yet.
      */}
      <table className="mt-4 min-w-full">
        <thead>
          <tr><th className="text-left">id</th></tr>
        </thead>
        <tbody>
          {items.map((item) => (
            <tr key={item.id}><td className="py-1 font-mono text-xs">{item.id}</td></tr>
          ))}
        </tbody>
      </table>
      [[LLM_INJECTION_END: ListContent]]
    </main>
  );
}
