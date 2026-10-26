import { describe, expect, it } from "vitest";
import { ApiError, errorMessage, unwrap } from "./errors";

describe("unwrap", () => {
  it("devuelve los datos cuando la respuesta es exitosa", async () => {
    const data = await unwrap(Promise.resolve({ data: { id: "1" }, response: new Response(null, { status: 200 }) }));

    expect(data).toEqual({ id: "1" });
  });

  it("lanza ApiError con el ProblemDetails cuando falla", async () => {
    const request = Promise.resolve({
      error: { title: "JobApplication.NotFound", detail: "Not found", errorCode: "JobApplication.NotFound" },
      response: new Response(null, { status: 404 }),
    });

    await expect(unwrap(request)).rejects.toMatchObject({ status: 404, code: "JobApplication.NotFound" });
  });
});

describe("errorMessage", () => {
  it("traduce los códigos conocidos del backend", () => {
    const error = new ApiError(503, { errorCode: "AI.ChatNotConfigured", detail: "No chat model is configured." });

    expect(errorMessage(error)).toContain("La IA no está activada");
  });

  it("usa el detalle del backend para códigos desconocidos", () => {
    expect(errorMessage(new ApiError(409, { errorCode: "X.Y", detail: "Algo chocó" }))).toBe("Algo chocó");
  });
});
