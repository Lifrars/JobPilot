import type { Metadata } from "next";
import { RegisterForm } from "@/features/auth/auth-forms";

export const metadata: Metadata = { title: "Crear cuenta" };

export default function RegisterPage() {
  return <RegisterForm />;
}
