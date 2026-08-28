export type FindingSource = "StaticAnalysis" | "Llm";
export type FindingCategory = "RequirementGap" | "LogicGap" | "CodeQuality" | "Security";
export type FindingSeverity = "Minor" | "Major" | "Critical";
export type TicketSource = "None" | "GitHubIssue" | "ManualOverride";

export interface RequirementCoverageItem {
  requirement: string;
  covered: boolean;
  evidence?: string | null;
}

export interface ReviewFinding {
  source: FindingSource;
  category: FindingCategory;
  severity: FindingSeverity;
  message: string;
  filePath?: string | null;
  line?: number | null;
  suggestion?: string | null;
}

export interface ReviewReport {
  owner: string;
  repository: string;
  pullRequestNumber: number;
  headSha: string;
  generatedAtUtc: string;
  summary: string;
  requirementCoverage: RequirementCoverageItem[];
  findings: ReviewFinding[];
  /** Computed server-side from findings; present in every API response. */
  criticalCount: number;
  majorCount: number;
  minorCount: number;
  uncoveredRequirementCount: number;
  ticketSource: TicketSource;
  ticketUrl?: string | null;
  modelName?: string | null;
}

/** Body of POST /api/reviews/run. Supply pullRequestUrl, or the three parts. */
export interface RunReviewRequest {
  pullRequestUrl?: string;
  owner?: string;
  repository?: string;
  pullRequestNumber?: number;
  /** Requirements typed by hand, replacing whatever the PR links to. */
  ticketDescription?: string;
  /** Where those requirements came from, e.g. a Jira URL. Provenance only. */
  ticketUrl?: string;
}
