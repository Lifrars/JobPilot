import { NextResponse, type NextRequest } from "next/server";
import { apiBaseUrl, SESSION_COOKIE } from "@/lib/config";

const PUBLIC_PAGES = new Set(["/login", "/register"]);

/**
 * Proxy (antes "middleware") corre antes de cada request, en Node.js:
 * 1. BFF: reenvía /api/v1/* a la API .NET. El navegador ve un solo origen, así que la cookie HttpOnly
 *    de Identity funciona sin CORS. La URL se lee en runtime (sirve igual en Aspire y en Docker).
 * 2. Chequeo OPTIMISTA de sesión: solo mira si existe la cookie. La validación real la hace la API
 *    en cada request (y el layout privado con getCurrentUser).
 */
export function proxy(request: NextRequest) {
  const { pathname, search } = request.nextUrl;

  if (pathname.startsWith("/api/v1/")) {
    return NextResponse.rewrite(new URL(pathname + search, apiBaseUrl()));
  }

  const hasSession = request.cookies.has(SESSION_COOKIE);
  const isPublicPage = PUBLIC_PAGES.has(pathname);

  if (!hasSession && !isPublicPage) {
    const login = new URL("/login", request.url);
    if (pathname !== "/") login.searchParams.set("next", pathname);
    return NextResponse.redirect(login);
  }

  if (hasSession && isPublicPage) {
    return NextResponse.redirect(new URL("/", request.url));
  }

  return NextResponse.next();
}

export const config = {
  // Todo menos los assets estáticos de Next.
  matcher: ["/((?!_next/static|_next/image|favicon.ico).*)"],
};
