# 📊 GPA Dashboard — UX Requirements

> **API:** `GET /api/registration/academic-status`  
> **Role:** Student  
> Call on every student dashboard load.

---

## Dashboard Layout (Student Home Page)

```
┌─────────────────────────────────────────────────────────────────────┐
│                     🎓 Academic Performance                          │
├──────────────┬──────────────┬──────────────┬──────────────────────  │
│  GPA         │  CGPA        │  Last Sem GPA │  Standing              │
│  3.42        │  3.38        │  3.65         │  ● Good                │
│  /4.0        │  /4.0        │  /4.0         │  (green badge)         │
├──────────────┴──────────────┴──────────────┴──────────────────────  │
│                                                                       │
│  Credit Hours Progress                                                │
│  ████████████████████░░░░░░░░░░░░░░  87 / 145  (60%)               │
│  Earned: 87  │  Remaining: 58  │  Current Level: 3                   │
│                                                                       │
│  This Semester: Max 18 hours allowed (GPA: 3.42 — Honor eligible!)  │
└─────────────────────────────────────────────────────────────────────┘
```

---

## GPA Stat Cards

Each card is a separate visual widget.

### Card 1 — Current GPA
```
┌────────────────────┐
│       GPA           │
│    3.42 / 4.0       │
│  ● Good Standing   │
└────────────────────┘
```
- Number: large font `text-4xl font-bold`
- Color based on value:
  - `>= 3.5` → green text + green ring `ring-green-400`
  - `>= 2.5` → blue text + blue ring
  - `>= 2.0` → yellow text + yellow ring
  - `< 2.0` → red text + red ring + pulsing animation

### Card 2 — CGPA (Cumulative)
Same design as GPA card but labeled "CGPA".

### Card 3 — Last Semester GPA
```
┌────────────────────┐
│  Last Semester GPA  │
│      3.65 / 4.0    │
│  ▲ +0.23 vs CGPA   │
└────────────────────┘
```
- Show delta vs CGPA: `▲ +0.23` (green) or `▼ -0.15` (red)

### Card 4 — Academic Standing Badge
```
┌────────────────────┐
│     Standing        │
│   ● Good           │
│   (no warnings)     │
└────────────────────┘
```

**Standing badge colors:**
| Standing | Color | Icon | Description |
|---|---|---|---|
| `Good` | `bg-green-100 text-green-800` | ✅ | Normal standing |
| `Warning` | `bg-yellow-100 text-yellow-800` | ⚠️ | GPA < 2.0 once |
| `Probation` | `bg-orange-100 text-orange-800` | 🔶 | GPA < 1.5 |
| `Suspended` | `bg-red-100 text-red-800` | 🚫 | Cannot register |
| `Graduated` | `bg-blue-100 text-blue-800` | 🎓 | Completed |
| `Expelled` | `bg-gray-100 text-gray-600` | ❌ | Terminated |

---

## Credit Hours Progress Section

```
Credit Hours Progress
──────────────────────────────────────────────────────────────
Earned:    87  ████████████████████████████████░░░░░░  60%
Required: 145

Breakdown:
  This semester registered: 12 / 18 max
  Earned to date: 87
  Still needed: 58
  Level: Year 3
```

**Implementation:**
- Main bar: `earnedHours / totalRequired * 100`
- Color: green if > 50%, amber if < 30%, red if very low
- Current semester sub-bar: current registered hours / maxAllowedHours
- Animate bar fill on page load (0 → value, 800ms ease-out)

---

## Warning Alert Banner (conditional)

Show this above everything if `hasWarning == true`:

### Academic Warning (yellow)
```
╔═══════════════════════════════════════════════════════════╗
║  ⚠️  ACADEMIC WARNING                                     ║
║  Your GPA (1.85) is below 2.0.                           ║
║  Maximum 12 credit hours allowed this semester.          ║
║  Improve your GPA to restore full enrollment capacity.   ║
╚═══════════════════════════════════════════════════════════╝
```
Color: `bg-yellow-50 border border-yellow-400`

### Academic Probation (orange)
```
╔═══════════════════════════════════════════════════════════╗
║  🔶  ACADEMIC PROBATION                                   ║
║  Your GPA (1.40) is below 1.5.                           ║
║  Maximum 9 credit hours this semester.                   ║
║  This is your final opportunity to improve before        ║
║  suspension. Contact your academic advisor.              ║
╚═══════════════════════════════════════════════════════════╝
```
Color: `bg-orange-50 border border-orange-500`

### Suspended (red, permanent — no dismiss)
```
╔═══════════════════════════════════════════════════════════╗
║  🚫  ENROLLMENT SUSPENDED                                 ║
║  Your academic enrollment has been suspended.            ║
║  You cannot register for any courses.                    ║
║  Please contact the Academic Affairs Office immediately. ║
╚═══════════════════════════════════════════════════════════╝
```
Color: `bg-red-50 border border-red-600`  
No dismiss button — persistent until admin lifts suspension.

---

## GPA Donut Chart Widget (optional but impressive for demo)

```
        ╭─────╮
       /  3.42  \
      |    /4.0  |
       \         /
        ╰───────╯
     ████████████░░  86%
```

- Fill: `strokeDasharray` animation 0 → value
- Color: same as GPA value color rules above

---

## Max Allowed Hours Widget

```
┌────────────────────────────────────────────┐
│  📋 Registration Limit This Semester       │
│                                            │
│  Your GPA: 3.42  → Tier: Standard         │
│  Max Hours: 18                             │
│                                            │
│  ≥ 3.5 GPA → Honor tier: 21 hrs          │
│  ≥ 2.0 GPA → Standard: 18 hrs ← YOU      │
│  < 2.0 GPA → Warning: 12 hrs             │
│  < 1.5 GPA → Probation: 9 hrs            │
└────────────────────────────────────────────┘
```

This widget makes the intelligent GPA-based system visible and explainable to reviewers.

---

## Enrollment Summary List (from `/api/registration/my-enrollments-summary`)

```
Current Semester Enrollments (4 subjects — 12 hours)
──────────────────────────────────────────────────────────────
CS301  Data Structures      3 hrs  Dr. Ahmed Hassan  ✓ Enrolled
CS302  Algorithms           3 hrs  Dr. Nour Eldin    ✓ Enrolled
CS401  Software Engineering 3 hrs  Dr. Omar Said     ✓ Enrolled
MATH201 Calculus 2          3 hrs  Dr. Hana Ahmed    ✓ Enrolled
──────────────────────────────────────────────────────────────
Total: 12 / 18 hours registered
```
