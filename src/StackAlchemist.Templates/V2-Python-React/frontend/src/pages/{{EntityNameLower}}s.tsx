import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import type { {{EntityName}} } from "../types";
import { get{{EntityName}}s } from "../lib/api";

export default function {{EntityName}}sPage() {
  const [items, setItems] = useState<{{EntityName}}[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    get{{EntityName}}s()
      .then(setItems)
      .catch((e) => setError(e.message))
      .finally(() => setLoading(false));
  }, []);

  return (
    <main className="min-h-screen bg-gray-50 p-8">
      <div className="mb-6">
        <Link to="/" className="text-sm text-gray-500 hover:underline">← Home</Link>
      </div>

      <h1 className="text-2xl font-bold">{{EntityName}}s</h1>

      {loading && <p className="mt-4 text-gray-500">Loading…</p>}
      {error && <p className="mt-4 text-red-600">{error}</p>}

      [[LLM_INJECTION_START: ListContent]]
      {/*
        LLM fills: a table or grid rendering the `items` array. Use the entity's
        non-pk Field names as columns. Each row links to `/{{EntityNameLower}}s/${id}`
        for a future detail page. Use Tailwind utility classes; keep it minimal —
        one table, no fancy filtering yet.
      */}
      {!loading && !error && (
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
      )}
      [[LLM_INJECTION_END: ListContent]]
    </main>
  );
}
