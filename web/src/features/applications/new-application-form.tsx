"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { z } from "zod";
import { api } from "@/lib/api/client";
import { unwrap } from "@/lib/api/errors";
import { queries } from "@/lib/api/queries";
import { apiErrorToFieldErrors, type FieldErrors, optionalNumber, optionalText, parseForm } from "@/lib/forms";
import { Alert, Button, Card, Field, Input, Select, Textarea } from "@/components/ui";

const NEW_COMPANY = "__new__";

const schema = z
  .object({
    companyId: z.string().min(1, "Elige o crea una empresa"),
    newCompanyName: optionalText,
    title: z.string().trim().min(1, "El puesto es obligatorio").max(200),
    postingUrl: optionalText.pipe(z.url("Debe ser una URL completa (https://…)").optional()),
    jobDescription: optionalText,
    workMode: z.enum(["Unknown", "Remote", "Hybrid", "OnSite"]),
    salaryMin: optionalNumber,
    salaryMax: optionalNumber,
    currency: optionalText,
  })
  .refine((v) => v.companyId !== NEW_COMPANY || !!v.newCompanyName, {
    path: ["newCompanyName"],
    error: "Escribe el nombre de la nueva empresa",
  })
  .refine((v) => v.salaryMin === undefined || v.salaryMax === undefined || v.salaryMax >= v.salaryMin, {
    path: ["salaryMax"],
    error: "Debe ser mayor o igual al mínimo",
  })
  .refine((v) => (v.salaryMin === undefined && v.salaryMax === undefined) || v.currency?.length === 3, {
    path: ["currency"],
    error: "Código ISO de 3 letras (USD, EUR, CLP…)",
  });

type Values = z.output<typeof schema>;

export function NewApplicationForm() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const companies = useQuery(queries.companies.list());
  const [companyId, setCompanyId] = useState("");
  const [errors, setErrors] = useState<FieldErrors<z.input<typeof schema>>>({});

  const create = useMutation({
    mutationFn: async (values: Values) => {
      // Si eligió "nueva empresa", la creamos primero y usamos su ID.
      const company =
        values.companyId === NEW_COMPANY
          ? await unwrap(api.POST("/api/v1/companies", { body: { name: values.newCompanyName!, website: null, industry: null } }))
          : { id: values.companyId };

      return unwrap(
        api.POST("/api/v1/applications", {
          body: {
            companyId: company.id,
            title: values.title,
            postingUrl: values.postingUrl ?? null,
            jobDescription: values.jobDescription ?? null,
            salaryMin: values.salaryMin ?? null,
            salaryMax: values.salaryMax ?? null,
            currency: values.currency?.toUpperCase() ?? null,
            workMode: values.workMode,
          },
        }),
      );
    },
    onSuccess: async (created) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queries.applications.all }),
        queryClient.invalidateQueries({ queryKey: queries.companies.all }),
      ]);
      router.push(`/applications/${created.id}`);
    },
    onError: (error) => setErrors(apiErrorToFieldErrors(error)),
  });

  return (
    <Card>
      <form
        noValidate
        className="grid gap-5 sm:grid-cols-2"
        onSubmit={(event) => {
          event.preventDefault();
          const parsed = parseForm(schema, new FormData(event.currentTarget));
          if (!parsed.success) return setErrors(parsed.errors);
          setErrors({});
          create.mutate(parsed.data);
        }}
      >
        {errors.form && (
          <div className="sm:col-span-2">
            <Alert>{errors.form}</Alert>
          </div>
        )}

        <Field label="Empresa" name="companyId" error={errors.companyId}>
          <Select id="companyId" name="companyId" value={companyId} onChange={(e) => setCompanyId(e.target.value)}>
            <option value="">Selecciona…</option>
            {companies.data?.items.map((company) => (
              <option key={company.id} value={company.id}>
                {company.name}
              </option>
            ))}
            <option value={NEW_COMPANY}>+ Nueva empresa</option>
          </Select>
        </Field>

        {companyId === NEW_COMPANY ? (
          <Field label="Nombre de la nueva empresa" name="newCompanyName" error={errors.newCompanyName}>
            <Input id="newCompanyName" name="newCompanyName" autoFocus />
          </Field>
        ) : (
          <div className="hidden sm:block" />
        )}

        <Field label="Puesto" name="title" error={errors.title}>
          <Input id="title" name="title" placeholder="Backend Developer .NET" aria-invalid={!!errors.title} />
        </Field>

        <Field label="Modalidad" name="workMode">
          <Select id="workMode" name="workMode" defaultValue="Unknown">
            <option value="Unknown">Sin especificar</option>
            <option value="Remote">Remoto</option>
            <option value="Hybrid">Híbrido</option>
            <option value="OnSite">Presencial</option>
          </Select>
        </Field>

        <div className="sm:col-span-2">
          <Field label="URL de la oferta" name="postingUrl" error={errors.postingUrl}>
            <Input id="postingUrl" name="postingUrl" type="url" placeholder="https://…" />
          </Field>
        </div>

        <div className="sm:col-span-2">
          <Field label="Descripción de la oferta" name="jobDescription" hint="Pégala completa: la IA la analiza y se usa para el match con tu CV.">
            <Textarea id="jobDescription" name="jobDescription" rows={8} />
          </Field>
        </div>

        <div className="grid grid-cols-3 gap-3 sm:col-span-2">
          <Field label="Salario mín." name="salaryMin" error={errors.salaryMin}>
            <Input id="salaryMin" name="salaryMin" inputMode="numeric" />
          </Field>
          <Field label="Salario máx." name="salaryMax" error={errors.salaryMax}>
            <Input id="salaryMax" name="salaryMax" inputMode="numeric" />
          </Field>
          <Field label="Moneda" name="currency" error={errors.currency}>
            <Input id="currency" name="currency" maxLength={3} placeholder="USD" />
          </Field>
        </div>

        <div className="flex justify-end gap-2 sm:col-span-2">
          <Button type="button" variant="secondary" onClick={() => router.back()}>
            Cancelar
          </Button>
          <Button type="submit" pending={create.isPending}>
            Guardar postulación
          </Button>
        </div>
      </form>
    </Card>
  );
}
