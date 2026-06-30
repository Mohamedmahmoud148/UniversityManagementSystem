# Backend report → Frontend action items (2026-06-30)

## 1. Complaints — "AI is analyzing this complaint..." stuck forever

**Root cause (backend, now fixed in commits `d28ab7f`, `96a890f`, `fcfd5f9`):**
Complaints submitted while a DB migration bug was active never got an AI
analysis row created at all (a background job query crashed before the
analysis was saved, and the crash was silently swallowed). The schema bug
is fixed, and the backend now automatically retries any complaint that has
no analysis every 15 minutes — no manual action needed going forward.

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

**This is not a bug — it's an accurate empty state**, but the current UI
makes it look broken. `StudentIntelligenceSnapshots` (the table backing
this whole page) is only generated for students with an **active
Enrollment** in a given `SubjectOffering`. For the "Data Structure"
offering shown in the screenshot, there are zero active enrollments, so:
- `TotalStudents = 0` is correct (no one enrolled yet).
- `OverallHealth = "unknown"` is the literal value the backend returns
  whenever `total == 0` — it's a deliberate "not enough data" signal, not
  an error.
- `0.0% avg` / the AI recommendation text are generic placeholders
  generated even when there's no underlying data, which is misleading.

**Action items for frontend:**
1. **Detect the zero-student case explicitly** (`totalStudents === 0` or
   `overallHealth === "unknown"`) and render a clear empty state per
   offering card, e.g. "No students enrolled in this course yet" instead
   of "unknown" badge + "0.0% avg" + generic AI tips. The badge styling
   currently looks like a failure state — it isn't.
2. **Suppress AI Recommendations when there's no data.** Recommendations
   like "Assignment completion rate is 0%..." are computed off zero
   students and aren't actionable. Hide the "AI Recommendations" panel
   (or replace with "Not enough data yet") when `totalStudents === 0`
   across all offerings.
3. No backend change is required for this unless product wants the
   recommendation generator itself to skip zero-data offerings — flag if
   that's wanted and we'll suppress it server-side instead.
