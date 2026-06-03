# AI System

> **Last refreshed:** 2026-06-03 | **Service:** FastAPI (Python 3.13) | **Primary LLM:** OpenRouter → GPT-4o-mini | **Deployed on:** Railway

---

## Architecture Overview

```
╔══════════════════════════════════════════════════════════════════════════════════╗
║                          UNIVERSITY AI SYSTEM — FULL ARCHITECTURE               ║
╚══════════════════════════════════════════════════════════════════════════════════╝

  ┌─────────────┐        ┌─────────────────┐
  │   Frontend  │──────► │  .NET Backend   │
  │  (React)    │◄──────  │  ChatService    │
  └─────────────┘   HTTP  └────────┬────────┘
                                   │ POST /api/chat
                                   │ (user_id, role, message, history, academic_context)
                                   ▼
╔══════════════════════════════════════════════════════════════════════════════════╗
║                         FastAPI AI Service  (Railway)                           ║
║                                                                                  ║
║  ┌──────────────────────────────────────────────────────────────────────────┐   ║
║  │  STEP 1 — RATE LIMITER                                                   │   ║
║  │  30 req/min per user_id  │  Redis sliding window  │  429 if exceeded     │   ║
║  └──────────────────────────────────────────────────────────────────────────┘   ║
║                                      │                                           ║
║  ┌──────────────────────────────────────────────────────────────────────────┐   ║
║  │  STEP 2 — MEMORY LOAD  (Redis)                                           │   ║
║  │  conversation history  │  entity stack  │  active document  │  profile   │   ║
║  └──────────────────────────────────────────────────────────────────────────┘   ║
║                                      │                                           ║
║  ┌──────────────────────────────────────────────────────────────────────────┐   ║
║  │  STEP 3 — 5-LAYER INTENT CLASSIFIER  (PlannerAgent)                     │   ║
║  │                                                                          │   ║
║  │  Layer 0 ──► Pre-processor                                               │   ║
║  │              normalize Arabic, transliterate Arabizi (3→ع, 7→ح)         │   ║
║  │                          │                                               │   ║
║  │  Layer 1 ──► Embedding Fast-Path  (~65% of traffic, free)                │   ║
║  │              cosine similarity vs intent centroids                       │   ║
║  │              confidence ≥ 0.82 → ✅ skip LLM                            │   ║
║  │                          │ (if < 0.82)                                   │   ║
║  │  Layer 2 ──► LLM Classifier  (GPT-4o-mini, function-calling)             │   ║
║  │              returns: { intent, confidence, extracted_params }           │   ║
║  │                          │                                               │   ║
║  │  Layer 2b ─► Keyword Safety Net  (regex patterns)                        │   ║
║  │              catches: generate_exam / regulation / complaint_submit      │   ║
║  │                          │                                               │   ║
║  │  Layer 3 ──► Confidence Router                                           │   ║
║  │              ≥ 0.78 → execute  │  0.55–0.78 → clarify  │  < 0.55 → LLM │   ║
║  │                          │                                               │   ║
║  │  Layer 4 ──► Action Guard  (write ops only)                              │   ║
║  │              stores pending action in Redis, asks user to confirm        │   ║
║  │                          │                                               │   ║
║  │  Layer 5 ──► Conversation State Update                                   │   ║
║  │              extract entities → push to entity stack → pronoun resolve   │   ║
║  └──────────────────────────────────────────────────────────────────────────┘   ║
║                                      │                                           ║
║  ┌──────────────────────────────────────────────────────────────────────────┐   ║
║  │  STEP 4 — RBAC GATE  (app/core/rbac.py)                                 │   ║
║  │  is_allowed(intent, role)?  ──── ❌ NO → deny + bilingual error message  │   ║
║  │                             ──── ✅ YES → continue                       │   ║
║  └──────────────────────────────────────────────────────────────────────────┘   ║
║                                      │                                           ║
║          ┌───────────────────────────┴────────────────────────┐                 ║
║          │                                                     │                 ║
║  ┌───────▼────────────────────┐              ┌────────────────▼──────────────┐  ║
║  │  PATH A — ReactAgent       │              │  PATH B — Module Dispatch     │  ║
║  │  (complex / multi-step)    │              │  (direct / pre-defined)       │  ║
║  │                            │              │                               │  ║
║  │  GPT-4o-mini               │              │  _MODULE_CLASS_MAP lookup     │  ║
║  │  max 4 iterations:         │              │  → importlib.import_module()  │  ║
║  │                            │              │  → module.run(context)        │  ║
║  │  Think                     │              │                               │  ║
║  │    │                       │              │  Modules:                     │  ║
║  │  Call Tool ─────────────────────────────► │  ExamGenerationModule         │  ║
║  │    │         call_backend_api             │  AcademicAdvisorModule        │  ║
║  │    │         read_regulation_pdf          │  StudyPlanModule              │  ║
║  │    │         read_material_pdf            │  RegulationModule             │  ║
║  │    │         generate_exam                │  DynamicApiModule             │  ║
║  │    │         analyze_academic_profile     │  ComplaintModule              │  ║
║  │    │                                      │  MaterialQAModule             │  ║
║  │  See Result                               │  SummarizationModule          │  ║
║  │    │                                      │  FileProcessorModule          │  ║
║  │  Think again  (up to 4x)                  │  CVAnalysisModule             │  ║
║  │    │                                      │  ProgressIntelligenceModule   │  ║
║  │  Final Answer                             │  DoctorIntelligenceModule     │  ║
║  └────────────────────────────┘              │  + 12 more modules...         │  ║
║          │                                   └───────────────────────────────┘  ║
║          └───────────────────────────┬────────────────────────┘                 ║
║                                      │                                           ║
║  ┌──────────────────────────────────────────────────────────────────────────┐   ║
║  │  STEP 5 — RESULT NARRATION  (LLM)                                        │   ║
║  │  raw data (JSON) → role-aware system prompt → natural language response  │   ║
║  │  role prompts loaded from: app/prompts/role_student.md etc.              │   ║
║  └──────────────────────────────────────────────────────────────────────────┘   ║
║                                      │                                           ║
║  ┌──────────────────────────────────────────────────────────────────────────┐   ║
║  │  STEP 6 — MEMORY SAVE  (Redis)                                           │   ║
║  │  save turn  │  update entity stack  │  trigger auto-summarize (>12 turns)│   ║
║  └──────────────────────────────────────────────────────────────────────────┘   ║
║                                      │                                           ║
║                              ChatResponse                                        ║
║                    { response, intent, tool, model,                              ║
║                      suggestions, emotion, metadata }                            ║
╚══════════════════════════════════════════════════════════════════════════════════╝
                                       │
                    ┌──────────────────┼──────────────────┐
                    │                  │                  │
           ┌────────▼───────┐ ┌───────▼────────┐ ┌──────▼────────────┐
           │  OpenRouter    │ │  Redis         │ │  .NET Backend     │
           │  (LLM calls)   │ │  (Memory)      │ │  (Real Data)      │
           │                │ │                │ │                   │
           │  GPT-4o-mini   │ │  conversation  │ │  /api/students    │
           │  GPT-4o        │ │  entities      │ │  /api/grades      │
           │  Fallback M1   │ │  preferences   │ │  /api/exams       │
           │  Fallback M2   │ │  active_doc    │ │  /api/complaints  │
           │  ─────────     │ │  profile cache │ │  /api/materials   │
           │  HuggingFace   │ │  rate limiter  │ │  + 40 more APIs   │
           │  (BART local)  │ │  circuit state │ │                   │
           └────────────────┘ └───────┬────────┘ └──────┬────────────┘
                                      │                  │
                             ┌────────▼───────┐ ┌───────▼────────────┐
                             │  ChromaDB      │ │  Circuit Breaker   │
                             │  (Vector DB)   │ │                    │
                             │                │ │  CLOSED → normal   │
                             │  regulations   │ │  5 fails in 30s    │
                             │  materials     │ │       ↓            │
                             │  HNSW index   │ │  OPEN → fast-fail  │
                             │  cosine sim   │ │  wait 30s          │
                             │  top-5 search │ │       ↓            │
                             └────────────────┘ │  HALF-OPEN → test  │
                                                └────────────────────┘
```

