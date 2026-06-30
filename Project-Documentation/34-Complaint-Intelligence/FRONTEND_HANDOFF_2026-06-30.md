# Backend report → Frontend action items (2026-06-30, updated)

> **Update:** Two more real backend bugs were found and fixed after this
> report was first sent — see the **"Update" notes** under each section.
> Section 2 originally said the Teaching Intelligence empty state was "not
> a bug." That was wrong; it's now confirmed and fixed as a real bug.

## 1. Complaints — "AI is analyzing this complaint..." stuck forever

**Root cause #1 (fixed in `d28ab7f`, `96a890f`, `fcfd5f9`):**
Complaints submitted while a DB migration bug was active never got an AI
analysis row created at all (a background job query crashed before the
analysis was saved, and the crash was silently swallowed). The schema bug
is fixed, and the backend now automatically retries any complaint that has
no analysis every 15 minutes — no manual action needed going forward.

**Root cause #2 (the actual reason retries kept failing — fixed in `ef42ab7`
on the AI service):** Every single call to the AI analysis service was
failing with HTTP 422, because the AI service's request/response model used
camelCase field names while our backend sends/expects snake_case (matching
the rest of that service's API). This meant *every* complaint, old and new,
was permanently stuck — the automatic retry from fix #1 was retrying
correctly, but every attempt failed at the AI service boundary. This is now
fixed; new complaints should get analyzed within ~1–2 minutes, and the
15-minute retry cycle will pick up the backlog automatically.

**What this means for the UI right now:**
- Old stuck complaints will self-heal within ~15–20 minutes of the new
  deploy going live. No frontend change is required for those to recover.
- Going forward, a complaint can legitimately stay in "pending analysis"
  for a few seconds to a couple of minutes (Hangfire job latency + AI call
  time). The frontend should not assume analysis is instant.

**Action items for frontend:**
1. **Add a timeout/fallback state.** If `complaint.analysis` is still null
   after a reasonable window (e.g. 2 minutes from `complaint.createdAt`),
   stop showing an indefinite spinner. Replace "AI is analyzing this
   complaint..." with something like "Analysis is taking longer than
   usual — we'll keep retrying automatically" plus a manual refresh
   button. Don't show a spinner that can never resolve.
2. **Poll, don't assume push.** If the complaints list/detail view isn't
   already polling or refetching periodically, add a poll (e.g. every
   30–60s) for complaints whose `analysis` is null, so the UI picks up
   the result once the backend finishes processing — without requiring
   a manual page reload.
3. **Admin-only manual retry button (optional but recommended):** call
   `POST /api/complaints/reprocess-pending` (Admin/SuperAdmin only, no
   body) — it returns `{ "requeued": <count> }`. Useful as an explicit
   "Retry analysis now" action in the admin Complaints dashboard instead
   of waiting for the 15-minute automatic cycle.

---

## 2. Teaching Intelligence — "0 students / unknown / 0.0% avg"

**Correction: this WAS a real backend bug, not a legitimate empty state.**
The original version of this report said this was expected behavior caused
by zero enrollments. That was wrong — the doctor confirmed real students
ARE enrolled in "Data Structure" (DS-101), which proved the zero-data
display was a bug, not real data. Root cause found and fixed in `42490af`.

**Root cause:** The hourly background job that builds
`StudentIntelligenceSnapshots` (and the manual "Refresh" button) queries
`SubjectOfferings` to find which offerings to process. That query's SQL
was compiling down to the literal `WHERE FALSE` — an Entity Framework
optimizer issue with how `SubjectOffering`'s required relationships
combine with soft-delete filtering — which silently made the snapshot
job process **zero offerings, every single cycle, for every doctor**,
regardless of real enrollment data. Two separate bugs compounded this:
1. The hourly snapshot job never found any offering to process (`WHERE
   FALSE` bug).
2. The manual "Refresh" button (the circular-arrow icon) looked like it
   worked — it returned a success response — but it was silently failing
   too: it ran on a database connection that had already been closed by
   the time the request returned, so clicking it never actually refreshed
   anything either.

Both are fixed now. Trigger a refresh (button or wait for the next hourly
cycle) and the "Data Structure" offering should show its real enrolled
students.

**Action items for frontend (still apply once data loads correctly):**
1. **Still detect the zero-student case explicitly** (`totalStudents === 0`
   or `overallHealth === "unknown"`) for any offering that genuinely has no
   enrollments — that part of the original advice still stands, this is a
   legitimate state for some offerings, just not for ones with real
   enrolled students. Render a clear "No students enrolled in this course
   yet" empty state instead of "unknown" badge + "0.0% avg" + generic AI
   tips.
2. **Suppress AI Recommendations when there's no data**, same as before —
   hide/replace the AI Recommendations panel when `totalStudents === 0`.
3. **Consider showing a "last refreshed at" timestamp** on each offering
   card now that refresh is a real, meaningful action — helps confirm to
   the doctor that data is current rather than stale.
