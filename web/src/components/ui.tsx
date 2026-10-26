import type { ComponentProps, ReactNode } from "react";
import { clsx } from "clsx";
import { isStatus, statusMeta } from "@/lib/status";

const buttonVariants = {
  primary: "bg-accent text-accent-foreground hover:opacity-90",
  secondary: "border border-border bg-surface hover:bg-background",
  ghost: "hover:bg-background",
  danger: "bg-danger text-white hover:opacity-90",
} as const;

type ButtonProps = ComponentProps<"button"> & {
  variant?: keyof typeof buttonVariants;
  pending?: boolean;
};

/** Clases del botón reutilizables en un <Link> (un link que parece botón, sin anidar <a><button>). */
export function buttonStyles(variant: keyof typeof buttonVariants = "primary", className?: string) {
  return clsx(
    "inline-flex items-center justify-center gap-2 rounded-lg px-3.5 py-2 text-sm font-medium transition",
    "disabled:cursor-not-allowed disabled:opacity-60 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent",
    buttonVariants[variant],
    className,
  );
}

export function Button({ variant = "primary", pending, disabled, className, children, ...props }: ButtonProps) {
  return (
    <button
      {...props}
      disabled={disabled || pending}
      className={buttonStyles(variant, className)}
    >
      {pending && <Spinner />}
      {children}
    </button>
  );
}

const controlClass =
  "w-full rounded-lg border border-border bg-surface px-3 py-2 text-sm outline-none transition focus:border-accent focus:ring-2 focus:ring-accent/20 aria-invalid:border-danger";

export function Input(props: ComponentProps<"input">) {
  return <input {...props} className={clsx(controlClass, props.className)} />;
}

export function Textarea(props: ComponentProps<"textarea">) {
  return <textarea {...props} className={clsx(controlClass, "min-h-28", props.className)} />;
}

export function Select(props: ComponentProps<"select">) {
  return <select {...props} className={clsx(controlClass, props.className)} />;
}

/** Etiqueta + control + mensaje de error accesible (aria-describedby). */
export function Field({ label, name, error, hint, children }: {
  label: string;
  name: string;
  error?: string;
  hint?: string;
  children: ReactNode;
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={name} className="text-sm font-medium">
        {label}
      </label>
      {children}
      {error ? (
        <p id={`${name}-error`} role="alert" className="text-xs text-danger">
          {error}
        </p>
      ) : (
        hint && <p className="text-xs text-muted">{hint}</p>
      )}
    </div>
  );
}

export function Card({ className, ...props }: ComponentProps<"section">) {
  return <section {...props} className={clsx("rounded-xl border border-border bg-surface p-5", className)} />;
}

export function StatusBadge({ status }: { status: string }) {
  const meta = isStatus(status) ? statusMeta[status] : { label: status, dot: "bg-zinc-400" };
  return (
    <span className="inline-flex items-center gap-1.5 rounded-full border border-border px-2.5 py-0.5 text-xs font-medium">
      <span className={clsx("size-2 rounded-full", meta.dot)} />
      {meta.label}
    </span>
  );
}

export function Chip({ children, tone = "neutral" }: { children: ReactNode; tone?: "neutral" | "good" | "bad" }) {
  return (
    <span
      className={clsx(
        "rounded-md px-2 py-0.5 text-xs",
        tone === "neutral" && "bg-background",
        tone === "good" && "bg-emerald-500/15 text-emerald-700 dark:text-emerald-300",
        tone === "bad" && "bg-rose-500/15 text-rose-700 dark:text-rose-300",
      )}
    >
      {children}
    </span>
  );
}

export function Alert({ children }: { children: ReactNode }) {
  return (
    <div role="alert" className="rounded-lg border border-danger/30 bg-danger/10 px-3 py-2 text-sm text-danger">
      {children}
    </div>
  );
}

export function Spinner({ className }: { className?: string }) {
  return (
    <span
      aria-hidden
      className={clsx("inline-block size-4 animate-spin rounded-full border-2 border-current border-t-transparent", className)}
    />
  );
}

export function Loading({ label = "Cargando…" }: { label?: string }) {
  return (
    <div className="flex items-center gap-2 p-6 text-sm text-muted">
      <Spinner /> {label}
    </div>
  );
}
