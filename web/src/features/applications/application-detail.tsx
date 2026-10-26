"use client";

import { useState } from "react";
import Link from "next/link";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { z } from "zod";
import { api } from "@/lib/api/client";
import { errorMessage, unwrap } from "@/lib/api/errors";
import { queries } from "@/lib/api/queries";
import type { JobApplication } from "@/lib/api/types";
import { apiErrorToFieldErrors, type FieldErrors, optionalText, parseForm } from "@/lib/forms";
import { isStatus, statusMeta, workModeLabels } from "@/lib/status";
import { Alert, Button, Card, Chip, Field, Input, Loading, Select, StatusBadge, Textarea } from "@/components/ui";
import { useMoveApplication } from "@/features/board/use-move-application";
import { AiPanel } from "./ai-panel";

const interviewLabels = {
  Hr: "RR. HH.",
  Technical: "Técnica",
  LiveCoding: "Live coding",
  SystemDesign: "System design",
  Manager: "Manager",
  Final: "Final",
} as const;

const formatDate = (value: string) => new Date(value).toLocaleString("es", { dateStyle: "medium", timeStyle: "short" });

export function ApplicationDetail({ id }: { id: string }) {
  const { data: application, isPending, error } = useQuery(queries.applications.detail(id));
  const companies = useQuery(queries.companies.list());

  if (isPending) return <Loading />;
  if (error) return <Alert>{errorMessage(error)}</Alert>;

  const company = companies.data?.items.find((c) => c.id === application.companyId);

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <Link href="/" className="text-sm text-muted hover:text-foreground">
            ← Tablero
          </Link>
          <h1 className="mt-1 text-2xl font-semibold tracking-tight">{application.title}</h1>
          <p className="text-muted">{company?.name}</p>
        </div>
        <StatusBadge status={application.status} />
      </div>

      <StatusActions application={application} />

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="flex flex-col gap-6 lg:col-span-2">
          <Card>
            <h2 className="mb-3 font-semibold">Detalles</h2>
            <dl className="grid grid-cols-2 gap-3 text-sm">
              <dt className="text-muted">Modalidad</dt>
              <dd>{workModeLabels[application.workMode]}</dd>
              <dt className="text-muted">Salario</dt>
              <dd>
                {application.salary
                  ? `${application.salary.min.toLocaleString("es")} – ${application.salary.max.toLocaleString("es")} ${application.salary.currency}`
                  : "—"}
              </dd>
              <dt className="text-muted">Postulado</dt>
              <dd>{application.appliedAt ? formatDate(application.appliedAt) : "—"}</dd>
              <dt className="text-muted">Oferta</dt>
              <dd className="truncate">
                {application.postingUrl ? (
                  <a href={application.postingUrl} target="_blank" rel="noreferrer" className="text-accent hover:underline">
                    Ver publicación ↗
                  </a>
                ) : (
                  "—"
                )}
              </dd>
            </dl>
            {application.jobDescription && (
              <details className="mt-4">
                <summary className="cursor-pointer text-sm font-medium">Descripción de la oferta</summary>
                <p className="mt-2 whitespace-pre-wrap text-sm text-muted">{application.jobDescription}</p>
              </details>
            )}
          </Card>

          <AiPanel application={application} />
        </div>

        <div className="flex flex-col gap-6">
          <Interviews application={application} />
          <History application={application} />
        </div>
      </div>
    </div>
  );
}

function StatusActions({ application }: { application: JobApplication }) {
  const move = useMoveApplication();

  if (application.allowedTransitions.length === 0) {
    return <p className="text-sm text-muted">Esta postulación está cerrada.</p>;
  }

  return (
    <div className="flex flex-col gap-2">
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-sm text-muted">Mover a:</span>
        {application.allowedTransitions.map((status) => (
          <Button
            key={status}
            variant="secondary"
            pending={move.isPending && move.variables?.status === status}
            disabled={move.isPending}
            onClick={() => move.mutate({ id: application.id, status })}
          >
            {isStatus(status) ? statusMeta[status].label : status}
          </Button>
        ))}
      </div>
      {move.isError && <Alert>{errorMessage(move.error)}</Alert>}
    </div>
  );
}

