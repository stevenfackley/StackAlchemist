const API_URL = import.meta.env.VITE_API_URL ?? "http://localhost:8000";

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${API_URL}${path}`, {
    headers: { "Content-Type": "application/json" },
    ...init,
  });
  if (!res.ok) {
    throw new Error(`API error ${res.status}: ${res.statusText}`);
  }
  return res.json() as Promise<T>;
}

[[LLM_INJECTION_START: ApiRouteHandlers]]
// LLM generates per-entity helpers:
// export function getProducts() { return apiFetch<Product[]>("/api/v1/products"); }
// export function getProduct(id: string) { return apiFetch<Product>(`/api/v1/products/${id}`); }
[[LLM_INJECTION_END: ApiRouteHandlers]]
