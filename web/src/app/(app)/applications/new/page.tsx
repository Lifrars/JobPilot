import type { Metadata } from "next";
import { Suspense } from "react";
import { NewApplicationForm } from "@/features/applications/new-application-form";
import { Loading } from "@/components/ui";

export const metadata: Metadata = { title: "Nueva postulación" };

export default function NewApplicationPage() {
  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-6">
      <h1 className="text-2xl font-semibold tracking-tight">Nueva postulación</h1>
      <Suspense fallback={<Loading />}>
        <NewApplicationForm />
      </Suspense>
    </div>
  );
}
