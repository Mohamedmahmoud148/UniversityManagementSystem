# AI System

> **Last refreshed:** 2026-05-31 | **Service:** FastAPI 3.12 | **LLM:** OpenRouter (GPT-4o-mini)

---

## 1. Overview

The AI system is a standalone FastAPI microservice called by the .NET backend over HTTP. It implements a multi-layer agent pipeline:

```
User → .NET ChatService → FastAPI /api/chat
                              │
                         PlannerAgent (LLM classify + Layer-2 keyword override)
                              │
                         RBAC gate
                              │
                         PlanExecutor → Module
                              │
                         LLM generates response
                              │
                         AgentOutput → .NET → Frontend
```

---

## 2. Intent Catalogue (17)

| Intent | Arabic Trigger Examples | Module | Roles |
|--------|------------------------|--------|-------|
| `study_plan` | "اعمللي خطة مذاكرة", "أولوياتي هذا الأسبوع", "study plan" | StudyPlanModule | Student, Doctor |
| `academic_advice` | "وضعي الأكاديمي", "هل أقدر أتخرج", "كيف أحسن معدلي" | AcademicAdvisorModule | Student |
| `material_qa` | "من المحاضرة", "من الكتاب", "from the lecture" | MaterialQAModule | Student, Doctor |
| `material_explanation` | "شرح مادة", "اشرح الكورس", "explain course" | MaterialExplanationModule | Student, Doctor |
| `regulation` | "اشرح اللائحة", "متطلبات التخرج", "graduation requirements" | RegulationModule | All |
| `result_query` | "درجاتي", "معدلي", "my GPA", "my grades" | ResultQueryModule | Student |
| `generate_exam` | "اعمل امتحان", "انشئ امتحان", "create exam" | ExamGenerationModule | Doctor, Admin |
| `assignment_query` | "واجباتي", "موعد التسليم", "assignment deadline" | AssignmentQueryModule | Student, Doctor |
| `backend_api_query` | "كام طالب", "how many students", "roadmap", "ايه المواد" | DynamicApiModule | All |
| `action_execute` | "سجلني في المواد", "enroll me" | DynamicApiModule | Student, Admin |
| `complaint_submit` | "عندي شكوى", "I want to complain" | ComplaintModule | Student |
| `complaint_summary` | "ملخص الشكاوى" | ComplaintModule | Doctor, Admin |
| `summarization` | "لخص", "summarize" | SummarizationModule | All |
| `file_extraction` | File URL in message | FileExtractionModule | All |
| `file_processing` | Bulk Excel/PDF | FileProcessorModule | Admin |
| `cv_analysis` | "حلل CV", "review my CV" | CVAnalysisModule | All |
| `general_chat` | Default fallback | LLM direct | All |

---

## 3. Layer-2 Deterministic Overrides

Fires AFTER LLM classification to correct Arabic dialect misclassifications:

| Detector Function | Key Keywords | Corrects To |
|------------------|-------------|------------|
| `_detect_study_plan` | "خطة مذاكرة", "جدول مذاكرة", "study plan", "اذاكر ايه" | `study_plan` |
| `_detect_generate_exam` | "اعمل امتحان", "create exam", "generate exam" | `generate_exam` |
| `_detect_regulation` | "اشرح اللائحة", "متطلبات التخرج", "الخطة الدراسية" | `regulation` |
| `_detect_material_qa` | "من المحاضرة", "من الكتاب", "from the lecture" | `material_qa` |
| `_detect_backend_query` | "كام طالب", "roadmap", "معدلي", "who am i" | `backend_api_query` |
| `_detect_assignment_query` | "واجباتي", "deadline", "تسليم" | `assignment_query` |

---

## 4. Module Details

### StudyPlanModule (`study_plan`)

Fetches 4 data sources **in parallel**, then generates a time-aware weekly study plan:

| Data Source | Endpoint | Data Retrieved |
|------------|----------|---------------|
| Roadmap | `GET /api/Regulations/my-roadmap` | GPA, semesters, mustRetake, recommendedNext |
| Overview | `GET /api/ai-tools/student-overview/{userId}` | Grades, exam history |
| Performance | `GET /api/analytics/student/{userId}/performance` | Per-subject attendance % + category |
| Assignments | `GET /api/assignments/offering/{id}` × N offerings | Upcoming deadlines sorted by urgency |

Context from .NET (injected by ChatService): `today` (YYYY-MM-DD), `dayOfWeek`, `studentName`, `enrolledSubjects`.

