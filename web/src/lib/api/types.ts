import type { components } from "./schema";

/**
 * Atajo genérico a los schemas del OpenAPI: `Schema<"JobApplicationSummary">`.
 * Si el backend cambia un DTO y regeneras los tipos (`npm run api:types`), TypeScript marca cada uso roto.
 */
export type Schema<TName extends keyof components["schemas"]> = components["schemas"][TName];

export type JobApplicationSummary = Schema<"JobApplicationSummary">;
export type JobApplication = Schema<"JobApplicationResponse">;
export type PipelineStats = Schema<"PipelineStatsResponse">;
export type Company = Schema<"CompanyResponse">;
export type Resume = Schema<"ResumeResponse">;
export type JobPostingAnalysis = Schema<"JobPostingAnalysis">;
export type MatchScore = Schema<"MatchScore">;
export type WorkMode = Schema<"WorkMode">;
export type InterviewType = Schema<"InterviewType">;
export type PagedList<TItem> = {
  items: TItem[];
  page: number;
  pageSize: number;
  totalCount: number;
};
