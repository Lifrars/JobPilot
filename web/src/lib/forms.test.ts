import { describe, expect, it } from "vitest";
import { z } from "zod";
import { ApiError } from "./api/errors";
import { apiErrorToFieldErrors, optionalNumber, optionalText, parseForm } from "./forms";

const schema = z.object({
  title: z.string().min(1, "Obligatorio"),
  url: optionalText,
  salary: optionalNumber,
});

const formData = (values: Record<string, string>) => {
  const data = new FormData();
  Object.entries(values).forEach(([key, value]) => data.set(key, value));
  return data;
};

describe("parseForm", () => {
  it("devuelve datos tipados y convierte vacíos en undefined", () => {
    const result = parseForm(schema, formData({ title: "Dev", url: "  ", salary: "1500" }));

    expect(result).toEqual({ success: true, data: { title: "Dev", url: undefined, salary: 1500 } });
  });

  it("devuelve un mensaje por campo inválido", () => {
    const result = parseForm(schema, formData({ title: "", salary: "abc" }));

    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.errors.title).toBe("Obligatorio");
      expect(result.errors.salary).toBe("Debe ser un número");
    }
  });
});

describe("apiErrorToFieldErrors", () => {
  it("mapea la validación de .NET (PascalCase) a campos del formulario (camelCase)", () => {
    const error = new ApiError(400, { errors: { Title: ["The job title is required."] } });

    expect(apiErrorToFieldErrors(error)).toEqual({ title: "The job title is required." });
  });

  it("usa el detalle como error general cuando no hay errores por campo", () => {
    const error = new ApiError(404, { title: "Company.NotFound", detail: "The company was not found." });

    expect(apiErrorToFieldErrors(error)).toEqual({ form: "The company was not found." });
  });
});
