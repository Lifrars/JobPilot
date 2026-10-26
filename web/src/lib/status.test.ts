import { describe, expect, it } from "vitest";
import { canMoveTo, groupBy, isStatus, STATUSES } from "./status";

describe("groupBy", () => {
  it("agrupa por clave e incluye las claves vacías pedidas", () => {
    const items = [
      { id: 1, status: "Applied" },
      { id: 2, status: "Offer" },
      { id: 3, status: "Applied" },
    ];

    const groups = groupBy(items, (item) => item.status, ["Wishlist", "Applied", "Offer"]);

    expect(groups.Applied.map((i) => i.id)).toEqual([1, 3]);
    expect(groups.Offer).toHaveLength(1);
    expect(groups.Wishlist).toEqual([]);
  });
});

describe("canMoveTo", () => {
  const applied = { status: "Applied", allowedTransitions: ["Screening", "Interviewing", "Rejected", "Withdrawn"] };

  it("permite solo las transiciones que informa el backend", () => {
    expect(canMoveTo(applied, "Interviewing")).toBe(true);
    expect(canMoveTo(applied, "Offer")).toBe(false);
  });

  it("no permite soltar en la misma columna", () => {
    expect(canMoveTo({ status: "Applied", allowedTransitions: ["Applied"] }, "Applied")).toBe(false);
  });
});

describe("isStatus", () => {
  it("reconoce los 8 estados del dominio", () => {
    expect(STATUSES.every(isStatus)).toBe(true);
    expect(isStatus("Hired")).toBe(false);
  });
});
