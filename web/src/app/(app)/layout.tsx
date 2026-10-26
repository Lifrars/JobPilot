import type { ReactNode } from "react";
import { Suspense } from "react";
import Link from "next/link";
import { UserMenu } from "@/features/auth/user-menu";
import { Spinner } from "@/components/ui";

const links = [
  { href: "/", label: "Tablero" },
  { href: "/applications/new", label: "Nueva postulación" },
  { href: "/resumes", label: "Mis CVs" },
];

/**
 * Layout privado. La cabecera es estática (entra en el shell prerenderizado); solo el menú de usuario
 * lee la sesión y por eso va en su propio <Suspense>: no bloquea el resto de la página.
 */
export default function AppLayout({ children }: { children: ReactNode }) {
  return (
    <>
      <header className="border-b border-border bg-surface">
        <nav className="mx-auto flex h-14 max-w-7xl items-center gap-6 px-4">
          <Link href="/" className="font-semibold tracking-tight">
            🧭 JobPilot
          </Link>
          <ul className="flex flex-1 gap-1 text-sm">
            {links.map((link) => (
              <li key={link.href}>
                <Link href={link.href} className="rounded-md px-3 py-1.5 text-muted hover:bg-background hover:text-foreground">
                  {link.label}
                </Link>
              </li>
            ))}
          </ul>
          <Suspense fallback={<Spinner className="text-muted" />}>
            <UserMenu />
          </Suspense>
        </nav>
      </header>
      <main className="mx-auto w-full max-w-7xl flex-1 px-4 py-6">{children}</main>
    </>
  );
}
