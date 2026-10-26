"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { z } from "zod";
import { api } from "@/lib/api/client";
import { ApiError, unwrap } from "@/lib/api/errors";
import { type FieldErrors, parseForm } from "@/lib/forms";
import { Alert, Button, Field, Input } from "@/components/ui";

const loginSchema = z.object({
  email: z.email("Ingresa un email válido"),
  password: z.string().min(1, "Ingresa tu contraseña"),
});

const registerSchema = z
  .object({
    email: z.email("Ingresa un email válido"),
    password: z
      .string()
      .min(8, "Mínimo 8 caracteres")
      .regex(/[A-Z]/, "Incluye al menos una mayúscula")
      .regex(/[0-9]/, "Incluye al menos un número")
      .regex(/[^A-Za-z0-9]/, "Incluye al menos un símbolo"),
    confirmPassword: z.string(),
  })
  .refine((values) => values.password === values.confirmPassword, {
    path: ["confirmPassword"],
    error: "Las contraseñas no coinciden",
  });

type Credentials = { email: string; password: string };

/** Solo rutas internas: evita "open redirect" (?next=https://sitio-malicioso.com). */
function safeNext(next: string | null): string {
  return next && next.startsWith("/") && !next.startsWith("//") ? next : "/";
}

/** Login con cookie HttpOnly: Identity responde Set-Cookie y el navegador la guarda; JS nunca ve el token. */
async function login({ email, password }: Credentials) {
  await unwrap(api.POST("/api/v1/auth/login", { params: { query: { useCookies: true } }, body: { email, password } }));
}

function useSignIn() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const queryClient = useQueryClient();

  return () => {
    queryClient.clear();
    router.replace(safeNext(searchParams.get("next")));
    router.refresh();
  };
}

export function LoginForm() {
  const [errors, setErrors] = useState<FieldErrors<z.input<typeof loginSchema>>>({});
  const signIn = useSignIn();

  const mutation = useMutation({
    mutationFn: login,
    onSuccess: signIn,
    onError: (error) =>
      setErrors({
        form: error instanceof ApiError && error.status === 401 ? "Email o contraseña incorrectos." : "No se pudo iniciar sesión.",
      }),
  });

  return (
    <form
      noValidate
      className="flex flex-col gap-4"
      onSubmit={(event) => {
        event.preventDefault();
        const parsed = parseForm(loginSchema, new FormData(event.currentTarget));
        if (!parsed.success) return setErrors(parsed.errors);
        setErrors({});
        mutation.mutate(parsed.data);
      }}
    >
      {errors.form && <Alert>{errors.form}</Alert>}
      <Field label="Email" name="email" error={errors.email}>
        <Input id="email" name="email" type="email" autoComplete="email" aria-invalid={!!errors.email} />
      </Field>
      <Field label="Contraseña" name="password" error={errors.password}>
        <Input id="password" name="password" type="password" autoComplete="current-password" aria-invalid={!!errors.password} />
      </Field>
      <Button type="submit" pending={mutation.isPending}>
        Iniciar sesión
      </Button>
      <p className="text-center text-sm text-muted">
        ¿No tienes cuenta?{" "}
        <Link href="/register" className="font-medium text-accent hover:underline">
          Regístrate
        </Link>
      </p>
    </form>
  );
}

export function RegisterForm() {
  const [errors, setErrors] = useState<FieldErrors<z.input<typeof registerSchema>>>({});
  const signIn = useSignIn();

  const mutation = useMutation({
    mutationFn: async (credentials: Credentials) => {
      await unwrap(api.POST("/api/v1/auth/register", { body: credentials }));
      await login(credentials);
    },
    onSuccess: signIn,
    onError: (error) => {
      // Identity devuelve errores como { DuplicateUserName: [...], PasswordRequiresUpper: [...] }.
      const messages = error instanceof ApiError ? Object.values(error.problem.errors ?? {}).flat() : [];
      setErrors({ form: messages.length > 0 ? messages.join(" ") : "No se pudo crear la cuenta." });
    },
  });

  return (
    <form
      noValidate
      className="flex flex-col gap-4"
      onSubmit={(event) => {
        event.preventDefault();
        const parsed = parseForm(registerSchema, new FormData(event.currentTarget));
        if (!parsed.success) return setErrors(parsed.errors);
        setErrors({});
        mutation.mutate({ email: parsed.data.email, password: parsed.data.password });
      }}
    >
      {errors.form && <Alert>{errors.form}</Alert>}
      <Field label="Email" name="email" error={errors.email}>
        <Input id="email" name="email" type="email" autoComplete="email" aria-invalid={!!errors.email} />
      </Field>
      <Field label="Contraseña" name="password" error={errors.password} hint="Mínimo 8 caracteres, con mayúscula, número y símbolo.">
        <Input id="password" name="password" type="password" autoComplete="new-password" aria-invalid={!!errors.password} />
      </Field>
      <Field label="Confirmar contraseña" name="confirmPassword" error={errors.confirmPassword}>
        <Input id="confirmPassword" name="confirmPassword" type="password" autoComplete="new-password" aria-invalid={!!errors.confirmPassword} />
      </Field>
      <Button type="submit" pending={mutation.isPending}>
        Crear cuenta
      </Button>
      <p className="text-center text-sm text-muted">
        ¿Ya tienes cuenta?{" "}
        <Link href="/login" className="font-medium text-accent hover:underline">
          Inicia sesión
        </Link>
      </p>
    </form>
  );
}
