# Design Decisions

## Overview

This document records the significant architectural decisions made during the design and development of the university management system. For each decision, we document the context, the options considered, the choice made, and the rationale. These records are valuable for the graduation committee review and for future developers who need to understand why the system is built the way it is.

---

## Decision 1 — Firebase for Classroom vs. .NET for Academic Records

**Context:**
The system needs to support two fundamentally different operational modes:
1. **Academic records** — grades, enrollments, transcripts, regulations. These are permanent, auditable, transactional records with strict consistency requirements.
2. **Classroom operations** — live quizzes, attendance, AI chat, engagement tracking. These require real-time updates, high concurrency, and are relatively ephemeral.

**Options Considered:**

| Option | Pros | Cons |
|--------|------|------|
| Everything in .NET + PostgreSQL | Single source of truth, strong ACID guarantees | SignalR cannot match Firestore's scale for concurrent listeners; quiz leaderboards and engagement tracking would require complex real-time infrastructure |
| Everything in Firebase | Real-time out-of-the-box, serverless | Poor fit for relational academic data; Firestore's document model struggles with grade aggregations, GPA calculations, prerequisite chains |
| Split by concern | Best tool for each job | Two auth systems, two data stores; complexity of keeping them in sync |

**Decision:** Split by concern — Firebase for classroom operations, .NET + PostgreSQL for academic records.

**Rationale:**
Classroom operations are inherently event-driven and real-time. Firestore's onSnapshot model is the perfect fit — it was designed for exactly this use case. Academic records are relational by nature: a grade depends on an enrollment, which depends on an offering, which depends on a semester, which depends on an academic year. This chain of relationships is expressed naturally in a relational database but is awkward in a document store.

The synchronization cost is minimal: the two systems share a common user identifier (Firebase UID stored in `SystemUsers`), and the Cloud Functions act as the bridge for operations that touch both sides (e.g., bulk import).

---

## Decision 2 — Why FastAPI for AI (Not .NET)

**Context:**
The AI service could have been implemented as a module within the existing .NET backend. Alternatively, it could be a separate Python service.

**Options Considered:**

