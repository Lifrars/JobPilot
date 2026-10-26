/** RFC 9457: así responde la API .NET cualquier error (ApiResults.Problem). */
export type ProblemDetails = {
  title?: string | null;
  detail?: string | null;
  status?: number | null;
  errorCode?: string;
  errors?: Record<string, string[]>;
};

/** Error tipado de la API: conserva el status y el ProblemDetails para mostrar mensajes útiles. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly problem: ProblemDetails,
  ) {
    super(problem.detail ?? problem.title ?? `Request failed with status ${status}`);
    this.name = "ApiError";
  }

  get code(): string | undefined {
    return this.problem.errorCode ?? this.problem.title ?? undefined;
  }

  /** Errores de validación del backend con la clave en camelCase ("Title" → "title"), listos para el formulario. */
  get fieldErrors(): Record<string, string> {
    return Object.fromEntries(
      Object.entries(this.problem.errors ?? {}).map(([key, messages]) => [
        key.charAt(0).toLowerCase() + key.slice(1),
        messages[0] ?? "",
      ]),
    );
  }

  static from(status: number, body: unknown): ApiError {
    const problem = body && typeof body === "object" ? (body as ProblemDetails) : {};
    return new ApiError(status, problem);
  }
}

type ApiResult<TData> = { data?: TData; error?: unknown; response: Response };

/**
 * openapi-fetch devuelve `{ data, error, response }` en vez de lanzar.
 * `unwrap` lo convierte en "devuelve los datos o lanza ApiError", que es lo que espera TanStack Query.
 * El genérico TData se infiere del endpoint: `unwrap(api.GET("/api/v1/resumes"))` ya es `Resume[]`.
 */
export async function unwrap<TData>(request: Promise<ApiResult<TData>>): Promise<TData> {
  const { data, error, response } = await request;
  if (!response.ok) {
    throw ApiError.from(response.status, error);
  }
  return data as TData;
}

/** Mensajes en español para los códigos de error conocidos del backend (`errorCode` del ProblemDetails). */
const friendlyMessages: Record<string, string> = {
  "AI.ChatNotConfigured": "La IA no está activada en el servidor. Configura AI:Chat:Provider (Ollama o Claude) para usar esta función.",
  "AI.EmbeddingsNotConfigured": "Los embeddings no están activados. Configura AI:Embeddings:Provider para la búsqueda semántica.",
  "AI.ProviderUnavailable": "No se pudo contactar al proveedor de IA. Revisa la URL, la API key y el modelo.",
  "AI.InvalidResponse": "La IA devolvió una respuesta inválida. Inténtalo de nuevo.",
  "Match.NoSkillsFound": "No se detectaron skills en la oferta. Analízala con IA o agrega una descripción más completa.",
  "Match.EmbeddingsMissing": "Faltan embeddings para el match semántico. Usa la estrategia de keywords.",
  "JobApplication.InvalidTransition": "Ese cambio de estado no está permitido.",
};

/** Mensaje amigable para el usuario a partir de cualquier error. */
export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    const friendly = error.code ? friendlyMessages[error.code] : undefined;
    if (friendly) return friendly;
    if (error.status === 429) {
      return "Demasiadas solicitudes de IA. Espera un minuto e inténtalo de nuevo.";
    }
    return error.message;
  }
  return error instanceof Error ? error.message : "Ocurrió un error inesperado.";
}
