import { describe, expect, it } from "vitest";
import { screen, within } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import type { JobApplicationSummary } from "@/lib/api/types";
import { apiUrl, server } from "@/test/msw-server";
import { renderWithClient } from "@/test/render";
import { BoardView } from "./board-view";

const company = { id: "c1", name: "Contoso", website: null, industry: null, createdAt: "2026-10-01T00:00:00Z" };

const summary = (overrides: Partial<JobApplicationSummary>): JobApplicationSummary => ({
  id: crypto.randomUUID(),
  companyId: company.id,
  title: "Backend Developer",
  status: "Wishlist",
  allowedTransitions: ["Applied", "Withdrawn"],
  workMode: "Remote",
  createdAt: "2026-10-20T10:00:00Z",
  updatedAt: "2026-10-25T10:00:00Z",
  ...overrides,
});

function mockApi(items: JobApplicationSummary[]) {
  server.use(
    http.get(apiUrl("/api/v1/applications"), () =>
      HttpResponse.json({ items, page: 1, pageSize: 100, totalCount: items.length }),
    ),
    http.get(apiUrl("/api/v1/companies"), () =>
      HttpResponse.json({ items: [company], page: 1, pageSize: 100, totalCount: 1 }),
    ),
  );
}

describe("BoardView", () => {
  it("ubica cada postulación en la columna de su estado, con el nombre de la empresa", async () => {
    mockApi([
      summary({ title: "Platform Engineer", status: "Interviewing", allowedTransitions: ["Offer", "Rejected", "Withdrawn"] }),
      summary({ title: "Data Engineer", status: "Applied" }),
    ]);

    renderWithClient(<BoardView />);

    const interviewing = await screen.findByRole("region", { name: "Entrevistas" });
    expect(within(interviewing).getByText("Platform Engineer")).toBeInTheDocument();
    expect(within(interviewing).getByText("Contoso")).toBeInTheDocument();
    expect(within(screen.getByRole("region", { name: "Postulado" })).getByText("Data Engineer")).toBeInTheDocument();
    expect(within(screen.getByRole("region", { name: "Oferta" })).queryAllByRole("listitem")).toHaveLength(0);
  });

  it("muestra un estado vacío con acceso a crear la primera postulación", async () => {
    mockApi([]);

    renderWithClient(<BoardView />);

    expect(await screen.findByText("Todavía no tienes postulaciones.")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Nueva postulación" })).toHaveAttribute("href", "/applications/new");
  });
});
