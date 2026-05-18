# 📚 Registration Page — Complete UX Requirements

> This is the most complex new page. Read carefully.

---

## Page Flow

```
Student logs in
    │
    ▼
Select semester from dropdown (or use active semester)
    │
    ▼
GET /api/registration/eligible-offerings?semesterId={id}
    │                   + GET /api/registration/academic-status
    ▼
Render offering cards with eligibility states
    │
    ▼
Student clicks "Enroll" or "Join Waitlist"
    │
    ▼
POST /api/registration/enroll/{offeringId}
    │
    ▼
Show result → update card state
```

---

## Credit Hours Banner (TOP of page — always visible)

```
┌─────────────────────────────────────────────────────────────────┐
│  📚 Course Registration — Fall 2026                              │
│                                                                 │
│  Registered This Semester: 9 / 18 hours     GPA: 3.42 ● Good   │
│  ████████████░░░░░░░░░░  50%                                    │
│                                                                 │
│  ⚠️  [warning banner if hasWarning == true]                      │
└─────────────────────────────────────────────────────────────────┘
```

**Implementation notes:**
- `maxAllowedHours` comes from `GET /api/registration/academic-status`.
- Progress bar: `(currentSemesterHours / maxAllowedHours) * 100%`.
- Color: green < 60%, amber 60-90%, red > 90%.
- If `hasWarning == true` → show `warningMessage` as a persistent amber/red alert below the header.

---

## Warning Banner (conditional — show when `hasWarning: true`)

```
┌─────────────────────────────────────────────────────────────────┐
│  ⚠️  Academic Warning                                            │
│  Your GPA (1.85) is below 2.0. You may register a maximum of   │
│  12 credit hours this semester.                                 │
│  [Dismiss ×]                                                    │
└─────────────────────────────────────────────────────────────────┘
```

For Probation (orange):
```
┌─────────────────────────────────────────────────────────────────┐
│  🔴 Academic Probation                                          │
│  Your GPA (1.40) is below 1.5. Maximum 9 hours allowed.        │
│  Improve your GPA next semester to avoid suspension.            │
└─────────────────────────────────────────────────────────────────┘
```

---

## Subject Cards Layout

### ELIGIBLE Card (isEligible: true, isFull: false)

```
┌──────────────────────────────────────────────────────────┐
│  CS301 — Data Structures                    ● 3 hrs      │
│  Dr. Ahmed Hassan  │  Group A                            │
│  ████████████████░░░░  87/120 enrolled                   │
│                                                          │
│                          [   Enroll →   ]   ✅ Eligible  │
└──────────────────────────────────────────────────────────┘
```

**Styling:**
- Border: `border-l-4 border-green-500`
- Badge: `bg-green-100 text-green-800` "Eligible"
- Button: filled green, enabled

---

### BLOCKED Card (isEligible: false, blockers.length > 0)

```
┌──────────────────────────────────────────────────────────┐
│  CS501 — Advanced AI                        ● 3 hrs      │
│  Dr. Sara Mostafa  │  All Groups              [BLOCKED]  │
│  ████████████████████  60/60  FULL                       │
│                                                          │
│  ❌ Prerequisite not completed: Operating Systems        │
│  ❌ Must PASS prerequisite: Algorithms (currently: F)    │
│                                                          │
│                          [  Cannot Enroll  ]  🔴 Blocked │
└──────────────────────────────────────────────────────────┘
```

**Styling:**
- Border: `border-l-4 border-red-400`
- Background: `bg-red-50`
- Badge: `bg-red-100 text-red-700` "Blocked"
- Button: disabled, grayed out, cursor-not-allowed
- Each blocker: small red pill `bg-red-100 text-red-700 text-sm`
- Add expand/collapse if > 2 blockers: "Show 2 more reasons ▼"

---

### WARNING Card (isEligible: true, warnings.length > 0)

```
┌──────────────────────────────────────────────────────────┐
│  CS401 — Software Engineering               ● 3 hrs  ⚠️  │
│  Dr. Omar Said  │  All Groups                            │
│  ████████████████████░░  78/80 enrolled                  │
│                                                          │
│  ⚠️  Academic Warning active: Max 12 hours this semester │
│                                                          │
│                          [   Enroll →   ]   ⚠️ Warning   │
└──────────────────────────────────────────────────────────┘
```

