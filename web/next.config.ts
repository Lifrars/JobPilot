import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Imagen de contenedor mínima (la usa Aspire al publicar).
  output: "standalone",
  // Cache Components: lo estático se prerenderiza y lo que depende de la request (sesión) va en <Suspense>.
  cacheComponents: true,
  partialPrefetching: true,
  reactCompiler: true,
  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
};

export default nextConfig;
