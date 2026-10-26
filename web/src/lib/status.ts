import type { WorkMode } from "@/lib/api/types";

/** Estados del pipeline, en el mismo orden que el smart enum ApplicationStatus del backend. */
export const STATUSES = [
  "Wishlist",
  "Applied",
  "Screening",
  "Interviewing",
  "Offer",
  "Accepted",
  "Rejected",
  "Withdrawn",
] as const;

export type Status = (typeof STATUSES)[number];

export const statusMeta: Record<Status, { label: string; dot: string }> = {
  Wishlist: { label: "Por postular", dot: "bg-slate-400" },
  Applied: { label: "Postulado", dot: "bg-sky-500" },
  Screening: { label: "Filtro inicial", dot: "bg-indigo-500" },
  Interviewing: { label: "Entrevistas", dot: "bg-violet-500" },
  Offer: { label: "Oferta", dot: "bg-amber-500" },
  Accepted: { label: "Aceptada", dot: "bg-emerald-500" },
  Rejected: { label: "Rechazada", dot: "bg-rose-500" },
  Withdrawn: { label: "Retirada", dot: "bg-zinc-500" },
};

/** Etiquetas en español del enum WorkMode del backend (el tipo sale del OpenAPI). */
export const workModeLabels = {
  Unknown: "Sin especificar",
  Remote: "Remoto",
  Hybrid: "Híbrido",
  OnSite: "Presencial",
} as const satisfies Record<WorkMode, string>;

/** Type guard: convierte un string del backend en `Status` de forma segura. */
export function isStatus(value: string): value is Status {
  return (STATUSES as readonly string[]).includes(value);
}

/**
 * Agrupa una lista por una clave (genérico: sirve para cualquier tipo y cualquier clave).
 * Las claves en `keys` siempre existen en el resultado, aunque queden vacías (columnas sin tarjetas).
 */
export function groupBy<TItem, TKey extends PropertyKey>(
  items: readonly TItem[],
  keyOf: (item: TItem) => TKey,
  keys: readonly TKey[] = [],
): Record<TKey, TItem[]> {
  const groups = Object.fromEntries(keys.map((key) => [key, [] as TItem[]])) as Record<TKey, TItem[]>;
  for (const item of items) {
    (groups[keyOf(item)] ??= []).push(item);
  }
  return groups;
}

/** La regla la decide el backend (State pattern); el frontend solo la refleja para la UX del drag & drop. */
export function canMoveTo(item: { status: string; allowedTransitions: readonly string[] }, target: string): boolean {
  return item.status !== target && item.allowedTransitions.includes(target);
}
