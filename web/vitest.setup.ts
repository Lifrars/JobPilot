import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterAll, afterEach, beforeAll } from "vitest";
import { server } from "@/test/msw-server";

// MSW intercepta fetch en Node: los componentes llaman a la "API real" y respondemos con handlers.
// MSW 3: "onUnhandledFrame" (antes onUnhandledRequest). Una request sin handler hace fallar el test.
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => {
  server.resetHandlers();
  cleanup();
});
afterAll(() => server.close());