Output format:
```
📊 Quick status (GPA + enrolled count)
🚨 Urgent: exams/assignments in next 7 days with daily hour targets
📅 Weekly schedule (day-by-day blocks, Mon–Fri)
🎯 Subject priority ranking with reasons
💡 Subject-specific tips (weak/failing subjects)
🏆 Weekly motivational goal
```

### AcademicAdvisorModule v2 (`academic_advice`)

Three parallel sources + regulation RAG:

| Source | Data |
|--------|------|
| `GET /api/Regulations/my-roadmap` | GPA, curriculum status, mustRetake |
| `GET /api/ai-tools/student-overview/{userId}` | Grades, exam submissions |
| ChromaDB RAG (3 queries) | Regulation passages: graduation requirements, retake rules, GPA thresholds |

Output: 📊 Standing → ⚠️ Risk points → 🛣️ Roadmap → 📚 Study tips → 🎓 Graduation timeline → 💬 3 action steps.

### MaterialQAModule (`material_qa`)

Answers student questions grounded ONLY in indexed course material:
1. Embed user question via OpenAI `text-embedding-3-small`
2. ChromaDB top-5 semantic search (filtered by offeringId)
3. LLM answers strictly from retrieved chunks
4. Returns answer + source citations

### DynamicApiModule (`backend_api_query` / `action_execute`)

Discovers allowed .NET endpoints from Swagger schema, routes user intent to the correct API, executes, and narrates the result in natural language. Supports write actions (POST) with a confirmation step.

### ExamGenerationModule (`generate_exam`)

1. Resolves `subjectOfferingId` via pre-execution step if missing
2. Fetches course materials via RAG for context
3. Calls OpenRouter to generate structured question bank (MCQ + T/F + Short Answer)
4. Returns structured JSON for .NET to store

---

## 5. RAG Pipeline

```
Material/Regulation uploaded
    │
POST /api/rag/index (FastAPI)
    │
Extract text: PDF → pdfplumber, DOCX → python-docx, plain text
    │
Chunk (~500 tokens, 50-token overlap)
    │
OpenAI text-embedding-3-small → float[] per chunk
    │
ChromaDB: upsert (materialId_chunkN as doc ID)
    │
Material.ValidationStatus = Indexed

Query:
question → embed → ChromaDB.query(top_k=5) → chunks
→ Module grounds LLM answer in retrieved chunks (ZERO hallucination rule)
```

---

## 6. Memory System

- **Backend:** Redis 7
- **Scope:** Per `user_id`
- **Content:** Last 10 conversation turns (role + content)
- **Usage:** PlannerAgent resolves pronouns from history (e.g., "اشرحها" after discussing a regulation)
- **Compression:** Old turns summarized to save space

---

## 7. AI Grading

```
POST /api/ai/grade-submission
Body: { submission_text, assignment_title, description, rubric, max_grade }

OpenRouter → GPT-4o-mini → structured JSON:
{ "score": 82, "feedback": "...", "strengths": [...], "weaknesses": [...], "confidence": 0.87 }

Stored on AssignmentSubmission:
  Grade, AiFeedback, Strengths, Weaknesses, IsAiGraded=true, Status=Graded
```

---

## 8. RBAC (AI-level)

Defined in `app/core/rbac.py` — enforced by PlanExecutor before any module runs:

| Intent | Student | Doctor | Admin |
|--------|---------|--------|-------|
| study_plan | ✅ | ✅ | ✅ |
| academic_advice | ✅ | ✅ | ✅ |
| generate_exam | ❌ | ✅ | ✅ |
| complaint_summary | ❌ | ✅ | ✅ |
| file_processing | ❌ | ❌ | ✅ |
| all others | ✅ | ✅ | ✅ |

---

## 9. Startup Sequence

On FastAPI startup (`main.py` lifespan):
1. Validate required env vars (`BACKEND_BASE_URL`, `OPENROUTER_API_KEY`)
2. Initialize `ToolExecutionClient` (backend HTTP client with circuit breaker)
3. Fetch Swagger schema from .NET backend
4. Create `ModelRouter` (OpenRouter + fallback chain)
5. Assemble `MemoryStore`, `RateLimiter`, `PlannerAgent`, `PlanExecutor`, `ReactAgent`
6. Initialize `EmbeddingService` + `VectorStore` (ChromaDB)
7. Auto-index active regulations in background (non-blocking)
