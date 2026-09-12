export enum ComplianceStandard {
  Strict = 1, // 0% Riba / Zero debt & interest
  Aaoifi = 2  // AAOIFI Standard No. 21
}

export enum ComplianceStatus {
  Compliant = 1,
  NonCompliant = 2,
  Questionable = 3
}

export interface ComplianceRuleEvaluation {
  ruleName: string;
  description: string;
  calculatedValue: number;
  allowedThreshold: number;
  passed: boolean;
  explanation: string;
}

export interface StandardComplianceDetail {
  standardApplied: ComplianceStandard | number;
  status: ComplianceStatus;
  rules: ComplianceRuleEvaluation[];
  dividendPurificationPercentage: number;
  aiExecutiveAudit: string;
}

export interface StockCheckRequest {
  ticker: string;
}

export interface StockCheckResponse {
  ticker: string;
  companyName: string;
  sector: string;
  currentPrice: number;
  marketCap: number;
  strictStandard: StandardComplianceDetail;
  aaoifiStandard: StandardComplianceDetail;
  timestamp: string;
}
