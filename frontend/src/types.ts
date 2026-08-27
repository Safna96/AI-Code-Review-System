export type FindingSource = "StaticAnalysis" | "Llm";
export type FindingCategory = "RequirementGap" | "LogicGap" | "CodeQuality" | "Security";
export type FindingSeverity = "Minor" | "Major" | "Critical";

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
}
