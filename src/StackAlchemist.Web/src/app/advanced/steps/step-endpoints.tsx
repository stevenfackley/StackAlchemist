"use client";

import type { Endpoint } from "@/lib/types";
import { cn } from "@/lib/utils";
import { Eyebrow, Cluster, Panel, Select, Stack, TextInput } from "@/components/ui";
import { ICON_BTN, ADD_BLOCK } from "./styles";

const HTTP_METHODS: Endpoint["method"][] = ["GET", "POST", "PUT", "DELETE", "PATCH"];

export function StepEndpoints({ endpoints, setEndpoints, entityNames }: {
  endpoints: Endpoint[];
  setEndpoints: React.Dispatch<React.SetStateAction<Endpoint[]>>;
  entityNames: string[];
}) {
  return (
    <Stack gap="md">
      <Eyebrow>Define API endpoints for your entities</Eyebrow>
      {endpoints.map((ep, idx) => (
        <Panel key={idx}>
          <Cluster gap="xs">
            <Select
              size="sm"
              value={ep.method}
              onChange={(e) => setEndpoints((p) => p.map((x, i) => i === idx ? { ...x, method: e.target.value as Endpoint["method"] } : x))}
              className={cn("w-20 font-bold",
                ep.method === "GET" && "text-success",
                ep.method === "POST" && "text-accent",
                ep.method === "PUT" && "text-yellow-400",
                (ep.method === "DELETE" || ep.method === "PATCH") && "text-danger",
              )}
            >
              {HTTP_METHODS.map((m) => <option key={m} value={m}>{m}</option>)}
            </Select>
            <TextInput
              size="sm"
              value={ep.path}
              onChange={(e) => setEndpoints((p) => p.map((x, i) => i === idx ? { ...x, path: e.target.value } : x))}
              placeholder="/api/v1/resource"
              className="min-w-[140px] flex-1"
            />
            <Select
              size="sm"
              value={ep.entity}
              onChange={(e) => setEndpoints((p) => p.map((x, i) => i === idx ? { ...x, entity: e.target.value } : x))}
            >
              <option value="">Entity...</option>
              {entityNames.map((n) => <option key={n} value={n}>{n}</option>)}
            </Select>
            <button type="button" aria-label={`Remove endpoint ${ep.method} ${ep.path}`} onClick={() => setEndpoints((p) => p.filter((_, i) => i !== idx))} className={ICON_BTN}>✕</button>
          </Cluster>
        </Panel>
      ))}
      <button
        type="button"
        onClick={() => setEndpoints((p) => [...p, { method: "GET", path: "", entity: "" }])}
        className={ADD_BLOCK}
      >
        + Add Endpoint
      </button>
    </Stack>
  );
}
