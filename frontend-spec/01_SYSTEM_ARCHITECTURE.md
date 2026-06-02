# 01 — System Architecture

## High-Level Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                    FRONTEND (React + TypeScript)              │
│                                                              │
│  ┌─────────────────┐   ┌──────────────────┐                 │
│  │   Auth Store     │   │  User Store       │                 │
│  │   (Zustand)      │   │  (Zustand)        │                 │
│  └────────┬────────┘   └────────┬─────────┘                 │
│           │                     │                            │
│  ┌────────▼─────────────────────▼─────────┐                 │
│  │          Router (React Router v6)        │                 │
│  │  /student/* /doctor/* /admin/* /auth/*   │                 │
│  └────────────────────┬────────────────────┘                 │
│                        │                                      │
│  ┌────────────────────▼────────────────────┐                 │
│  │          React Query Cache               │                 │
│  │  (Server state, background refetch)      │                 │
│  └──────┬─────────────────────────┬────────┘                 │
│         │                         │                          │
│  ┌──────▼────────┐  ┌─────────────▼──────────┐              │
│  │  .NET API      │  │  FastAPI AI Service     │              │
│  │  Client        │  │  Client                 │              │
│  │  (Axios)       │  │  (Axios)                │              │
│  └───────────────┘  └────────────────────────┘              │
│                                                              │
│  ┌──────────────────────────────────────────┐               │
│  │  SignalR / WebSocket Client               │               │
│  │  (Real-time notifications)                │               │
│  └──────────────────────────────────────────┘               │
└──────────────────────────────────────────────────────────────┘
         │                          │
         ▼                          ▼
  .NET Backend API           FastAPI AI Service
  (Port 443/HTTPS)           (Port 443/HTTPS)
  auth, students,            /api/chat
  exams, grades,             /api/companion/*
  attendance,                /api/teaching-intelligence/*
  materials,                 /api/rag/*
  analytics                  (all AI features)
```

---

## Frontend Folder Structure

```
src/
├── api/                        # API client functions
│   ├── auth.ts                 # POST /api/auth/login, /refresh, etc.
│   ├── students.ts             # /api/students/*
│   ├── doctors.ts              # /api/doctors/*
│   ├── exams.ts                # /api/exams/*
│   ├── assignments.ts          # /api/assignments/*
│   ├── grades.ts               # /api/grades/*
│   ├── attendance.ts           # /api/attendance/*
│   ├── materials.ts            # /api/materials/*
│   ├── complaints.ts           # /api/complaints/*
│   ├── notifications.ts        # /api/notification/*
│   ├── chat.ts                 # /api/Chat/*
│   ├── companion.ts            # /api/companion/*
│   ├── teaching.ts             # /api/teaching-intelligence/*
│   ├── analytics.ts            # /api/analytics/*
│   ├── enrollment.ts           # /api/enrollments/*
│   ├── regulations.ts          # /api/Regulations/*
│   ├── structure.ts            # /api/university|colleges|departments|batches
│   └── aiService.ts            # FastAPI endpoints
│
├── components/                 # Shared reusable components
│   ├── ui/                     # Base Shadcn components
│   ├── charts/                 # Chart wrappers
│   ├── forms/                  # Form components
│   ├── layout/                 # Layout components
│   ├── tables/                 # Data table components
│   ├── ai/                     # AI-specific components
│   └── notifications/          # Notification components
│
├── features/                   # Feature modules
│   ├── auth/                   # Login, register, change password
│   ├── student/                # All student pages
│   ├── doctor/                 # All doctor pages
│   ├── admin/                  # All admin pages
│   ├── exams/                  # Exam taking, management
│   ├── assignments/            # Assignment submission, grading
│   ├── chat/                   # AI chat interface
│   ├── companion/              # AI companion features
│   ├── teaching/               # Teaching intelligence
│   ├── materials/              # Course materials
│   ├── attendance/             # Attendance management
│   ├── complaints/             # Complaint system
│   └── notifications/          # Notifications
│
├── hooks/                      # Custom React hooks
│   ├── useAuth.ts
│   ├── useNotifications.ts
│   ├── useRealtime.ts
│   ├── useRole.ts
│   └── useDebounce.ts
│
├── lib/                        # Utility functions
│   ├── axios.ts                # Axios instance + interceptors
│   ├── queryClient.ts          # React Query configuration
│   ├── formatters.ts           # Date, number, grade formatters
│   └── validators.ts           # Zod schemas
│
├── store/                      # Zustand stores
│   ├── authStore.ts
│   ├── userStore.ts
│   ├── notificationStore.ts
│   └── chatStore.ts
│
├── types/                      # TypeScript interfaces
│   ├── api.ts                  # API response types
│   ├── entities.ts             # Entity types
│   └── ui.ts                   # UI component types
│
├── router/                     # Routing configuration
│   ├── index.tsx               # Root router
│   ├── studentRoutes.tsx       # Student-only routes
│   ├── doctorRoutes.tsx        # Doctor-only routes
│   ├── adminRoutes.tsx         # Admin-only routes
│   └── guards.tsx              # Route protection HOCs
│
├── i18n/                       # Translations
│   ├── ar.json                 # Arabic translations
│   └── en.json                 # English translations
│
└── styles/                     # Global styles
    ├── globals.css             # Tailwind + CSS variables
    └── themes.css              # Light/dark theme tokens
```

---

## Axios Configuration

```typescript
// lib/axios.ts
import axios from 'axios';

export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  headers: { 'Content-Type': 'application/json' },
});

export const aiClient = axios.create({
  baseURL: import.meta.env.VITE_AI_SERVICE_URL,
  headers: { 'Content-Type': 'application/json' },
});

// Request interceptor — attach JWT
apiClient.interceptors.request.use((config) => {
  const token = localStorage.getItem('access_token');
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

// Response interceptor — auto refresh on 401
apiClient.interceptors.response.use(
  (response) => response,
  async (error) => {
    if (error.response?.status === 401) {
      // attempt token refresh
      const refreshed = await refreshAccessToken();
      if (refreshed) return apiClient(error.config);
      // redirect to login
      window.location.href = '/auth/login';
    }
    return Promise.reject(error);
  }
);
```

---

## React Query Configuration

```typescript
// lib/queryClient.ts
import { QueryClient } from '@tanstack/react-query';

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 5 * 60 * 1000,         // 5 minutes
      gcTime: 10 * 60 * 1000,           // 10 minutes
      retry: 2,
      retryDelay: (attempt) => Math.min(1000 * 2 ** attempt, 30000),
      refetchOnWindowFocus: false,
    },
    mutations: {
      retry: 0,
    },
  },
});
```

---

## Route Protection

```typescript
// router/guards.tsx
type RequiredRole = 'Student' | 'Doctor' | 'Admin' | 'SuperAdmin' | 'TeachingAssistant';

export function ProtectedRoute({ 
  roles, 
  children 
}: { roles?: RequiredRole[]; children: React.ReactNode }) {
  const { user, isAuthenticated } = useAuthStore();
  
  if (!isAuthenticated) return <Navigate to="/auth/login" />;
  if (roles && !roles.includes(user.role)) return <Navigate to="/unauthorized" />;
  
  return <>{children}</>;
}
```

---

## SignalR / WebSocket Setup

The backend exposes a SignalR hub at `/hubs/notifications`.

```typescript
// hooks/useRealtime.ts
import * as signalR from '@microsoft/signalr';

export function useRealtime() {
  const connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/notifications', {
      accessTokenFactory: () => localStorage.getItem('access_token') || '',
    })
    .withAutomaticReconnect()
    .build();

  connection.on('ReceiveNotification', (notification) => {
    useNotificationStore.getState().addNotification(notification);
  });

  connection.start();
}
```

---

## RTL/LTR Handling

```typescript
// main.tsx
const locale = localStorage.getItem('locale') || 'ar';
document.documentElement.dir = locale === 'ar' ? 'rtl' : 'ltr';
document.documentElement.lang = locale;
```

Tailwind RTL utilities: use `rtl:` prefix — e.g., `rtl:flex-row-reverse`, `rtl:text-right`.
