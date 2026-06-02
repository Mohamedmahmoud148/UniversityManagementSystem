# 11 — Teaching Intelligence Platform

## Overview
The Teaching Intelligence Platform is the doctor's AI-powered command center. It uses pre-computed snapshots (updated hourly) to provide instant analytics on all students across all courses.

---

## Core API: `/api/teaching-intelligence`

All endpoints require `Doctor` or `SuperAdmin` role.

### Dashboard Response Shape
```typescript
TeachingDashboardDto {
  offerings: DoctorOfferingSummaryDto[]   // Each course with health indicator
  overallStats: DashboardStatsDto        // Aggregate numbers
  atRiskStudents: StudentIntelligenceDto[] // Top 20 at-risk
  weakTopics: WeakTopicDto[]             // Across all courses
  classComparisons: ClassComparisonDto[] // Side-by-side comparison
  recentAlerts: TeachingAlertDto[]       // Unread alerts
  aiRecommendations: string[]            // AI-generated actions
}
```

---

## Risk Score Algorithm (Frontend Display)

The risk score (0-100) is computed server-side. The frontend uses it for:

```typescript
function getRiskColor(score: number): string {
  if (score >= 75) return 'text-red-600 bg-red-50';     // Critical
  if (score >= 55) return 'text-orange-600 bg-orange-50'; // High
  if (score >= 30) return 'text-amber-600 bg-amber-50';  // Medium
  return 'text-green-600 bg-green-50';                    // Low
}

function getRiskLevel(score: number): string {
  if (score >= 75) return 'Critical';
  if (score >= 55) return 'High';
  if (score >= 30) return 'Medium';
  return 'Low';
}
```

---

## Student Intelligence Table

Key column set for the student table at `/doctor/students/:offeringId`:

| Column | Source Field | Sortable | Filterable |
|--------|-------------|---------|------------|
| Name | `studentName` | ✅ | Search |
| University ID | `studentUniversityId` | ❌ | ❌ |
| Final Score | `finalScore` | ✅ | Range |
| Attendance | `attendancePercent` | ✅ | Range |
| Assignment Completion | `assignmentCompletionRate` | ✅ | ❌ |
| Risk Score | `riskScore` | ✅ | Level filter |
| Risk Level | `riskLevel` | ✅ | Dropdown |
| Trend | `overallTrend` | ✅ | improving/declining/stable |
| Engagement | `engagementScore` | ✅ | ❌ |
| Actions | — | ❌ | ❌ |

### Filter Panel
```
[Risk Level: All ▾]  [Trend: All ▾]  [Score Range: ___-___]
[☐ At Risk Only]     [Search by name...]
```

### Query Params
```
?riskLevel=High&atRiskOnly=true&trend=declining&sortBy=RiskScore&sortDir=desc&page=1&pageSize=50
```

---

## Offering Health Indicator

```typescript
type OfferingHealth = 'excellent' | 'good' | 'concerning' | 'critical';

const healthConfig = {
  excellent:   { color: 'green',  icon: '✅', label: 'Excellent' },
  good:        { color: 'blue',   icon: '👍', label: 'Good' },
  concerning:  { color: 'amber',  icon: '⚠️', label: 'Concerning' },
  critical:    { color: 'red',    icon: '🚨', label: 'Critical' },
};
```

---

## Excel Export Implementation

```typescript
// Doctor clicks "Export Excel"
const { data } = await teachingApi.getStudentExport(offeringId);

// Build Excel with SheetJS
import * as XLSX from 'xlsx';

const headers = [
  'University ID', 'Student Name', 'Batch', 'Group', 'Department', 'College', 'Subject',
  'Final Score', 'Midterm', 'Coursework', 'Final Exam', 'Grade',
  'Sessions', 'Attended', 'Attendance%',
  'Total Assignments', 'Submitted', 'Missing', 'Completion%',
  'Total Exams', 'Avg Exam', 'Avg Quiz',
  'Risk Score', 'Risk Level', 'Risk Factors',
  'AI Sessions', 'Study Minutes', 'Streak Days'
];

const rows = data.rows.map(r => Object.values(r));
const ws = XLSX.utils.aoa_to_sheet([headers, ...rows]);

// Style headers
const headerRange = XLSX.utils.decode_range(ws['!ref']!);
for (let C = headerRange.s.c; C <= headerRange.e.c; C++) {
  const addr = XLSX.utils.encode_cell({ r: 0, c: C });
  if (!ws[addr]) continue;
  ws[addr].s = { font: { bold: true }, fill: { fgColor: { rgb: 'EFF6FF' } } };
}

const wb = XLSX.utils.book_new();
XLSX.utils.book_append_sheet(wb, ws, 'Students');
XLSX.writeFile(wb, `${data.exportTitle}_${new Date().toISOString().split('T')[0]}.xlsx`);
```

---

## Manual Refresh

Doctor can trigger immediate snapshot refresh:
```typescript
// POST /api/teaching-intelligence/offerings/{id}/refresh
// Response: 202 Accepted
// Show toast: "Refreshing data... This may take a minute."
// After 30 seconds: refetch dashboard data
```

---

## Teaching Alerts

Alerts are generated automatically by `TeachingIntelligenceBackgroundService` (every hour):
- Risk escalation (student becomes Critical)
- Low class attendance
- Low assignment completion

Frontend: poll `GET /api/teaching-intelligence/alerts?unreadOnly=true` every 5 minutes.
Show badge count on Alerts nav item.

Mark read: `POST /api/teaching-intelligence/alerts/{id}/read`
