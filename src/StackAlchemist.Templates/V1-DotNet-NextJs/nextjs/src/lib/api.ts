const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${API_URL}${path}`, {
    headers: { "Content-Type": "application/json", ...init?.headers },
    ...init,
  });
  if (!res.ok) throw new Error(`API error ${res.status}: ${await res.text()}`);
  return res.json() as Promise<T>;
}

{{!-- LLM_INJECTION_START: ApiRouteHandlers --}}
{{!--
  The LLM will generate typed fetch helper functions per entity here.
  Expected format per entity:

  export async function get{EntityName}s(): Promise<{EntityName}[]> {
    return apiFetch<{EntityName}[]>("/api/v1/{entityLower}s");
  }

  export async function get{EntityName}(id: string): Promise<{EntityName}> {
    return apiFetch<{EntityName}>(`/api/v1/{entityLower}s/${id}`);
  }

  export async function create{EntityName}(data: Create{EntityName}Input): Promise<{EntityName}> {
    return apiFetch<{EntityName}>("/api/v1/{entityLower}s", { method: "POST", body: JSON.stringify(data) });
  }

  export async function update{EntityName}(id: string, data: Partial<Create{EntityName}Input>): Promise<{EntityName}> {
    return apiFetch<{EntityName}>(`/api/v1/{entityLower}s/${id}`, { method: "PUT", body: JSON.stringify(data) });
  }

  export async function delete{EntityName}(id: string): Promise<void> {
    return apiFetch<void>(`/api/v1/{entityLower}s/${id}`, { method: "DELETE" });
  }
--}}
{{!-- LLM_INJECTION_END: ApiRouteHandlers --}}
