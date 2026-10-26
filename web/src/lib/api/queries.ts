import { queryOptions } from "@tanstack/react-query";
import { api } from "./client";
import { unwrap } from "./errors";

/**
 * Contrato de caché: cada query define su key y su queryFn en UN lugar.
 * Los componentes usan `useQuery(queries.applications.list())` y las mutaciones invalidan por prefijo
 * (`queryKey: queries.applications.all`) sin repetir strings mágicos.
 */
export const queries = {
  applications: {
    all: ["applications"] as const,
    list: () =>
      queryOptions({
        queryKey: [...queries.applications.all, "list"] as const,
        queryFn: () => unwrap(api.GET("/api/v1/applications", { params: { query: { pageSize: 100 } } })),
      }),
    detail: (id: string) =>
      queryOptions({
        queryKey: [...queries.applications.all, "detail", id] as const,
        queryFn: () => unwrap(api.GET("/api/v1/applications/{id}", { params: { path: { id } } })),
      }),
    stats: () =>
      queryOptions({
        queryKey: [...queries.applications.all, "stats"] as const,
        queryFn: () => unwrap(api.GET("/api/v1/applications/stats")),
        // Las métricas se recalculan en el backend vía Outbox (consistencia eventual): refrescamos solas.
        refetchInterval: 15_000,
      }),
  },
  companies: {
    all: ["companies"] as const,
    list: () =>
      queryOptions({
        queryKey: [...queries.companies.all, "list"] as const,
        queryFn: () => unwrap(api.GET("/api/v1/companies", { params: { query: { pageSize: 100 } } })),
      }),
  },
  resumes: {
    all: ["resumes"] as const,
    list: () =>
      queryOptions({
        queryKey: [...queries.resumes.all, "list"] as const,
        queryFn: () => unwrap(api.GET("/api/v1/resumes")),
      }),
  },
};
