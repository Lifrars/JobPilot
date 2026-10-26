import { Suspense } from "react";
import { Loading } from "@/components/ui";

/** Layout público (login/registro): centrado, sin navegación. */
export default function AuthLayout({ children }: LayoutProps<"/">) {
  return (
    <main className="flex flex-1 items-center justify-center p-6">
      <div className="w-full max-w-sm">
        <div className="mb-8 text-center">
          <p className="text-2xl font-semibold tracking-tight">🧭 JobPilot</p>
          <p className="mt-1 text-sm text-muted">Tu búsqueda de empleo, con IA y en orden.</p>
        </div>
        <div className="rounded-xl border border-border bg-surface p-6 shadow-sm">
          {/* useSearchParams (?next=) es un dato de request: debe ir dentro de Suspense. */}
          <Suspense fallback={<Loading />}>{children}</Suspense>
        </div>
      </div>
    </main>
  );
}
