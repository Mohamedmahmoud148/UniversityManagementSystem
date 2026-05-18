# 🕐 Waitlist UX — Complete Guide

---

## How Waitlist Works (backend logic)

1. Student tries to enroll in a full offering.
2. Backend runs full validation pipeline — if eligible, **auto-adds to waitlist** instead of rejecting.
3. API returns `{ success: false, addedToWaitlist: true, waitlistPosition: 4 }`.
4. When another student unenrolls, the admin/system can enroll waitlisted students.

---

## Enrollment Button States (on Offering Card)

```
State 1 — Normal (not full)
[ Enroll → ]  (green filled button)

State 2 — Near capacity warning
[ Enroll → ]  ⚠️  (green button + amber warning: "Only 2 spots left")

State 3 — Full, eligible for waitlist
[ Join Waitlist (pos. 4) ]  (amber outlined button)

State 4 — Already on waitlist
[ 🕐 Waitlist #4 ]  (amber filled button)
[ Leave Waitlist ]  (small red text link below)

State 5 — Full, already enrolled somewhere else (blocked)
[ Cannot Enroll ]  (gray disabled)
Reason: [red pill] "You are already enrolled in this offering."
```

---

## Joining Waitlist — UX Flow

```
1. Student clicks "Join Waitlist (pos. 4)"
        ↓
2. Button shows spinner: "⏳ Joining..."
        ↓
3. POST /api/registration/waitlist/{offeringId}
        ↓
4a. Success (200):
    - Button → "🕐 Waitlist #4" (amber, not clickable)
    - Small link appears: "Leave Waitlist"
    - Amber toast: "You are #4 on the waitlist for Algorithms"
    - Card gets amber left border

4b. Error (already on waitlist):
    - Toast: "You are already on the waitlist at position 3"
    - Button updates to show correct position
```

---

## Leaving Waitlist — UX Flow

```
1. Student clicks "Leave Waitlist" link
        ↓
2. Confirmation dialog:
   "Leave the waitlist for Algorithms?
    You will lose your position (#4)."
   [Cancel]  [Leave Waitlist]
        ↓
3. DELETE /api/registration/waitlist/{offeringId}
        ↓
4. Success:
   - Button → "Join Waitlist" (amber outlined, clickable again)
   - Toast: "Removed from waitlist"
   - Position badge removed
```

---

## Capacity Visualization

### Progress bar on offering card

```
[ 87/120 enrolled ]
████████████████████████░░░░░░  72%
```

### Full offering

```
[ 120/120 enrolled ]  FULL
████████████████████████████████  100%
```

### With waitlist count

```
[ 120/120 ]  FULL  +4 on waitlist
████████████████████████████████
```

---

## Waitlist Badge in Subject Card Header

When `waitlistCount > 0`, show:
```
┌──────────────────────────────────────────────────────────┐
│  CS302 — Algorithms          ● 3 hrs    [3 on waitlist]  │
│                                          ╰─ amber badge  │
```

---

## Capacity Color Rules

| Filled % | Bar Color | Status Label |
|---|---|---|
| 0–59% | `bg-green-500` | — |
| 60–84% | `bg-yellow-500` | — |
| 85–99% | `bg-orange-500` | "Almost Full" badge |
| 100% | `bg-red-600` | "FULL" badge |

---

## Full Capacity Banner on Offering Detail Page

If the user opens the offering detail page and it's full:

```
┌──────────────────────────────────────────────────────────┐
│  ⚠️  This offering is currently FULL (120/120 students)   │
│  You can join the waitlist — current queue: 3 students   │
│  You would be position #4.                              │
│                                                          │
│       [ Join Waitlist — I'll be #4 ]                    │
└──────────────────────────────────────────────────────────┘
```
