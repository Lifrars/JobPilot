import type { Metadata } from "next";
import { Suspense } from "react";
import { ResumesView } from "@/features/resumes/resumes-view";
import { Loading } from "@/components/ui";

export const metadata: Metadata = { title: "Mis CVs" };

export default function ResumesPage() {
  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Mis CVs</h1>
        <p className="text-sm text-muted">Guarda una versión por tipo de rol para comparar qué tan bien encaja cada una.</p>
      </div>
      <Suspense fallback={<Loading />}>
        <ResumesView />
      </Suspense>
    </div>
  );
}
