import { getCurrentUser } from "@/lib/auth/session";
import { LogoutButton } from "./logout-button";

/** Server Component: lee la sesión en el servidor. Va dentro de <Suspense> (dato de request). */
export async function UserMenu() {
  const user = await getCurrentUser();

  return (
    <div className="flex items-center gap-3">
      <span className="hidden text-sm text-muted sm:inline">{user.email}</span>
      <LogoutButton />
    </div>
  );
}
