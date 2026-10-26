"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { z } from "zod";
import { api } from "@/lib/api/client";
import { errorMessage, unwrap } from "@/lib/api/errors";
import { queries } from "@/lib/api/queries";
import { apiErrorToFieldErrors, type FieldErrors, parseForm } from "@/lib/forms";
import { Alert, Button, Card, Chip, Field, Input, Loading, Textarea } from "@/components/ui";

const schema = z.object({
  name: z.string().trim().min(1, "Ponle un nombre (ej. Backend .NET – ES)").max(120),
  content: z.string().trim().min(50, "Pega el contenido completo de tu CV (mínimo 50 caracteres)"),
});

export function ResumesView() {
  const queryClient = useQueryClient();
  const resumes = useQuery(queries.resumes.list());
  const [errors, setErrors] = useState<FieldErrors<z.input<typeof schema>>>({});

  const create = useMutation({
    mutationFn: (body: z.output<typeof schema>) => unwrap(api.POST("/api/v1/resumes", { body })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queries.resumes.all }),
    onError: (error) => setErrors(apiErrorToFieldErrors(error)),
  });

  return (
    <div className="grid gap-6 lg:grid-cols-5">
      <Card className="lg:col-span-3">
        <h2 className="mb-4 font-semibold">Agregar versión de CV</h2>
        <form
          noValidate
          className="flex flex-col gap-4"
          onSubmit={(event) => {
            event.preventDefault();
            const form = event.currentTarget;
            const parsed = parseForm(schema, new FormData(form));
            if (!parsed.success) return setErrors(parsed.errors);
            setErrors({});
            create.mutate(parsed.data, { onSuccess: () => form.reset() });
          }}
        >
          {errors.form && <Alert>{errors.form}</Alert>}
          <Field label="Nombre" name="name" error={errors.name}>
            <Input id="name" name="name" placeholder="Backend .NET – Español" />
          </Field>
          <Field
            label="Contenido"
            name="content"
            error={errors.content}
            hint="Texto plano o Markdown. Se usa para el match y las cartas; si hay embeddings configurados, también se vectoriza."
          >
            <Textarea id="content" name="content" rows={14} />
          </Field>
          <Button type="submit" pending={create.isPending} className="self-end">
            Guardar CV
          </Button>
        </form>
      </Card>

      <Card className="lg:col-span-2">
        <h2 className="mb-4 font-semibold">Tus CVs</h2>
        {resumes.isPending ? (
          <Loading />
        ) : resumes.isError ? (
          <Alert>{errorMessage(resumes.error)}</Alert>
        ) : resumes.data.length === 0 ? (
          <p className="text-sm text-muted">Todavía no agregaste ningún CV.</p>
        ) : (
          <ul className="flex flex-col gap-2">
            {resumes.data.map((resume) => (
              <li key={resume.id} className="rounded-lg bg-background p-3">
                <p className="text-sm font-medium">{resume.name}</p>
                <div className="mt-1 flex items-center gap-2 text-xs text-muted">
                  <span>{resume.characters.toLocaleString("es")} caracteres</span>
                  {resume.hasEmbedding && <Chip tone="good">vectorizado</Chip>}
                </div>
              </li>
            ))}
          </ul>
        )}
      </Card>
    </div>
  );
}
