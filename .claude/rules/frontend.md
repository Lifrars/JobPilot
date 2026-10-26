---
paths:
  - "web/**"
---

# Estándares del frontend (Next.js 16)

- **Antes de usar una API de Next, leer la guía en `web/node_modules/next/dist/docs/`** (ver `web/AGENTS.md`). Next 16 cambió:
  `middleware` → `proxy.ts`, `params`/`searchParams` son Promises, Cache Components activado, React Compiler activado.
- Estructura: `src/app` (rutas delgadas: metadata + `<Suspense>` + componente de feature), `src/features/<feature>` (UI y hooks),
  `src/lib` (API, forms, dominio del cliente), `src/components/ui.tsx` (primitivas).
- **Datos:**
  - Navegador: `api` de `@/lib/api/client` + TanStack Query con las `queryOptions` de `@/lib/api/queries` (no repetir query keys).
  - Servidor: `serverApi()` de `@/lib/api/server` (reenvía la cookie). Solo en Server Components/Actions (`server-only`).
  - Siempre `unwrap(...)`: convierte la respuesta de openapi-fetch en datos o `ApiError`.
  - Tipos solo desde `@/lib/api/types` (`Schema<"...">`), generados del OpenAPI. Nunca escribir a mano un DTO del backend.
- **Cache Components:** todo lo que lea la request (sesión, cookies, params) o use TanStack Query va dentro de `<Suspense>`.
  La sesión se lee con `getCurrentUser()` (`'use cache: private'`), nunca en el nivel superior de un layout.
- **Auth:** la sesión es la cookie HttpOnly de Identity (`login?useCookies=true`). Nunca guardar tokens en localStorage.
  `proxy.ts` hace solo un chequeo optimista; la autorización real es la API.
- **Formularios:** `<form onSubmit>` + `parseForm(schema, FormData)` (zod) + `useMutation`; errores del backend con `apiErrorToFieldErrors`.
  Campos accesibles con `<Field>` (label + `aria-invalid` + mensaje con `role="alert"`).
- **Mutaciones:** invalidar por prefijo (`queries.applications.all`). Para UX instantánea, optimistic update con rollback (ver `use-move-application.ts`).
- Las reglas de negocio las decide el backend (ej. `allowedTransitions`); el frontend solo las refleja.
- Texto de UI en español; código en inglés. Tailwind con los tokens de `globals.css` (`bg-surface`, `text-muted`, `border-border`, `bg-accent`).
- **Tests:** Vitest + Testing Library + MSW (`server.use(http.get(apiUrl(...)))`). Consultar por rol/label, no por clases.
  `renderWithClient` de `@/test/render`. Mockear `next/navigation` cuando el componente navega.
- Verificación: `npm run lint && npm run typecheck && npm test && npm run build`.
