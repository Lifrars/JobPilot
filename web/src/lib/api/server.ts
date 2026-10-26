import "server-only";
import createClient from "openapi-fetch";
import { cookies } from "next/headers";
import { apiBaseUrl } from "@/lib/config";
import type { paths } from "./schema";

/**
 * Cliente para Server Components y Server Actions: llama directo a la API .NET (sin pasar por el navegador)
 * reenviando la cookie de sesión del usuario. `server-only` hace fallar el build si se importa desde el cliente.
 * Mismo genérico `paths` que el cliente del navegador: un solo contrato, dos transportes.
 */
export async function serverApi() {
  const cookieHeader = (await cookies()).toString();

  return createClient<paths>({
    baseUrl: apiBaseUrl(),
    headers: { cookie: cookieHeader },
  });
}
