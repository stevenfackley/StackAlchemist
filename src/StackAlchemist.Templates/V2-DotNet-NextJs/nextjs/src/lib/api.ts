import type {
  {{#each Entities}}
  {{Name}},
  Create{{Name}}Input,
  {{/each}}
} from "@/types";

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${API_URL}${path}`, {
    headers: { "Content-Type": "application/json", ...init?.headers },
    ...init,
  });
  if (!res.ok) throw new Error(`API error ${res.status}: ${await res.text()}`);
  return res.json() as Promise<T>;
}

{{#each Entities}}
// ── {{Name}} ──────────────────────────────────────────────────────────────────

export const get{{Name}}s = (): Promise<{{Name}}[]> =>
  apiFetch<{{Name}}[]>("/api/{{NameLower}}s");

export const get{{Name}} = (id: string): Promise<{{Name}}> =>
  apiFetch<{{Name}}>(`/api/{{NameLower}}s/${id}`);

export const create{{Name}} = (data: Create{{Name}}Input): Promise<{{Name}}> =>
  apiFetch<{{Name}}>("/api/{{NameLower}}s", {
    method: "POST",
    body: JSON.stringify(data),
  });

export const update{{Name}} = (id: string, data: Partial<Create{{Name}}Input>): Promise<{{Name}}> =>
  apiFetch<{{Name}}>(`/api/{{NameLower}}s/${id}`, {
    method: "PUT",
    body: JSON.stringify(data),
  });

export const delete{{Name}} = (id: string): Promise<void> =>
  apiFetch<void>(`/api/{{NameLower}}s/${id}`, { method: "DELETE" });

{{/each}}
