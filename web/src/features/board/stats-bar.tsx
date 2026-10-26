"use client";

import { useQuery } from "@tanstack/react-query";
import { queries } from "@/lib/api/queries";
import { Card } from "@/components/ui";

export function StatsBar() {
  const { data } = useQuery(queries.applications.stats());

  const items = [
    { label: "Postulaciones", value: data?.total ?? "–" },
    { label: "Activas", value: data?.active ?? "–" },
    { label: "Tasa de respuesta", value: data ? `${data.responseRate}%` : "–" },
    { label: "En entrevistas", value: data?.byStatus["Interviewing"] ?? "–" },
    { label: "Ofertas", value: data?.byStatus["Offer"] ?? "–" },
  ];

  return (
    <div className="grid grid-cols-2 gap-3 sm:grid-cols-5">
      {items.map((item) => (
        <Card key={item.label} className="p-4">
          <p className="text-xs text-muted">{item.label}</p>
          <p className="mt-1 text-2xl font-semibold tabular-nums">{item.value}</p>
        </Card>
      ))}
    </div>
  );
}