---

## 1. What Is the AI System?

The AI system is a **standalone FastAPI microservice** that sits between the frontend and the .NET backend. It acts as an intelligent middleware: it understands what the user wants (in Arabic, English, or mixed), decides how to get it done, calls the right APIs, and returns a natural language response.

It is **not** a simple chatbot wrapper. It has its own memory, its own security layer, its own knowledge base (RAG), and its own multi-step reasoning engine.

```
User (Frontend)
      │
      ▼
  .NET Backend  ──────────────────────────────────────────────────┐
      │  forwards /api/chat request                                │
      ▼                                                            │
  FastAPI AI Service                                              │
      │                                                            │
      ├─ Understands the message (5-layer classification)          │
      ├─ Checks permissions (RBAC)                                 │
      ├─ Calls backend APIs to get real data  ────────────────────►│
      ├─ Searches PDF knowledge base (RAG)                         │
      ├─ Generates a natural language response (LLM)               │
      └─ Returns structured ChatResponse                           │
                                                                   │
      ◄──────────────────────────────────────────────────────────-─┘
```

---

## 2. Request Lifecycle (How One Message Gets Processed)

Every incoming message goes through this exact sequence:

```
1. Rate Limiter        → block abusive users (30 requests/min per user)
2. Memory Load         → load conversation history + user profile from Redis
3. Pre-processor       → normalize Arabic, transliterate Arabizi (3→ع, 7→ح)
4. Intent Classifier   → 5-layer pipeline (see Section 3)
5. RBAC Gate           → is this role allowed to do this intent?
6. ActionGuard         → for write ops (complaint, exam): ask user to confirm
7. Execution           → ReactAgent loop OR Module dispatch
8. Response Narration  → LLM converts raw data into natural language
9. Memory Save         → save turn to Redis, update entity tracking
10. Return ChatResponse → response + suggestions + emotion + metadata
```