**Styling:**
- Border: `border-l-4 border-yellow-400`
- Badge: `bg-yellow-100 text-yellow-800` "Warning"
- Button: still enabled (warnings don't block)
- Warning pills: `bg-yellow-50 text-yellow-700 text-sm`

---

### FULL + WAITLIST Card (isFull: true, isEligible: true)

```
┌──────────────────────────────────────────────────────────┐
│  CS302 — Algorithms                         ● 3 hrs      │
│  Dr. Nour Eldin  │  Group B                              │
│  ████████████████████  100/100  FULL  [3 on waitlist]    │
│                                                          │
│                   [ Join Waitlist (#4) ] 🟡 Full         │
└──────────────────────────────────────────────────────────┘
```

**Styling:**
- Border: `border-l-4 border-amber-400`
- Capacity bar: fully filled, red color
- "FULL" badge: `bg-red-100 text-red-700`
- Waitlist badge: `bg-amber-100 text-amber-700` e.g., "3 on waitlist"
- Button: amber "Join Waitlist (#4)" — position = waitlistCount + 1

---

### ALREADY ENROLLED Card

```
┌──────────────────────────────────────────────────────────┐
│  CS301 — Data Structures                    ● 3 hrs      │
│  Dr. Ahmed Hassan  │  Group A                            │
│  ████████████████░░░░  88/120                            │
│                                                          │
│                          [ ✓ Enrolled ]     ✅ Enrolled  │
└──────────────────────────────────────────────────────────┘
```

**Styling:**
- Border: `border-l-4 border-blue-400`
- Badge: `bg-blue-100 text-blue-700` "Enrolled"
- Button: static "✓ Enrolled", not clickable

---

### ON WAITLIST Card

```
┌──────────────────────────────────────────────────────────┐
│  CS302 — Algorithms                         ● 3 hrs      │
│  Dr. Nour Eldin  │  Group B                              │
│  ████████████████████  100/100  FULL                     │
│                                                          │
│  🕐 You are #4 on the waitlist                           │
│                          [ Leave Waitlist ]  🟡 Waitlist │
└──────────────────────────────────────────────────────────┘
```

---

## Capacity Progress Bar

```
[█████████████████░░░░░]  87 / 120
```

- 0-60%: `bg-green-500`
- 60-85%: `bg-yellow-500`
- 85-99%: `bg-orange-500`
- 100%: `bg-red-600` + "FULL" label

---

## Filters & Sort Bar

```
[ All (12) ] [ Eligible (8) ] [ Blocked (3) ] [ Full (1) ]

Search: [_______________] Sort: [Name ▾] Credits: [All ▾]
```

- Default tab: "All"
- "Eligible" tab filters `isEligible: true`
- "Blocked" tab filters `isEligible: false`
- Search filters by `subjectName` or `subjectCode`
- Sort options: Name A-Z, Credits ↑, Enrolled % ↑

---

## Loading State (while fetching)

Show skeleton cards — 6 placeholder cards with shimmer animation.

```
┌──────────────────────────────────────────────────────────┐
│  ████████████████░░░░░░░░░░░░  (shimmer)                 │
│  ████████░░░░  │  ████░░░░                               │
│  ████████████████████░░░░  87/120                        │
│                                                          │
│                          [ ████████████░░ ]              │
└──────────────────────────────────────────────────────────┘
```

---

## Empty State (no offerings found)

```
     📭
  No offerings available for this semester.
  Contact your academic advisor or check back later.
```

---

## Post-Enroll Success Flow

1. Click "Enroll →" → button changes to spinner `⏳ Enrolling...`
2. Response arrives:
   - **Success** → button becomes `✓ Enrolled` (blue, static) + green toast
   - **Waitlist** → button becomes `🕐 Waitlist #4` (amber) + amber toast
   - **Error** → button resets to `Enroll →` + red toast with error message
3. Credit hours banner updates: `9 → 12 hours` with smooth animation.

### Toast Messages
```
✅ "Enrolled in Data Structures successfully!"
🕐 "Added to waitlist for Algorithms — you are #4"
❌ "Cannot enroll: Prerequisite not completed: Operating Systems"
```
