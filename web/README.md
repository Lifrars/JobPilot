# JobPilot Web

Frontend de JobPilot en **Next.js 16** (App Router): tablero kanban del pipeline, detalle con asistente de IA y gestión de CVs.

## Arquitectura en 30 segundos
- **BFF (Backend for Frontend):** `src/proxy.ts` reenvía `/api/v1/*` a la API .NET. El navegador ve un solo origen, así que
  la sesión es la **cookie HttpOnly** de ASP.NET Core Identity: JavaScript nunca toca un token y no hace falta CORS.
- **Contrato tipado de punta a punta:** `npm run api:types` genera `src/lib/api/schema.d.ts` desde el OpenAPI de .NET.
  Un mismo `createClient<paths>()` sirve en el navegador (`lib/api/client.ts`) y en el servidor (`lib/api/server.ts`).
- **Server + Client Components:** la sesión se valida en el servidor (`getCurrentUser`, con `'use cache: private'`).
  El tablero es interactivo (TanStack Query + dnd-kit) con **actualización optimista** y rollback si la API rechaza la transición.
- **Cache Components / Partial Prerender:** cada página privada sirve un shell estático al instante y transmite los datos tras `<Suspense>`.

## Desarrollo
Lo más simple es levantar todo con Aspire desde la raíz del repo:
```bash
dotnet run --project src/JobPilot.AppHost
```
O solo el frontend, con la API ya corriendo en `http://localhost:5002`:
```bash
npm install
npm run dev
```

| Script | Qué hace |
|---|---|
| `npm run dev` | Servidor de desarrollo (Turbopack) |
| `npm run build` | Build de producción (`output: standalone`) |
| `npm run lint` · `npm run typecheck` | ESLint y TypeScript |
| `npm test` | Vitest + Testing Library + MSW |
| `npm run api:types` | Regenera los tipos desde `http://localhost:5002/openapi/v1.json` |

Variables: `API_HTTP` (la inyecta Aspire) o `JOBPILOT_API_URL` para apuntar a otra API.