---

## 3. Intent Classification — 5 Layers

This is the most critical component. It figures out what the user wants.

### Layer 0 — Pre-processor
- Detects language (Arabic / English / Mixed)
- Transliterates Arabizi: `"ana 3ayez"` → `"أنا عايز"`
- Normalizes Arabic: strips diacritics, unifies Alef forms (أ إ ا → ا)

### Layer 1 — Embedding Fast-Path (free, instant)
- Converts the message to a vector (mathematical meaning representation)
- Compares against pre-computed "intent centroids" (average of 15–20 example phrases per intent)
- If similarity ≥ 0.82 → **skip LLM entirely**, use this intent directly
- Handles **~65% of all traffic** at near-zero cost

### Layer 2 — LLM Classifier (GPT-4o-mini)
- Used when Layer 1 is below threshold
- Structured function-calling → model returns: `{ intent, confidence, extracted_params, goal_summary }`
- Handles ambiguous Arabic dialect, compound sentences, sarcasm

### Layer 2b — Keyword Safety Net (regex fallback)
- Runs after LLM for high-stakes intents where a wrong classification is costly
- `generate_exam`, `regulation`, `complaint_submit`, `result_query`, `assignment_query`
- Catches cases where creative user phrasing fools the LLM

### Layer 3 — Confidence Router
| Confidence | Action |
|-----------|--------|
| ≥ 0.78 | Execute the intent |
| 0.55 – 0.78 | Ask user to clarify (disambiguation) |
| < 0.55 | Fall back to general LLM response |

### Layer 4 — Action Guard
- For **write operations** (submit complaint, distribute exam): intercepts before execution
- Stores pending action in Redis (5-min TTL), asks user "Are you sure?"
- On next message: if user confirms → execute. If not → discard.