| Option | Pros | Cons |
|--------|------|------|
| .NET module (C# + Semantic Kernel) | Single deployment, shared auth | Python ML ecosystem is significantly more mature; ChromaDB, LangChain, and most vector DB SDKs are Python-first |
| Python FastAPI (separate service) | Full Python ML ecosystem, ChromaDB native SDK, asyncio for LLM streaming | Second deployment, service-to-service call overhead |
| Node.js (Cloud Function) | Already using Firebase Functions | Not suitable for long-running AI workflows; cold start penalty; no vector DB integration |

**Decision:** Separate Python FastAPI service.

**Rationale:**
The Python AI ecosystem is unmatched. ChromaDB's primary SDK is Python. The embedding libraries (sentence-transformers, openai embeddings) are Python-first. Async streaming of LLM responses is mature in Python's `httpx`/`asyncio` ecosystem. Building this in .NET would mean fighting the ecosystem at every step.

The service-to-service call overhead (FastAPI → .NET) is 5–15ms on Railway's private network — negligible compared to the 2–4 second LLM response time.

---

## Decision 3 — Why PostgreSQL (Not MongoDB)

**Context:**
The academic data model is highly relational: students → enrollments → subject offerings → subjects → regulations → departments → colleges. MongoDB (a document store) was considered as an alternative.

**Options Considered:**

| Option | Pros | Cons |
|--------|------|------|
| PostgreSQL | ACID transactions, native JOINs, strong consistency, EF Core support | More complex schema design upfront |
| MongoDB | Flexible schema, easy horizontal scaling | No native joins (aggregation pipelines are complex); eventual consistency issues for grade/GPA calculations; EF Core MongoDB provider is immature |
| Firebase Firestore (for everything) | Already in use | See Decision 1 above |

**Decision:** PostgreSQL.

**Rationale:**
GPA calculation requires aggregating grade records across multiple enrollments, joining to subject credit hours, and computing weighted averages. This is a natural SQL query:

```sql
SELECT
  SUM(g."GradePoints" * s."CreditHours") / SUM(s."CreditHours") AS gpa
FROM "Grades" g
JOIN "Enrollments" e ON g."EnrollmentId" = e."Id"
JOIN "SubjectOfferings" so ON e."SubjectOfferingId" = so."Id"
JOIN "Subjects" s ON so."SubjectId" = s."Id"
WHERE e."StudentId" = @studentId AND g."IsPublished" = true;
```

Replicating this in MongoDB would require a multi-stage aggregation pipeline with `$lookup` operations — complex, slower, and harder to maintain. The relational model also enforces referential integrity natively (FK constraints), which is critical for academic records.

---

## Decision 4 — Dual Auth: JWT + Firebase Auth

**Context:**
The system needed authentication for two distinct subsystems that were built at different times and serve different purposes.

**Why not unify on one system?**

| Approach | Problem |
|----------|---------|
| JWT only (no Firebase) | Cannot use Firebase real-time features (Firestore security rules require Firebase Auth; onSnapshot requires a Firebase user) |
| Firebase only (no JWT) | Firebase ID tokens expire every 1 hour and require a network call to refresh; the .NET backend would need to validate Firebase tokens on every request (requires Firebase Admin SDK in .NET, adds latency and external dependency) |
| Dual system (JWT + Firebase) | Slight login complexity, but each system is optimal for its domain |

**Decision:** Dual authentication system — JWT for .NET API, Firebase Auth for classroom features.

**Rationale:**
The systems have fundamentally different requirements. .NET's JWT auth is self-contained: the .NET server validates tokens without any external calls. Firebase's auth integrates natively with Firestore Security Rules — without Firebase Auth, you cannot use server-enforced Firestore rules at all.

The linking mechanism (Firebase UID stored in `SystemUsers`) is simple and reliable. The login flow issues both tokens in one user interaction, so the user experience is seamless.

---

## Decision 5 — Why Hangfire for Background Jobs

**Context:**
The system requires 8 types of background operations: GPA recalculation, enrollment window management, deadline reminders, etc. Several options were available.

**Options Considered:**

| Option | Pros | Cons |
|--------|------|------|
| Hangfire | Deep .NET integration, persistent job storage, built-in retry, dashboard UI, recurring jobs | Requires PostgreSQL table allocation |
| Quartz.NET | Mature, powerful scheduling | More configuration overhead; no built-in web dashboard |
| ASP.NET IHostedService | Built-in, no extra dependency | No persistence (jobs lost on restart), no retry, no dashboard |
| Railway Cron | Platform-level scheduling | Separate deployment unit; cannot access in-process services easily |
| Firebase Cloud Functions (scheduled) | Serverless, auto-scaling | Cannot access .NET internals; latency for DB operations |

**Decision:** Hangfire.

**Rationale:**
Hangfire is purpose-built for .NET background job processing. Its killer feature for this system is **persistence** — jobs are stored in PostgreSQL, so they survive service restarts. If the Railway service restarts mid-semester during a GPA recalculation run, Hangfire picks up where it left off. The built-in dashboard (at `/hangfire`) provides real-time visibility into job status, execution history, and failures without any extra tooling.

---

## Decision 6 — Why ChromaDB for RAG (Not Pinecone or Weaviate)

**Context:**
The RAG pipeline requires a vector database to store and retrieve lecture content embeddings. Several managed and self-hosted options were evaluated.

**Options Considered:**

| Option | Pros | Cons |
|--------|------|------|
| ChromaDB | Open source, Python-native, embedded mode (no separate server needed), free | Single-node in embedded mode; not managed |
| Pinecone | Fully managed, scalable, fast | Paid service; vendor lock-in; adds latency (external API call per query) |
| Weaviate | Rich features, GraphQL API, managed option | More complex setup; overkill for university-scale data |
| pgvector (PostgreSQL extension) | Same database as academic data | Requires PostgreSQL extension on Railway; vector operations slower than dedicated vector DBs |
| FAISS (Facebook AI Similarity Search) | Very fast, battle-tested | Low-level library, no built-in server or metadata filtering |

**Decision:** ChromaDB in embedded mode.

**Rationale:**
For the scale of a single university (estimated 50,000–200,000 lecture content chunks), ChromaDB in embedded mode is more than sufficient. Running it in the same process as FastAPI eliminates network latency for vector queries (queries resolve in <10ms). There is no managed cost, and the Python SDK is the most mature of any vector database. If scale demands it, ChromaDB can be moved to server mode with minimal code changes.

---

## Decision 7 — Why Railway for Deployment (Not AWS or Azure)

**Context:**
The two backend services (.NET and FastAPI) needed a cloud deployment platform. AWS, Azure, Heroku, and Railway were considered.

**Options Considered:**

| Option | Pros | Cons |
|--------|------|------|
| Railway | Simple deployment from GitHub, managed PostgreSQL + Redis plugins, private networking, reasonable pricing, zero infrastructure management | Less enterprise-grade than AWS/Azure; fewer advanced features |
| AWS (ECS or Elastic Beanstalk) | Industry standard, scalable, rich services | Steep learning curve; significant DevOps overhead; expensive for small scale |
| Azure App Service | Native .NET support, Azure AD integration | Complex pricing, vendor lock-in, overkill for project scale |
| Heroku | Similar simplicity to Railway | More expensive at equivalent specs; Postgres addon costs; slower builds |
| Render | Similar to Railway | Slightly less mature tooling; fewer native plugins |

**Decision:** Railway.

**Rationale:**
Railway matches the project's needs perfectly: deploy from GitHub, managed database plugins (PostgreSQL + Redis with one click), private networking between services, and environment variable management. The time saved on infrastructure compared to AWS was redirected to feature development. For a graduation project at university scale, Railway is the right balance of simplicity and capability.

---

## Decision 8 — Soft-Delete Everywhere

**Context:**
The system needs to handle "deletion" of academic entities: retiring a subject, deactivating a student account, archiving a semester. Physical deletion would destroy referential integrity.

**Options Considered:**

| Option | Pros | Cons |
|--------|------|------|
| Physical DELETE | Simple; no deleted data stored | Destroys grade history if a student is deleted; breaks FK references; no audit trail |
| Soft-delete (DeletedAt column) | Audit trail; recoverable; FK integrity maintained | Queries must always filter `DeletedAt IS NULL`; index complexity |
| Archive tables | Separate tables for deleted records | Complex schema; duplicate table structures; complex queries across live + archive |

**Decision:** Soft-delete with `DeletedAt TIMESTAMP NULL` on all entity tables.

**Rationale:**
Academic data must never be physically destroyed. If a student is withdrawn, their grade history must remain accessible for transcript generation. If a subject is retired from the curriculum, past enrollments and grades referencing it must remain valid. The EF Core global query filter (`HasQueryFilter(e => e.DeletedAt == null)`) makes this transparent to application code — developers don't need to remember to add the filter; it applies automatically to every query.

---

## Decision 9 — Credit-Hour Weighted GPA vs. Simple Average

**Context:**
GPA could be calculated as a simple average of all course grades, or as a credit-hour-weighted average (standard practice in most universities).

**Formula comparison:**

Simple average (not used):
```
GPA = SUM(GradePoints) / COUNT(Grades)
```

Credit-hour weighted (used):
```
GPA = SUM(GradePoints * CreditHours) / SUM(CreditHours)
```

**Why it matters:**
- A 1-credit-hour gym course and a 3-credit-hour algorithms course should not have equal weight.
- A student who fails a 3-credit course and passes a 1-credit course has a different academic standing than one who fails a 1-credit course and passes a 3-credit course.

**Decision:** Credit-hour weighted GPA.

**Rationale:**
This is the international academic standard (used by virtually all accredited universities worldwide, including those following the ABET or NAQAAE frameworks). It reflects the actual academic load each subject represents. The calculation is performed by the `GpaRecalculationJob` Hangfire background job and cached in the `Students.CumulativeGPA` column to avoid real-time aggregation on every grade query.

---

## Decision 10 — Orchestrator Pattern in AI Service (Not Monolithic Prompt)

**Context:**
The AI service needs to handle 17 different intents across 15 modules. This could be implemented as a single large prompt that handles everything, or as a routing layer that dispatches to specialized module handlers.

**Options Considered:**

| Option | Pros | Cons |
|--------|------|------|
| Monolithic prompt | Simpler code structure | Huge prompt for every request (high token cost); difficult to maintain and update individual module behavior; harder to add per-intent data fetching |
| Orchestrator + specialized modules | Each module is focused, cheap to run; easy to add/modify; enables per-intent data loading | More code; routing latency (2-step LLM call: classify + respond) |

**Decision:** Orchestrator pattern.

**Rationale:**
A monolithic prompt that handles every possible university question would be thousands of tokens per request, costing 10x more and being slower than a focused module prompt. The orchestrator's two-step approach (classify intent cheaply, then execute the right module fully) is both more economical and more maintainable. New intents can be added by writing a new module without touching existing ones. Module prompts are tuned specifically for their domain — the `academic_advisor` prompt is very different from the `schedule_query` prompt.

---

## Decision 11 — In-Browser Face Detection (Not Server-Side)

**Context:**
Student engagement tracking requires processing webcam video to detect face presence and attention. This could be done server-side (stream video to a server for processing) or client-side (process locally in the browser).

**Options Considered:**

| Option | Pros | Cons |
|--------|------|------|
| Server-side video processing | Centralized processing; consistent hardware | Significant bandwidth cost (streaming video); privacy concern (video leaves device); backend complexity; high server CPU cost |
| Client-side (MediaPipe WASM) | No video ever leaves the device (privacy); no bandwidth cost; no server infrastructure for video processing; Google MediaPipe is production-quality and runs at 30fps on modern laptops | Requires WebAssembly support (all modern browsers); slightly higher battery usage on the client device |

**Decision:** Client-side MediaPipe WASM.

**Rationale:**
Privacy is the overriding concern. Students should not have their webcam video streamed to a server without explicit understanding and consent. By running MediaPipe entirely in the browser, only derived numeric scores (not images) ever leave the device. This also eliminates the infrastructure cost of video processing servers, which would be prohibitively expensive at scale.

---

## Summary Table

| Decision | Choice Made | Primary Reason |
|----------|------------|----------------|
| Classroom vs. academic data store | Firebase + PostgreSQL (split) | Each tool is optimal for its domain |
| AI service language | Python FastAPI | Python ML ecosystem maturity |
| Database type | PostgreSQL (relational) | Relational academic data; GPA aggregations; FK integrity |
| Authentication | Dual (JWT + Firebase Auth) | Each auth system is optimal for its domain |
| Background jobs | Hangfire | Persistent, retryable, .NET-native, dashboard |
| Vector database | ChromaDB | Open-source, Python-native, zero cost, sufficient scale |
| Deployment platform | Railway | Simplicity, managed plugins, private networking |
| Delete strategy | Soft-delete everywhere | Audit trail, data recovery, FK integrity |
| GPA formula | Credit-hour weighted | International academic standard |
| AI routing | Orchestrator pattern | Cost, maintainability, focused module prompts |
| Engagement processing | Client-side MediaPipe | Privacy, no bandwidth cost |
