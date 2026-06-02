# 19 — Component Library

## Core Components (src/components/)

### MetricCard
```tsx
interface MetricCardProps {
  icon: LucideIcon;
  title: string;
  value: string | number;
  subtitle?: string;
  trend?: { value: number; direction: 'up' | 'down' | 'neutral' };
  variant?: 'default' | 'success' | 'warning' | 'danger';
  isLoading?: boolean;
  onClick?: () => void;
}
```
Usage: Dashboard stat cards everywhere

### RiskBadge
```tsx
interface RiskBadgeProps {
  level: 'Low' | 'Medium' | 'High' | 'Critical';
  score?: number;
  showScore?: boolean;
}
```
Usage: Student tables, risk analysis

### ProgressBar
```tsx
interface ProgressBarProps {
  value: number;
  max?: number; // default 100
  variant?: 'auto' | 'success' | 'warning' | 'danger';
  showLabel?: boolean;
  label?: string;
  size?: 'sm' | 'md' | 'lg';
}
```
Usage: Attendance %, assignment completion, GPA progress

### DataTable
```tsx
interface DataTableProps<T> {
  data: T[];
  columns: Column<T>[];
  onRowClick?: (row: T) => void;
  isLoading?: boolean;
  emptyMessage?: string;
  sortable?: boolean;
  pagination?: PaginationState;
  searchable?: boolean;
  filters?: FilterConfig[];
}
```
Usage: Every list/table in the app

### ExamCard
```tsx
interface ExamCardProps {
  exam: ExamDto;
  role: 'student' | 'doctor';
  onAction?: (action: 'take' | 'view' | 'edit' | 'results') => void;
}
```
Usage: Exam lists for students and doctors

### AssignmentCard
```tsx
interface AssignmentCardProps {
  assignment: AssignmentDto;
  submission?: SubmissionDto;
  role: 'student' | 'doctor';
}
```

### FlashcardReview
```tsx
interface FlashcardReviewProps {
  card: FlashcardDto;
  onRate: (quality: 0 | 1 | 2 | 3 | 4 | 5) => void;
  cardNumber: number;
  totalCards: number;
}
```
Usage: Flashcard review mode

### InsightCard
```tsx
interface InsightCardProps {
  insight: AiInsightDto;
  onAcknowledge: (id: string) => void;
  onClick?: () => void;
}
```

### StudentIntelligenceRow
```tsx
interface StudentIntelligenceRowProps {
  student: StudentIntelligenceDto;
  onViewProfile: () => void;
  onContact: () => void;
}
```
Usage: Teaching Intelligence student table

### OfferingHealthCard
```tsx
interface OfferingHealthCardProps {
  offering: DoctorOfferingSummaryDto;
  onViewAnalytics: () => void;
}
```

### AttendanceQR
```tsx
interface AttendanceQRProps {
  sessionId: string;
  qrContent: string;
  checkedInCount: number;
  totalStudents: number;
  timeRemaining: number; // seconds
}
```

### StreakBadge
```tsx
interface StreakBadgeProps { days: number; size?: 'sm' | 'md' | 'lg'; }
```

### AITypingIndicator
No props — shows animated dots with "AI is thinking..."

### QuestionCard (Exam)
```tsx
interface QuestionCardProps {
  question: ExamQuestionDto;
  questionNumber: number;
  totalQuestions: number;
  selectedAnswer?: string;
  onAnswer: (answer: string) => void;
  isReview?: boolean; // shows correct answer in review mode
}
```

### WeakTopicRow
```tsx
interface WeakTopicRowProps {
  topic: WeakTopicDto;
  showRecommendation?: boolean;
}
```

### PageHeader
```tsx
interface PageHeaderProps {
  title: string;
  subtitle?: string;
  breadcrumbs?: BreadcrumbItem[];
  actions?: React.ReactNode;
}
```
Usage: Top of every page

### EmptyState
```tsx
interface EmptyStateProps {
  icon: LucideIcon;
  title: string;
  description: string;
  action?: { label: string; onClick: () => void };
}
```

### ConfirmDialog
```tsx
interface ConfirmDialogProps {
  open: boolean;
  title: string;
  description: string;
  confirmLabel?: string;
  variant?: 'default' | 'danger';
  onConfirm: () => void;
  onCancel: () => void;
}
```

### FileUploadZone
```tsx
interface FileUploadZoneProps {
  accept?: string; // e.g., ".pdf,.xlsx"
  maxSize?: number; // bytes
  onFile: (file: File) => void;
  isUploading?: boolean;
  uploadProgress?: number;
  label?: string;
}
```

### ExcelExportButton
```tsx
interface ExcelExportButtonProps {
  offeringId: string;
  fileName?: string;
}
// Internally: calls getStudentExport API + SheetJS
```
