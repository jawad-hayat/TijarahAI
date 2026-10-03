export interface RedlinedClause {
  clauseNumber: number;
  originalClause: string;
  shariahIssue: string;
  prohibitedElement: string;
  remediedClause: string;
  scholarlyJustification: string;
  standardCited: string;
}

export interface ContractAuditResponse {
  contractType: string;
  hasViolations: boolean;
  totalViolationsFound: number;
  redlinedClauses: RedlinedClause[];
  fullyRemediedContract: string;
  executiveSummary: string;
  timestamp: string;
}

export interface ResearchSource {
  title: string;
  url: string;
  snippet: string;
}

export interface WebResearchResponse {
  query: string;
  hanafiVerdict: string;
  ahleHadithVerdict: string;
  keyConsensusAndDifferences: string;
  webSources: ResearchSource[];
  timestamp: string;
}
