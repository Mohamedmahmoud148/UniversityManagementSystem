# 35 — AI Service: Production Hardening & Intelligence Upgrade

> **Scope:** The FastAPI **AI Orchestration Service** (separate repo: `ai-orchestration-service`).
> **Date:** 2026-06-22 | **Status:** root-cause fixes shipped; larger features documented as roadmap.

This section documents the production hardening, performance optimization, and
intelligence upgrade of the AI service — the FastAPI layer that powers chat, RAG,
PDF understanding, the academic advisor, and the AI companion.

---

## Why this section exists

Production logs showed the AI running at **half quality**: the embedding system had
silently fallen back to keyword matching (no semantic search), which degraded RAG,
intent classification, the academic advisor, material explanation, regulation search,
and PDF understanding. This section explains the root cause, the fix, and the roadmap.

---

## Documents

| # | Document | Read if you… |
|---|----------|--------------|
| 1 | [AI Architecture](AI_ARCHITECTURE_REPORT.md) | want the end-to-end picture of the AI service |
| 2 | [Embedding System](EMBEDDING_SYSTEM_REPORT.md) | need to understand the root-cause RAG fix |
| 3 | [RAG Pipeline](RAG_PIPELINE_REPORT.md) | work on retrieval / chunking / vector store |
| 4 | [PDF Processing](PDF_PROCESSING_REPORT.md) | work on file explanation / large PDFs |
| 5 | [Model Routing](MODEL_ROUTING_REPORT.md) | need model tiering + fallback chain details |
| 6 | [Performance Optimization](PERFORMANCE_OPTIMIZATION_REPORT.md) | want the speed/cost roadmap + deploy order |
| 7 | [Health & Monitoring](HEALTH_MONITORING_REPORT.md) | need health endpoints + observability |
| 8 | [Frontend AI Integration](FRONTEND_AI_INTEGRATION_REPORT.md) | are a frontend dev wiring streaming/upload/study mode |

---

## TL;DR — what changed

**Shipped (code):**
- `numpy<2` pin — root-cause fix that restores semantic embeddings.
- Dockerfile `chroma_data` permissions + `CHROMADB_PATH` — persistent vector store.
- Embedding provider priority (`EMBEDDING_API_KEY` → `OPENAI_API_KEY`) + explicit, no-silent-fallback startup logging.
- Real cross-provider fallback chain (`gemini-flash-1.5` → `mistral-7b-instruct`).
- `ModelRouter.pick_model()` task→model tiering (simple / complex / vision).
- `rag_health` mode-string fix.

**Needs a deploy-time action (Railway env):**
- Set `EMBEDDING_API_KEY` (or `OPENAI_API_KEY`) → redeploy → confirm `Fallback=False`.
- Then set `EMBEDDING_CLASSIFIER_SHADOW_MODE=False`.
- Optional: `MODEL_COMPLEX=openai/gpt-4o`.

**Roadmap (documented, not yet coded):**
- Reranking, page-aware answering for very large PDFs, full study mode, streaming
  everywhere, Redis response cache, integration test suite.

See [Performance Optimization](PERFORMANCE_OPTIMIZATION_REPORT.md) for the exact deploy order.
