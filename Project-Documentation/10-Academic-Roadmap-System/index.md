# Academic Roadmap System

> **Last refreshed:** 2026-05-31

---

## 1. What is the Roadmap?

The Academic Roadmap is a real-time view of a student's entire curriculum journey, comparing their actual progress (grades, enrollments) against the requirements defined in their assigned Regulation. It drives:
- The AI Study Plan Generator (StudyPlanModule)
- The AI Academic Advisor (AcademicAdvisorModule)
- The graduation readiness check
- Academic risk alerts

---

## 2. Data Sources

| Source | Entity | Key Data |
|--------|--------|---------|
| Regulation | `Regulation` + `RegulationSubject` | All required/optional subjects per semester |
| Grades | `StudentGrade` | Finalized grades (letter + points) per subject |
| Enrollments | `Enrollment` | Currently active enrollments |
| Student | `Student` | GPA, department, batch |

---

## 3. GET /api/regulations/my-roadmap

Requires: `[Authorize(Roles = "Student,SuperAdmin")]`

**Algorithm:**
1. Load student with Department + Batch
2. Load student's Regulation with all RegulationSubjects + Subject details
3. Parallel: load finalized grades + active enrollment subjectIds
4. Compute GPA in-memory from loaded grades (no extra DB round-trip)
5. For each subject in the regulation, determine status:
   - `passed` — grade ≥ 1.0 (D or higher)
   - `failed` — grade < 1.0, not currently enrolled
   - `enrolled` — currently in active enrollment
   - `upcoming` — not yet taken
6. Group by semester, compute per-semester stats
7. Identify `currentSemester` (first non-completed)
8. Build `recommendedNext` (next semester's upcoming subjects)
9. Build `mustRetake` (failed mandatory subjects not currently enrolled)

**Response (abbreviated):**
```json
{
  "regulationId": "...",
  "regulationTitle": "دليل الطالب",
  "collegeName": "COMPUTER SCIENCE",
  "departmentName": "AI & MACHINE LEARNING",
  "batchName": "2023",
  "totalSemesters": 8,
  "totalCreditHours": 130,
  "completedCreditHours": 45,
  "remainingCreditHours": 85,
  "totalSubjects": 40,
  "passedSubjects": 14,
  "failedSubjects": 1,
  "currentlyEnrolled": 5,
  "currentGpa": 3.1,
  "semesters": [
    {
      "semesterNumber": 1,
      "status": "completed",
      "totalSubjects": 6,
      "passedSubjects": 6,
      "failedSubjects": 0,
      "earnedCreditHours": 18,
      "subjects": [
        {
          "subjectId": "...",
          "subjectName": "Introduction to Programming",
          "subjectCode": "CS101",
          "creditHours": 3,
          "isRequired": true,
          "status": "passed",
          "gradeLetter": "A",
          "gradePoints": 4.0,
          "finalScore": 95
        }
      ]
    }
  ],
  "mustRetake": [
    { "subjectName": "Calculus 2", "creditHours": 3, "gradeLetter": "F", "status": "failed" }
  ],
  "recommendedNext": [
    { "subjectName": "Data Structures", "creditHours": 3, "isRequired": true, "status": "upcoming" }
  ]
}
```

---

## 4. Subject Status Logic

```csharp
if (passedSubjectIds.Contains(subjectId))
    status = "passed";
else if (failedSubjectIds.Contains(subjectId) && !enrolledSet.Contains(subjectId))
    status = "failed";
else if (enrolledSet.Contains(subjectId))
    status = "enrolled";
else
    status = "upcoming";
```

**Passing threshold:** GradePoints ≥ 1.0 (D grade)

---

## 5. Regulation Structure

```
Regulation
  ├── Title: "دليل الطالب"
  ├── Type: Academic
  ├── IsActive: true
  ├── FileId → UploadedFile (PDF of the regulation document)
  └── RegulationSubjects[]
        ├── { SubjectId, Semester: 1, IsRequired: true }
        ├── { SubjectId, Semester: 2, IsRequired: true }
        └── { SubjectId, Semester: 5, IsRequired: false }  ← elective
```

Students are assigned a Regulation via `Student.RegulationId`.

---

## 6. AI Integration

The roadmap endpoint is the primary data source for two AI modules:

### StudyPlanModule
- Fetches roadmap for: GPA, enrolled subjects, mustRetake list
- Combines with attendance, assignments, and today's date to generate weekly schedule

### AcademicAdvisorModule v2
- Fetches roadmap for: full semester breakdown, GPA, progress stats
- Compares against RAG-retrieved regulation passages
- Generates personalized academic advice with regulation citations

---

## 7. Caching

The roadmap endpoint is **not cached** — it is always computed live to reflect the latest grades and enrollments. The regulation list (used by other endpoints) is cached for 5 minutes.