### Layer 5 — Conversation State Update
- Extracts entities from the message (subject name, doctor, semester)
- Updates the entity stack in Redis (last 3 entities, 2-hour TTL)
- Enables pronoun resolution: "اشرحها" → resolves to the last mentioned subject

---

## 4. Execution — Two Paths

After classification, the system executes via one of two paths:

### Path A — ReactAgent (Primary Path)
Used for: complex queries, multi-step reasoning, follow-up questions.

The ReAct (Reasoning + Acting) pattern: the LLM **thinks** → **calls a tool** → **sees the result** → **thinks again** → up to **4 iterations**.

Available tools:
| Tool | What It Does |
|------|-------------|
| `call_backend_api` | GET/POST to .NET backend (validated against endpoint whitelist) |
| `read_regulation_pdf` | RAG search over university regulation documents |
| `read_material_pdf` | Download + extract + explain a course material PDF |
| `generate_exam` | Full exam generation (Doctor/Admin only) |
| `analyze_academic_profile` | Deep GPA + graduation analysis (Student only) |

Example flow for "Compare my Networks grade with the class average":
```
Think → call_backend_api(/my-grades) → see result
Think → call_backend_api(/class-average/Networks) → see result
Think → compose comparison
Answer → "Your grade is 78, class average is 71. You're above average."
```

### Path B — Module Dispatch (Fallback Path)
Used for: straightforward intents, admin operations, pre-defined workflows.

| Intent | Module | Description |
|--------|--------|-------------|
| `generate_exam` | ExamGenerationModule | Generates MCQ / T/F / Short Answer exam bank |
| `summarization` | SummarizationModule | Summarizes text, PDF, or URL content |
| `complaint_submit` | ComplaintModule | Submits a student complaint |
| `complaint_summary` | ComplaintModule | Summarizes complaints for Doctor/Admin |
| `file_processing` | FileProcessorModule | Bulk Excel/PDF operations (Admin) |
| `cv_analysis` | CVAnalysisModule | Analyzes and critiques a CV |
| `academic_advice` | AcademicAdvisorModule | Full graduation readiness analysis |
| `material_explanation` | MaterialExplanationModule | Explains full course material |
| `regulation` | RegulationModule | Answers from regulation RAG |
| `backend_api_query` | DynamicApiModule | Auto-discovers and calls .NET APIs |
| `material_qa` | MaterialQAModule | Answers questions from course materials |
| `assignment_query` | AssignmentQueryModule | Fetches assignments and deadlines |
| `study_plan` | StudyPlanModule | Generates weekly study schedule |
| `academic_coach` | AcademicCoachModule | Motivational coaching + action items |
| `learning_assistant` | LearningAssistantModule | Adaptive learning support |
| `progress_report` | ProgressIntelligenceModule | Detailed progress analytics |
| `quiz_me` | QuizModule | Generates practice quizzes from material |
| `generate_flashcards` | FlashcardModule | Creates study flashcards |
| `generate_examples` | ExamplesModule | Generates worked examples |
| `generate_exercises` | ExercisesModule | Generates practice exercises |
| `doctor_analytics` | DoctorIntelligenceModule | Course analytics for Doctors |
| `doctor_risk_students` | DoctorIntelligenceModule | Students at risk of failing |
| `doctor_weak_topics` | DoctorIntelligenceModule | Weak topics across the class |
| `doctor_recommendations` | DoctorIntelligenceModule | Pedagogical recommendations |
| `general_chat` | LLM direct | Default fallback, no module |

---

## 5. Full Intent List (32 Intents)

```
general_chat          summarization         generate_exam
result_query          file_extraction       complaint_submit
complaint_summary     file_processing       cv_analysis
academic_advice       material_explanation  material_qa
regulation            backend_api_query     action_execute
assignment_query      study_plan            academic_coach
quiz_me               generate_flashcards   generate_examples
generate_exercises    progress_report       learning_assistant
doctor_analytics      doctor_risk_students  doctor_weak_topics
doctor_recommendations
```

---

## 6. RBAC — AI-Level Access Control

Defined in `app/core/rbac.py`. Runs **before** any module executes. Independent from the .NET backend's JWT — two separate security layers.

