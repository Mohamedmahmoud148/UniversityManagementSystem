# 23 — Frontend Architecture

## Stack Summary
React 18 + TypeScript + Vite + Tailwind CSS + Shadcn/UI + React Query + Zustand + React Router v6

---

## Application Entry

```tsx
// main.tsx
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClientProvider } from '@tanstack/react-query';
import { RouterProvider } from 'react-router-dom';
import { queryClient } from './lib/queryClient';
import { router } from './router';
import './i18n/i18n';
import './styles/globals.css';

// Set RTL/LTR based on stored locale
const locale = localStorage.getItem('locale') || 'ar';
document.documentElement.dir = locale === 'ar' ? 'rtl' : 'ltr';
document.documentElement.lang = locale;

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  </StrictMode>
);
```

---

## Router Configuration

```tsx
// router/index.tsx
import { createBrowserRouter } from 'react-router-dom';
import { RootLayout } from '../components/layout/RootLayout';
import { ProtectedRoute } from './guards';
import { studentRoutes } from './studentRoutes';
import { doctorRoutes } from './doctorRoutes';
import { adminRoutes } from './adminRoutes';

export const router = createBrowserRouter([
  {
    path: '/',
    element: <RootLayout />,
    children: [
      // Auth routes (no layout)
      { path: 'auth/login', element: <LoginPage /> },
      { path: 'auth/change-password', element: <ChangePasswordPage /> },
      
      // Shared authenticated routes
      {
        element: <ProtectedRoute />,
        children: [
          { path: 'chat', element: <ChatPage /> },
          { path: 'profile', element: <ProfilePage /> },
          { path: 'notifications', element: <NotificationsPage /> },
        ],
      },
      
      // Role-specific routes
      {
        path: 'student',
        element: <ProtectedRoute roles={['Student', 'SuperAdmin']} />,
        children: studentRoutes,
      },
      {
        path: 'doctor',
        element: <ProtectedRoute roles={['Doctor', 'SuperAdmin']} />,
        children: doctorRoutes,
      },
      {
        path: 'admin',
        element: <ProtectedRoute roles={['Admin', 'SuperAdmin']} />,
        children: adminRoutes,
      },
      
      // Error pages
      { path: 'unauthorized', element: <UnauthorizedPage /> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]);
```

---

## Layout Components

### RootLayout
```tsx
function RootLayout() {
  const { isAuthenticated } = useAuthStore();
  
  if (!isAuthenticated) return <Outlet />;
  
  return (
    <div className="flex h-screen overflow-hidden">
      <Sidebar />
      <div className="flex-1 flex flex-col overflow-hidden">
        <Navbar />
        <main className="flex-1 overflow-y-auto bg-background">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
```

### Navbar
```tsx
function Navbar() {
  const { user } = useAuthStore();
  const { unreadCount } = useNotificationStore();
  
  return (
    <header className="h-16 border-b bg-background flex items-center px-6 gap-4">
      {/* Mobile hamburger */}
      <SidebarToggle className="md:hidden" />
      
      {/* Breadcrumb */}
      <Breadcrumb />
      
      <div className="flex-1" />
      
      {/* Search */}
      <GlobalSearch />
      
      {/* Streak badge (student only) */}
      {user?.role === 'Student' && <StreakBadge />}
      
      {/* Notifications */}
      <NotificationBell count={unreadCount} />
      
      {/* AI Chat shortcut */}
      <Link to="/chat">
        <Button variant="ghost" size="sm">
          <Sparkles className="w-4 h-4 mr-2" />
          AI Chat
        </Button>
      </Link>
      
      {/* User menu */}
      <UserMenu />
    </header>
  );
}
```

---

## Zustand Stores

### Auth Store
```typescript
// store/authStore.ts
interface AuthState {
  user: UserProfile | null;
  accessToken: string | null;
  refreshToken: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  
  login: (credentials: LoginCredentials) => Promise<void>;
  logout: () => void;
  refreshTokens: () => Promise<boolean>;
  setUser: (user: UserProfile) => void;
}

export const useAuthStore = create<AuthState>((set, get) => ({
  user: null,
  accessToken: localStorage.getItem('access_token'),
  refreshToken: localStorage.getItem('refresh_token'),
  isAuthenticated: !!localStorage.getItem('access_token'),
  isLoading: false,
  
  login: async (credentials) => {
    set({ isLoading: true });
    const response = await authApi.login(credentials);
    localStorage.setItem('access_token', response.token);
    localStorage.setItem('refresh_token', response.refreshToken);
    set({ 
      user: response.user,
      accessToken: response.token,
      refreshToken: response.refreshToken,
      isAuthenticated: true,
      isLoading: false 
    });
  },
  
  logout: () => {
    localStorage.removeItem('access_token');
    localStorage.removeItem('refresh_token');
    set({ user: null, accessToken: null, refreshToken: null, isAuthenticated: false });
    queryClient.clear();
    window.location.href = '/auth/login';
  },
  
  refreshTokens: async () => {
    const refreshToken = get().refreshToken;
    if (!refreshToken) return false;
    try {
      const response = await authApi.refreshToken({ token: get().accessToken!, refreshToken });
      localStorage.setItem('access_token', response.token);
      localStorage.setItem('refresh_token', response.refreshToken);
      set({ accessToken: response.token, refreshToken: response.refreshToken });
      return true;
    } catch {
      get().logout();
      return false;
    }
  },
}));
```

