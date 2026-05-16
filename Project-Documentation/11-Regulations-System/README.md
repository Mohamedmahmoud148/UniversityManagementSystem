# 📋 Regulations System — Complete Guide

## What Is a Regulation?

A **Regulation** (لائحة دراسية) is the official academic curriculum document for a department. It defines:

- **Which subjects** students must take
- **In which semester** (1 through 8) each subject should be taken
- **Whether each subject is Required or Elective**

Think of it as the university's official "roadmap template." Every student in the same department+batch follows the same regulation — but their personal progress through it differs.

---

## The Regulation Data Model

```
Regulation
├── Id (ULID)
├── Title          → "Computer Science Curriculum 2022-2026"
├── Content        → Text description (optional)
├── Type           → Academic | Conduct | Exam | General
├── IsActive       → Is this currently enforced?
├── FileId         → Optional PDF attachment (stored in R2)
├── DepartmentId   → Scoped to one department (or null = university-wide)
│
└── RegulationSubjects[] (the curriculum itself)
        ├── SubjectId   → Which subject
        ├── Semester    → When to take it (1-8)
        └── IsRequired  → Must pass to graduate?
```

---

## Regulation Types

| Type | Value | Use Case |
|------|-------|---------|
| `Academic` | 0 | Main curriculum — list of required/elective subjects per semester |
| `Conduct` | 1 | Rules of student behavior and discipline |
| `Exam` | 2 | Exam policies (allowed materials, grading criteria) |
| `General` | 3 | General university-wide policies |

For the Academic Roadmap feature, only `Academic` type regulations with `RegulationSubjects` matter.

---

## How Regulations Connect to Students

```
Department (e.g., Computer Science)
    │
    └── Regulation (e.g., "CS Plan 2022")
            │
            └── RegulationSubjects
                    ├── CS101, Semester 1, Required
                    ├── MATH101, Semester 1, Required
                    ├── CS201, Semester 2, Required
                    └── ... (all 40 subjects)

Student (Ahmed, CS Batch 2022)
    │
    └── RegulationId → links to "CS Plan 2022"
                            │
                            └── Used to build Ahmed's personal roadmap
```

**Key point:** `Student.RegulationId` is **nullable**. A student without a regulation assigned shows a friendly error in the roadmap endpoint. Admins must assign regulations to students (or batches) after creating them.

---

## Complete API Reference

### GET /api/regulations
**Auth:** Any authenticated  
**Returns:** All regulations (paginated)
```json
[
  {
    "id": "01H...",
    "title": "CS Department Plan 2022-2026",
    "type": "Academic",
    "isActive": true,
    "departmentId": "01H...",
    "fileId": null,
    "fileUrl": null,
    "subjects": []
  }
]
```

---

### GET /api/regulations/{id}
**Auth:** Any authenticated  
**Returns:** Full regulation with all subjects
```json
{
  "id": "01H...",
  "title": "CS Department Plan 2022-2026",
  "content": "This plan covers 8 semesters...",
  "type": "Academic",
  "isActive": true,
  "departmentId": "01H...",
  "fileId": "01H...",
  "fileUrl": "https://r2.cloudflare.com/...?expires=...",
  "subjects": [
    { "subjectId": "01H...", "semester": 1, "isRequired": true },
    { "subjectId": "01H...", "semester": 1, "isRequired": false },
    { "subjectId": "01H...", "semester": 2, "isRequired": true }
  ]
}
```

---

### GET /api/regulations/my-roadmap ⭐
**Auth:** Student  
**Full documentation in:** `10-Academic-Roadmap-System/README.md`  
**Returns:** Complete personalized academic roadmap for the logged-in student

---

### GET /api/regulations/student/{studentId}
**Auth:** Admin, SuperAdmin  
**Returns:** Same roadmap format but for any specific student (admin use)

---

### GET /api/regulations/by-department/{departmentId}
**Auth:** Any authenticated  
**Returns:** All regulations scoped to a department

---

### POST /api/regulations
**Auth:** Admin, SuperAdmin  
**Content-Type:** `multipart/form-data`

```
Title:        string (required)
Content:      string (optional — text body)
Type:         int — 0=Academic, 1=Conduct, 2=Exam, 3=General
DepartmentId: ULID string (optional)
File:         PDF/Word/Excel/TXT (optional)
SubjectsJson: JSON string of subjects array
```

