# 35 — Complete Build Guide

## Quick Start for Claude Code

This guide tells you exactly what to build, in what order, using which files in this spec.

---

## Step 1: Read These Files First
1. `00_PROJECT_OVERVIEW.md` — understand the platform
2. `02_USER_TYPES.md` — understand the roles
3. `05_ENTITY_REFERENCE.md` — all TypeScript interfaces
4. `04_API_CATALOG.md` — all endpoints

---

## Step 2: Project Setup
```bash
npm create vite@latest university-platform -- --template react-ts
cd university-platform
npm install

# Core dependencies
npm install @tanstack/react-query @tanstack/react-router
npm install zustand axios
npm install react-hook-form @hookform/resolvers zod
npm install framer-motion
npm install recharts
npm install react-markdown remark-gfm
npm install date-fns
npm install xlsx
npm install @microsoft/signalr
npm install sonner  # toast notifications

# Shadcn/UI
npx shadcn@latest init
npx shadcn@latest add button card input form table dialog toast badge progress skeleton tabs

# i18n
npm install react-i18next i18next

# Icons  
npm install lucide-react

# QR code (for attendance)
npm install html5-qrcode qrcode.react

# RTL support
npm install tailwindcss-logical  # or use tailwind v3 rtl: prefix
```

---

## Step 3: Create File Structure
Reference `01_SYSTEM_ARCHITECTURE.md` → section "Frontend Folder Structure"

Create exactly:
```
src/api/ src/components/ src/features/ src/hooks/
src/lib/ src/store/ src/types/ src/router/
src/i18n/ src/styles/
```

---

## Step 4: Implement in Phase Order

### Phase 1 — Run after reading `23_FRONTEND_ARCHITECTURE.md`
1. `src/lib/axios.ts` — Axios with interceptors
2. `src/lib/queryClient.ts`
3. `src/store/authStore.ts`
4. `src/router/index.tsx` + guards
5. `src/components/layout/` (Navbar, Sidebar, RootLayout)
6. `/auth/login` page + form

### Phase 2 — Run after reading `06_STUDENT_EXPERIENCE.md`
1. `/student/dashboard`
2. `/student/grades`
3. `/student/attendance`
4. `/student/roadmap`
5. All student data fetching hooks

### Phase 3 — Run after reading `07_DOCTOR_EXPERIENCE.md`
1. `/doctor/dashboard` (basic)
2. `/doctor/exams` (list + create)
3. `/doctor/assignments`
4. `/doctor/materials`

### Phase 4 — Run after reading `08_ADMIN_EXPERIENCE.md`
1. `/admin/dashboard`
2. `/admin/structure` (CRUD modals)
3. `/admin/students` (with import)

### Phase 5 — Run after reading `13_EXAMINATION_PLATFORM.md`
1. `/student/exams/{id}/take` (full exam UI)
2. Proctoring event tracking
3. Timer component
4. Exam result page

### Phase 6 — Run after reading `09_AI_CHAT_PLATFORM.md`
1. `/chat` full page
2. Conversation sidebar
3. Message components
4. AI typing indicator

### Phase 7 — Run after reading `10_AI_COMPANION_PLATFORM.md` + `11_TEACHING_INTELLIGENCE_PLATFORM.md`
1. `/student/companion/*` (all sub-pages)
2. Flashcard flip animation
3. Quiz session flow
4. `/doctor/dashboard` (Teaching Intelligence version)
5. Student intelligence table with Excel export

---

## Step 5: Final Checklist

Before declaring done, verify `32_QA_TESTING_CHECKLIST.md` (all checkboxes).

---

## Key Files Cross-Reference

| What you're building | Read this file |
|---------------------|---------------|
| Any page | `17_PAGE_MAP.md` for routes |
| Any component | `19_COMPONENT_LIBRARY.md` for specs |
| Any API call | `04_API_CATALOG.md` for exact params |
| Any TypeScript type | `05_ENTITY_REFERENCE.md` |
| Design decisions | `20_DESIGN_SYSTEM.md` |
| State management | `24_STATE_MANAGEMENT.md` |
| Loading states | `29_LOADING_STATES.md` |
| Empty states | `28_EMPTY_STATES.md` |
| Errors | `27_ERROR_HANDLING.md` |
| Animations | `22_ANIMATIONS_AND_MICROINTERACTIONS.md` |
| Excel export | `07_DOCTOR_EXPERIENCE.md` + `11_TEACHING_INTELLIGENCE_PLATFORM.md` |
| AI intents | `34_FRONTEND_AI_MAPPING.md` |

---

## Environment Setup
```env
VITE_API_BASE_URL=http://localhost:5000
VITE_AI_SERVICE_URL=http://localhost:8000
VITE_APP_NAME=University AI Platform
VITE_DEFAULT_LOCALE=ar
```

## Production Build
```bash
npm run build
# Output: dist/ folder
# Deploy to: Vercel / Netlify / Nginx
```

---

## Important Notes for Claude Code

1. **All dates from API are UTC ISO 8601** — convert for display using date-fns
2. **All API errors return JSON** — use `handleApiError()` from `27_ERROR_HANDLING.md`
3. **Arabic is RTL** — use `dir="rtl"` on html element and tailwind `rtl:` prefix
4. **AI responses are markdown** — always render with `react-markdown`
5. **Exam taking is fullscreen** — prevent navigation during active exam
6. **File uploads use multipart/form-data** — not JSON
7. **Excel export uses SheetJS** — build file in browser, trigger download
8. **Notifications use SignalR** — implement connection in `useRealtime` hook
9. **Risk scores are 0-100** — color-code using `getRiskColor()` from `11_TEACHING_INTELLIGENCE_PLATFORM.md`
10. **The frontend NEVER calls FastAPI directly** — all AI goes through .NET `/api/Chat/messages`
