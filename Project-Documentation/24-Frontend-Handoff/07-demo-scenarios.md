# 🎬 Demo Scenarios — Committee Presentation Guide

> These are scripted walkthroughs for the graduation committee demo.  
> Each scenario should be practiced and executable in under 2 minutes.

---

## Scenario 1 — Successful Smart Enrollment

**Story:** A normal student in Good standing registers for an eligible subject.

### Steps to Demo

**Setup:** Log in as a Student with GPA ≥ 2.0.

1. Navigate to **Registration Page**.
2. Show the **Credit Hours Banner**: "0 / 18 hours registered, GPA: 3.42 — Standard tier"
3. Point to a green offering card (e.g., Data Structures).
4. Show the capacity bar: "87/120 — plenty of room".
5. Click **"Enroll →"**.
6. Button shows spinner briefly → changes to **"✓ Enrolled"** (blue).
7. Green toast appears: **"Enrolled in Data Structures successfully!"**
8. Credit hours banner animates: "0 → 3 hours registered".

**What to say to committee:**
> "The system runs 7 validation rules in real-time before allowing any enrollment — checking prerequisites, GPA, credit limits, capacity, and academic standing."

---

## Scenario 2 — Blocked by Prerequisite

**Story:** Student tries to enroll in a subject whose prerequisite they failed.

### Steps to Demo

**Setup:** Ensure CS501 has prerequisite CS401 (Operating Systems) defined.

1. On Registration Page, scroll to **Advanced AI (CS501)**.
2. Show the card — it has a **red left border** and **"Blocked"** badge.
3. The card shows two red pills:
   - **"❌ Prerequisite not completed: Operating Systems"**
4. The "Cannot Enroll" button is **gray and disabled**.
5. Hover over it — no cursor action.

**What to say:**
> "The system automatically detects prerequisite dependencies. The frontend receives a specific reason for why enrollment is blocked — not just a generic error."

---

## Scenario 3 — GPA Restriction (Credit Hours Limit)

**Story:** A student on Academic Warning has a lower credit hour ceiling.

### Setup
- Log in as a student with GPA 1.85 (below 2.0 threshold).

### Steps

1. Open **GPA Dashboard** → show the **Academic Warning banner** (yellow):
   > "Your GPA (1.85) is below 2.0. Maximum 12 credit hours allowed this semester."
2. Show **GPA card**: "1.85 / 4.0" in amber/orange text.
3. **Standing badge** shows "⚠️ Warning" in yellow.
4. Go to **Registration Page** → **Credit Hours Banner** shows:
   > "0 / **12** hours (reduced due to GPA) — Academic Warning Active"
5. Try to register a 4th subject that would exceed 12 hours.
6. Card shows yellow warning pill:
   > "⚠️ Credit hours limit exceeded: 12 + 3 = 15 > max 12 (GPA: 1.85)"

**What to say:**
> "The system dynamically adjusts allowed credit hours based on GPA — students on academic warning get a reduced ceiling. This is driven by a configurable AcademicPolicy — admins can change the thresholds."

---

## Scenario 4 — Full Offering + Waitlist

**Story:** Student wants to enroll in a full subject and joins the waitlist.

### Setup
- Make an offering at 100% capacity (MaxCapacity reached).

### Steps

1. On Registration Page, find the offering with **"FULL" badge** and **red capacity bar**.
2. Show `enrolledCount / maxCapacity`: "**120/120 enrolled**".
3. Show **"3 on waitlist"** badge.
4. The button shows **"Join Waitlist (pos. 4)"** in amber.
5. Click it → spinner → button changes to **"🕐 Waitlist #4"**.
6. Amber toast: **"You are #4 on the waitlist for Algorithms"**.
7. Show the **"Leave Waitlist"** link below the button.

**What to say:**
> "When an offering is full, we don't just reject the student — we automatically queue them. They get their position number immediately."

---

## Scenario 5 — GPA Auto-Update After Grade Finalization

**Story:** A doctor finalizes grades → student's GPA updates automatically.

### Setup
- Student has no finalized grades → GPA = 0.
- Doctor finalizes grades for one offering.

### Steps

1. Show **Student GPA Dashboard**: "GPA: 0.00, No grades yet".
2. Log in as **Doctor** → go to **Grades Management**.
3. Click **"Calculate & Finalize Grades"** for their offering.
4. Log back in as **Student** → refresh GPA Dashboard.
5. GPA is now **calculated, persisted, and shown**: "GPA: 3.20".
6. Standing badge updates from "—" to **"✅ Good"**.

**What to say:**
> "GPA is now persisted in the database and updated automatically after every grade finalization — there's no manual calculation step."

---

## Scenario 6 — Import Preview with Errors (Dry Run)

**Story:** Admin uploads an Excel file with some bad rows — sees a preview before committing.

### Setup
- Prepare an Excel file with:
  - 10 valid rows
  - 1 row with wrong BatchCode ("CS2022X")
  - 1 row with duplicate NationalId

### Steps

1. Navigate to **Import Students** page.
2. Drag and drop the Excel file.
3. Click **"Upload & Preview"** (triggers dry-run).
4. Show the **Preview Screen**:
   - "✅ Will Succeed: 10 students"
   - "⏭️ Will Skip: 2 rows"
5. Expand the **error table** — shows:
   - `Row 3 | BatchCode | CS2022X | Batch not found`
   - `Row 7 | NationalId | 30305XXXX | Already exists in system`
6. Point out: **"No data has been saved yet."**
7. Click **"✅ Import 10 Students"** to confirm.
8. Show success: "10 students created. 2 skipped."

**What to say:**
> "The import system has a two-phase workflow — preview first, then commit. The admin always knows exactly what will happen before anything is saved."

---

## Scenario 7 — Admin Manages Prerequisites

**Story:** Admin adds a prerequisite relationship for a subject.

### Steps

1. Go to **Admin Panel → Prerequisites**.
2. Select "Advanced AI (CS501)".
3. Click **"Add Prerequisite"**.
4. Select "Operating Systems (CS401)" from dropdown.
5. Optionally set minimum grade: "70".
6. Click **Save**.
7. Preview: "CS501 now requires CS401 (min score: 70)".
8. Return to Registration Page as a student → CS501 card now shows blocker.

---

## Scenario 8 — Academic Policy Configuration

**Story:** Admin views and explains the academic policy.

### Steps

1. Go to **Admin Panel → Academic Policy**.
2. Show the policy table:
   ```
   GPA ≥ 3.5 → Honor tier: 21 credit hours max
   GPA ≥ 2.0 → Standard: 18 credit hours max
   GPA < 2.0 → Warning: 12 credit hours max
   GPA < 1.5 → Probation: 9 credit hours max
   ```
3. Show: "These values are stored in the database — not hardcoded."
4. Mention: "Each department can have its own policy (e.g., medical school may have different rules)."

---

## Demo Setup Checklist

Before presenting, prepare these in the database:

- [ ] 1 student with GPA ≥ 3.5 (Honor tier demo)
- [ ] 1 student with GPA 1.85 (Warning demo)
- [ ] At least 2 offerings: one with room, one at full capacity
- [ ] At least 1 subject with a prerequisite defined
- [ ] The prerequisite subject NOT completed by the Warning student
- [ ] Excel file ready with 10 valid + 2 invalid rows
- [ ] Doctor account with a subject offering and no finalized grades yet
- [ ] AcademicPolicy seeded (use migration — already seeded with defaults)