| Intent | Student | Doctor | Admin | SuperAdmin |
|--------|---------|--------|-------|-----------|
| `general_chat` | ✅ | ✅ | ✅ | ✅ |
| `result_query` | ✅ | ✅ | ✅ | ✅ |
| `academic_advice` | ✅ | ✅ | ✅ | ✅ |
| `material_explanation` | ✅ | ✅ | ✅ | ✅ |
| `regulation` | ✅ | ✅ | ✅ | ✅ |
| `study_plan` | ✅ | ✅ | ✅ | ✅ |
| `generate_exam` | ❌ | ✅ | ✅ | ✅ |
| `complaint_submit` | ✅ | ❌ | ✅ | ✅ |
| `complaint_summary` | ❌ | ✅ | ✅ | ✅ |
| `file_processing` | ❌ | ❌ | ✅ | ✅ |
| `doctor_analytics` | ❌ | ✅ | ✅ | ✅ |
| `doctor_risk_students` | ❌ | ✅ | ✅ | ✅ |
| `quiz_me` | ✅ | ❌ | ❌ | ✅ |
| `generate_flashcards` | ✅ | ❌ | ❌ | ✅ |

---

## 7. RAG System (Knowledge Base from PDFs)

RAG = Retrieval-Augmented Generation. Instead of making the LLM guess from training data, we retrieve the actual relevant text from university documents and give it to the LLM to answer from.

### Indexing Pipeline (runs at startup + on upload)
```
PDF / DOCX uploaded
      │
      ▼
Extract text  (pdfminer.six → pypdf fallback for scanned docs)
      │
      ▼
Split into chunks  (~512 tokens each, 64-token overlap)
      │
      ▼
Embed each chunk  (OpenAI text-embedding-3-small = 1536 dimensions)
      │
      ▼
Store in ChromaDB  (persistent vector database on disk)
      │
      ▼
Save MD5 hash to Redis  (so re-indexing is skipped if file unchanged)
```

### Query Pipeline (per user question)
```
User question
      │
      ▼
Embed the question  (same embedding model)
      │
      ▼
ChromaDB semantic search  (top-5 most similar chunks, filtered by type)
      │
      ▼
LLM answers ONLY from retrieved chunks  (zero-hallucination rule)
      │
      ▼
Response with source references
```

### What is indexed?
| Type | Filter | Used By |
|------|--------|---------|
| `regulation` | `where={"type": "regulation"}` | RegulationModule, AcademicAdvisorModule |
| `material` | `where={"type": "material", "offeringId": X}` | MaterialQAModule, MaterialExplanationModule |

---

## 8. Memory System

All memory is stored in **Redis** with automatic TTL expiry. Falls back to disk (JSON files) if Redis is down.

| Redis Key | Content | TTL |
|-----------|---------|-----|
| `user:{id}:conversation` | Full chat history (role + content turns) | 24 hours |
| `user:{id}:summary` | Compressed summary of old turns (auto-generated after 12 turns) | 24 hours |
| `user:{id}:entities` | Last 3 mentioned entities (subject, doctor, semester) | 2 hours |
| `user:{id}:preferences` | Language preference, interests | 7 days |
| `user:{id}:active_document` | Currently open PDF URL + type | 2 hours |
| `user:{id}:file_context` | Last file URL mentioned | 1 hour |
| `user:{id}:academic_profile` | Cached GPA, grades, courses (avoids repeated backend calls) | Session |
| `user:{id}:personalized_context` | Enriched profile (weakest subject, at-risk flags) | Session |
| `user:{id}:clarification` | Pending disambiguation options | 5 minutes |
| `user:{id}:pending_action` | Write operation awaiting user confirmation | 5 minutes |

**Active Document Tracking:** When a user opens a PDF, its URL is saved to `active_document`. If they later say "لخصه" (summarize it) or "اشرحه" (explain it), the system automatically retrieves the URL — no need to repeat it.

---

## 9. Model Routing — Which LLM for What?

