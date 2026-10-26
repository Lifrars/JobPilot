import type { Metadata } from "next";
import { Suspense } from "react";
import { BoardView } from "@/features/board/board-view";
import { StatsBar } from "@/features/board/stats-bar";
import { Loading } from "@/components/ui";

export const metadata: Metadata = { title: "Tablero" };

export default function BoardPage() {
  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Tu pipeline</h1>
        <p className="text-sm text-muted">Arrastra una tarjeta para cambiar su estado. Solo se iluminan las columnas permitidas.</p>
      </div>
      {/* Con Cache Components, los Client Components con datos también se prerenderizan: van tras Suspense. */}
      <Suspense fallback={<Loading label="Cargando métricas…" />}>
        <StatsBar />
      </Suspense>
      <Suspense fallback={<Loading label="Cargando tablero…" />}>
        <BoardView />
      </Suspense>
    </div>
  );
}