### Notification Store
```typescript
interface NotificationState {
  notifications: NotificationDto[];
  unreadCount: number;
  
  setNotifications: (n: NotificationDto[]) => void;
  addNotification: (n: NotificationDto) => void;
  markRead: (id: string) => void;
  markAllRead: () => void;
}

export const useNotificationStore = create<NotificationState>((set) => ({
  notifications: [],
  unreadCount: 0,
  
  setNotifications: (notifications) => set({
    notifications,
    unreadCount: notifications.filter(n => !n.isRead).length,
  }),
  
  addNotification: (n) => set(state => ({
    notifications: [n, ...state.notifications],
    unreadCount: state.unreadCount + 1,
  })),
  
  markRead: (id) => set(state => ({
    notifications: state.notifications.map(n => n.id === id ? {...n, isRead: true} : n),
    unreadCount: Math.max(0, state.unreadCount - 1),
  })),
}));
```

---

## Custom Hooks

### useCurrentUser
```typescript
export function useCurrentUser() {
  const { user } = useAuthStore();
  const { data: profile } = useQuery({
    queryKey: ['auth', 'me'],
    queryFn: () => authApi.getMe(),
    enabled: !!user,
    staleTime: 10 * 60 * 1000,
  });
  return { user: profile || user };
}
```

### useNotifications (with polling)
```typescript
export function useNotifications() {
  const { setNotifications, addNotification } = useNotificationStore();
  
  const { data } = useQuery({
    queryKey: ['notifications'],
    queryFn: () => notificationApi.getAll(),
    refetchInterval: 60_000, // poll every minute
    onSuccess: (data) => setNotifications(data),
  });
  
  // SignalR for real-time
  useEffect(() => {
    const connection = createSignalRConnection();
    connection.on('ReceiveNotification', addNotification);
    connection.start();
    return () => { connection.stop(); };
  }, []);
}
```

### usePagination
```typescript
export function usePagination(initialPage = 1, initialSize = 20) {
  const [page, setPage] = useState(initialPage);
  const [size, setSize] = useState(initialSize);
  
  return {
    page, size,
    nextPage: () => setPage(p => p + 1),
    prevPage: () => setPage(p => Math.max(1, p - 1)),
    setPage,
    setSize,
  };
}
```

---

## API Layer Pattern

```typescript
// api/exams.ts
export const examsApi = {
  getMyExams: () => 
    apiClient.get<ExamDto[]>('/api/exams/my-exams').then(r => r.data),
  
  getExam: (id: string) => 
    apiClient.get<ExamDto>(`/api/exams/${id}`).then(r => r.data),
  
  submitExam: (id: string, dto: ExamSubmissionDto) =>
    apiClient.post<ExamSubmissionResponseDto>(`/api/exams/${id}/submit`, dto).then(r => r.data),
    
  saveProgress: (id: string, dto: SaveProgressDto) =>
    apiClient.post(`/api/exams/${id}/save-progress`, dto),
    
  createAiExam: (dto: CreateAiExamRequest) =>
    apiClient.post<ExamDto>('/api/exams/generate-ai', dto).then(r => r.data),
};
```

```typescript
// features/exams/hooks/useExams.ts
export function useMyExams() {
  return useQuery({
    queryKey: ['exams', 'mine'],
    queryFn: examsApi.getMyExams,
    staleTime: 5 * 60 * 1000,
  });
}

export function useSubmitExam() {
  return useMutation({
    mutationFn: ({ id, dto }: { id: string; dto: ExamSubmissionDto }) =>
      examsApi.submitExam(id, dto),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['exams', 'mine'] });
    },
  });
}
```

---

## Error Boundary

```tsx
// components/ErrorBoundary.tsx
class ErrorBoundary extends React.Component {
  state = { hasError: false, error: null };
  
  static getDerivedStateFromError(error: Error) {
    return { hasError: true, error };
  }
  
  render() {
    if (this.state.hasError) {
      return <ErrorFallback error={this.state.error} />;
    }
    return this.props.children;
  }
}
```

---

## Code Splitting

```tsx
// Lazy load heavy pages
const TeachingDashboard = lazy(() => import('../features/doctor/TeachingDashboard'));
const ExamTaking = lazy(() => import('../features/exams/ExamTaking'));
const AICompanion = lazy(() => import('../features/companion/CompanionHub'));

// Wrap with Suspense
<Suspense fallback={<PageSkeleton />}>
  <TeachingDashboard />
</Suspense>
```

---

## Performance Guidelines

1. **Memoization**: Wrap expensive components with `React.memo`
2. **Virtual scrolling**: Use `@tanstack/react-virtual` for lists > 100 items
3. **Image optimization**: Use lazy loading for profile photos
4. **Bundle splitting**: Split by route (React.lazy)
5. **Query deduplication**: React Query handles this automatically
6. **Debounce search**: 300ms debounce on all search inputs
7. **Optimistic updates**: Use for mark-as-read, flashcard reviews