| Intent | Model | Reason |
|--------|-------|--------|
| `summarization` | HuggingFace BART (local, free) | Specialized summarization model |
| `generate_exam` | GPT-4o (OpenRouter) | Quality-critical: exam questions must be correct |
| `material_explanation` | GPT-4o (OpenRouter) | Faculty-grade output expected |
| `material_qa` | GPT-4o (OpenRouter) | Accuracy over cost |
| `file_processing` (Admin) | GPT-4o (OpenRouter) | Large bulk operations |
| Everything else | GPT-4o-mini (OpenRouter) | Best cost/quality balance |

**Fallback chain:** Primary model → `OPENROUTER_FALLBACK_MODEL_1` → `OPENROUTER_FALLBACK_MODEL_2` → Local HuggingFace

---

## 10. API Endpoints

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| `POST` | `/api/chat` | Main chat endpoint | Bearer JWT |
| `POST` | `/api/chat/stream` | Streaming response (SSE) | Bearer JWT |
| `POST` | `/generate-exam` | Direct exam generation | Bearer JWT |
| `POST` | `/api/ai/analyze-complaint` | AI complaint analysis | Bearer JWT |
| `POST` | `/api/ai/grade-submission` | AI grading of assignment | Bearer JWT |
| `POST` | `/api/rag/upsert` | Index a document into ChromaDB | Internal |
| `DELETE` | `/api/rag/material/{id}` | Remove document from ChromaDB | Internal |
| `GET` | `/api/rag/stats` | Vector store stats | Internal |
| `GET` | `/api/memory/{user_id}/conversation` | Get conversation history | Internal |
| `DELETE` | `/api/memory/{user_id}` | Clear all user memory | Internal |
| `GET` | `/health` | Health check (Redis, ChromaDB, circuit breaker) | Open |

### Chat Request / Response

```json
// POST /api/chat — Request
{
  "user_id": "string",
  "role": "student | doctor | admin | superadmin",
  "message": "string",
  "conversation_id": "string (optional, UUID)",
  "history": [{"role": "user", "content": "..."}, ...],
  "academic_context": {
    "userId": "...",
    "studentId": "...",
    "studentName": "...",
    "GPA": 2.8,
    "courses": [...]
  },
  "explain": false
}

// POST /api/chat — Response
{
  "response": "string (natural language)",
  "conversation_id": "string",
  "intent_executed": "result_query",
  "tool_used": "GetStudentGrades",
  "model_used": "openai/gpt-4o-mini",
  "suggestions": ["Check my GPA", "Show weak subjects"],
  "actions_available": [],
  "emotion": "joy | trust | anticipation | ...",
  "metadata": { "planner_duration_ms": 310, "explain": false }
}
```

### Streaming (SSE) Frame Format

```
data: {"type": "thinking"}
data: {"type": "token", "content": "مرحباً"}
data: {"type": "token", "content": " يا"}
data: {"type": "meta", "intent": "general_chat", "suggestions": [...]}
data: {"type": "done"}
data: {"type": "error", "message": "..."}
```

---

## 11. Infrastructure & Reliability

### Rate Limiting
- **30 requests/minute** per `user_id`
- Implemented as a Redis sliding window sorted set
- Returns `HTTP 429` with `retry_after_seconds` when exceeded

### Circuit Breaker (Backend Protection)
- Monitors all calls to the .NET backend
- After **5 consecutive failures** → circuit **opens** (fast-fail, no waiting)
- After **30 seconds** → one probe request allowed (half-open)
- If probe succeeds → circuit **closes** (normal operation resumes)
- State is stored in Redis (shared across all FastAPI workers)

### Connection Pool
- Single shared `httpx.AsyncClient` for all .NET backend calls
- Max 100 connections, 20 kept alive between requests
- Eliminates 150–200ms TCP/TLS setup overhead per request

### Timeouts
| Call | Timeout |
|------|---------|
| OpenRouter (LLM) | 45 seconds |
| .NET backend | 30 seconds |
| Full request (`agent.run`) | 60 seconds |

