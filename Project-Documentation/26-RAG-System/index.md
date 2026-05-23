---
layout: default
title: "RAG System"
---

# RAG System — Retrieval-Augmented Generation for Course Materials

> **Allows students and doctors to ask questions answered strictly from uploaded course material content — no hallucination, grounded answers, cited sources.**

---

## Table of Contents

1. [Overview](#1-overview)
2. [Architecture](#2-architecture)
3. [Components](#3-components)
   - 3.1 Chunker
   - 3.2 EmbeddingService
   - 3.3 VectorStore (ChromaDB)
   - 3.4 MaterialQAModule
4. [API Endpoints — .NET Backend (RagController)](#4-api-endpoints--net-backend-ragcontroller)
5. [API Endpoints — FastAPI](#5-api-endpoints--fastapi)
6. [How It Works — Step by Step](#6-how-it-works--step-by-step)
7. [Anti-Hallucination Rules](#7-anti-hallucination-rules)
8. [Database Entities](#8-database-entities)
9. [Deployment Notes](#9-deployment-notes)

---

## 1. Overview

The RAG (Retrieval-Augmented Generation) pipeline was introduced in Phase 1 of the AI upgrade. It enables the AI assistant to answer questions that are **grounded in the actual content of course materials** uploaded by doctors.

Without RAG, the AI could only answer from general knowledge — potentially hallucinating course-specific details. With RAG:
- Course materials are indexed into a vector database (ChromaDB)
- Student questions are embedded and matched against material chunks using cosine similarity
- The top-K most relevant chunks are injected into the LLM prompt as context
- The LLM answers **only** from that context and cites which chunk the answer came from

---

## 2. Architecture

### Index Flow

```
Doctor uploads PDF/file via /api/Materials/upload
        │
        ▼ (doctor or admin triggers, or RagIndexingJob runs daily)
POST /api/rag/index/{materialId}   (.NET RagController)
        │  calls FastAPI
        ▼
POST /api/rag/index                (FastAPI)
        │
        ├── chunker.py — chunk_text()
        │     Split into 500-token chunks, 100-token overlap
        │
        ├── embedding_service.py — embed()
        │     OpenAI text-embedding-3-small → 1536-dim vector
        │     (TF-IDF fallback if no API key)
        │
        └── vector_store.py — upsert_chunks()
              ChromaDB PersistentClient
              Collection: "university_materials"
              Metadata: {materialId, chunkIndex, tokenCount, offeringId}
```

### Query Flow

```
Student: "اشرحلي مفهوم الـ Stack من المحاضرة"
        │
        ▼
PlannerAgent (planner.py)
  Layer-2 Override 3 → intent: "material_qa"
        │
        ▼
MaterialQAModule (material_qa.py)
        │
        ├── embed query → 1536-dim vector
        │
        ├── vector_store.search(query_vec, top_k=5, filter={offeringId})
        │     Cosine similarity → ranked chunk list
        │
        ├── Build context from top-K chunks
        │
        └── LLM call (strict grounding prompt)
              Answer ONLY from chunks
              Cite chunk index in response
              Refuse if answer not in context
        │
        ▼
AgentOutput → cited, grounded answer
```

---

## 3. Components

### 3.1 Chunker (`app/services/chunker.py`)

```python
def chunk_text(text: str, max_tokens: int = 500, overlap: int = 100) -> list[str]:
```

- Splits text on sentence boundaries where possible
- Falls back to hard token split if text has no sentence delimiters
- Overlap of 100 tokens between consecutive chunks prevents context loss at boundaries
- Returns a list of text strings ready for embedding

---

### 3.2 EmbeddingService (`app/services/embedding_service.py`)

**Primary method:** OpenAI `text-embedding-3-small`
- 1536-dimensional dense vector
- Called via `openai.embeddings.create(model="text-embedding-3-small", input=text)`
- Batch embedding supported for indexing multiple chunks at once

**Fallback method:** TF-IDF sparse vectors
- Activates automatically when `OPENAI_API_KEY` is not set or API call fails
- Lower quality but ensures the system degrades gracefully

**Similarity function:**
```python
def cosine_similarity(vec_a: list[float], vec_b: list[float]) -> float:
    # dot product / (magnitude_a * magnitude_b)
    # Returns 0.0 to 1.0
```

Chunks scoring below **0.3 cosine similarity** are discarded before being sent to the LLM.

---

### 3.3 VectorStore (`app/services/vector_store.py`)

```python
client = chromadb.PersistentClient(path="./chroma_data")
collection = client.get_or_create_collection("university_materials")
```

**Key operations:**

| Method | Description |
|--------|-------------|
| `upsert_chunks(material_id, chunks, embeddings)` | Insert or update chunks for a material |
| `search(query_embedding, top_k, filter)` | Cosine similarity search, returns top-K chunks |
| `delete_material(material_id)` | Remove all vectors for a material from the collection |

**Metadata stored per chunk:**
- `materialId` — links back to the `Materials` table
- `chunkIndex` — position within the material
- `tokenCount` — actual size of this chunk
- `offeringId` — enables filtering search to a specific offering's materials

---

### 3.4 MaterialQAModule (`app/modules/material_qa.py`)

**Intent handled:** `"material_qa"`

Registered in:
- `executor.py` — `"material_qa": MaterialQAModule`
- `tool_registry.py` — `"material_qa": "material_qa"`
- `rbac.py` — allowed roles: `Student`, `Doctor`, `TeachingAssistant`, `Admin`

**Strict grounding prompt (key rules):**
```
You are a course material assistant. Answer the student's question using ONLY the
context chunks provided below. Do not use general knowledge.

If the answer is present in the chunks:
  - Answer clearly and cite the chunk (e.g., "وفقاً للمحاضرة — الجزء 3:")
  - Use the student's language (Arabic → Arabic, English → English)

If the answer is NOT in the chunks:
  - Respond: "لم أجد هذه المعلومة في المحاضرات المتاحة — The answer was not found in the uploaded materials."
  - Do NOT guess or use external knowledge.
```

---

## 4. API Endpoints — .NET Backend (RagController)

| Method | Endpoint | Role | Description |
|--------|----------|------|-------------|
| `POST` | `/api/rag/index/{materialId}` | Doctor, Admin | Trigger indexing of a specific uploaded material |
| `GET` | `/api/rag/status/{materialId}` | Doctor, Admin | Get indexing status (`IndexingStatusDto`) |
| `POST` | `/api/rag/search` | Student, Doctor, TA, Admin | Semantic search across indexed materials (`RagSearchRequest`) |
| `GET` | `/api/rag/search/offering/{offeringId}` | Student, Doctor | Search within a specific offering's materials only |
| `DELETE` | `/api/rag/index/{materialId}` | Doctor, Admin | Delete all chunks for a material from the vector store |

**RagSearchRequest:**
```json
{ "query": "ما هو الـ Stack?", "offeringId": "01H...", "topK": 5 }
```

**RagSearchResponse:**
```json
{
  "answer": "الـ Stack هو هيكل بيانات يعمل بمبدأ LIFO — وفقاً للمحاضرة الجزء 2.",
  "chunks": [
    { "chunkIndex": 2, "content": "A Stack is a LIFO data structure...", "similarity": 0.91 }
  ]
}
```

**IndexingStatusDto:**
```json
{ "materialId": "01H...", "isIndexed": true, "chunkCount": 18, "indexedAt": "2026-05-20T10:30:00Z" }
```

---

## 5. API Endpoints — FastAPI

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/rag/index` | Receive chunks + metadata from .NET, embed, upsert to ChromaDB |
| `POST` | `/api/rag/search` | Embed query, return top-K chunks with similarity scores |
| `DELETE` | `/api/rag/material/{id}` | Remove all vectors for a material from ChromaDB |
| `GET` | `/api/rag/stats` | Return total chunk count and collection metadata |

---

## 6. How It Works — Step by Step

1. **Doctor uploads a PDF** via `POST /api/Materials/upload` — file is stored in Cloudflare R2
2. **Doctor triggers indexing** via `POST /api/rag/index/{materialId}` on the .NET side, or the **daily `RagIndexingJob`** picks it up automatically
3. **`.NET RagService`** calls FastAPI `POST /api/rag/index` with the material text and metadata
4. **`chunker.py`** splits the text into 500-token chunks with 100-token overlap
5. **`embedding_service.py`** embeds each chunk using `text-embedding-3-small`
6. **`vector_store.py`** upserts all chunk vectors into ChromaDB collection `"university_materials"`
7. **`MaterialChunk`** records are written to PostgreSQL (Id, MaterialId, ChunkIndex, Content, Embedding JSON, TokenCount)
8. **Student asks**: "اشرحلي مفهوم الـ Recursion من المحاضرة"
9. **PlannerAgent Layer-2** detects keyword "من المحاضرة" → `intent = "material_qa"`
10. **MaterialQAModule** embeds the query and calls `vector_store.search()` filtered to the student's offering
11. **Top-5 chunks** with highest cosine similarity are retrieved (threshold 0.3)
12. **Strict grounding prompt** is built with the chunks as context
13. **LLM answers** citing the chunk index; refuses to answer if context does not contain the answer
14. **`RagSearchLog`** entry is written (StudentId, Query, RetrievedChunkIds, ResponseSummary)

---

## 7. Anti-Hallucination Rules

| Rule | Implementation |
|------|---------------|
| No external knowledge | Prompt explicitly forbids using general knowledge |
| Mandatory citation | Answer must cite the chunk number/index |
| Refusal threshold | If cosine similarity < 0.3 for all chunks, answer is "not found in materials" |
| Structured response | LLM output is validated before returning to user |
| Offering-scoped search | By default, search is filtered to the student's current offering — prevents cross-course leakage |

---

## 8. Database Entities

### `MaterialChunks` Table (migration: `AddRagPipeline`)

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `MaterialId` | ULID | FK → Materials (CASCADE) |
| `ChunkIndex` | int | 0-based position in material |
| `Content` | text | Chunk text (~500 tokens) |
| `Embedding` | text | JSON float array (1536 dims) |
| `TokenCount` | int | Actual token count |

### `RagSearchLogs` Table (migration: `AddRagPipeline`)

| Column | Type | Description |
|--------|------|-------------|
| `Id` | ULID | PK |
| `StudentId` | ULID | Who searched |
| `Query` | text | Search question |
| `RetrievedChunkIds` | text | JSON array of returned chunk IDs |
| `ResponseSummary` | text? | AI-generated answer summary |
| `CreatedAt` | datetime | |

---

## 9. Deployment Notes

| Item | Detail |
|------|--------|
| Required env var | `OPENAI_API_KEY` — for `text-embedding-3-small`; TF-IDF fallback activates if absent |
| Python packages | `chromadb>=0.4.0`, `numpy>=1.26.0` (added to `requirements.txt`) |
| ChromaDB storage | `./chroma_data` directory — must be a **persistent volume** in production (Railway volume or similar) |
| Daily indexing | `RagIndexingJob` (Hangfire) runs daily via `IRagIndexingJob.IndexAllUnindexedMaterialsAsync()` |
| Scaling note | ChromaDB PersistentClient is single-node; for high-load production consider migrating to Pinecone or Weaviate |
| Cold start | On first startup, ChromaDB creates the `"university_materials"` collection automatically if it does not exist |
