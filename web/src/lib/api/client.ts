import createClient, { type Middleware } from "openapi-fetch";
import type { paths } from "./schema";

/**
 * Cliente del navegador. Llama a /api/v1/* en el MISMO origen que la web: proxy.ts lo reenvía a .NET.
 * La cookie HttpOnly de sesión viaja sola; el JavaScript nunca ve un token.
 */
export const api = createClient<paths>({
  baseUrl: typeof window === "undefined" ? "http://localhost" : window.location.origin,
  credentials: "same-origin",
  // Resolver fetch en cada llamada (no al crear el cliente): permite que MSW lo intercepte en los tests.
  fetch: (request) => globalThis.fetch(request),
});

const redirectToLoginOnUnauthorized: Middleware = {
  onResponse({ request, response }) {
    const isAuthCall = new URL(request.url).pathname.startsWith("/api/v1/auth/");
    if (response.status === 401 && !isAuthCall && typeof window !== "undefined") {
      // Recarga completa A PROPÓSITO (no router.push): la sesión expiró y se descarta todo el estado en memoria.
      // eslint-disable-next-line @next/next/no-location-assign-relative-destination
      window.location.assign(`/login?next=${encodeURIComponent(window.location.pathname)}`);
    }
    return response;
  },
};

api.use(redirectToLoginOnUnauthorized);
