# RAG Pipeline Report

> **Date:** 2026-06-22

---

## 1. Pipeline

```
Document → Extract Text → Chunk → Batch Embeddings → ChromaDB
                                                         │
Query → Embed → Similarity Search → [Rerank] → Top Context → LLM → Answer
```

| Stage | Component | Status |
|---|---|---|
| Extract | `modules/file_extraction.py` | ✅ PDF/DOCX/XLSX/CSV/TXT + vision OCR |
| Chunk | `services/chunker.py` | ✅ paragraph→sentence→word, overlap |
| Embed (batch) | `services/embedding_service.py` | ✅ `embed_batch` |
| Store | `services/vector_store.py` | ✅ ChromaDB + in-memory fallback |
| Search | `vector_store.search()` | ✅ cosine + metadata filters |
| Rerank | — | 🔶 Roadmap (see §5) |
| Generate | `agents/model_router.py` | ✅ tiered + fallback |

## 2. Chunking (`chunker.py`)

Current strategy — `chunk_text(text, chunk_size=500, overlap=100)`:
- Splits on paragraph boundaries (blank lines) first.
- Falls back to sentence boundaries (Arabic `؟ .` + English `. ! ?`).
- Falls back to word boundaries for oversized sentences.
- Produces overlapping windows so context isn't lost at boundaries.

**Recommended upgrade (Phase 5):** raise defaults to `chunk_size=800, overlap=150` for richer context, and attach heading/section metadata to each chunk. The current splitter already preserves paragraph/sentence boundaries (never splits mid-sentence except for pathologically long sentences).

## 3. Vector Store (`vector_store.py`)

- Backend: ChromaDB persistent (`CHROMADB_PATH=/app/chroma_data`), in-memory fallback if unavailable.
- `upsert_chunks(material_id, chunks)` — batch upsert with sanitised metadata.
- `search(query_embedding, filter_material_id?, filter_offering_id?, top_k=5, min_score)` — cosine search with optional filters; converts Chroma distance → similarity `[0,1]`.
- All Chroma calls run via `asyncio.to_thread` so the event loop never blocks.

## 4. Metadata Schema

Each chunk stores: `materialId`, `chunkIndex`, `materialTitle`, `offeringId` (+ page mapping for large PDFs — see [PDF_PROCESSING_REPORT.md](PDF_PROCESSING_REPORT.md)).

## 5. Roadmap

| Item | Phase | Benefit |
|---|---|---|
| Reranking step (cross-encoder or LLM rerank of top-k) | 4 | Higher context precision |
| Chunk size 800 / overlap 150 + heading metadata | 5 | Richer retrieval units |
| Page-number metadata on every chunk | 6/7 | Page-aware answering |
| Redis response cache for regulation/general Q | 10 | Faster repeated queries |

## 6. Health

`GET /health/rag` returns embedding mode, real_embeddings flag, vector store stats, a write/read probe, and regulation-index presence. Use it to confirm the pipeline is healthy after deploy.
