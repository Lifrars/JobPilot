# ADR 0006: Frontend con Next.js 16 como BFF

- **Estado:** aceptada · **Fecha:** 2026-10-26

## Contexto
JobPilot necesita una interfaz que un reclutador entienda en segundos (un tablero de postulaciones) y que también
sirva para usarlo a diario. El backend ya expone una API REST con OpenAPI y autenticación con ASP.NET Core Identity.

## Decisión
1. **Next.js 16 (App Router)** con TypeScript, Tailwind 4, React Compiler y **Cache Components**.
2. **Next como BFF:** `proxy.ts` (el nuevo nombre del middleware) reenvía `/api/v1/*` a la API .NET leyendo la URL en
   runtime (`API_HTTP`, inyectada por Aspire con `AddNextJsApp(...).WithReference(api)`).
3. **Sesión con cookie HttpOnly** (`/api/v1/auth/login?useCookies=true`). Como el navegador solo habla con el origen de Next,
   la cookie funciona sin CORS y ningún token queda expuesto a JavaScript, lo que mitiga el robo de sesión por XSS.
   Se agregó `POST /api/v1/auth/logout` en la API.
4. **Contrato tipado:** `openapi-typescript` genera los tipos desde el OpenAPI de .NET y `openapi-fetch` los usa con un
   genérico `createClient<paths>()` en servidor y navegador. La API usa `JsonNumberHandling.Strict` para un contrato limpio.
5. **Datos:** Server Components para la sesión (`getCurrentUser` con `'use cache: private'`) y TanStack Query en
   Client Components para lo interactivo (kanban con actualización optimista).
6. **Formularios** con `FormData` + zod (sin react-hook-form): menos dependencias y un helper genérico `parseForm`.

## Alternativas
- **SPA con Vite + React Router:** más simple, pero sin SSR ni BFF, y los tokens tendrían que vivir en el navegador.
- **Blazor:** stack 100 % .NET, pero menos demandado fuera de ese ecosistema.
- **Rewrites en `next.config.ts`:** se evalúan al hacer el build. Si la URL de la API cambia por entorno (Docker), queda fija.
  `proxy.ts` la resuelve en cada request.

## Consecuencias
- ✅ Un solo origen, cookie HttpOnly y tipos sincronizados con el backend.
- ✅ Las páginas privadas son Partial Prerender: un shell estático instantáneo con los datos en streaming.
- ⚠️ Next 16 cambia APIs (Promises en `params`, `proxy.ts`, reglas de Cache Components). `web/AGENTS.md` obliga a
  consultar la documentación incluida en el paquete.
- ⚠️ Al cambiar un DTO hay que regenerar `schema.d.ts`. Una mejora futura es generarlo en el CI y fallar si hay diferencias.
- ⚠️ Cookie + mismo origen: si algún día se habilita CORS para otros orígenes, habrá que agregar protección CSRF.
