"use client";

import { useEffect, useState } from "react";
import { getFreeQuotaStatus, type FreeQuotaStatus } from "@/lib/actions";

export function useFreeQuota(): { quota: FreeQuotaStatus | null; loading: boolean } {
  const [quota, setQuota] = useState<FreeQuotaStatus | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    getFreeQuotaStatus()
      .then((q) => { setQuota(q); setLoading(false); })
      .catch(() => { setLoading(false); });
  }, []);

  return { quota, loading };
}
