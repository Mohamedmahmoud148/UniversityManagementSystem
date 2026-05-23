---
layout: default
title: "🗺️ Academic Roadmap System"
---

# 🗺️ Academic Roadmap System — Complete Guide

## What Is It?

The Academic Roadmap is the **most technically complex and student-facing feature** of the platform. It answers one fundamental question every student has:

> "Where am I in my academic journey? What have I passed? What do I still need? What should I take next?"

---

## How It Works — Simple Explanation

Imagine a 4-year university plan as a grid:

```
Semester 1 │ Math 101 ✅ | CS101 ✅ | Physics 101 ✅ | English ✅ | Ethics ✅
Semester 2 │ Math 102 ✅ | CS201 ❌ | Physics 102 ✅ | DS 101 ✅  | --
Semester 3 │ CS301 📚 | CS302 📚 | Math 301 📚 | -- | --
Semester 4 │ CS401 ⏳ | CS402 ⏳ | CS403 ⏳ | -- | --
...
Semester 8 │ Graduation Project ⏳ | ...
```

**Legend:** ✅ Passed | ❌ Failed | 📚 Currently Enrolled | ⏳ Upcoming

The roadmap API computes this entire grid from:
- The student's **Regulation** (which subjects they need to take, in which semester)
- Their **StudentGrades** (what they passed/failed)
- Their **Enrollments** (what they're currently taking)

---

## The Regulation-to-Roadmap Connection

```
Student
  │
  └── RegulationId (nullable FK)
            │
            ▼
        Regulation
          │
          └── RegulationSubjects []
                │
                ├── SubjectId → Subject (name, code, credit hours)
                ├── Semester (1, 2, 3... 8)
                └── IsRequired (bool)
```

A **Regulation** is the official curriculum document. It lists ALL subjects a student must (or may) take, organized by semester. Different departments/batches may have different regulations.

---

## The Algorithm — Step by Step

```csharp
GET /api/Regulations/my-roadmap

1. Extract student from JWT ProfileId claim
2. Check student.RegulationId — return 404 if null
3. Load Regulation with RegulationSubjects (each with Subject details)
4. Load StudentGrades for this student:
   - Join through SubjectOffering to get SubjectId
   - Filter IsFinalized = true
   - Index by SubjectId
5. Load active Enrollments for this student:
   - Join through SubjectOffering to get SubjectId
   - Collect as HashSet<SubjectId>

For each RegulationSubject (grouped by Semester):

  foreach (var subject in semesterSubjects):
    if (subjectId in passedGrades AND gradePoints >= 1.0):
      status = "passed"
      gradeLetter = grade.GradeLetter
      gradePoints = grade.GradePoints
    elif (subjectId in failedGrades):
      status = "failed"
    elif (subjectId in enrolledSubjectIds):
      status = "enrolled"
    else:
      status = "upcoming"

  semesterStatus:
    all passed → "completed"
    any enrolled → "in_progress"
    else → "upcoming"

7. Compute RecommendedNext:
   Find first "upcoming" semester → return its subjects

8. Compute MustRetake:
   All failed+required subjects

9. Compute GPA:
   SUM(GradePoints × CreditHours) / SUM(CreditHours)
   for all finalized grades

10. Return AcademicRoadmapDto
```

---

## Complete Response Structure

```json
{
  "regulationId": "01HXYZ...",
  "regulationTitle": "Computer Science Curriculum 2022-2026",
  "departmentName": "Computer Science",
  "collegeName": "Faculty of Engineering",
  "batchName": "Batch 2022",
  
  "totalSemesters": 8,
  "totalCreditHours": 120,
  "completedCreditHours": 45,
  "remainingCreditHours": 75,
  "totalSubjects": 40,
  "passedSubjects": 15,
  "failedSubjects": 2,
  "currentlyEnrolled": 5,
  "currentGpa": 2.85,
  
  "semesters": [
    {
      "semesterNumber": 1,
      "status": "completed",
      "totalSubjects": 5,
      "passedSubjects": 5,
      "failedSubjects": 0,
      "enrolledSubjects": 0,
      "totalCreditHours": 15,
      "earnedCreditHours": 15,
      "subjects": [
        {
          "subjectId": "01H...",
          "subjectName": "Mathematics 1",
          "subjectCode": "MATH101",
          "creditHours": 3,
          "isRequired": true,
          "status": "passed",
          "gradeLetter": "A",
          "gradePoints": 4.0,
          "finalScore": 92.5
        },
        {
          "subjectId": "01H...",
          "subjectName": "Introduction to CS",
          "subjectCode": "CS101",
          "creditHours": 3,
          "isRequired": true,
          "status": "passed",
          "gradeLetter": "B",
          "gradePoints": 3.0,
          "finalScore": 83.0
        }
      ]
    },
    {
      "semesterNumber": 2,
      "status": "in_progress",
      "totalSubjects": 5,
      "passedSubjects": 3,
      "failedSubjects": 1,
      "enrolledSubjects": 1,
      "totalCreditHours": 15,
      "earnedCreditHours": 9,
      "subjects": [
        { "status": "passed", ... },
        { "status": "failed", "gradeLetter": "F", "gradePoints": 0.0, ... },
        { "status": "enrolled", "gradeLetter": null, ... },
        { "status": "passed", ... },
        { "status": "passed", ... }
      ]
    },
    {
      "semesterNumber": 3,
      "status": "upcoming",
      ...
    }
  ],
  
  "recommendedNext": [
    {
      "subjectName": "Data Structures",
      "subjectCode": "CS301",
      "creditHours": 3,
      "isRequired": true,
      "status": "upcoming"
    }
  ],
  
  "mustRetake": [
    {
      "subjectName": "Algorithms",
      "subjectCode": "CS201",
      "creditHours": 3,
      "isRequired": true,
      "status": "failed",
      "gradeLetter": "F",
      "gradePoints": 0.0,
      "finalScore": 45.0
    }
  ]
}
```

---

## AI Integration with Roadmap

The roadmap endpoint is designed to be the **primary data source for the AI advisor**. One call answers ALL academic questions:

| Student Question | AI Action | Data Source |
|-----------------|-----------|-------------|
| "كام ساعة خلصت؟" | Read `completedCreditHours` | roadmap |
| "كام مادة باقي؟" | Read `remainingCreditHours / avgCredits` | roadmap |
| "معدلي كام؟" | Read `currentGpa` | roadmap |
| "رسبت في إيه؟" | Read `mustRetake[]` | roadmap |
| "هاخد إيه الترم الجاي؟" | Read `recommendedNext[]` | roadmap |
| "أنهيت ترم كام؟" | Count semesters where status="completed" | roadmap |

**One API call → AI answers any academic question without additional DB queries.**

---

## Edge Cases Handled

### No Regulation Assigned
```json
HTTP 404:
{
  "message": "لم يتم تعيين لائحة دراسية لك بعد. تواصل مع الإدارة."
}
```

### Multiple Attempts at Same Subject
- If student failed CS201 in Semester 1 and passed it in Semester 3:
  - `passedSubjectIds` contains CS201 (latest grade = passed)
  - Status shown as "passed" in the semester where it appears in regulation
  - `mustRetake` would NOT include it (since now passed)

### Grade Points Threshold for "Passed"
```csharp
// GradePoints >= 1.0 is considered PASSING (grade D or higher)
bool isPassed = grade.GradePoints >= 1.0;
// This means D (1.0) is the minimum passing grade
// F (0.0) is failing
```

### Student with No Grades Yet
- All subjects show "upcoming" status
- `completedCreditHours = 0`
- `currentGpa = null`

---

## Frontend Rendering Recommendations

### Visual Roadmap Component
```
Semester 1 ████████████ 100% Complete
Semester 2 ████████░░░░  67% Complete (1 failed)
Semester 3 ████░░░░░░░░  Enrolled (in progress)
Semester 4 ░░░░░░░░░░░░  Upcoming
...

Subject Color Coding:
  Green (passed) | Red (failed) | Blue (enrolled) | Gray (upcoming)
```

### Progress Ring
```
GPA: 2.85 / 4.00
Credit Hours: 45 / 120 (37.5%)
[Progress circle visualization]
```

### Must Retake Alert
```
⚠️ You must retake:
  • Algorithms (CS201) — Required
  • Database Fundamentals (CS305) — Required
```
