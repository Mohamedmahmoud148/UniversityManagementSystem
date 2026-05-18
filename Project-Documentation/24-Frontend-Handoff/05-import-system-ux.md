# 📥 Import System UX — Complete Guide

---

## Import Flow (Two-Phase)

```
Phase 1 — DRY RUN (Preview)       Phase 2 — EXECUTE (Actual Import)
──────────────────────────────     ──────────────────────────────────
1. Upload Excel file            →  4. Review preview results
2. POST ?dryRun=true            →  5. Click "Confirm Import"
3. See row-level preview        →  6. POST (actual import)
                                →  7. See final results
```

**Why dry-run?** The user sees exactly what WILL happen before anything is saved to the database. This prevents surprises and allows fixing the Excel file before committing.

---

## Upload Screen

```
┌─────────────────────────────────────────────────────────────┐
│  📥  Import Students                                         │
│                                                             │
│  ┌─────────────────────────────────────────────────────┐   │
│  │                                                     │   │
│  │    📄  Drag & drop your .xlsx file here             │   │
│  │        or click to browse                          │   │
│  │                                                     │   │
│  │        Accepted: .xlsx only  │  Max: 10MB          │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  📥 Download Template    📋 View Required Columns           │
│                                                             │
│  Required columns: FullName, BatchCode, GroupCode           │
│  Optional: NationalId, Phone, Email, UniversityStudentId   │
│                                                             │
│                              [ Upload & Preview → ]         │
└─────────────────────────────────────────────────────────────┘
```

---

## Required Excel Columns (for Students)

| Column | Required? | Validation |
|---|---|---|
| `FullName` | ✅ | Non-empty |
| `BatchCode` | ✅ | Must match existing batch code |
| `GroupCode` | ✅ | Must match existing group code under batch |
| `NationalId` | ⚠️ Optional | 14-digit number, no duplicates |
| `Phone` | ⚠️ Optional | Egyptian format (01XXXXXXXXX) |
| `Email` | ⚠️ Optional | Auto-generated if missing |
| `UniversityStudentId` | ⚠️ Optional | Auto-generated if missing |

---

## Dry-Run Preview Screen

After uploading, show the preview BEFORE any data is saved:

```
┌─────────────────────────────────────────────────────────────┐
│  📊  Import Preview — students_batch2026.xlsx               │
│  ─────────────────────────────────────────────────────────  │
│                                                             │
│  ✅ Will Succeed:  142 students                             │
│  ⏭️  Will Skip:     8 rows (see errors below)              │
│  ❌ Will Fail:      0                                        │
│                                                             │
│  ──── Issues Found (8 rows) ────                           │
│                                                             │
│  ┌─────┬───────────┬──────────────┬─────────────────────┐ │
│  │ Row │ Column    │ Value        │ Issue               │ │
│  ├─────┼───────────┼──────────────┼─────────────────────┤ │
│  │  5  │ BatchCode │ CS2022X      │ Batch not found     │ │
│  │ 12  │ NationalId│ 30305123456  │ Already exists      │ │
│  │ 23  │ Phone     │ 0112345      │ Invalid format      │ │
│  │     │           │              │ (will use default)  │ │
│  │ 45  │ GroupCode │ GroupZ       │ Group not found     │ │
│  └─────┴───────────┴──────────────┴─────────────────────┘ │
│                                                             │
│  ℹ️  Phone warnings are non-fatal — placeholder used.      │
│                                                             │
│  📥 Download Error Report (.xlsx)                          │
│                                                             │
│  [← Fix & Re-upload]        [✅ Import 142 Students →]     │
└─────────────────────────────────────────────────────────────┘
```

---

## Error Severity Colors in Table

| Severity | Color | Meaning |
|---|---|---|
| `Error` | `text-red-600 bg-red-50` | Row will be skipped |
| `Warning` | `text-yellow-700 bg-yellow-50` | Row will proceed with adjustment |
| `Info` | `text-blue-600 bg-blue-50` | Informational — no problem |

---

## Actual Import Progress Screen

```
┌─────────────────────────────────────────────────────────────┐
│  ⚙️  Importing 142 Students...                               │
│                                                             │
│  ████████████████████░░░░░░░░░░░░  63%                     │
│  Processing row 90 of 142                                  │
│                                                             │
│  (This may take a few seconds)                             │
│                                                             │
│  [Cancel]  (disabled after 10%)                            │
└─────────────────────────────────────────────────────────────┘
```

Note: The actual import API is synchronous (returns when done). Show a spinner with an estimated progress animation (fake progress up to 90%, then jump to 100% on response).

---

## Import Result Screen

```
┌─────────────────────────────────────────────────────────────┐
│  ✅  Import Complete!                                        │
│  ─────────────────────────────────────────────────────────  │
│                                                             │
│  ✅  Created:   142  students                               │
│  ⏭️   Skipped:    8  rows (already exist or invalid)       │
│  ❌  Failed:     0                                          │
│                                                             │
│  🔑  Temporary Password: [sent to admin email]              │
│      (All imported students must change password on login) │
│                                                             │
│  📥 Download Full Report                                    │
│                                                             │
│  [Done]                          [Import Another File]      │
└─────────────────────────────────────────────────────────────┘
```

**Important:** Never display `TemporaryPassword` in plaintext on screen. Send via email. Show: "Temporary passwords have been sent to the admin email on file."

---

## Grades Import UX

### Supported columns in grades Excel:
| Column | Required? | Notes |
|---|---|---|
| `StudentId` or `id` | ✅ | UniversityStudentId |
| `Midterm` | ⚠️ Optional | 0 to offering max |
| `Coursework` | ⚠️ Optional | 0 to offering max |
| `FinalExam` or `Final` | ⚠️ Optional | 0 to offering max |

### Grades Import Blockers
```
"Row 12: Student 'STU20260047' is not enrolled in this offering."
"Row 18: Midterm score 25 exceeds max 20."
"Row 31: Grade for 'STU20260089' is already finalized — skipped."
```

The last error is new — **finalized grades are now protected**. Show this clearly to the doctor:

```
⛔ Row 31: This student's grade is already finalized and published.
   Finalized grades cannot be modified via import.
   To update, use the individual grade edit interface.
```

---

## Import History Table (for Admin)

Show a table of past imports:

```
┌────────────────────────────────────────────────────────────────────────┐
│  Import Type  │  File Name            │  Date       │  Result           │
├───────────────┼───────────────────────┼─────────────┼───────────────────┤
│  Students     │  batch_2026.xlsx      │  2026-05-18 │  142 ✅  8 ⏭️    │
│  Grades       │  cs301_grades.xlsx    │  2026-05-15 │  38 ✅  2 ⏭️     │
│  Students     │  batch_2025.xlsx      │  2026-03-01 │  Failed ❌        │
└────────────────────────────────────────────────────────────────────────┘
```
