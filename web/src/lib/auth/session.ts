import "server-only";
import { redirect } from "next/navigation";
import { serverApi } from "@/lib/api/server";

export type CurrentUser = { email: string };

/**
 * Data Access Layer de la sesión: valida la cookie contra la API .NET (no confía en el proxy optimista).
 * `use cache: private` puede leer cookies() y cachea el resultado SOLO en el navegador de este usuario,
 * así la navegación entre páginas privadas no vuelve a preguntar por la sesión en cada clic.
 * Debe llamarse dentro de un <Suspense> (es un dato de request, no puede ir en el shell estático).
 */
export async function getCurrentUser(): Promise<CurrentUser> {
  "use cache: private";

  const api = await serverApi();
  const { data, response } = await api.GET("/api/v1/auth/manage/info");

  if (response.status === 401 || !data) {
    redirect("/login");
  }

  // Exponer solo lo necesario al cliente (no el objeto completo de Identity).
  return { email: data.email };
}
