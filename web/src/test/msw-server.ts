import { setupServer } from "msw/node";

/** Sin handlers por defecto: cada test declara qué responde la API (`server.use(...)`). */
export const server = setupServer();

/** Los tests corren en jsdom con origin http://localhost:3000; el cliente usa window.location.origin. */
export const apiUrl = (path: string) => new URL(path, window.location.origin).toString();