**SubjectsJson format:**
```json
[
  { "subjectId": "01H...", "semester": 1, "isRequired": true },
  { "subjectId": "01H...", "semester": 1, "isRequired": false },
  { "subjectId": "01H...", "semester": 2, "isRequired": true }
]
```

**Why SubjectsJson as a string?**  
Because this is a `multipart/form-data` request (to allow file upload), JSON arrays can't be sent directly as form fields. The frontend must `JSON.stringify()` the subjects array and send it as a text field.

---

### PUT /api/regulations/{id}
**Auth:** Admin, SuperAdmin  
**Same format as POST**

---

### DELETE /api/regulations/{id}
**Auth:** Admin, SuperAdmin  
**Effect:** Soft delete (IsActive set to false, DeletedAt set)

---

## File Attachment Flow

```
Admin creates regulation with PDF file
        │
        ▼
POST /api/regulations (multipart/form-data)
  Title: "CS Exam Policy"
  Type: 2
  File: exam-policy-2024.pdf
        │
        ▼
RegulationService:
  1. Upload file to Cloudflare R2
     StorageKey: "regulations/{ulid}/exam-policy-2024.pdf"
  2. Create UploadedFile record in DB
  3. Create Regulation: { FileId: uploadedFile.Id }
        │
        ▼
Student reads regulation:
GET /api/regulations/{id}
        │
        ▼
Response includes:
  "fileId": "01H...",
  "fileUrl": "https://r2-signed-url?expires=60min"
        │
        ▼
Frontend: <a href={fileUrl}>Download PDF</a>
```

---

## Multi-Department / Multi-Regulation Support

This system supports **9+ colleges** with completely different regulations:

```
Engineering College
  ├── CS Department → "CS Plan 2022" (40 subjects, 8 semesters)
  ├── Electronics   → "EE Plan 2022" (38 subjects, 8 semesters)
  └── Civil         → "CE Plan 2022" (42 subjects, 8 semesters)

Medicine College
  └── Medicine Dept → "Med Plan 2022" (60 subjects, 12 semesters)

Commerce College
  ├── Accounting    → "ACC Plan 2022" (35 subjects, 8 semesters)
  └── Management    → "MG Plan 2022"  (32 subjects, 8 semesters)
```

Each department's students get their department's regulation. The roadmap algorithm works identically for all of them — just different data.

---

## Admin: How to Set Up a Regulation

### Step 1: Create all Subjects first
```
POST /api/subjects (for each subject)
{ name: "Data Structures", code: "CS301", creditHours: 3, departmentId: "..." }
```

### Step 2: Create the Regulation
```
POST /api/regulations (multipart/form-data)
Title: "CS Department Curriculum 2022"
Type: 0 (Academic)
DepartmentId: "01H..."
SubjectsJson: '[
  {"subjectId":"...CS101...", "semester":1, "isRequired":true},
  {"subjectId":"...MATH101...", "semester":1, "isRequired":true},
  ...all 40 subjects...
]'
```

### Step 3: Assign to Students
```
PUT /api/students/{studentId}
{ regulationId: "01H...regulation-id..." }
```

Or assign to a whole batch:
```
POST /api/batches/{batchId}/assign-regulation
{ regulationId: "..." }
```

### Step 4: Students can now use AI roadmap
```
GET /api/regulations/my-roadmap → returns their personalized plan
```

---

## Business Rules

| Rule | Details |
|------|---------|
| A student with no regulation | /my-roadmap returns HTTP 404 |
| Multiple regulations per department | Allowed — each batch can have its own regulation |
| Regulation with no subjects | Valid for Conduct/Exam/General types |
| Subject in multiple semesters | Not allowed — each subject appears once per regulation |
| IsRequired=false subjects | Electives — failing them doesn't appear in MustRetake |
| IsActive=false regulation | Still visible in history; students assigned to it still use it |

---

## AI Integration with Regulations

The AI assistant can answer regulation questions using two sources:

1. **GET /api/regulations/my-roadmap** — personalized, includes pass/fail status
2. **GET /api/regulations/{id}** — the raw regulation document

For student-facing questions ("كام مادة باقي؟", "رسبت في إيه؟"), the AI always uses `/my-roadmap` first since it has personal progress data.

For general questions ("ما هي متطلبات التخرج؟"), the AI may use the regulation document text content.
