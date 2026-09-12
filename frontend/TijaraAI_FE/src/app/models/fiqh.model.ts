export interface FiqhPerspectiveResult {
  perspective: string; // "Hanafi" or "Ahl-e-Hadith"
  ruling: string;
  reasoning: string;
  permissibleConditions: string[];
  prohibitions: string[];
  citations: string[];
}

export interface FiqhQueryRequest {
  question: string;
}

export interface FiqhQueryResponse {
  question: string;
  hanafiPerspective: FiqhPerspectiveResult;
  ahleHadithPerspective: FiqhPerspectiveResult;
  comparativeSummary: string;
  timestamp: string;
}
