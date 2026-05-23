---
layout: default
title: "Proactive Alerts"
---

# Proactive Alerts — Academic Risk Scoring System

> **A daily Hangfire job that calculates academic risk for every student in every active offering, persists scores, and sends bilingual Arabic/English notifications to at-risk students.**

---

## Table of Contents

1. [Overview](#1-overview)
2. [AcademicRiskScore Entity](#2-academicriskScore-entity)
3. [Risk Scoring Formula](#3-risk-scoring-formula)
4. [AcademicRiskJob — Daily Flow](#4-academicriskjob--daily-flow)
5. [Notification Messages](#5-notification-messages)
6. [RiskController Endpoints](#6-riskcontroller-endpoints)
7. [DTOs Reference](#7-dtos-reference)
8. [Hangfire Schedule](#8-hangfire-schedule)

---

## 1. Overview

The Proactive Alerts system (Phase 2) was introduced to detect academic risk early — before a student fails — and trigger automated, personalized notifications. It operates entirely in the background without any manual trigger required.

**What it does:**
- Every day at 6 AM, `AcademicRiskJob` runs across all active subject offerings
- For each student in each offering it calculates attendance percentage and average grade
- A `RiskLevel` (Low / Medium / High / Critical) is assigned based on thresholds
- An `AiRecommendation` is generated in Arabic with an English summary
- The score is upserted to `AcademicRiskScores` (insert on first run, update on subsequent runs)
- Students at Medium/High/Critical risk receive an `AppNotification` via the notifications system
- Doctors and admins can view risk data via the `RiskController` endpoints

---

## 2. AcademicRiskScore Entity

**Table:** `AcademicRiskScores` (migration: `AddAcademicRiskScoring`)

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `StudentId` | ULID | FK → Students (RESTRICT) |
| `SubjectOfferingId` | ULID | FK → SubjectOfferings (RESTRICT) |
| `AttendancePercent` | float | Calculated attendance % (sessions attended / total sessions × 100) |
| `AverageGrade` | float | Average of all recorded grades for this student in this offering |
| `RiskLevel` | int | enum: Low=0, Medium=1, High=2, Critical=3 |
| `AiRecommendation` | text? | Bilingual recommendation text |
| `AnalyzedAt` | datetime | Timestamp of last analysis run |

---

## 3. Risk Scoring Formula

Risk level is assigned by evaluating **both** attendance and grade thresholds. The **higher** risk level from either dimension wins.

```
AttendancePercent = (sessionsAttended / totalSessions) × 100
AverageGrade      = mean(all FinalScore values for this student + offering)

Risk Level Assignment:
┌─────────────┬───────────────────────────────────────────────────┐
│ RiskLevel   │ Condition (either dimension can trigger this level)│
├─────────────┼───────────────────────────────────────────────────┤
│ Critical    │ attendance < 50%  OR  average grade < 40          │
│ High        │ attendance < 65%  OR  average grade < 55          │
│ Medium      │ attendance < 75%  OR  average grade < 65          │
│ Low         │ all metrics within acceptable range                │
└─────────────┴───────────────────────────────────────────────────┘
```

**Special cases:**
- If a student has no attendance sessions recorded → `AttendancePercent = 0` → triggers Critical
- If a student has no grades recorded → `AverageGrade = 0` → triggers Critical
- If total sessions = 0 → attendance is treated as "not applicable" and not used as a risk trigger

---

## 4. AcademicRiskJob — Daily Flow

**Class:** `AcademicRiskJob`  
**Schedule:** Daily at 6 AM (`"0 6 * * *"`)  
**Hangfire trigger:** Registered in `Program.cs` via `RecurringJob.AddOrUpdate`

```
AcademicRiskJob.ExecuteAsync()
        │
        ├── Load all active SubjectOfferings with enrolled students
        │
        └── For each offering:
              │
              └── For each enrolled student:
                    │
                    ├── Calculate AttendancePercent
                    │     AttendanceSessions for this offering → count student's present records
                    │
                    ├── Calculate AverageGrade
                    │     StudentGrades for this student + offering → average FinalScore
                    │
                    ├── Determine RiskLevel (formula above)
                    │
                    ├── Generate AiRecommendation
                    │     Template-based bilingual text (see Section 5)
                    │
                    ├── Upsert AcademicRiskScore
                    │     INSERT if new, UPDATE if exists (match on StudentId + SubjectOfferingId)
                    │
                    └── If RiskLevel >= Medium:
                          Send AppNotification to student
                          (Title + Message in Arabic/English bilingual format)
```

**Performance notes:**
- Uses `AsNoTracking()` for all read queries
- Batch loads attendance counts per offering (single query, dictionary lookup)
- Batch loads grade averages per student (single GROUP BY query)
- Upsert uses `_context.Database.ExecuteSqlRawAsync()` for conflict-free upsert

---

## 5. Notification Messages

All notifications sent to at-risk students are bilingual (Arabic primary, English secondary).

### Critical Risk
```
Title:   "تنبيه أكاديمي عاجل — Critical Academic Alert"
Message: "أداؤك في مادة {SubjectName} يحتاج تدخلاً فورياً.
          الحضور: {AttendancePercent}% | المتوسط: {AverageGrade}
          يرجى التواصل مع الدكتور المسؤول فوراً.
          ——
          Your performance in {SubjectName} requires immediate attention.
          Attendance: {AttendancePercent}% | Average: {AverageGrade}
          Please contact your instructor immediately."
```

### High Risk
```
Title:   "تحذير أكاديمي — Academic Warning"
Message: "لاحظنا تراجعاً في أدائك في مادة {SubjectName}.
          الحضور: {AttendancePercent}% | المتوسط: {AverageGrade}
          ننصحك بمراجعة الدكتور وزيادة المذاكرة.
          ——
          We noticed a decline in your performance in {SubjectName}.
          Attendance: {AttendancePercent}% | Average: {AverageGrade}
          We recommend consulting your instructor and increasing study time."
```

### Medium Risk
```
Title:   "تنبيه أكاديمي — Academic Notice"
Message: "أداؤك في مادة {SubjectName} يستدعي الانتباه.
          الحضور: {AttendancePercent}% | المتوسط: {AverageGrade}
          التزم بالحضور وراجع الدروس بانتظام.
          ——
          Your performance in {SubjectName} needs attention.
          Attendance: {AttendancePercent}% | Average: {AverageGrade}
          Maintain regular attendance and review course material."
```

**Template recommendations stored in `AiRecommendation` field** — same bilingual text saved alongside the score for doctor/admin review.

---

## 6. RiskController Endpoints

All endpoints are at `/api/risk`.

| Method | Endpoint | Role | Description |
|--------|----------|------|-------------|
| `GET` | `/api/risk/at-risk-students` | Doctor, Admin | List at-risk students, optionally filtered by `?offeringId=` |
| `GET` | `/api/risk/student/{studentId}` | Doctor, Admin, Student (own) | Get all risk scores for a specific student |
| `POST` | `/api/risk/analyze/trigger` | Admin | Manually trigger the AcademicRiskJob immediately (on-demand) |
| `GET` | `/api/risk/dashboard` | Admin | Aggregated at-risk overview across all offerings |

### GET /api/risk/at-risk-students
**Query:** `?offeringId=` (optional), `?minRiskLevel=Medium` (optional)

```json
[
  {
    "studentId":          "01H...",
    "studentName":        "Ahmed Ali",
    "subjectName":        "Data Structures",
    "riskLevel":          "High",
    "attendancePercent":  58.0,
    "averageGrade":       51.5,
    "aiRecommendation":   "يجب على الطالب تحسين الحضور... — Attendance critical.",
    "analyzedAt":         "2026-05-23T06:00:00Z"
  }
]
```

### GET /api/risk/dashboard
```json
{
  "totalAtRisk":       47,
  "criticalCount":     8,
  "highCount":         15,
  "mediumCount":       24,
  "topRiskOfferings": [
    { "offeringId": "01H...", "subjectName": "Algorithms", "atRiskCount": 12 }
  ],
  "recentAlerts": [
    { "studentName": "Ali Hassan", "riskLevel": "Critical", "subjectName": "Math II" }
  ]
}
```

---

## 7. DTOs Reference

### StudentRiskDto
```json
{
  "studentId":         "01H...",
  "studentName":       "Ahmed Ali",
  "subjectOfferingId": "01H...",
  "subjectName":       "Data Structures",
  "attendancePercent": 58.0,
  "averageGrade":      51.5,
  "riskLevel":         "High",
  "aiRecommendation":  "...",
  "analyzedAt":        "2026-05-23T06:00:00Z"
}
```

### RiskAnalysisResultDto
```json
{
  "totalProcessed": 1250,
  "atRiskFound":    47,
  "notificationsSent": 39,
  "errors":         2,
  "ranAt":          "2026-05-23T06:00:00Z"
}
```

### RiskRecommendationRequest (for POST /api/risk/analyze/trigger)
```json
{
  "offeringId": "01H..."   // optional — if null, runs for ALL offerings
}
```

---

## 8. Hangfire Schedule

| Job | Cron | Description |
|-----|------|-------------|
| `AcademicRiskJob` | `"0 6 * * *"` | Runs every day at 06:00 UTC. Analyzes all active offerings and upserts risk scores. |

**Registration in Program.cs:**
```csharp
RecurringJob.AddOrUpdate<IAcademicRiskJob>(
    "academic-risk-analysis",
    job => job.ExecuteAsync(),
    "0 6 * * *"
);
```

**Manual trigger** (Admin only):
```
POST /api/risk/analyze/trigger
```
Enqueues the job immediately via `BackgroundJob.Enqueue<IAcademicRiskJob>(j => j.ExecuteAsync())`.
