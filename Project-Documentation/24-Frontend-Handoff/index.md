---
layout: default
title: "🎓 Frontend Handoff"
---

# 🎓 Frontend Handoff — Academic ERP Smart Registration & GPA System
> **Date:** 2026-05-18  
> **For:** Frontend Development Team  
> **From:** Backend Architecture Team  
> **Build:** Stable — all features deployed and migration applied

---

## 📋 Index

| Document | Description |
|---|---|
| [README.md](./README.md) | This file — start here |
| [01-api-reference.md](./01-api-reference.md) | Every new endpoint with request/response shapes |
| [02-registration-page.md](./02-registration-page.md) | Registration UX requirements (most complex page) |
| [03-gpa-dashboard.md](./03-gpa-dashboard.md) | GPA dashboard and academic status widgets |
| [04-waitlist-ux.md](./04-waitlist-ux.md) | Waitlist joining, queue position, capacity UI |
| [05-import-system-ux.md](./05-import-system-ux.md) | Excel import dry-run, error tables, progress |
| [06-states-and-badges.md](./06-states-and-badges.md) | Every status, badge color, and error message |
| [07-demo-scenarios.md](./07-demo-scenarios.md) | Step-by-step demo walkthroughs for committee |
| [08-polish-and-animations.md](./08-polish-and-animations.md) | WOW factor — animations, transitions, visuals |

---

## 🔄 What Changed in the Backend

The backend was upgraded from a **simple CRUD system** to a **smart Academic Operating System**.

### Before this upgrade
- Enrollment just checked: same department? same batch? same group? → enroll.
- GPA was recalculated fresh on every request — never stored.
- No prerequisite concept existed.
- If an offering was full, enrollment just failed with a generic error.
- Excel import had silent bugs — data was processed but never saved.

### After this upgrade
- Enrollment runs through a **7-policy validation pipeline**.
- GPA is **persisted** in `StudentAcademicStatus` and updated automatically after grade finalization.
- **Prerequisites** can be defined per subject — enrollment is blocked if not completed.
- When an offering is full, the student is **automatically added to a waitlist**.
- Import system now has **dry-run preview mode** and **structured row errors**.

---

## 🚀 Feature Summary for Frontend

| Feature | Impact on Frontend |
|---|---|
| Eligible Offerings API | Registration page shows green/red/yellow per subject |
| 7-Policy Registration | Blocked enrollments show specific reasons |
| Prerequisites | "You must complete X first" message on blocked card |
| AcademicPolicy | "Your GPA allows max 12 hours this semester" banner |
| StudentAcademicStatus | GPA dashboard now has real persisted data |
| Waitlist | "Offering full → Join waitlist (position 3)" button |
| Import dry-run | Upload → Preview errors → Confirm import flow |
| Structured errors | Import row errors have row number, column, fix suggestion |

---

## ⚡ Quick Start for Frontend Developers

### Base URL
```
https://your-backend.railway.app
```

### Authentication
All new endpoints require `Authorization: Bearer {jwt_token}` header.

### Role Requirements
| Endpoint Group | Required Role |
|---|---|
| `GET /api/registration/eligible-offerings` | Student |
| `POST /api/registration/enroll/*` | Student |
| `GET /api/registration/academic-status` | Student |
| `GET/POST/DELETE /api/registration/prerequisites` | Admin, SuperAdmin |
| `GET /api/registration/policy` | Admin, SuperAdmin |

### Response Wrapper
All responses are wrapped:
```json
{
  "success": true,
  "message": "Found 12 offerings.",
  "data": { ... },
  "errors": null
}
```
