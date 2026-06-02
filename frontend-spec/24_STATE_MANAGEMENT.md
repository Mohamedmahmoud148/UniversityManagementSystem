# 24 — State Management

## Architecture: Zustand + React Query

| Type of State | Tool | Where |
|--------------|------|-------|
| Server state (API data) | React Query | Everywhere |
| Auth (tokens, user) | Zustand | Global |
| Notifications (unread count) | Zustand | Global |
| Chat messages | Zustand | Chat feature |
| Exam progress (in-progress) | Zustand | Exam taking |
| UI state (modals, drawers) | Local useState | Component |
| Form state | React Hook Form | Form components |

---

## Zustand Stores

### authStore (see 23_FRONTEND_ARCHITECTURE.md)

### notificationStore
```typescript
interface NotificationState {
  notifications: NotificationDto[];
  unreadCount: number;
  setNotifications: (n: NotificationDto[]) => void;
  addNotification: (n: NotificationDto) => void;
  markRead: (id: string) => void;
  markAllRead: () => void;
}
```

### chatStore
```typescript
interface ChatState {
  conversations: Conversation[];
  activeConversationId: string | null;
  messages: Record<string, Message[]>;
  isTyping: boolean;
  
  setActiveConversation: (id: string) => void;
  addMessage: (convId: string, msg: Message) => void;
  setTyping: (v: boolean) => void;
  createConversation: (title: string) => Promise<Conversation>;
}
```

### examStore (during active exam)
```typescript
interface ExamState {
  activeExamId: string | null;
  answers: Record<string, string>; // questionId → answer
  startTime: number;
  lastSaved: number;
  
  startExam: (examId: string) => void;
  setAnswer: (questionId: string, answer: string) => void;
  endExam: () => void;
}
```

---

## React Query Keys Convention

```typescript
// Consistent query key structure
const queryKeys = {
  // Auth
  me: ['auth', 'me'],
  
  // Students
  students: (filters?: object) => ['students', filters],
  student: (id: string) => ['students', id],
  
  // Exams
  myExams: ['exams', 'mine'],
  exam: (id: string) => ['exams', id],
  examResults: (id: string) => ['exams', id, 'results'],
  
  // Companion
  companionDashboard: ['companion', 'dashboard'],
  flashcardDecks: ['companion', 'flashcards'],
  dueCards: ['companion', 'flashcards', 'due'],
  sessions: ['companion', 'sessions'],
  insights: (unreadOnly?: boolean) => ['companion', 'insights', { unreadOnly }],
  
  // Teaching Intelligence
  teachingDashboard: ['teaching', 'dashboard'],
  offeringAnalytics: (id: string) => ['teaching', 'offerings', id, 'analytics'],
  offeringStudents: (id: string, filters?: object) => ['teaching', 'offerings', id, 'students', filters],
  atRiskStudents: ['teaching', 'at-risk'],
  
  // Notifications
  notifications: ['notifications'],
  
  // Dashboard
  studentDashboard: ['dashboard', 'student'],
  doctorDashboard: ['dashboard', 'doctor'],
  adminDashboard: ['dashboard', 'admin'],
};
```

---

## Optimistic Updates

For operations that should feel instant:

```typescript
// Mark notification as read — optimistic
const { mutate: markRead } = useMutation({
  mutationFn: (id: string) => notificationApi.markRead(id),
  onMutate: async (id) => {
    useNotificationStore.getState().markRead(id);
  },
  onError: (_, id) => {
    // Rollback
    useNotificationStore.getState().markUnread(id);
  },
});

// Flashcard review — optimistic
const { mutate: reviewCard } = useMutation({
  mutationFn: ({ cardId, quality }) => companionApi.reviewCard(cardId, { quality }),
  onMutate: async ({ cardId }) => {
    // Remove card from due list immediately
    queryClient.setQueryData(queryKeys.dueCards, (old: FlashcardDto[]) =>
      old?.filter(c => c.id !== cardId) ?? []
    );
  },
});
```

---

## Cache Invalidation Strategy

```typescript
// After completing a learning session:
onSuccess: () => {
  queryClient.invalidateQueries({ queryKey: queryKeys.sessions });
  queryClient.invalidateQueries({ queryKey: queryKeys.companionDashboard });
  // Don't invalidate insights — they update slowly
}

// After submitting exam:
onSuccess: () => {
  queryClient.invalidateQueries({ queryKey: queryKeys.myExams });
  queryClient.invalidateQueries({ queryKey: queryKeys.studentDashboard });
  // Invalidate specific exam result
  queryClient.invalidateQueries({ queryKey: ['exams', examId, 'result'] });
}

// After importing students:
onSuccess: () => {
  queryClient.invalidateQueries({ queryKey: ['students'] });
  queryClient.invalidateQueries({ queryKey: queryKeys.adminDashboard });
}
```
