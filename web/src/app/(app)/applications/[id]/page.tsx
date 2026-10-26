import type { Metadata } from "next";
import { Suspense } from "react";
import { ApplicationDetail } from "@/features/applications/application-detail";
import { Loading } from "@/components/ui";

export const metadata: Metadata = { title: "Postulación" };

/** En Next.js 16, `params` es una Promise: se resuelve dentro del Suspense para no bloquear el shell. */
export default function ApplicationPage({ params }: PageProps<"/applications/[id]">) {
  return (
    <Suspense fallback={<Loading />}>
      {params.then(({ id }) => (
        <ApplicationDetail id={id} />
      ))}
    </Suspense>
  );
}