const interviewSchema = z.object({
  type: z.enum(["Hr", "Technical", "LiveCoding", "SystemDesign", "Manager", "Final"]),
  scheduledAt: z.string().min(1, "Elige fecha y hora"),
  notes: optionalText,
});

function Interviews({ application }: { application: JobApplication }) {
  const queryClient = useQueryClient();
  const [errors, setErrors] = useState<FieldErrors<z.input<typeof interviewSchema>>>({});
  const canSchedule = ["Applied", "Screening", "Interviewing"].includes(application.status);

  const schedule = useMutation({
    mutationFn: (values: z.output<typeof interviewSchema>) =>
      unwrap(
        api.POST("/api/v1/applications/{id}/interviews", {
          params: { path: { id: application.id } },
          // datetime-local no trae zona horaria: new Date() la interpreta como hora local y toISOString la pasa a UTC.
          body: { type: values.type, scheduledAt: new Date(values.scheduledAt).toISOString(), notes: values.notes ?? null },
        }),
      ),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queries.applications.all }),
    onError: (error) => setErrors(apiErrorToFieldErrors(error)),
  });

  return (
    <Card>
      <h2 className="mb-3 font-semibold">Entrevistas</h2>
      {application.interviews.length === 0 ? (
        <p className="text-sm text-muted">Sin entrevistas todavía.</p>
      ) : (
        <ul className="flex flex-col gap-2">
          {application.interviews.map((interview) => (
            <li key={interview.id} className="rounded-lg bg-background p-3 text-sm">
              <p className="font-medium">{interviewLabels[interview.type as keyof typeof interviewLabels] ?? interview.type}</p>
              <p className="text-muted">{formatDate(interview.scheduledAt)}</p>
              {interview.notes && <p className="mt-1">{interview.notes}</p>}
            </li>
          ))}
        </ul>
      )}

      {canSchedule && (
        <form
          noValidate
          className="mt-4 flex flex-col gap-3 border-t border-border pt-4"
          onSubmit={(event) => {
            event.preventDefault();
            const form = event.currentTarget;
            const parsed = parseForm(interviewSchema, new FormData(form));
            if (!parsed.success) return setErrors(parsed.errors);
            setErrors({});
            schedule.mutate(parsed.data, { onSuccess: () => form.reset() });
          }}
        >
          {errors.form && <Alert>{errors.form}</Alert>}
          <Field label="Tipo" name="type">
            <Select id="type" name="type" defaultValue="Technical">
              {Object.entries(interviewLabels).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Fecha y hora" name="scheduledAt" error={errors.scheduledAt}>
            <Input id="scheduledAt" name="scheduledAt" type="datetime-local" />
          </Field>
          <Field label="Notas" name="notes">
            <Textarea id="notes" name="notes" rows={2} className="min-h-0" />
          </Field>
          <Button type="submit" variant="secondary" pending={schedule.isPending}>
            Agendar entrevista
          </Button>
        </form>
      )}
    </Card>
  );
}

function History({ application }: { application: JobApplication }) {
  return (
    <Card>
      <h2 className="mb-3 font-semibold">Historial</h2>
      <ol className="flex flex-col gap-3 border-l border-border pl-4 text-sm">
        <li>
          <p>Creada</p>
          <p className="text-xs text-muted">{formatDate(application.createdAt)}</p>
        </li>
        {application.statusHistory.map((change) => (
          <li key={change.changedAt}>
            <p className="flex flex-wrap items-center gap-1">
              <Chip>{isStatus(change.from) ? statusMeta[change.from].label : change.from}</Chip>→
              <Chip>{isStatus(change.to) ? statusMeta[change.to].label : change.to}</Chip>
            </p>
            <p className="mt-1 text-xs text-muted">{formatDate(change.changedAt)}</p>
          </li>
        ))}
      </ol>
    </Card>
  );
}
