import { describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { apiUrl, server } from "@/test/msw-server";
import { renderWithClient } from "@/test/render";
import { LoginForm } from "./auth-forms";

const router = { replace: vi.fn(), refresh: vi.fn() };
let searchParams = new URLSearchParams();

vi.mock("next/navigation", () => ({
  useRouter: () => router,
  useSearchParams: () => searchParams,
}));

describe("LoginForm", () => {
  it("valida en el cliente sin llamar a la API", async () => {
    renderWithClient(<LoginForm />);

    await userEvent.click(screen.getByRole("button", { name: "Iniciar sesión" }));

    expect(await screen.findByText("Ingresa un email válido")).toBeInTheDocument();
    expect(screen.getByText("Ingresa tu contraseña")).toBeInTheDocument();
  });

  it("inicia sesión con cookie y redirige a la ruta interna pedida", async () => {
    searchParams = new URLSearchParams({ next: "/resumes" });
    let requestUrl: URL | undefined;
    server.use(
      http.post(apiUrl("/api/v1/auth/login"), ({ request }) => {
        requestUrl = new URL(request.url);
        return new HttpResponse(null, { status: 200 });
      }),
    );
    renderWithClient(<LoginForm />);

    await userEvent.type(screen.getByLabelText("Email"), "ana@jobpilot.dev");
    await userEvent.type(screen.getByLabelText("Contraseña"), "Secret-123");
    await userEvent.click(screen.getByRole("button", { name: "Iniciar sesión" }));

    await vi.waitFor(() => expect(router.replace).toHaveBeenCalledWith("/resumes"));
    expect(requestUrl?.searchParams.get("useCookies")).toBe("true");
  });

  it("ignora un ?next externo (open redirect)", async () => {
    searchParams = new URLSearchParams({ next: "//evil.example.com" });
    server.use(http.post(apiUrl("/api/v1/auth/login"), () => new HttpResponse(null, { status: 200 })));
    renderWithClient(<LoginForm />);

    await userEvent.type(screen.getByLabelText("Email"), "ana@jobpilot.dev");
    await userEvent.type(screen.getByLabelText("Contraseña"), "Secret-123");
    await userEvent.click(screen.getByRole("button", { name: "Iniciar sesión" }));

    await vi.waitFor(() => expect(router.replace).toHaveBeenLastCalledWith("/"));
  });

  it("muestra un mensaje claro con credenciales incorrectas", async () => {
    server.use(
      http.post(apiUrl("/api/v1/auth/login"), () =>
        HttpResponse.json({ title: "Unauthorized", status: 401 }, { status: 401 }),
      ),
    );
    renderWithClient(<LoginForm />);

    await userEvent.type(screen.getByLabelText("Email"), "ana@jobpilot.dev");
    await userEvent.type(screen.getByLabelText("Contraseña"), "wrong");
    await userEvent.click(screen.getByRole("button", { name: "Iniciar sesión" }));

    expect(await screen.findByText("Email o contraseña incorrectos.")).toBeInTheDocument();
  });
});
