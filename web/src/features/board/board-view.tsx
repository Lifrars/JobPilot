"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import {
  DndContext,
  type DragEndEvent,
  KeyboardSensor,
  PointerSensor,
  useDndContext,
  useDraggable,
  useDroppable,
  useSensor,
  useSensors,
} from "@dnd-kit/core";
import { clsx } from "clsx";
import { queries } from "@/lib/api/queries";
import { errorMessage } from "@/lib/api/errors";
import type { JobApplicationSummary } from "@/lib/api/types";
import { canMoveTo, groupBy, STATUSES, type Status, statusMeta, workModeLabels } from "@/lib/status";
import { Alert, buttonStyles, Chip, Loading } from "@/components/ui";
import { useMoveApplication } from "./use-move-application";

export function BoardView() {
  const applications = useQuery(queries.applications.list());
  const companies = useQuery(queries.companies.list());
  const move = useMoveApplication();
  const [error, setError] = useState<string>();

  // Requiere mover 6px antes de arrastrar: así un clic normal sigue abriendo el detalle.
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 6 } }), useSensor(KeyboardSensor));

  const companyNames = useMemo(
    () => new Map((companies.data?.items ?? []).map((company) => [company.id, company.name])),
    [companies.data],
  );

  const columns = useMemo(
    () => groupBy(applications.data?.items ?? [], (item) => item.status as Status, STATUSES),
    [applications.data],
  );

  if (applications.isPending) return <Loading label="Cargando tablero…" />;
  if (applications.isError) return <Alert>{errorMessage(applications.error)}</Alert>;

  if (applications.data.totalCount === 0) {
    return (
      <div className="rounded-xl border border-dashed border-border p-10 text-center">
        <p className="font-medium">Todavía no tienes postulaciones.</p>
        <p className="mt-1 text-sm text-muted">Agrega la primera oferta que te interese.</p>
        <Link href="/applications/new" className={buttonStyles("primary", "mt-4")}>
          Nueva postulación
        </Link>
      </div>
    );
  }

  function handleDragEnd({ active, over }: DragEndEvent) {
    const item = active.data.current as JobApplicationSummary | undefined;
    if (!item || !over || !canMoveTo(item, String(over.id))) return;

    setError(undefined);
    move.mutate({ id: item.id, status: String(over.id) }, { onError: (e) => setError(errorMessage(e)) });
  }

  return (
    <div className="flex flex-col gap-3">
      {error && <Alert>{error}</Alert>}
      <DndContext sensors={sensors} onDragEnd={handleDragEnd}>
        <div className="flex gap-3 overflow-x-auto pb-4">
          {STATUSES.map((status) => (
            <Column key={status} status={status} items={columns[status]} companyNames={companyNames} />
          ))}
        </div>
      </DndContext>
    </div>
  );
}

function Column({ status, items, companyNames }: {
  status: Status;
  items: JobApplicationSummary[];
  companyNames: Map<string, string>;
}) {
  const { active } = useDndContext();
  const dragged = active?.data.current as JobApplicationSummary | undefined;
  const allowed = dragged ? canMoveTo(dragged, status) : false;
  const { setNodeRef, isOver } = useDroppable({ id: status, disabled: !allowed });

  return (
    <section
      ref={setNodeRef}
      aria-label={statusMeta[status].label}
      className={clsx(
        "flex w-64 shrink-0 flex-col gap-2 rounded-xl border p-2 transition",
        dragged && allowed ? "border-accent bg-accent/5" : "border-border bg-surface/60",
        dragged && !allowed && dragged.status !== status && "opacity-40",
        isOver && allowed && "ring-2 ring-accent",
      )}
    >
      <header className="flex items-center justify-between px-1.5 py-1">
        <span className="flex items-center gap-2 text-sm font-medium">
          <span className={clsx("size-2 rounded-full", statusMeta[status].dot)} />
          {statusMeta[status].label}
        </span>
        <span className="text-xs text-muted tabular-nums">{items.length}</span>
      </header>
      <ul className="flex min-h-20 flex-col gap-2">
        {items.map((item) => (
          <ApplicationCard key={item.id} item={item} company={companyNames.get(item.companyId)} />
        ))}
      </ul>
    </section>
  );
}

function ApplicationCard({ item, company }: { item: JobApplicationSummary; company?: string }) {
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({ id: item.id, data: item });

  return (
    <li
      ref={setNodeRef}
      {...attributes}
      {...listeners}
      style={transform ? { transform: `translate3d(${transform.x}px, ${transform.y}px, 0)` } : undefined}
      className={clsx(
        "cursor-grab rounded-lg border border-border bg-surface p-3 shadow-sm active:cursor-grabbing",
        isDragging && "z-10 shadow-lg ring-2 ring-accent",
      )}
    >
      <Link href={`/applications/${item.id}`} className="block text-sm font-medium hover:text-accent">
        {item.title}
      </Link>
      {company && <p className="mt-0.5 text-xs text-muted">{company}</p>}
      <div className="mt-2 flex items-center justify-between">
        {item.workMode !== "Unknown" ? <Chip>{workModeLabels[item.workMode]}</Chip> : <span />}
        <time className="text-[11px] text-muted" dateTime={item.updatedAt}>
          {new Date(item.updatedAt).toLocaleDateString("es")}
        </time>
      </div>
    </li>
  );
}