### Prompt Security
- User messages are wrapped in `<USER_MESSAGE>` tags before sending to LLM
- Prompt injection patterns detected and logged (e.g., "ignore previous instructions")
- Response guard checks for hallucinated specifics (numbers/facts not in context)

---

## 12. Startup Sequence

When the FastAPI service starts (before accepting any requests):

```
1. Validate required env vars (OPENROUTER_API_KEY, BACKEND_BASE_URL)
2. Initialize shared httpx client pool (100 connections to .NET)
3. Connect to Redis (tries internal URL → public URL → host/port → disk fallback)
4. Initialize EmbeddingService (OpenAI API → sentence-transformers → TF-IDF fallback)
5. Initialize VectorStore (ChromaDB on disk → in-memory fallback)
6. Initialize RateLimiter (Redis sliding window)
7. Create ModelRouter (OpenRouter client, fallback chain)
8. Create ReactAgent + PlannerAgent + PlanExecutor (wired together)
9. Run RegulationIndexer in background:
   - Fetch regulation PDFs from .NET backend
   - Check MD5 hash (skip if unchanged)
   - Chunk → embed → upsert to ChromaDB
10. Server ready → start accepting requests
```

---

## 13. Role-Specific Behavior

The AI responds differently based on the authenticated user's role. System prompts are loaded from `app/prompts/role_*.md` files.

| Role | Tone | Focus |
|------|------|-------|
| **Student** | Friendly mentor ("مرشد"), uses student's name | Grades, study help, materials, assignments |
| **Doctor** | Peer-to-peer academic, professional | Analytics, exam generation, course insights |
| **Admin** | Executive summary first, headline stats | System overview, anomalies, bulk operations |
| **SuperAdmin** | TL;DR + full deep-dive capability | Technical + strategic view |

Suggestions after each response are also role-specific (deterministic lookup, zero LLM cost).

---

## 14. File Processing

Supported file types for upload/processing:

| Format | Library | Notes |
|--------|---------|-------|
| PDF | pdfminer.six → pypdf (fallback) | Fallback handles scanned PDFs |
| DOCX | python-docx | Paragraph-by-paragraph extraction |
| XLSX / XLS | openpyxl | Cell-by-cell, joined with ` \| ` |
| Images | Pillow | OCR hint flagging |
| Plain text | Built-in | Direct UTF-8 decode |

---

## 15. AI Grading

```
POST /api/ai/grade-submission
Body: {
  "submission_text": "student's answer",
  "assignment_title": "...",
  "description": "...",
  "rubric": "...",
  "max_grade": 100
}

LLM (GPT-4o-mini) returns:
{
  "score": 82,
  "feedback": "Well structured but missing X...",
  "strengths": ["Clear explanation", "Good examples"],
  "weaknesses": ["Missing Y", "Needs more depth on Z"],
  "confidence": 0.87
}

Stored on .NET: Grade, AiFeedback, IsAiGraded=true, Status=Graded
```

---

## 16. Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `OPENROUTER_API_KEY` | ✅ | LLM API access |
| `BACKEND_BASE_URL` | ✅ | .NET backend URL |
| `REDIS_URL` | Recommended | Redis connection (falls back to disk) |
| `OPENAI_API_KEY` | Recommended | High-quality embeddings (falls back to sentence-transformers) |
| `OPENROUTER_MODEL` | Optional | Default: `openai/gpt-4o-mini` |
| `OPENROUTER_FALLBACK_MODEL_1` | Optional | First fallback model |
| `RATE_LIMIT_RPM` | Optional | Default: `30` requests/minute |
| `REQUEST_TIMEOUT_SECONDS` | Optional | Default: `60` |
| `EMBEDDING_CLASSIFIER_ENABLED` | Optional | Default: `true` |
| `EMBEDDING_HIGH_CONFIDENCE_THRESHOLD` | Optional | Default: `0.82` |
| `ACTION_GUARD_ENABLED` | Optional | Default: `true` |
| `ALLOWED_ORIGINS` | Optional | Comma-separated CORS origins |
