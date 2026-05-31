# Regulations System

> **Last refreshed:** 2026-05-31

---

## 1. Overview

The Regulations System manages academic regulations (اللوائح الأكاديمية) — the official curriculum documents that define:
- Required and elective subjects per semester
- Credit hour requirements for graduation
- GPA thresholds and academic standing rules
- Conduct and exam regulations

Regulations power both the **Academic Roadmap** (structured data) and the **AI Regulation Q&A** (RAG over indexed PDF content).

---

## 2. Regulation Entity

```csharp
Regulation {
    Title          // e.g. "دليل الطالب"
    Content?       // Optional text body
    Code           // Auto-generated URL slug: "dlyl-alttalb"
    Type           // Academic | Conduct | Exam | General
    IsActive       // Active regulations shown to students
    FileId?        // FK to UploadedFile (attached PDF/Word)
    DepartmentId?  // Optional: department-specific regulation
    RegulationSubjects[]  // Curriculum subjects per semester
}
```

---

## 3. API Endpoints

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/api/regulations` | **Public** | All regulations with file URLs |
| GET | `/api/regulations/active` | **Public** | Active only |
| GET | `/api/regulations/by-code/{code}` | Auth | By slug (preferred for admin) |
| GET | `/api/regulations/my-roadmap` | Student | Personal roadmap (see section 10) |
| GET | `/api/regulations/by-department/{deptId}` | Auth | Dept-specific |
| GET | `/api/regulations/student/{studentId}` | Auth | Student's assigned regulation |
| POST | `/api/regulations` | Admin | Create (multipart, up to 50 MB file) |
| PUT | `/api/regulations/by-code/{code}` | Admin | Update |
| DELETE | `/api/regulations/by-code/{code}` | Admin | Delete |

---

## 4. Creating a Regulation (multipart/form-data)

```
title:        "دليل الطالب"
content:      "نص اللائحة..." (optional, text version)
type:         0  (Academic=0, Conduct=1, Exam=2, General=3)
departmentId: "01HX..."  (optional)
file:         PDF/Word/Excel (up to 50 MB)
subjectsJson: '[{"subjectId":"...","semester":1,"isRequired":true}, ...]'
```

**Code auto-generation:** Title → URL slug (Arabic stripped, spaces → hyphens). Unique suffix appended if collision.

---

## 5. File Attachment

Regulations support an attached file (PDF, DOC, DOCX, XLS, XLSX, TXT up to 50 MB).

File flow:
```
Admin uploads file → stored in Cloudflare R2 under "files/" folder
→ UploadedFile record created → FileId stored on Regulation
→ ToDto() builds public R2 URL (no auth needed for regulation files)
```

---

## 6. Caching

The regulation list is cached in Redis for **5 minutes** under key `Regulations_All`.
Cache is invalidated on every create, update, or delete operation.

---

## 7. RAG Integration (AI Q&A)

When a regulation file is uploaded, it is automatically indexed into ChromaDB for semantic search:

```
Upload → RagService.IndexRegulationAsync()
    → FastAPI POST /api/rag/index-regulations
    → PDF text extracted → chunked → embedded → ChromaDB
```

Students can then ask: "ايه متطلبات التخرج في لائحتي؟" and the AI will search the indexed regulation and answer with quoted passages.

**Auto-indexing on startup:** FastAPI auto-indexes all active regulations on first boot.

---

## 8. RegulationSubject — Curriculum Mapping

Each subject in a regulation defines:

```csharp
RegulationSubject {
    RegulationId   // parent regulation
    SubjectId      // FK to Subject catalogue
    Semester       // 1–8 (which semester in the curriculum)
    IsRequired     // mandatory vs elective
}
```

This is the data structure the roadmap endpoint uses to determine a student's academic progress.
