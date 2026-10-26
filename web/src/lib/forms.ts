import { z } from "zod";
import { ApiError } from "@/lib/api/errors";

/** Un mensaje por campo del schema, más un error general del formulario. */
export type FieldErrors<TValues> = Partial<Record<Extract<keyof TValues, string>, string>> & { form?: string };

export type ParseResult<TSchema extends z.ZodType> =
  | { success: true; data: z.output<TSchema> }
  | { success: false; errors: FieldErrors<z.input<TSchema>> };

/**
 * Valida un FormData con un schema de zod y devuelve datos TIPADOS o errores por campo.
 * El tipo de `data` sale del schema: una sola fuente de verdad para validación y tipos.
 */
export function parseForm<TSchema extends z.ZodType>(schema: TSchema, formData: FormData): ParseResult<TSchema> {
  const result = schema.safeParse(Object.fromEntries(formData));
  if (result.success) {
    return { success: true, data: result.data };
  }

  const errors: Record<string, string> = {};
  for (const issue of result.error.issues) {
    const field = String(issue.path[0] ?? "form");
    errors[field] ??= issue.message;
  }
  return { success: false, errors: errors as FieldErrors<z.input<TSchema>> };
}

/** Convierte un error de la API en errores de formulario: validación por campo o mensaje general. */
export function apiErrorToFieldErrors<TValues>(error: unknown): FieldErrors<TValues> {
  if (error instanceof ApiError) {
    const fields = error.fieldErrors;
    return Object.keys(fields).length > 0
      ? (fields as FieldErrors<TValues>)
      : ({ form: error.message } as FieldErrors<TValues>);
  }
  return { form: "No se pudo completar la operación." } as FieldErrors<TValues>;
}

/** Campo de texto opcional: "" del formulario se convierte en undefined. */
export const optionalText = z
  .string()
  .trim()
  .transform((value) => (value === "" ? undefined : value))
  .optional();

/** Número opcional desde un input: "" → undefined, "1200" → 1200. */
export const optionalNumber = z.preprocess(
  (value) => (value === "" || value === undefined ? undefined : Number(value)),
  z.number({ error: "Debe ser un número" }).nonnegative("Debe ser positivo").optional(),
);
