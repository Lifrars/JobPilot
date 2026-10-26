"use client";

import { useState } from "react";
import Link from "next/link";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api/client";
import { errorMessage, unwrap } from "@/lib/api/errors";
import { queries } from "@/lib/api/queries";
import type { JobApplication, MatchScore } from "@/lib/api/types";
import { Alert, Button, Card, Chip, Select, Textarea } from "@/components/ui";

const STRATEGIES = [
  { value: "hybrid", label: "Híbrida (keywords + semántica)" },
  { value: "keyword", label: "Keywords" },
  { value: "embedding", label: "Semántica (embeddings)" },
] as const;

/** Las tres funciones de IA de la API: análisis estructurado, match (Strategy pattern) y carta de presentación. */
export function AiPanel({ application }: { application: JobApplication }) {
  const resumes = useQuery(queries.resumes.list());
  const [resumeId, setResumeId] = useState("");
  const selectedResume = resumeId || resumes.data?.[0]?.id || "";

  return (
    <Card className="flex flex-col gap-6">
      <div className="flex items-center justify-between">
        <h2 className="font-semibold">✨ Asistente IA</h2>
        {resumes.data && resumes.data.length > 0 && (
          <Select aria-label="CV a usar" value={selectedResume} onChange={(e) => setResumeId(e.target.value)} className="w-auto">
            {resumes.data.map((resume) => (
              <option key={resume.id} value={resume.id}>
                {resume.name}
              </option>
            ))}
          </Select>
        )}
      </div>

      <Analysis application={application} />

      {resumes.data?.length === 0 ? (
        <p className="text-sm text-muted">
          Para calcular el match y generar cartas,{" "}
          <Link href="/resumes" className="text-accent hover:underline">
            agrega tu CV
          </Link>
          .
        </p>
      ) : (
        selectedResume && (
          <>
            <Match applicationId={application.id} resumeId={selectedResume} />
            <CoverLetter applicationId={application.id} resumeId={selectedResume} />
          </>
        )
      )}
    </Card>
  );
}

function Analysis({ application }: { application: JobApplication }) {
  const queryClient = useQueryClient();
  const analysis = application.analysis;

  const analyze = useMutation({
    mutationFn: () =>
      unwrap(
        api.POST("/api/v1/applications/{id}/analyze", {
          params: { path: { id: application.id } },
          body: { jobDescription: null },
        }),
      ),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queries.applications.detail(application.id).queryKey }),
  });

  return (
    <section className="flex flex-col gap-3">
      <div className="flex items-center justify-between gap-2">
        <h3 className="text-sm font-medium">Análisis de la oferta</h3>
        <Button
          variant="secondary"
          pending={analyze.isPending}
          disabled={!application.jobDescription}
          title={application.jobDescription ? undefined : "La postulación no tiene descripción"}
          onClick={() => analyze.mutate()}
        >
          {analysis ? "Reanalizar" : "Analizar con IA"}
        </Button>
      </div>
      {analyze.isError && <Alert>{errorMessage(analyze.error)}</Alert>}

      {analysis ? (
        <div className="flex flex-col gap-3 text-sm">
          <p>{analysis.summary}</p>
          <p className="text-muted">
            Seniority: <span className="text-foreground">{analysis.seniority}</span>
          </p>
          <SkillList title="Requeridas" skills={analysis.requiredSkills} />
          <SkillList title="Deseables" skills={analysis.niceToHaveSkills} />
          {analysis.redFlags.length > 0 && <SkillList title="🚩 Señales de alerta" skills={analysis.redFlags} tone="bad" />}
        </div>
      ) : (
        <p className="text-sm text-muted">Extrae skills, seniority, salario y señales de alerta de la descripción.</p>
      )}
    </section>
  );
}

function Match({ applicationId, resumeId }: { applicationId: string; resumeId: string }) {
  const [strategy, setStrategy] = useState<string>("hybrid");

  const match = useMutation({
    mutationFn: (): Promise<MatchScore> =>
      unwrap(
        api.GET("/api/v1/applications/{id}/match", {
          params: { path: { id: applicationId }, query: { resumeId, strategy } },
        }),
      ),
  });

  return (
    <section className="flex flex-col gap-3 border-t border-border pt-5">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h3 className="text-sm font-medium">Match con tu CV</h3>
        <div className="flex gap-2">
          <Select aria-label="Estrategia" value={strategy} onChange={(e) => setStrategy(e.target.value)} className="w-auto">
            {STRATEGIES.map((s) => (
              <option key={s.value} value={s.value}>
                {s.label}
              </option>
            ))}
          </Select>
          <Button variant="secondary" pending={match.isPending} onClick={() => match.mutate()}>
            Calcular
          </Button>
        </div>
      </div>
      {match.isError && <Alert>{errorMessage(match.error)}</Alert>}
      {match.data && (
        <div className="flex flex-col gap-3 text-sm">
          <div className="flex items-center gap-3">
            <span className="text-3xl font-semibold tabular-nums">{Math.round(match.data.score)}%</span>
            <div className="h-2 flex-1 overflow-hidden rounded-full bg-background">
              <div className="h-full rounded-full bg-accent" style={{ width: `${Math.min(100, match.data.score)}%` }} />
            </div>
          </div>
          <p className="text-muted">{match.data.explanation}</p>
          <SkillList title="Tienes" skills={match.data.matchedSkills} tone="good" />
          <SkillList title="Te faltan" skills={match.data.missingSkills} tone="bad" />
        </div>
      )}
    </section>
  );
}

function CoverLetter({ applicationId, resumeId }: { applicationId: string; resumeId: string }) {
  const [language, setLanguage] = useState("es");

  const letter = useMutation({
    mutationFn: () =>
      unwrap(
        api.POST("/api/v1/applications/{id}/cover-letter", {
          params: { path: { id: applicationId } },
          body: { resumeId, language },
        }),
      ),
  });

  return (
    <section className="flex flex-col gap-3 border-t border-border pt-5">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h3 className="text-sm font-medium">Carta de presentación</h3>
        <div className="flex gap-2">
          <Select aria-label="Idioma" value={language} onChange={(e) => setLanguage(e.target.value)} className="w-auto">
            <option value="es">Español</option>
            <option value="en">English</option>
          </Select>
          <Button variant="secondary" pending={letter.isPending} onClick={() => letter.mutate()}>
            Generar
          </Button>
        </div>
      </div>
      {letter.isError && <Alert>{errorMessage(letter.error)}</Alert>}
      {letter.data && (
        <>
          <Textarea readOnly value={letter.data.content} rows={12} aria-label="Carta generada" />
          <Button variant="ghost" className="self-end" onClick={() => navigator.clipboard.writeText(letter.data.content)}>
            Copiar
          </Button>
        </>
      )}
    </section>
  );
}

function SkillList({ title, skills, tone = "neutral" }: {
  title: string;
  skills: readonly string[];
  tone?: "neutral" | "good" | "bad";
}) {
  if (skills.length === 0) return null;
  return (
    <div>
      <p className="mb-1.5 text-xs text-muted">{title}</p>
      <div className="flex flex-wrap gap-1.5">
        {skills.map((skill) => (
          <Chip key={skill} tone={tone}>
            {skill}
          </Chip>
        ))}
      </div>
    </div>
  );
}
