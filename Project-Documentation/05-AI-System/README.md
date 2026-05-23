---
layout: default
title: "🤖 AI System"
---

# 🤖 AI System — Complete Technical Reference

> **Covers every function, every keyword set, every prompt, every routing rule, every Pydantic model, and every security boundary in the FastAPI AI microservice.**

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Pydantic Data Models (schemas.py)](#2-pydantic-data-models-schemaspy)
3. [PlannerAgent (planner.py)](#3-planneragent-plannerpy)
   - 3.1 Valid Intents
   - 3.2 Keyword Sets — Verbatim
   - 3.3 _detect_generate_exam() — Two-Pass Algorithm
   - 3.4 _detect_backend_query() — Full Keyword Dictionary
   - 3.5 _SYSTEM_PROMPT — Full Text + Rule Annotations
   - 3.6 PlannerAgent.run() — 6-Step Flow
   - 3.7 _call_planner_model() — Model + Message Format
   - 3.8 _parse_plan() — Validation + Safety Guards
   - 3.9 _fallback_plan() — Always-Valid Fallback
   - 3.10 _ensure_resolve_step() — Deterministic Guard
   - 3.11 Layer-2 Override Logic
   - 3.12 MemoryStore Protocol
4. [DynamicApiModule (dynamic_api.py)](#4-dynamicapimodule-dynamic_apipy)
   - 4.1 _ROUTING_PROMPT — University Hierarchy + All 17 Categories
   - 4.2 _SUMMARY_PROMPT — All 10 Response Quality Rules
   - 4.3 DynamicApiModule.run() — Complete Retry Loop
   - 4.4 _pick_endpoint() — Failure-History Injection
   - 4.5 _substitute_placeholders() — 9 Placeholder Mappings
   - 4.6 _answer_from_context() — API-Less Answer
   - 4.7 _summarize() — Narrative Generation
   - 4.8 _graceful_failure() — Multi-Layer Fallback
5. [API Discovery & RBAC (api_discovery.py)](#5-api-discovery--rbac-api_discoverypy)
   - 5.1 Global State Variables
   - 5.2 _BLOCKED_METHODS — Absolute Denial List
   - 5.3 _BLOCKED_PREFIXES — Path Prefix Blacklist
   - 5.4 _SAFE_POST_PATHS — Allowed POST Whitelist
   - 5.5 _is_allowed() — Decision Function
   - 5.6 fetch_and_filter_schema() — Schema Download + Caching
   - 5.7 _PRIORITY_SEGMENTS — Relevance Boosting
   - 5.8 get_allowed_endpoints_schema() — Schema Retrieval
   - 5.9 validate_endpoint() — Path-Parameter-Aware Validation
6. [Security Boundaries](#6-security-boundaries)
7. [Memory System](#7-memory-system)
8. [Academic Context Injection](#8-academic-context-injection)
9. [Hallucination Prevention](#9-hallucination-prevention)
10. [End-to-End Flow: Student Asks a Question](#10-end-to-end-flow-student-asks-a-question)

---

## 1. Architecture Overview

```
┌───────────────────────────────────────────────────────────────────┐
│                     FastAPI AI Microservice                        │
│                  (Railway — separate deployment)                   │
│                                                                    │
│  ┌─────────────┐     ┌───────────────────┐                        │
│  │PlannerAgent │────▶│ Intent + Plan JSON │                        │
│  │(planner.py) │     └────────┬──────────┘                        │
│  └─────────────┘              │                                    │
│                               │ intent = "backend_api_query"       │
│                               ▼                                    │
│  ┌─────────────────────────────────────────┐                      │
│  │        DynamicApiModule                 │                      │
│  │        (dynamic_api.py)                 │                      │
│  │                                         │                      │
│  │  ┌──────────────┐   ┌────────────────┐ │                      │
│  │  │_pick_endpoint│   │validate_endpnt │ │                      │
│  │  │  (LLM call)  │──▶│(api_discovery) │ │                      │
│  │  └──────────────┘   └───────┬────────┘ │                      │
│  │                             │ allowed   │                      │
│  │                             ▼           │                      │
│  │                   ┌─────────────────┐   │                      │
│  │                   │  Backend C# API │   │                      │
│  │                   │  (PostgreSQL)   │   │                      │
│  │                   └────────┬────────┘   │                      │
│  │                            │ raw JSON   │                      │
│  │                            ▼           │                      │
│  │                   ┌─────────────────┐   │                      │
│  │                   │  _summarize()   │   │                      │
│  │                   │  (LLM → text)   │   │                      │
│  │                   └─────────────────┘   │                      │
│  └─────────────────────────────────────────┘                      │
└───────────────────────────────────────────────────────────────────┘
```

**Key design decisions:**
- **Two-layer intent detection:** LLM (Layer 1) + deterministic keyword override (Layer 2)
- **Retry loop (MAX_ATTEMPTS=3):** Failed API attempts are fed back to LLM to pick different endpoint
- **Clean Architecture boundary:** AI microservice never touches the database directly — only the C# backend API
- **JWT forwarding:** The user's auth token is forwarded as-is to the backend, so backend RBAC enforces access
- **Schema caching:** Swagger schema downloaded once at startup, cached in memory, AI sees filtered view only

---

## 2. Pydantic Data Models (schemas.py)

These are the canonical data contracts used throughout the AI system. Every LLM output is validated against these models.

### AgentInput

```python
class AgentInput(BaseModel):
    message: str = Field(..., description="The user's raw message or goal")
    user_id: Optional[str] = Field(None, description="The origin user's ID for context")
    auth_header: Optional[str] = Field(None, description="Forwarded JWT for backend auth")
    context: Dict[str, Any] = Field(default_factory=dict, description="Any existing context for the agent")
```

**What goes in `context`:**
- `role` — "student" | "doctor" | "admin" | "superadmin"
- `history` — list of `{"role": "user"|"assistant", "content": "..."}` (last 6 turns used)
- `academic_context` — dict with userId, profileId, studentId, batchId, departmentId, etc.
- `selected_model` — model ID override (default: "openai/gpt-4o-mini")
- `explain` — bool: if True, appends explain_text to the response
- `debug` — bool: if True, returns endpoint, attempts, timing in response data
- `auth_header` — JWT Bearer token forwarded to backend

---

### AgentOutput

```python
class AgentOutput(BaseModel):
    status: str = Field(default="success", description="Status of the agent execution")
    response: str = Field(..., description="Natural language response or final summary")
    data: Optional[Dict[str, Any]] = Field(None, description="Any raw data to pass continuously")
```

**Status values:**
- `"success"` — normal completion
- `"partial"` — answer from context when backend failed
- `"failed"` — complete failure, apology returned

**data keys returned by DynamicApiModule:**
- `endpoint_called` — which endpoint was used
- `method_called` — GET or POST
- `raw_backend_data` — the full JSON from the backend
- `suggestions` — 3 follow-up question suggestions
- `actions_available` — same as suggestions (alias)
- `debug_info` — only populated when `debug=True`

---

### ExecutionStep

```python
class ExecutionStep(BaseModel):
    step_id: int       # Order of execution (1, 2, 3...)
    action: str        # "tool" | "model" | "agent_module"
    tool_name: Optional[str]    # e.g. "GetStudentGrades"
    model_name: Optional[str]   # e.g. "claude-3-sonnet"
    module_name: Optional[str]  # e.g. "ExamGenerationModule"
    input_payload: Dict[str, Any]   # Parameters for the action
    depends_on: List[int]           # step_ids this step waits for
    condition: Optional[str]        # Optional execution condition
```

Multi-step example: `{{step_1.output}}` in `input_payload` references previous step output.

---

### ExamParams

```python
class ExamParams(BaseModel):
    collegeName: Optional[str]      # e.g. "Faculty of Engineering"
    departmentName: Optional[str]   # e.g. "Computer Science"
    batchName: Optional[str]        # e.g. "Batch 2022"
    subjectName: Optional[str]      # e.g. "Data Structures"
    numberOfQuestions: Optional[int]    # default 10 if not specified
    examType: Optional[Literal["midterm", "final"]]     # default "midterm"
    variationMode: Optional[Literal["same_for_all", "different_per_student"]]
    subjectOfferingId: Optional[str]   # ULID — if None, triggers ResolveSubjectOffering
```

**Critical rule:** If `subjectOfferingId` is `None`, `_ensure_resolve_step()` automatically injects a `ResolveSubjectOffering` pre-execution step. The ExamGenerationModule never receives an incomplete plan.

---

### PreExecutionStep

```python
class PreExecutionStep(BaseModel):
    tool: str               # Tool name: "ResolveSubjectOffering"
    reason: str             # Human-readable why this step is needed
    input_payload: Dict[str, Any]   # e.g. {"subjectName": "Data Structures"}
```

Pre-execution steps run **before** the main ExecutionPlan steps. Currently used only for `ResolveSubjectOffering` when generating exams without a known `subjectOfferingId`.

---

### ExecutionPlan

```python
class ExecutionPlan(BaseModel):
    goal_summary: str           # "Generate midterm exam for Data Structures"
    intent: Optional[str]       # "generate_exam" | "backend_api_query" | ...
    steps: List[ExecutionStep]  # MUST be [] for general_chat
    is_executable: bool = True  # False = plan is advisory only
    exam_params: Optional[ExamParams]       # Populated when intent == "generate_exam"
    pre_execution_steps: List[PreExecutionStep]  # Runs before steps[]
```

---

## 3. PlannerAgent (planner.py)

**File:** `f:\fastApi\app\agents\planner.py` (685 lines)

The PlannerAgent is the **first component** to process every user message. It classifies intent, extracts parameters, and builds an `ExecutionPlan` that tells the orchestrator what to do next.

---

### 3.1 Valid Intents

```python
VALID_INTENTS = {
    "general_chat",         # Conversation, greetings, non-data questions
    "summarization",        # Summarize a document or text
    "generate_exam",        # Create/generate a university exam
    "result_query",         # Academic results, grades, GPA, transcripts, schedules
    "file_extraction",      # Extract info from uploaded file (no bulk ops)
    "complaint_submit",     # Student submitting a complaint
    "complaint_summary",    # Admin/doctor reviewing complaints
    "file_processing",      # Bulk upload Excel/PDF (students/grades)
    "cv_analysis",          # Analyze student CV for skills + recommendations
    "academic_advice",      # Personalized GPA/course recommendations
    "material_explanation", # Explain/summarize real course material from backend
    "material_qa",          # RAG-powered Q&A grounded strictly in course material chunks
    "backend_api_query",    # Query any university data via dynamic backend routing
}

_FALLBACK_INTENT = "general_chat"
```

---

### 3.2 Keyword Sets — Verbatim

#### English Exam Creation Keywords (`_EXAM_KEYWORDS_EN`)

```python
_EXAM_KEYWORDS_EN: frozenset[str] = frozenset({
    "generate exam",    "create exam",     "make exam",
    "build exam",       "write exam",      "prepare exam",
    "prepare test",     "create test",     "generate test",
    "make test",        "build test",      "write test",
    "exam for subject", "exam for course", "new exam",
    "draft exam",       "design exam",     "set exam",
    "set a test",       "produce exam",    "develop exam",
})
```

#### English Exam Action Verbs (`_EXAM_ACTION_VERBS_EN`)

```python
_EXAM_ACTION_VERBS_EN: frozenset[str] = frozenset({
    "create", "generate", "make", "build", "write",
    "prepare", "draft", "design", "produce", "develop",
    "compose", "set",
})
```

#### English Exam Target Words (`_EXAM_TARGET_WORDS_EN`)

```python
_EXAM_TARGET_WORDS_EN: frozenset[str] = frozenset({"exam", "test", "quiz", "assessment"})
```

#### Material Q&A Keywords (`_MATERIAL_QA_KEYWORDS`)

```python
_MATERIAL_QA_KEYWORDS: frozenset[str] = frozenset({
    # Arabic — course-grounded questions
    "من المحاضرة", "من المادة", "في المحاضرة", "في المادة",
    "اللي في المحاضرة", "اللي في المادة", "من ملف",
    # English — course-grounded questions
    "explain from lecture", "explain from material", "from the lecture",
    "from the material", "from lecture", "from material",
    "according to the lecture", "based on the lecture",
    "what does the lecture say", "what does the material say",
})
```

---

#### Arabic Exam Keywords (`_EXAM_KEYWORDS_AR`)

```python
_EXAM_KEYWORDS_AR: frozenset[str] = frozenset({
    "اعمل امتحان",  "انشئ امتحان",  "سوي امتحان",
    "حضّر امتحان",   "اكتب امتحان",  "امتحان لمادة",
    "عمل امتحان",    "أنشئ امتحان",  "صمّم امتحان",
    "عايز امتحان",   "نعمل امتحان",  "جهّز امتحان",
    "جهّز اختبار",  "عمل اختبار",   "انشئ اختبار",
    "طوّر امتحان",  "اكتب اختبار",  "حضر امتحان",
    "انشئ امتحان",  "صمم امتحان",
})
```

---

### 3.3 _detect_generate_exam() — Two-Pass Algorithm

```python
def _detect_generate_exam(message: str) -> bool:
```

**Purpose:** Deterministic fallback that fires when the LLM misclassifies an exam-creation request as `general_chat`.

**Pass 1 — Direct phrase substring match:**
```
msg = message.strip().lower()
for kw in _EXAM_KEYWORDS_EN:  # 20 English phrases
    if kw in msg: return True
for kw in _EXAM_KEYWORDS_AR:  # 20 Arabic phrases
    if kw in msg: return True
```

**Pass 2 — Loose verb+target word match (handles inserted adjectives):**
```
words = set(re.findall(r"\b\w+\b", msg))
has_action = bool(words & _EXAM_ACTION_VERBS_EN)   # any creation verb
has_target = bool(words & _EXAM_TARGET_WORDS_EN)   # any exam target word
if has_action and has_target: return True
```

**Example: Pass 2 catches "create a new introduction to ML exam"**
- `words` contains "create" (action) and "exam" (target) → True

**What it NEVER fires on (by design):**
- "view exam", "exam results", "I failed the exam", "when is the exam?"
- These lack a creation action verb → `has_action` is False

---

### 3.4 _detect_backend_query() — Full Keyword Dictionary

```python
def _detect_backend_query(message: str) -> bool:
```

**Purpose:** Catches data queries that the LLM incorrectly classified as `general_chat`.

**Algorithm:** `msg = message.strip().lower()` → substring search across `_BACKEND_KEYWORDS` set.

**Complete keyword dictionary (verbatim):**

```python
_BACKEND_KEYWORDS = {
    # Arabic: regulation / roadmap queries
    "لائحة", "لوائح", "خطة دراسية", "خارطة طريق",
    "مواد الترم", "مواد الفصل", "مواد السنة",
    "المواد اللي هسجلها", "المواد المقترحة", "ايه المواد",
    "كام ساعة خلصت", "ساعات معتمدة", "الساعات الباقية",
    "رسبت في", "مواد راسب", "مواد باقية", "مواد خلصتها",
    "تقدمي الأكاديمي", "وضعي الأكاديمي", "هل انا في المسار",
    "الترم الجاي", "المواد القادمة", "ايه اللي باقيلي",
    "roadmap", "academic plan", "study plan", "academic progress",
    "credit hours", "remaining subjects", "passed subjects",
    "failed subjects", "next semester subjects", "what subjects",

    # Arabic: enrollment ACTIONS (register/enroll me)
    "سجلني", "سجل لي", "سجل لى", "اسجلني", "عايز أسجل",
    "عايز اسجل", "ابدأ التسجيل", "ابدا التسجيل",
    "سجلني في المواد", "سجل في كل المواد", "سجلني في الترم",
    "تسجيل المواد", "تسجيل في المواد", "اعملي تسجيل",
    "register me", "enroll me", "sign me up", "auto enroll",
    "register for courses", "enroll in courses",

    # Arabic: count / analytics
    "كم عدد", "كام بدرس", "كام طالب", "كام دكتور", "كام مادة",
    "عدد الدكاترة", "عدد الطلاب", "عدد المواد", "عدد الاقسام",
    "نسبة", "احصائيات", "إحصائيات", "احصاء", "إحصاء",
    "تحليل", "تقرير", "ملخص الشكاوى", "ملخص النتائج",
    "مين هم", "قائمة", "اللي بيدرس", "اللي مسجل",
    "اعلى", "أعلى", "اقل", "أقل", "افضل", "أفضل",

    # Arabic: filter / relationship queries
    "دكاترة في", "طلاب في", "مواد في", "عرض في",
    "في قسم", "في كلية", "في الفرقة", "في الدفعة",
    "بيدرس في", "مسجل في", "تابع ل",

    # Arabic: entity names
    "كليات", "الكليات", "دكاترة", "الدكاترة",
    "قسم", "اقسام", "الأقسام", "الاقسام",
    "طلاب", "الطلاب", "مواد", "المواد",
    "فرقة", "دفعة", "الفرقة", "الدفعة",
    "عروض", "العروض", "التسجيلات",

    # Arabic: identity / profile
    "اسمي", "اسم", "انا مين", "أنا مين",
    "من انا", "من أنا", "مين انا", "مين أنا",
    "معلوماتي", "بياناتي", "بروفايلي", "حسابي",
    "كليتي", "قسمي", "دفعتي", "فرقتي",

    # Arabic: system data
    "بيانات", "جامعه", "جامعة", "السيستم",

    # English: count / analytics
    "how many", "count of", "number of", "total students",
    "total doctors", "total courses", "how much",
    "statistics", "analytics", "distribution", "breakdown",
    "top students", "at risk", "failing students",
    "most enrolled", "most popular", "average gpa",

    # English: filter / relationship queries
    "doctors in", "students in", "courses in", "offerings in",
    "in department", "in college", "in batch", "in year",
    "who teaches", "enrolled in", "assigned to",
    "list doctors", "list students", "list courses",

    # English: list / show
    "list of", "show me", "what are", "give me",
    "students list", "doctors list", "departments",

    # English: my data
    "my courses", "my subjects", "my schedule", "my grades",
    "my gpa", "my results", "my profile", "my info",
    "my college", "my department", "my batch",
    "who am i", "my name", "my account", "my details",
    "profile", "courses i have", "subjects i have",
}
```

---

### 3.5 _SYSTEM_PROMPT — Full Text + Rule Annotations

The system prompt is sent to `openai/gpt-4o-mini` as the first message in every classification request. Below is the complete prompt with inline annotations.

```
You are an AI Planning Agent for a university management system.

Your job is to classify the user's request and return a structured JSON plan.

## Valid Intents
- general_chat       — conversation, questions, greetings, anything not needing backend data
- backend_api_query  — MANDATORY for querying system stats, counting numbers (كم عدد), user lists, or any database retrieval.
- summarization      — summarise a document or text
- generate_exam      — generate a university exam (doctor/admin only)
- result_query       — query academic results, grades, GPA, transcripts, schedules
- file_extraction    — extract information from an uploaded file (no bulk ops)
- complaint_submit   — student submitting a complaint or feedback about a doctor/exam/grade
- complaint_summary  — admin/doctor requesting a summary of submitted complaints
- file_processing    — bulk upload of Excel (students/grades) or PDF summarization via fileUrl
- cv_analysis        — analyzing a student CV to extract skills and give recommendations
- academic_advice    — personalized academic recommendations based on GPA and enrolled courses
- material_explanation — explain or summarize real course material fetched from the backend

## Output Schema (return ONLY this JSON, no markdown, no extra text)
{
  "intent": "<one of the valid intents>",
  "goal_summary": "<one clear sentence describing what the user wants>",
  "is_executable": true,
  "exam_params": null,
  "pre_execution_steps": [],
  "steps": []
}

## Rules

### Rule 1 — general_chat
- steps MUST be [] (empty array). Never add steps for general_chat.
- exam_params MUST be null.
- Use this intent for greetings, explanations, advice, and any question
  that does not require fetching real student/exam data from the backend.

### Rule 2 — Tool-bound intents (summarization, result_query, file_extraction, generate_exam)
- You MAY include steps when multiple sequential backend calls are needed.
- Available tools: ResolveSubjectOffering, GetStudentResults, GetStudentGrades,
  GetGPASummary, GetTranscript, GetSchedule, GetSubjectOfferings,
  GetCourseEnrollments, GenerateExam, DistributeExam, SubmitComplaint,
  GetComplaints, GetStudentAcademicSummary, BulkCreateStudents,
  BulkUploadGrades, GetMaterials
- Use {{step_N.output}} to reference the output of step N in a later step.

### Rule 3 — generate_exam (HIGHEST PRIORITY INTENT FOR EXAM CREATION)
English triggers:
  "create exam", "generate exam", "make exam", "build exam",
  "write exam",  "prepare exam",  "prepare test", "new exam",
  "draft exam",  "design exam",  "set exam",    "produce exam",
  "exam for subject", "exam for course", "create test", "generate test"

Arabic triggers:
  "اعمل امتحان", "انشئ امتحان", "سوي امتحان", "حضّر امتحان",
  "اكتب امتحان", "امتحان لمادة", "عمل امتحان", "جهّز امتحان"

Rules:
- Role does NOT affect intent classification. Even if role=student, use generate_exam.
- numberOfQuestions defaults to 10 if not specified.
- examType defaults to "midterm" if not specified.
- If subjectOfferingId is unknown, add ResolveSubjectOffering to pre_execution_steps.

### Rule 4 — Context-aware auto-fill (MANDATORY)
- Extract userId, studentId, courseId, subjectOfferingId, departmentId, batchId,
  collegeName, departmentName, batchName from academic_context.
- NEVER ask the user for parameters already present in academic_context.
- NEVER leave userId or studentId blank when they exist in academic_context.

### Rule 5 — complaint_submit (student only)
- Use when a student reports a problem about a doctor, exam, grade, or the system.
- targetType MUST be: "Doctor" | "Exam" | "Grade" | "Other"
- If role is NOT "student" → use general_chat instead.

### Rule 6 — complaint_summary (admin/doctor only)
- If role is "student" → use general_chat instead.

### Rule 7 — file_processing
- Use when message contains a fileUrl OR mentions bulk file operations.
- Do NOT use for single-file text extraction (use file_extraction).

### Rule 8 — cv_analysis
- Use when user wants CV reviewed, analyzed, or wants job-readiness feedback.

### Rule 9 — academic_advice
- Use when student asks for study advice, course recommendations, or GPA improvement.

### Rule 10 — material_explanation (STRICT DATA-FIRST — HIGHEST PRIORITY INTENT)
- ALWAYS use when user asks to EXPLAIN, SUMMARIZE, DESCRIBE, UNDERSTAND, or REVIEW
  a specific subject, course, topic, or lecture.
- The subjectOfferingId MUST be injected from academic_context.
- If subjectOfferingId not available: set goal_summary to clarification message, leave steps=[].
- NEVER use general_chat for these triggers.
- NEVER add tool steps — MaterialExplanationModule handles the backend fetch internally.

### Rule 11 — backend_api_query
- Use for ANY question requesting data from the university system.

### Rule 12 — Identity & Profile Queries → ALWAYS backend_api_query
Arabic triggers: "انا مين", "أنا مين", "مين انا", "مين أنا",
  "من انا", "من أنا", "اسمي ايه", "اسمي إيه",
  "معلوماتي", "بياناتي", "بروفايلي", "حسابي"

English triggers: "who am i", "what is my name", "my profile", "my info",
  "my account", "my details"

### Rule 13 — When in doubt → use general_chat with steps=[].
```

---

### 3.6 PlannerAgent.run() — 6-Step Flow

```python
async def run(self, agent_input: AgentInput) -> AgentOutput:
```

**Step 1 — Optional memory context:**
```python
if self.memory:
    past = await self.memory.get_context(agent_input.user_id)
    if past:
        memory_prefix = f"[Conversation summary]: {past}\n\n"
```

**Step 2 — Extract context components:**
```python
ctx = agent_input.context or {}
role = ctx.get("role", "user")
raw_history: list[dict] = ctx.get("history", [])
academic_ctx: dict = ctx.get("academic_context", {})
```

**Step 3 — Build academic_context auto-fill note:**
```python
safe_keys = [
    "userId", "studentId", "courseId", "subjectOfferingId",
    "departmentId", "batchId", "collegeName", "departmentName", "profileId",
]
relevant = {k: v for k, v in academic_ctx.items() if k in safe_keys and v}
auto_fill_note = f"\nAvailable context for auto-filling parameters: {json.dumps(relevant)}"
```

**Step 4 — Build structured history turns (last 3 pairs = 6 messages):**
```python
history_turns: list[dict] = []
for turn in raw_history[-6:]:    # Last 6 messages only
    turn_role = turn.get("role", "user")
    turn_content = str(turn.get("content", ""))
    if turn_role in ("user", "assistant") and turn_content:
        history_turns.append({"role": turn_role, "content": turn_content})
```

**Step 5 — Compose user content and call LLM:**
```python
user_content = (
    f"{memory_prefix}"
    f"User role: {role}\n"
    f"User message: {agent_input.message}"
    f"{auto_fill_note}"
)
raw_json = await self._call_planner_model(history_turns, user_content)
```

**Step 6 — Parse, validate, apply Layer-2 overrides, return:**
```python
plan = self._parse_plan(raw_json, agent_input)
if plan.intent == "general_chat" and _detect_generate_exam(agent_input.message):
    plan.intent = "generate_exam"
    ...
if plan.intent == "general_chat" and _detect_backend_query(agent_input.message):
    plan.intent = "backend_api_query"
    ...
plan = self._ensure_resolve_step(plan)
return AgentOutput(status="success", response=plan.goal_summary, data={"plan": plan})
```

---

### 3.7 _call_planner_model() — Model + Message Format

**Model:** `openai/gpt-4o-mini`

**Response format:** `{"type": "json_object"}` — forces the model to return valid JSON

**Message structure sent to LLM:**
```
[
  {"role": "system",    "content": _SYSTEM_PROMPT},
  {"role": "user",      "content": "<turn 1 from history>"},
  {"role": "assistant", "content": "<turn 1 response>"},
  ...up to 6 history turns...
  {"role": "user",      "content": "User role: student\nUser message: ...\nAvailable context: {...}"}
]
```

**Error handling:**
- `json.JSONDecodeError` → logs error, returns `None`
- Any exception → logs with `exc_info=True`, returns `None`
- Empty response from model → logs warning, returns `None`
- `None` returned → `_parse_plan()` triggers `_fallback_plan()`

---

### 3.8 _parse_plan() — Validation + Safety Guards

**Guard 1 — None input:** Returns `_fallback_plan()`

**Guard 2 — Unknown intent:** Downgrades to `_FALLBACK_INTENT = "general_chat"`

**Guard 3 — Hard rule: general_chat must never have steps or exam context:**
```python
if intent == "general_chat":
    raw["steps"] = []
    raw["exam_params"] = None
```

**Guard 4 — Non-list steps sanitization:**
```python
if not isinstance(raw.get("steps"), list):
    raw["steps"] = []
```

**Guard 5 — Pydantic validation:**
```python
try:
    plan = ExecutionPlan(**raw)
    return plan
except (ValidationError, TypeError):
    return self._fallback_plan(agent_input.message)
```

---

### 3.9 _fallback_plan() — Always-Valid Fallback

```python
@staticmethod
def _fallback_plan(message: str) -> ExecutionPlan:
    return ExecutionPlan(
        intent=_FALLBACK_INTENT,      # "general_chat"
        goal_summary=f"Handle the user's request: {message[:120]}",
        is_executable=True,
    )
```

Always valid: `intent="general_chat"` in `VALID_INTENTS`, `steps=[]` (default), `exam_params=None` (default).

---

### 3.10 _ensure_resolve_step() — Deterministic Guard

**Trigger:** `intent == "generate_exam" AND exam_params.subjectOfferingId is None`

**Action:** Appends to `plan.pre_execution_steps`:
```python
PreExecutionStep(
    tool="ResolveSubjectOffering",
    reason="subjectOfferingId is required to generate the exam but was not provided by the user",
    input_payload={"subjectName": plan.exam_params.subjectName},
)
```

Checks for existing `ResolveSubjectOffering` in pre_execution_steps to avoid duplicates.

---

### 3.11 Layer-2 Override Logic

Fires **only** when `plan.intent == "general_chat"`:

**Override 1 — Exam creation:**
```python
if plan.intent == "general_chat" and _detect_generate_exam(agent_input.message):
    plan.intent = "generate_exam"
    plan.goal_summary = f"Generate an exam for: {agent_input.message[:120]}"
    if plan.exam_params is None:
        plan.exam_params = ExamParams(
            subjectName=None,
            numberOfQuestions=10,
            examType="midterm",
            variationMode="same_for_all",
        )
```

**Override 2 — Backend data query:**
```python
if plan.intent == "general_chat" and _detect_backend_query(agent_input.message):
    plan.intent = "backend_api_query"
    plan.goal_summary = "Query dynamic backend APIs to answer the user request."
```

**Override 3 — RAG material Q&A:**
```python
if plan.intent == "general_chat" and _detect_material_qa(agent_input.message):
    plan.intent = "material_qa"
    plan.goal_summary = "Answer question grounded strictly in course material chunks via RAG."
```

`_detect_material_qa()` fires when the message contains any phrase from `_MATERIAL_QA_KEYWORDS` (substring match on lowercased message). This ensures course-specific questions always route to the MaterialQAModule rather than general chat.

If the LLM correctly identified a specific intent (not `general_chat`), Layer-2 does NOT fire.

---

### 3.12 MemoryStore Protocol

```python
class MemoryStore(Protocol):
    async def get_context(self, user_id: str | None) -> str: ...
```

Structural subtyping (duck typing). Any class implementing `async def get_context()` satisfies it. The backend stores AI memory in the `AiMemory` PostgreSQL table.

---

## 4. DynamicApiModule (dynamic_api.py)

**File:** `f:\fastApi\app\modules\dynamic_api.py` (879 lines)

Handles all `backend_api_query` intents. Selects endpoint via LLM, validates it, calls the C# backend, narrates results.

---

### 4.1 _ROUTING_PROMPT — University Hierarchy + All 17 Categories

**University Hierarchy (hardcoded in prompt):**
```
University
  └── College  (e.g. "كلية الحاسبات", "Faculty of Engineering")
        └── Department  (e.g. "قسم الذكاء الاصطناعي", "CS Department")
              └── Batch  (a year group, e.g. "2024", "الفرقة الرابعة")
                    ├── Subject  (course catalogue entry, e.g. "Data Structures")
                    │     └── SubjectOffering  (a specific semester instance)
                    │           ├── Doctor  (who teaches this offering)
                    │           └── Enrollment  (which students are enrolled)
                    └── Group  (e.g. "group A", "group 1")

KEY RELATIONSHIPS:
- A Doctor teaches SubjectOfferings (not Subjects directly)
- A Student is enrolled in SubjectOfferings via Enrollments
- SubjectOffering links: Subject + Doctor + Semester + Group + Batch
- To find "doctors of batch X" → get SubjectOfferings for that batch, extract doctors
- To find "students in subject Y" → get Enrollments by offering
```

**Step 1 — Context-First Check (ALWAYS do this first):**
```
If answer is ALREADY in academic_context → return: {"endpoint": "", "method": "GET", "params": {}}
Already-known fields: collegeName, departmentName, batchName, studentName, gpa
```

**Step 2 — Route by category:**

#### A — IDENTITY / MY PROFILE
```
Keywords: "انا مين", "اسمي", "معلوماتي", "بياناتي", "بروفايلي",
          "who am i", "my profile", "my name"

- student → GET /api/Gpa/my-gpa
- doctor  → GET /api/SubjectOfferings/my-offerings
- admin   → GET /api/Admins/{profileId}

⛔ /api/Students/{code} and /api/Doctors/{code} use SHORT CODE strings, NEVER ULIDs.
```

#### B — DOCTOR QUERIES
```
Keywords: "دكتور", "دكاترة", "الدكاترة", "أستاذ", "مدرس", "بيدرس",
          "doctor", "doctors", "faculty", "professor", "who teaches"

B1. ALL DOCTORS:       → GET /api/Doctors  params: page=1, size=20
B2. BY DEPARTMENT:     → GET /api/Doctors/filter  params: departmentId={departmentId}, page=1, size=50
B3. BY COLLEGE:        → GET /api/Doctors/filter  params: collegeId={collegeId}, page=1, size=50
B4. OF A BATCH/YEAR:   → GET /api/SubjectOfferings/by-semester/{semesterId}
                         OR GET /api/Subjects/by-batch/{batchId}
B5. WHO TEACHES SUBJECT: → GET /api/Subjects/search  params: name={subject_name}
B6. WORKLOAD:          → GET /api/Doctors/filter  params: page=1, size=100
B7. SPECIFIC DOCTOR:   → GET /api/Doctors/search  params: q={name_or_code}
```

#### C — STUDENT QUERIES
```
Keywords: "طالب", "طلاب", "الطلاب", "طلبة", "المسجلين", "student", "students", "enrolled"

C1. ALL STUDENTS:      → GET /api/Students  params: page=1, size=20
C2. BY BATCH:          → GET /api/Students/filter  params: batchId={batchId}, page=1, size=50
C3. BY DEPARTMENT:     → GET /api/Students/filter  params: departmentId={departmentId}, page=1, size=50
C4. BY COLLEGE:        → GET /api/Students/filter  params: collegeId={collegeId}, page=1, size=50
C5. IN A OFFERING:     → GET /api/Enrollments/by-offering/{offeringId}
C6. MY ENROLLMENTS:    → GET /api/SubjectOfferings/my-enrollments  (student only)
                         ⛔ NEVER use /my-offerings for students (403)
C7. SEARCH:            → GET /api/Students/search  params: q={name_or_code}
C8. AT-RISK:           → GET /api/Students/filter  params: page=1, size=50
```

#### D — SUBJECT / COURSE QUERIES
```
Keywords: "مادة", "مواد", "subject", "course", "curriculum"

D1. MY SUBJECTS (student):   → GET /api/SubjectOfferings/my-enrollments
D2. MY SUBJECTS (doctor):    → GET /api/SubjectOfferings/my-offerings
D3. BY BATCH:                → GET /api/Subjects/by-batch/{batchId}
D4. BY DEPARTMENT:           → GET /api/Subjects/by-department/{departmentId}
D5. BY COLLEGE:              → GET /api/Subjects/by-college/{collegeId}
D6. SEARCH:                  → GET /api/Subjects/search  params: name={query}
```

#### E — SUBJECT OFFERING QUERIES
```
Keywords: "offering", "سكشن", "شعبة", "الفصل الدراسي", "by semester"

E1. BY SEMESTER:    → GET /api/SubjectOfferings/by-semester/{semesterId}
E2. DOCTOR'S OWN:   → GET /api/SubjectOfferings/my-offerings
E3. STUDENT'S:      → GET /api/SubjectOfferings/my-enrollments
```

#### F — ENROLLMENT / REGISTRATION ACTIONS
```
Keywords: "تسجيل", "مسجل", "enrolled", "سجلني", "سجل لي", "register me", "enroll me"

F1. MY ENROLLMENTS:    → GET /api/Enrollments/my-enrollments
F2. BY OFFERING:       → GET /api/Enrollments/by-offering/{offeringId}

F3. AUTO-ENROLL (student action) — ⚡ USE THIS when student wants to REGISTER for courses:
  Arabic: "سجلني في المواد", "سجلني في المواد المتاحة", "سجل لي كل المواد",
          "عايز أسجل مواد", "سجلني", "اعملي تسجيل", "ابدأ التسجيل",
          "سجل في كل المواد", "سجلني في الترم ده"
  English: "register me for courses", "enroll me in subjects",
           "sign me up for courses", "auto enroll"
  → POST /api/Enrollments/auto-enroll  (NO body needed — JWT identifies student)
  method: "POST", params: {}
  ⛔ STUDENT ROLE ONLY.
  ✅ Backend automatically finds all open offerings for the student's batch/dept/group.
```

#### G — ANALYTICS / COUNTS / AGGREGATIONS
```
Keywords: "كام", "عدد", "كم", "توزيع", "أكتر", "تحليل", "إحصاء",
          "how many", "count", "total", "distribution", "most", "analytics"

G1. SYSTEM-WIDE SUMMARY:    → GET /api/Analytics/summary
G2. STUDENT COUNT BY BATCH: → GET /api/Analytics/student-count-by-batch
G3. STUDENT COUNT BY DEPT:  → GET /api/Analytics/student-count-by-department
G4. DOCTOR WORKLOAD:        → GET /api/Analytics/doctor-workload  params: departmentId= (optional)
G5. MOST ENROLLED SUBJECTS: → GET /api/Analytics/top-enrolled-subjects  params: top=10
G6. OFFERING STATS:         → GET /api/Analytics/offering-enrollment-stats
G7. FULL STRUCTURE:         → GET /api/Colleges/full-structure
G8. SIMPLE DASHBOARD:       → GET /api/Dashboard
```

#### H — GRADES / GPA
```
Keywords: "درجة", "درجات", "GPA", "نتيجة", "grade", "result", "transcript"

- student own GPA:    → GET /api/Gpa/my-gpa
- specific student:   → GET /api/Gpa/student/{studentId}
```

#### I — SCHEDULE / TIMETABLE
```
Keywords: "جدول", "محاضرة", "موعد", "schedule", "timetable", "today", "النهارده", "بكرا"

FOR STUDENT:
  Today     → GET /api/Schedule/batch/{batchId}/today
  Tomorrow  → GET /api/Schedule/batch/{batchId}/day/{dayNum}  (dayNum=(today+1)%7, Sun=0)
  Full week → GET /api/Schedule/batch/{batchId}

FOR DOCTOR:
  Today     → GET /api/Schedule/my-today
  Full week → GET /api/Schedule/my-schedule
  ⛔ NEVER use batch schedule for doctor role
```

#### J — EXAMS
```
Keywords: "امتحان", "اختبار", "exam", "quiz"

My exams      → GET /api/Exams/my-exams
By offering   → GET /api/Exams/by-offering/{offeringId}
Enrolled exams → GET /api/Exams/my-enrolled-exams
```

#### K — ATTENDANCE
```
Keywords: "حضور", "غياب", "attendance", "absent"
→ GET /api/Attendance/student/{studentId}/report
```

#### L — COMPLAINTS
```
Keywords: "شكوى", "شكاوى", "complaint"

Admin view   → GET /api/Complaints/all
Doctor view  → GET /api/Complaints/my-reports
Student      → GET /api/Complaints/my-complaints
```

#### M — MATERIALS / LECTURE FILES
```
Keywords: "ملف", "محاضرة", "material", "lecture file", "مادة تعليمية"
→ GET /api/Materials/by-offering/{offeringId}
```

#### N — STRUCTURE (COLLEGES / DEPARTMENTS / BATCHES)
```
All colleges          → GET /api/Colleges
Full structure        → GET /api/Colleges/full-structure
Departments           → GET /api/Departments
By college            → GET /api/Departments/by-college/{collegeId}
Batches               → GET /api/Batches
By department         → GET /api/Batches/by-department/{departmentId}
Groups                → GET /api/Groups
By batch              → GET /api/Groups/by-batch/{batchId}

⚠️ /api/Colleges/by-code/{code} expects SHORT STRING CODE (e.g. "ENG"), NEVER a ULID.
```

#### O — DASHBOARD / SYSTEM STATS
```
Keywords: "dashboard", "احصائيات", "overview", "stats", "نظرة عامة"
→ GET /api/Dashboard
```

#### P — ACADEMIC YEARS / SEMESTERS
```
Keywords: "سنة دراسية", "فصل دراسي", "academic year", "semester"
→ GET /api/AcademicYears
By academic year → GET /api/Semesters/by-academic-year/{academicYearId}
```

#### Q — REGULATION / ACADEMIC ROADMAP (HIGHEST PRIORITY FOR STUDENTS)
```
Keywords: "لائحة", "لوائح", "خطة دراسية", "خارطة طريق", "regulation",
          "roadmap", "academic plan", "study plan",
          "مواد الترم", "مواد الفصل", "مواد السنة",
          "المواد اللي هسجلها", "المواد المقترحة", "ايه المواد",
          "كام ساعة خلصت", "ساعات معتمدة", "الساعات الباقية",
          "رسبت في", "مواد راسب", "مواد باقية", "مواد خلصتها",
          "هل انا في المسار", "تقدمي الأكاديمي", "وضعي الأكاديمي",
          "الترم الجاي", "المواد القادمة", "ايه اللي باقيلي",
          "credit hours", "remaining subjects", "passed subjects",
          "academic progress", "what subjects", "next semester subjects",
          "failed subjects", "must retake"

Q1. STUDENT'S FULL ACADEMIC ROADMAP (primary for almost ALL regulation questions):
  ⚡ Use for: subjects by semester, academic progress, credit hours,
     recommended next semester, failed/passed subjects, لائحة details.
  → GET /api/Regulations/my-roadmap
  method: "GET", params: {}
  ✅ JWT-aware — no params needed. Returns full personalized roadmap.
  ⛔ Student role ONLY.

Q2. SPECIFIC STUDENT'S REGULATION (Admin/Doctor):
  → GET /api/Regulations/student/{studentId}

Q3. BY DEPARTMENT (Admin):
  → GET /api/Regulations/by-department/{departmentId}

Q4. ALL REGULATIONS (Admin):
  → GET /api/Regulations
```

**Parameter Injection Rules (in routing prompt):**
```
- Inject known IDs from academic_context into params (batchId, departmentId, etc.)
- For filter endpoints: put IDs in "params" (query string), NOT path.
- For path params: substitute the real value directly into the URL string.
- NEVER leave a {placeholder} in the final endpoint string.
- NEVER use a ULID where a short code is expected.
- For paginated lists: page=1 and size=20 (or size=50 for analytics).
- Omit params with no value — never send empty strings.
```

**Fail Safe:**
```
If no rule matches → return: {"endpoint": "", "method": "GET", "params": {}}
NEVER hallucinate an endpoint. NEVER include markdown. JSON only.
```

---

### 4.2 _SUMMARY_PROMPT — All 10 Response Quality Rules

**Variables injected:** `{user_message}`, `{method}`, `{endpoint}`, `{role}`, `{academic_context}`, `{raw_response}` (truncated to 3000 chars)

**Rule 0a — Academic Roadmap Results — HIGHEST PRIORITY:**
```
Extract exactly what the user asked about from /api/Regulations/my-roadmap:

"ايه مواد الترم الثاني؟"
  → "مواد الترم الثاني في لائحتك هي: Data Structures (3 ساعات)، Algorithms (3 ساعات)، ..."

"كام ساعة خلصت؟"
  → "أنهيت حتى الآن X ساعة معتمدة من أصل Y ساعة — تبقّى Z ساعة."

"المواد اللي رسبت فيها؟"
  → List mustRetake OR subjects with status=="failed" with gradeLetter

"المواد المقترحة الترم الجاي؟"
  → List recommendedNext

"هل أنا في المسار الصح؟"
  → Compare passedSubjects/totalSubjects, GPA, mustRetake count
  → Honest, encouraging assessment

❌ NEVER dump the full JSON — always focus on what user actually asked.
```

**Rule 0b — Action Results (POST responses) — HIGHEST PRIORITY:**
```
POST responses are ACTION RESULTS, not data queries.

auto-enroll result: {"enrolled": 5, "alreadyHad": 2, "enrolledSubjects": [...]}
→ "تم تسجيلك بنجاح في 5 مواد جديدة: Data Structures، Algorithms، ...
   كنت مسجلاً مسبقاً في 2 مادة."

❌ NEVER say "no data found" for a POST result.
```

**Rule 1 — Counts & Analytics:** Always state numbers explicitly.

**Rule 2 — Lists:** Summarize meaningfully. `"الدكاترة في قسم AI هم: د. أحمد علي (Data Structures)، ..."`

**Rule 3 — Doctor Lists:** Always include: name + department + subject(s) they teach.

**Rule 4 — Student Lists:** Include: name + batch + enrollment status.

**Rule 5 — Empty Data:** `"لم يتم العثور على بيانات لهذا الطلب."` then suggest alternatives.

**Rule 6 — Analytics:** Rank, compare, highlight insights. `"أكثر دكتور لديه مواد هو د. X بـ 5 مواد."`

**Rule 7 — Language matching:** Arabic question → Arabic answer, English → English.

**Rule 8 — Use student's name** if available in academic_context.

**Rule 9 — Never invent data.** If a number isn't in the JSON, don't state it.

**Rule 10 — Pagination awareness:** If `totalCount > size`, mention "يوجد المزيد — اطلب الصفحة التالية."

**Output format:**
```json
{
    "narrative": "<full natural answer in the user's language>",
    "suggestions": ["<follow-up 1>", "<follow-up 2>", "<follow-up 3>"],
    "explain_text": "<data source explanation>"
}
```

---

### 4.3 DynamicApiModule.run() — Complete Retry Loop

```python
MAX_ATTEMPTS = 3
```

**Setup (before loop):**
```python
role, selected_model, explain_mode, debug_mode = ... (from context)
raw_ac = ctx.get("academic_context", {})
academic_ctx = json.dumps(raw_ac, ensure_ascii=False)
schema_text = get_allowed_endpoints_schema()
failed_attempts: list[dict] = []
```

**For attempt in 1..3:**

```
Step 1 — Route:
  route = await _pick_endpoint(..., failed_attempts)  ← history injected
  if route is None: break  (JSON parse error — stop)

Step 2 — Context shortcut:
  if endpoint == "": return _answer_from_context(...)  (no API call)

Step 3 — Placeholder substitution:
  endpoint, error = _substitute_placeholders(endpoint, raw_ac)
  if error: failed_attempts.append({...}); continue

Step 4 — Allowlist validation:
  if not validate_endpoint(method, endpoint): failed_attempts.append({...}); continue

Step 5 — Clean params + pagination:
  clean_params = {k:v for k,v in params.items() if v not in ("", None)}
  if GET: clean_params.setdefault("page", 1); clean_params.setdefault("size", 10)

Step 6 — Execute backend call:
  GET  → backend_client.fetch(route, auth_header, params=clean_params)
  POST → backend_client.post(route, payload=clean_params, auth_header)

Step 7 — Evaluate response:
  _error == "unauthorized" (403) → failed_attempts.append("403 Forbidden..."); continue
  _error == "not_found" (404)    → failed_attempts.append("404 Not Found..."); continue
  not raw_data (empty)           → record as candidate, continue
  real data                      → break ✅
```

**Post-loop:**
```
last_raw_data is None   → _graceful_failure()
not last_raw_data       → "مش لاقي أي بيانات مطابقة لطلبك."
else                    → _summarize() → AgentOutput
```

**Debug mode** (when `debug=True`) adds to `data.debug_info`:
```json
{
  "endpoint": "...", "method": "GET",
  "attempts": 2,
  "failed_attempts": [...],
  "execution_time_seconds": 1.234,
  "intent_detected": "backend_api_query"
}
```

---

### 4.4 _pick_endpoint() — Failure-History Injection

When `failed_attempts` is non-empty, appends to the routing prompt:
```
⚠️ PREVIOUS ATTEMPTS THAT FAILED (do NOT repeat these):
  - GET /api/Dashboard → FAILED: empty result — try a broader or different endpoint
  - GET /api/Students  → FAILED: 403 Forbidden — this endpoint requires a higher role.
Pick a DIFFERENT endpoint that avoids the same failure modes.
```

Model call: `generate_with_messages()` with `response_format={"type": "json_object"}`

Returns `{"endpoint": "...", "method": "GET", "params": {...}}` or `None` on parse error.

---

### 4.5 _substitute_placeholders() — 9 Placeholder Mappings

```python
def _substitute_placeholders(self, endpoint: str, raw_ac: dict) -> tuple[str, str]:
    # Returns (resolved_endpoint, error_string)
```

**User code resolution (tries 5 keys in order):**
```python
user_code = (
    raw_ac.get("userCode") or raw_ac.get("doctorCode")
    or raw_ac.get("studentCode") or raw_ac.get("universityId")
    or raw_ac.get("staffId") or ""
)
```

**9 substitutions:**
```python
substitutions = {
    "{userId}":    raw_ac.get("userId", ""),
    "{profileId}": raw_ac.get("profileId", ""),
    "{studentId}": raw_ac.get("studentId") or raw_ac.get("userId", ""),
    "{doctorId}":  raw_ac.get("doctorId") or raw_ac.get("profileId", ""),
    "{batchId}":   raw_ac.get("batchId", ""),
    "{offeringId}": raw_ac.get("subjectOfferingId", ""),
    "{id}":        raw_ac.get("profileId") or raw_ac.get("userId", ""),
    "{code}":      user_code or raw_ac.get("profileId") or raw_ac.get("userId", ""),
    "{userCode}":  user_code,
}
```

If `{` still in endpoint after all substitutions → error "could not resolve placeholders in '...'" → retry.

---

### 4.6 _answer_from_context() — API-Less Answer

Called when `endpoint=""` (answer already in context).

System prompt:
```
"Answer using ONLY the academic context provided.
Be warm, concise, and natural. Match the student's language.
NEVER invent data not present in the context."
```

Returns `AgentOutput(data={"source": "academic_context"})`.

---

### 4.7 _summarize() — Narrative Generation

```python
async def _summarize(...) -> tuple[str, list, str]:
    # Returns (narrative, suggestions, explain_text)
```

Raw data truncated: `json.dumps(raw_data, ensure_ascii=False)[:3000]`

Returns `("تمت العملية بنجاح.", [], "")` on any exception — always returns valid tuple.

---

### 4.8 _graceful_failure() — Multi-Layer Fallback

**Layer 1:** Tries to answer from context using a custom LLM prompt that includes the full list of failed attempts. Status: `"partial"`.

**Layer 2:** If Layer 1 LLM call itself fails:
```python
AgentOutput(
    status="failed",
    response=(
        "أنا آسف، في مشكلة مؤقتة. حاول تاني بعد شوية.\n"
        "(Temporary issue — please try again.)"
    ),
)
```

---

## 5. API Discovery & RBAC (api_discovery.py)

**File:** `f:\fastApi\app\core\api_discovery.py` (248 lines)

The security gateway between the AI and the C# backend. Downloads Swagger, filters to safe endpoints only, validates every AI-selected endpoint before execution.

---

### 5.1 Global State Variables

```python
_cached_schema: Optional[str] = None
_allowed_endpoints: Set[Tuple[str, str]] = set()
```

Populated once at application startup by `fetch_and_filter_schema()`. In-memory only — no Redis, no disk.

---

### 5.2 _BLOCKED_METHODS — Absolute Denial List

```python
_BLOCKED_METHODS = {"delete", "put", "patch"}
```

AI can **never** call DELETE, PUT, or PATCH — regardless of path or role. Only GET and (selective) POST are permitted.

---

### 5.3 _BLOCKED_PREFIXES — Path Prefix Blacklist

```python
_BLOCKED_PREFIXES = (
    "/api/auth",          # Authentication — AI never handles login/logout
    "/api/dev",           # Developer/debug routes
    "/api/ai",            # Prevent self-loop (orchestrator calling itself)
    "/api/auditlogs",     # Internal audit — not for AI queries
    "/api/notification",  # Push notifications — not an AI tool
)
```

Blocked for **all** methods including GET.

---

### 5.4 _SAFE_POST_PATHS — Allowed POST Whitelist

```python
_SAFE_POST_PATHS = (
    # Exams
    "/api/exams",
    "/api/exams/generate-ai",
    "/api/exams/upload-pdf",
    "/api/exams/grade-submission",
    "/api/exams/",              # covers /api/exams/{id}/submit, /api/exams/{id}/auto-grade

    # Complaints (via ai-tools)
    "/api/ai-tools/create-complaint",
    "/api/ai-tools/distribute-exams",
    "/api/ai-tools/bulk-create-students",
    "/api/ai-tools/bulk-upload-grades",

    # Attendance
    "/api/attendance/sessions",
    "/api/attendance/check-in",

    # Enrollment
    "/api/enrollments/",
    "/api/enrollments/auto-enroll",
    "/api/enrollment/upload",

    # GPA recalculate
    "/api/gpa/student/",

    # Grades
    "/api/grades/calculate/",
    "/api/grades/",

    # Files
    "/api/file/upload",
    "/api/studentfiles/upload",
    "/api/materials/upload",

    # Students bulk
    "/api/students/bulk-upload-direct",
    "/api/students/bulk-upload-ai",
    "/api/students/import-excel",
)
```

Matching: `path_lower.startswith(safe.lower())` — prefix match.

---

### 5.5 _is_allowed() — Decision Function

```python
def _is_allowed(path: str, method: str) -> bool:
    method = method.lower()
    path_lower = path.lower()

    if method in _BLOCKED_METHODS:        # Rule 1: block destructive
        return False
    for prefix in _BLOCKED_PREFIXES:      # Rule 2: block forbidden prefixes
        if path_lower.startswith(prefix):
            return False
    if method == "get":                    # Rule 3: GET allowed if not blocked
        return True
    if method == "post":                   # Rule 4: POST must match safe prefix
        for safe in _SAFE_POST_PATHS:
            if path_lower.startswith(safe.lower()):
                return True
        return False
    return False                           # Rule 5: anything else → deny
```

---

### 5.6 fetch_and_filter_schema() — Schema Download + Caching

```python
async def fetch_and_filter_schema() -> None:
```

Called once at FastAPI app startup.

1. GET `{BACKEND_BASE_URL}/swagger/v1/swagger.json` (verify=False, timeout=15s)
2. Iterate all `(path, method)` pairs → `_is_allowed()` filter
3. Extract `summary` and `parameters` (query + path only)
4. Store `(method.upper(), path)` in `allowed_set`
5. Build line: `"- GET /api/Students → Get all students. [page, size, departmentId]"`
6. If path contains any `_PRIORITY_SEGMENTS` → `priority_lines` (shown first to LLM)
7. `_cached_schema = "\n".join(priority_lines + other_lines)`
8. `_allowed_endpoints = allowed_set`

On error: `_cached_schema = "Backend API schema currently unavailable."`

---

### 5.7 _PRIORITY_SEGMENTS — Relevance Boosting

```python
_PRIORITY_SEGMENTS = (
    "students", "doctors", "departments", "offerings", "enrollments",
    "analytics", "stats", "colleges", "batches", "subjects", "complaints",
    "grades", "gpa", "schedule", "materials",
)
```

Endpoints with these path segments appear first in the schema string, improving LLM routing accuracy for the most common queries.

---

### 5.8 get_allowed_endpoints_schema() — Schema Retrieval

```python
def get_allowed_endpoints_schema() -> str:
    if _cached_schema is None:
        return "Backend API schema not loaded. Tell user to try again later."
    return _cached_schema
```

Called by `DynamicApiModule` at the start of every routing decision.

---

### 5.9 validate_endpoint() — Path-Parameter-Aware Validation

```python
def validate_endpoint(method: str, endpoint: str) -> bool:
```

**Algorithm:**
```python
target_parts = endpoint.strip("/").split("/")
# "/api/Students/01HXYZ" → ["api", "Students", "01HXYZ"]

for allowed_method, allowed_path in _allowed_endpoints:
    if allowed_method != method.upper(): continue

    allowed_parts = allowed_path.strip("/").split("/")
    # "/api/Students/{code}" → ["api", "Students", "{code}"]

    if len(target_parts) != len(allowed_parts): continue

    match = True
    for tp, ap in zip(target_parts, allowed_parts):
        if ap.startswith("{") and ap.endswith("}"):
            continue    # ← placeholder matches ANY value
        if tp.lower() != ap.lower():
            match = False; break

    if match:
        return True   # ALLOWED

return False   # BLOCKED
```

**Example:**
- Target: `GET /api/Students/01HXYZ123`
- Allowed: `GET /api/Students/{code}`
- Match: `"api"=="api"` ✅, `"students"=="students"` ✅, `"01HXYZ123"` matches `{code}` ✅ → ALLOWED

**Empty allowlist fallback** (Swagger not loaded): Returns `True` and lets the C# backend enforce RBAC via JWT.

---

## 6. Security Boundaries

Four independent security layers:

| Layer | Where | Blocks |
|-------|-------|--------|
| 1. Blocked Methods | `_is_allowed()` | DELETE, PUT, PATCH — AI can never modify/delete data |
| 2. Blocked Prefixes | `_is_allowed()` | Auth routes, dev routes, self-loop, audit, notifications |
| 3. Allowlist Validation | `validate_endpoint()` | Hallucinated endpoints not in Swagger |
| 4. JWT Forwarding | `backend_client` | Role violations — C# backend is the final authority |

Each layer catches different attack vectors:
- **Layer 1:** Prompt injection trying to get AI to modify data
- **Layer 2:** Infrastructure route exposure
- **Layer 3:** Endpoint hallucination by the LLM
- **Layer 4:** Privilege escalation even if layers 1-3 somehow passed

---

## 7. Memory System

### Short-term Memory (per request)
```python
raw_history = ctx.get("history", [])
history_turns = raw_history[-6:]   # Last 6 messages (3 user + 3 assistant)
```

Sent as structured `messages[]` to the planner model.

### Long-term Memory (cross-session)
- **Table:** `AiMemory` in PostgreSQL
- **Protocol:** `MemoryStore.get_context(user_id) -> str`
- **Injected as:** `"[Conversation summary]: {past}\n\n"` prepended to classification user content
- **Failure:** Memory lookup failure is caught and logged — memory is advisory, never blocking

---

## 8. Academic Context Injection

### Safe Keys Exposed to LLM (PlannerAgent)
```python
safe_keys = [
    "userId",            # SystemUser.Id (ULID)
    "studentId",         # Student.Id (ULID)
    "courseId",          # Current subject course ID
    "subjectOfferingId", # Current subject offering ID
    "departmentId",      # User's department ID
    "batchId",           # Student's batch ID
    "collegeName",       # Human-readable college name
    "departmentName",    # Human-readable department name
    "profileId",         # Admin/Doctor profile ID
]
```

Never exposed: passwords, raw tokens, internal system IDs, audit fields.

### Context-First Optimization
Before any API call, routing prompt checks if answer is already in context:
```
Already-known fields (no API call needed):
  collegeName, departmentName, batchName, studentName, gpa
→ return {"endpoint": "", "method": "GET", "params": {}}
→ _answer_from_context() responds without an API round-trip
```

---

## 9. Hallucination Prevention

| Strategy | Implementation |
|----------|---------------|
| Schema-constrained routing | LLM sees only filtered Swagger; `validate_endpoint()` catches hallucinated paths |
| Retry with failure history | Failed endpoints fed back to LLM: "do NOT repeat these" |
| Data-first response rules | `_SUMMARY_PROMPT Rule 9`: "NEVER INVENT DATA" |
| JSON response format | `response_format={"type": "json_object"}` — structured output only |
| Hard guards in _parse_plan() | Unknown intent → downgraded; general_chat → steps forced to []; Pydantic validation |

---

## 10. End-to-End Flow: Student Asks a Question

**Example:** `"كام ساعة معتمدة خلصت؟"` (How many credit hours have I completed?)

```
1. Request arrives:
   message: "كام ساعة معتمدة خلصت؟"
   auth_header: "Bearer eyJ..."
   context: {role: "student", academic_context: {userId: "01H...", batchId: "01H...", ...}}

2. PlannerAgent.run():
   - "كام ساعة خلصت" is in _BACKEND_KEYWORDS → but LLM classifies it correctly anyway
   - LLM returns: {"intent": "backend_api_query", "goal_summary": "...", "steps": []}

3. Layer-2 check:
   - intent != "general_chat" → Layer-2 does NOT fire

4. Routes to DynamicApiModule

5. DynamicApiModule attempt 1:
   - _pick_endpoint() sends _ROUTING_PROMPT with Swagger schema
   - "كام ساعة خلصت" matches Category Q keywords → Q1
   - LLM returns: {"endpoint": "/api/Regulations/my-roadmap", "method": "GET", "params": {}}

6. _substitute_placeholders(): no {placeholders} → passes

7. validate_endpoint("GET", "/api/Regulations/my-roadmap"):
   - Matches ("GET", "/api/Regulations/my-roadmap") in _allowed_endpoints ✅

8. backend_client.fetch("/api/Regulations/my-roadmap", auth_header, params={page:1,size:10})
   - C# validates JWT → role=Student ✅
   - Returns AcademicRoadmapDto JSON

9. _summarize():
   - Rule 0a fires: endpoint is /api/Regulations/my-roadmap
   - User asked about "كام ساعة" → extract completedCreditHours and totalCreditHours
   - narrative: "أنهيت حتى الآن 87 ساعة معتمدة من أصل 140 ساعة — تبقّى 53 ساعة للتخرج."
   - suggestions: ["ايه المواد المقترحة الترم الجاي؟", "عايز أشوف اللي رسبت فيه؟", "هل أنا في المسار الصح؟"]

10. Final AgentOutput:
    status: "success"
    response: "أنهيت حتى الآن 87 ساعة معتمدة من أصل 140 ساعة — تبقّى 53 ساعة للتخرج."
    data: {endpoint_called: "/api/Regulations/my-roadmap", suggestions: [...]}
```

---

## Quick Reference

| Component | File | Key Class/Function | Model |
|-----------|------|--------------------|-------|
| Intent Classification | `planner.py` | `PlannerAgent.run()` | `openai/gpt-4o-mini` |
| Layer-2 Override | `planner.py` | `_detect_generate_exam()`, `_detect_backend_query()` | deterministic |
| API Routing | `dynamic_api.py` | `DynamicApiModule._pick_endpoint()` | `openai/gpt-4o-mini` |
| Response Narration | `dynamic_api.py` | `DynamicApiModule._summarize()` | `openai/gpt-4o-mini` |
| Schema Filtering | `api_discovery.py` | `fetch_and_filter_schema()` | no LLM |
| Endpoint Validation | `api_discovery.py` | `validate_endpoint()` | no LLM |
| Data Contracts | `schemas.py` | `ExecutionPlan`, `ExamParams`, etc. | Pydantic |

| Security Rule | Enforced In | Blocks |
|---------------|-------------|--------|
| No DELETE/PUT/PATCH | `_is_allowed()` | Destructive operations |
| No auth/dev/ai routes | `_is_allowed()` | Infrastructure access |
| Only allowlisted POSTs | `_is_allowed()` | Arbitrary POST operations |
| Endpoint validation | `validate_endpoint()` | Hallucinated endpoints |
| JWT forwarding | `backend_client` | Role violations |
| general_chat → steps=[] | `_parse_plan()` | Unauthorized tool calls |

---

## 11. RAG Pipeline — Retrieval-Augmented Generation for Course Materials

**Files:** `app/services/chunker.py`, `app/services/embedding_service.py`, `app/services/vector_store.py`, `app/modules/material_qa.py`, `app/api/routes/rag.py`

The RAG pipeline allows students and doctors to ask questions that are answered **strictly from the content of uploaded course materials**, preventing any hallucination or off-topic responses.

### 11.1 Index Flow (triggered by .NET RagController or daily RagIndexingJob)

```
Doctor uploads Material (PDF/file)
        │
        ▼
POST /api/rag/index  (FastAPI)
        │
        ▼
chunker.py — chunk_text()
  ├── Split text into 500-token chunks
  └── 100-token overlap between consecutive chunks
        │
        ▼
embedding_service.py — embed_chunks()
  ├── Primary: OpenAI text-embedding-3-small (1536 dimensions)
  └── Fallback: TF-IDF sparse vector (if OPENAI_API_KEY unavailable)
        │
        ▼
vector_store.py — upsert_chunks()
  └── ChromaDB PersistentClient
      └── collection: "university_materials"
          └── metadata: {materialId, chunkIndex, tokenCount}
```

### 11.2 Query Flow (student asks a course question)

```
Student message: "اشرحلي مفهوم الـ Recursion من المحاضرة"
        │
        ▼
PlannerAgent — intent: "material_qa"   (Layer-2 Override 3)
        │
        ▼
MaterialQAModule.run()
        │
        ├── Embed user query (text-embedding-3-small)
        │
        ├── vector_store.search()
        │     ├── Cosine similarity against "university_materials" collection
        │     ├── Filter by materialId or offeringId (from academic_context)
        │     └── Return top-K chunks (default K=5)
        │
        ├── Build grounded context from retrieved chunks
        │
        └── LLM call with strict prompt:
              "Answer ONLY from the provided context chunks.
               If the answer is not in the chunks, say so explicitly.
               Cite the source chunk index in your answer."
        │
        ▼
AgentOutput — narrative cites source chunk, no hallucination
```

### 11.3 Components

#### EmbeddingService (`app/services/embedding_service.py`)
- **Primary:** `openai.embeddings.create(model="text-embedding-3-small")` — 1536-dimensional vectors
- **Fallback:** TF-IDF sparse vectors when OpenAI API is unavailable
- **Similarity:** `cosine_similarity(query_vec, chunk_vec)` — scores range 0.0–1.0
- **Threshold:** Chunks scoring below 0.3 are discarded before LLM injection

#### VectorStore (`app/services/vector_store.py`)
- **Client:** `chromadb.PersistentClient(path="./chroma_data")`
- **Collection:** `"university_materials"` (created at startup if missing)
- **Operations:** `upsert_chunks(material_id, chunks, embeddings)`, `search(query_embedding, top_k, filter)`, `delete_material(material_id)`
- **Metadata stored per chunk:** `materialId`, `chunkIndex`, `tokenCount`, `offeringId`

#### Chunker (`app/services/chunker.py`)
- **Function:** `chunk_text(text: str, max_tokens: int = 500, overlap: int = 100) -> list[str]`
- **Strategy:** Splits on sentence boundaries where possible; falls back to hard token split
- **Output:** List of text chunks ready for embedding

#### MaterialQAModule (`app/modules/material_qa.py`)
- **Intent handled:** `"material_qa"`
- **Strict grounding prompt:** Only answers from retrieved chunks; explicitly refuses to guess when context is insufficient
- **Source citation:** Each answer includes the chunk indices used (e.g., "وفقاً للمحاضرة — الجزء 3")
- **Anti-hallucination enforcement:** Prompt instructs the model: "If the information is not present in the provided context, respond: لم أجد هذه المعلومة في المحاضرات المتاحة"

### 11.4 .NET Integration (RagController)

| Endpoint | Role | Description |
|----------|------|-------------|
| `POST /api/rag/index/{materialId}` | Doctor, Admin | Trigger indexing of a specific material |
| `GET /api/rag/status/{materialId}` | Doctor, Admin | Get indexing status (IndexingStatusDto) |
| `POST /api/rag/search` | Student, Doctor, TA, Admin | Semantic search (RagSearchRequest → RagSearchResponse) |
| `GET /api/rag/search/offering/{offeringId}` | Student, Doctor | Search within a specific offering's materials |
| `DELETE /api/rag/index/{materialId}` | Doctor, Admin | Delete all chunks for a material |

### 11.5 FastAPI RAG Routes (`app/api/routes/rag.py`)

| Endpoint | Description |
|----------|-------------|
| `POST /api/rag/index` | Accepts chunks + metadata, embeds, upserts to ChromaDB |
| `POST /api/rag/search` | Embeds query, returns top-K chunks with similarity scores |
| `DELETE /api/rag/material/{id}` | Removes all vectors for a material from ChromaDB |
| `GET /api/rag/stats` | Returns total chunk count and collection metadata |

### 11.6 Deployment Notes

- **Required env var:** `OPENAI_API_KEY` (for text-embedding-3-small; TF-IDF fallback activates automatically if absent)
- **Python package:** `chromadb>=0.4.0` and `numpy>=1.26.0` in `requirements.txt`
- **Persistence:** ChromaDB stores data in `./chroma_data` directory — must be a persistent volume in production
- **Daily job:** `RagIndexingJob` (Hangfire) runs daily to index any materials uploaded since the last run, using `IRagIndexingJob.IndexAllUnindexedMaterialsAsync()`

---

## 12. AI Auto-Grading

**File:** `app/api/routes/ai_grading.py`

The AI auto-grading endpoint provides rubric-based LLM scoring of student assignment submissions. It is called by the .NET `AssignmentService` when `AiGradingEnabled = true` on an assignment.

### 12.1 Endpoint

```
POST /api/ai/grade-submission
```

**Input payload:**
```json
{
  "submission_text": "Student's full written answer",
  "assignment_title": "Assignment 1 — Sorting Algorithms",
  "description": "Explain merge sort and compare it with quicksort",
  "rubric": "Award up to 40 pts for correctness, 30 for clarity, 30 for comparison depth",
  "max_grade": 100
}
```

**Output:**
```json
{
  "score": 82.5,
  "feedback": "Good explanation of merge sort. Quicksort comparison was superficial.",
  "strengths": ["Clear step-by-step merge sort walkthrough", "Correct time complexity analysis"],
  "weaknesses": ["Quicksort space complexity not discussed", "No code examples provided"],
  "confidence": 0.88
}
```

### 12.2 Prompt Principles

- **Rubric-bound scoring:** The LLM is instructed to score each rubric dimension independently and sum the result; it cannot award more than `max_grade`
- **Structured JSON output:** Response format is enforced as `{"type": "json_object"}` — no free-form text returned
- **No hallucination:** Prompt explicitly states: "Base your evaluation ONLY on what the student wrote. Do not assume knowledge the student did not demonstrate."
- **Confidence field:** LLM estimates its own certainty (0.0–1.0); low-confidence scores are flagged for mandatory human review
- **Human override:** `IsHumanReviewed` flag on `AssignmentSubmission` — doctor can always override the AI score via `GradeAssignmentSubmissionDto`

### 12.3 RBAC
- Endpoint is called **server-to-server** (from .NET backend to FastAPI) — not exposed directly to users
- Doctor triggers grading via `POST /api/assignments/{id}/submissions/{submissionId}/ai-grade` on the .NET side
- Students see only the final `Grade` and `AiFeedback` fields after doctor approval
