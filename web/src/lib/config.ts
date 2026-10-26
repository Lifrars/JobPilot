/**
 * URL interna de la API .NET (servidor a servidor: proxy.ts y Server Components).
 * Aspire la inyecta al hacer `.WithReference(api)`; fuera de Aspire se usa el puerto del launchSettings.
 * Se lee en cada llamada (no al importar) para que funcione con variables definidas en runtime (Docker).
 */
export function apiBaseUrl(): string {
  return (
    process.env.API_HTTP ??
    process.env.services__api__http__0 ??
    process.env.JOBPILOT_API_URL ??
    "http://localhost:5002"
  );
}

/** Cookie de sesión que emite ASP.NET Core Identity con `login?useCookies=true`. */
export const SESSION_COOKIE = ".AspNetCore.Identity.Application";
