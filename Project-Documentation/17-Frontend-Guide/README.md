# 🖥️ Frontend Developer Guide — Complete Integration Manual

> This guide is written specifically for the **frontend developer** who needs to build the UI for this system. Everything you need is here.

---

## Quick Start

**Backend Base URL:** `https://your-backend.railway.app`  
**AI Service URL:** `https://your-ai-service.railway.app` (called via backend, not directly)  
**SignalR Hub:** `wss://your-backend.railway.app/hubs/notifications`

---

## Authentication Flow

### Step 1: Login
```javascript
const response = await fetch('/api/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ email, password })
});

const data = await response.json();
// data = {
//   token: "eyJhbG...",
//   refreshToken: "abc123",
//   expiresAt: "2026-05-16T12:00:00Z",
//   role: "Student",           // "Student" | "Doctor" | "Admin" | "SuperAdmin"
//   userId: "01HXYZ...",       // SystemUser.Id
//   profileId: "01HABC...",   // Student/Doctor/Admin profile Id
//   mustChangePassword: false
// }
```

**Critical:** If `mustChangePassword === true`, block the app and redirect to change-password screen immediately.

### Step 2: Store Tokens
```javascript
// Recommended: store in memory (not localStorage) for security
// Use localStorage only for non-sensitive tokens
let accessToken = data.token;
let refreshToken = data.refreshToken;

// Store role for UI routing
let userRole = data.role;
let profileId = data.profileId;
```

### Step 3: Attach Token to Every Request
```javascript
// Create an API client with interceptors
const apiClient = {
  async get(url) {
    const response = await fetch(url, {
      headers: {
        'Authorization': `Bearer ${accessToken}`,
        'Content-Type': 'application/json'
      }
    });
    
    if (response.status === 401) {
      // Token expired → try refresh
      const refreshed = await refreshAccessToken();
      if (!refreshed) { logout(); return; }
      // Retry request with new token
      return this.get(url);
    }
    
    return response.json();
  }
};
```

### Step 4: Silent Token Refresh
```javascript
async function refreshAccessToken() {
  const response = await fetch('/api/auth/refresh', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken })
  });
  
  if (!response.ok) return false;
  
  const data = await response.json();
  accessToken = data.token;
  refreshToken = data.refreshToken;
  return true;
}
```

---

## Role-Based UI Architecture

```javascript
// Route protection helper
function ProtectedRoute({ allowedRoles, children }) {
  if (!allowedRoles.includes(userRole)) {
    return <Redirect to="/unauthorized" />;
  }
  return children;
}

// Route definitions
const routes = [
  // Public
  { path: "/login",          component: LoginPage },
  { path: "/change-password", component: ChangePasswordPage },
  
  // Student routes
  { path: "/dashboard",      component: StudentDashboard,  roles: ["Student"] },
  { path: "/my-roadmap",     component: RoadmapPage,       roles: ["Student"] },
  { path: "/my-grades",      component: GradesPage,        roles: ["Student"] },
  { path: "/my-schedule",    component: SchedulePage,      roles: ["Student"] },
  { path: "/enrollment",     component: EnrollmentPage,    roles: ["Student"] },
  { path: "/chat",           component: AiChatPage,        roles: ["Student", "Doctor", "Admin"] },
  { path: "/complaints",     component: ComplaintsPage,    roles: ["Student"] },
  { path: "/exams",          component: ExamsPage,         roles: ["Student"] },
  
  // Doctor routes
  { path: "/my-courses",     component: DoctorCoursesPage, roles: ["Doctor"] },
  { path: "/my-students",    component: MyStudentsPage,    roles: ["Doctor"] },
  { path: "/grades-entry",   component: GradeEntryPage,    roles: ["Doctor"] },
  { path: "/create-exam",    component: CreateExamPage,    roles: ["Doctor"] },
  { path: "/attendance",     component: AttendancePage,    roles: ["Doctor"] },
  { path: "/materials",      component: MaterialsPage,     roles: ["Doctor", "Student"] },
  
  // Admin routes
  { path: "/admin",          component: AdminDashboard,    roles: ["Admin", "SuperAdmin"] },
  { path: "/analytics",      component: AnalyticsPage,     roles: ["Admin", "SuperAdmin"] },
  { path: "/manage-students",component: ManageStudents,    roles: ["Admin", "SuperAdmin"] },
  { path: "/manage-doctors", component: ManageDoctors,     roles: ["Admin", "SuperAdmin"] },
  { path: "/regulations",    component: RegulationsAdmin,  roles: ["Admin", "SuperAdmin"] },
  { path: "/audit-logs",     component: AuditLogsPage,     roles: ["SuperAdmin"] },
];
```

---

## Student Dashboard — All Required API Calls

```javascript
// Load everything for student dashboard
async function loadStudentDashboard() {
  const [roadmap, grades, notifications, schedule, enrollments] = await Promise.all([
    fetch('/api/regulations/my-roadmap'),        // Academic progress
    fetch('/api/grades/my-grades'),               // Recent grades
    fetch('/api/notification?unreadOnly=true'),   // Unread count
    fetch('/api/schedule/my-schedule'),           // This week's classes
    fetch('/api/enrollments/my-enrollments'),     // Current courses
  ]);
  
  return {
    gpa: roadmap.currentGpa,
    completedHours: roadmap.completedCreditHours,
    totalHours: roadmap.totalCreditHours,
    passedSubjects: roadmap.passedSubjects,
    failedSubjects: roadmap.failedSubjects,
    currentlyEnrolled: roadmap.currentlyEnrolled,
    recentGrades: grades.slice(0, 5),
    unreadNotifications: notifications.length,
    todayClasses: filterToday(schedule),
    enrolledCourses: enrollments
  };
}
```

---

## Academic Roadmap UI Guide

```javascript
// Fetch roadmap
const roadmap = await fetch('/api/regulations/my-roadmap').then(r => r.json());

// Display top stats
displayStats({
  gpa: roadmap.currentGpa?.toFixed(2) ?? 'N/A',
  completed: roadmap.completedCreditHours,
  total: roadmap.totalCreditHours,
  progress: (roadmap.completedCreditHours / roadmap.totalCreditHours * 100).toFixed(0)
});

// Render semesters
roadmap.semesters.forEach(semester => {
  const semesterCard = {
    number: semester.semesterNumber,
    status: semester.status,       // "completed" | "in_progress" | "upcoming"
    progressPercent: Math.round(semester.passedSubjects / semester.totalSubjects * 100),
    subjects: semester.subjects.map(sub => ({
      name: sub.subjectName,
      code: sub.subjectCode,
      credits: sub.creditHours,
      status: sub.status,          // "passed" | "failed" | "enrolled" | "upcoming"
      grade: sub.gradeLetter,
      score: sub.finalScore,
      required: sub.isRequired
    }))
  };
});

// Status → color mapping
const statusColors = {
  passed:   '#22c55e',  // green
  failed:   '#ef4444',  // red
  enrolled: '#3b82f6',  // blue
  upcoming: '#9ca3af',  // gray
};

// Must retake alert
if (roadmap.mustRetake.length > 0) {
  showAlert(`⚠️ يجب عليك إعادة ${roadmap.mustRetake.length} مادة`);
}
```

---

## AI Chat Integration

```javascript
// Chat state
const [messages, setMessages] = useState([]);
const [conversationId, setConversationId] = useState(null);
const [isLoading, setIsLoading] = useState(false);

async function sendMessage(userMessage) {
  // Add user message to UI immediately
  setMessages(prev => [...prev, { role: 'user', content: userMessage }]);
  setIsLoading(true);
  
  try {
    const response = await fetch('/api/chat', {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${accessToken}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        message: userMessage,
        conversationId: conversationId  // null for first message
      })
    });
    
    const data = await response.json();
    
    // Save conversation ID for next messages
    setConversationId(data.conversationId);
    
    // Add AI response
    setMessages(prev => [...prev, { role: 'assistant', content: data.reply }]);
  } catch (error) {
    showError('فشل الاتصال بالمساعد الذكي');
  } finally {
    setIsLoading(false);
  }
}

// Load previous conversations list
const conversations = await fetch('/api/chat/conversations');

// Load specific conversation history
const history = await fetch(`/api/chat/history/${conversationId}`);
```

### AI Chat Best Practices
```javascript
// Show typing indicator while waiting (AI takes 3-15 seconds)
// AI responses may be in Arabic or English (match user's language)
// Display code blocks with syntax highlighting (AI may return code)
// Support markdown rendering for formatted AI responses
```

---

## SignalR Real-Time Notifications

```javascript
import * as signalR from "@microsoft/signalr";

// Initialize once when user logs in
let hubConnection = null;

async function initSignalR(token) {
  hubConnection = new signalR.HubConnectionBuilder()
    .withUrl("https://your-backend.railway.app/hubs/notifications", {
      accessTokenFactory: () => token
    })
    .withAutomaticReconnect({
      nextRetryDelayInMilliseconds: (retryContext) => {
        if (retryContext.elapsedMilliseconds < 60000) return 2000;  // 2s for 1 min
        return 30000;  // 30s after that
      }
    })
    .build();

  // Register handler BEFORE start
  hubConnection.on("ReceiveNotification", (notification) => {
    // notification = { title, message, actionUrl, createdAt }
    
    // 1. Show toast
    toast.success(notification.title, {
      description: notification.message,
      action: notification.actionUrl ? {
        label: "View",
        onClick: () => navigate(notification.actionUrl)
      } : undefined,
      duration: 5000
    });
    
    // 2. Update bell counter
    setUnreadCount(prev => prev + 1);
    
    // 3. Prepend to notifications list
    setNotifications(prev => [notification, ...prev]);
  });

  hubConnection.onreconnected(() => {
    console.log("SignalR reconnected");
  });

  await hubConnection.start();
}

// Disconnect when user logs out
async function disconnectSignalR() {
  if (hubConnection) {
    await hubConnection.stop();
    hubConnection = null;
  }
}
```

---

## Doctor Dashboard — Required API Calls

```javascript
async function loadDoctorDashboard() {
  const [offerings, workloadStats] = await Promise.all([
    fetch('/api/subjectofferings/by-doctor/me'),       // My current courses
    fetch('/api/analytics/doctor-workload'),            // Workload summary
  ]);
  
  // For each offering, load enrolled count
  // (included in offerings response via EnrolledCount field)
}

// Doctor sends notification to students
async function sendToStudents(title, message, offeringId = null) {
  const url = offeringId 
    ? `/api/notification/send-to-my-students?offeringId=${offeringId}`
    : '/api/notification/send-to-my-students';
    
  const result = await fetch(url, {
    method: 'POST',
    headers: { 'Authorization': `Bearer ${token}`, 'Content-Type': 'application/json' },
    body: JSON.stringify({ title, message })
  });
  
  const data = await result.json();
  showSuccess(`تم الإرسال إلى ${data.sentTo} طالب`);
}
```

---

## Admin Dashboard — Analytics API

```javascript
// Summary stats (top of admin dashboard)
const summary = await fetch('/api/analytics/summary');
// { totalStudents, totalDoctors, totalOfferings, totalEnrollments, 
//   totalColleges, totalDepartments, topDepartments[], topSubjects[] }

// Department breakdown chart
const deptStats = await fetch('/api/analytics/student-count-by-department');
// [{ departmentName, collegeName, studentCount, doctorCount }]

// Doctor workload table
const workload = await fetch('/api/analytics/doctor-workload?departmentId=...');
// [{ doctorName, offeringCount, totalStudents }]

// Top enrolled subjects
const topSubjects = await fetch('/api/analytics/top-enrolled-subjects?top=10');
// [{ subjectName, subjectCode, enrolledCount, offeringCount }]
```

---

## File Upload (Regulations, Materials, Exams)

```javascript
// Generic file upload
async function uploadFile(file) {
  const formData = new FormData();
  formData.append('file', file);
  
  const response = await fetch('/api/file/upload', {
    method: 'POST',
    headers: { 'Authorization': `Bearer ${token}` },
    body: formData  // DO NOT set Content-Type — browser sets multipart boundary
  });
  
  const { fileId, fileUrl } = await response.json();
  return { fileId, fileUrl };
}

// Upload regulation with file
async function createRegulation(data, file, subjects) {
  const formData = new FormData();
  formData.append('Title', data.title);
  formData.append('Content', data.content || '');
  formData.append('Type', data.type.toString());
  formData.append('DepartmentId', data.departmentId);
  formData.append('SubjectsJson', JSON.stringify(subjects));
  if (file) formData.append('File', file);
  
  return fetch('/api/regulations', {
    method: 'POST',
    headers: { 'Authorization': `Bearer ${token}` },
    body: formData
  });
}

// Upload course material
async function uploadMaterial(file, subjectOfferingId, title) {
  const formData = new FormData();
  formData.append('file', file);
  formData.append('subjectOfferingId', subjectOfferingId);
  formData.append('title', title);
  
  return fetch('/api/materials/upload', {
    method: 'POST',
    headers: { 'Authorization': `Bearer ${token}` },
    body: formData
  });
}
```

---

## Pagination Pattern

All list endpoints follow the same pagination pattern:

```javascript
// Request
GET /api/students?page=1&size=20&departmentId=...

// Response
{
  "data": [...],
  "totalCount": 450,
  "page": 1,
  "size": 20
}

// Pagination component helper
const totalPages = Math.ceil(totalCount / size);
const hasNextPage = page < totalPages;
const hasPrevPage = page > 1;
```

---

## Error Handling

```javascript
// All errors return:
{
  "success": false,
  "message": "Error description",
  "correlationId": "abc-123-xyz"
}

// HTTP Status codes:
// 400 → Validation error (show message to user)
// 401 → Token expired (trigger refresh)
// 403 → Forbidden (show "no permission" message)
// 404 → Not found (show empty state)
// 422 → Business rule violation (show specific message)
// 500 → Server error (show generic error + correlation ID for support)

// Standard error handler
async function apiRequest(url, options = {}) {
  try {
    const response = await fetch(url, {
      headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json',
        ...options.headers
      },
      ...options
    });
    
    if (!response.ok) {
      const error = await response.json();
      throw new ApiError(error.message, response.status, error.correlationId);
    }
    
    return response.json();
  } catch (error) {
    if (error instanceof ApiError) throw error;
    throw new ApiError('Network error — check your connection', 0);
  }
}
```

---

## ULID ID Handling

All IDs in this system are **ULID strings** (26 characters):

```javascript
// Example ULID: "01HXYZ2ABC3DEF4GHI5JKL6MNO"
// Always treat as strings — never parse as number

// When displaying IDs to users:
const shortId = id.slice(-6);  // Show last 6 chars for readability

// When navigating with ID:
navigate(`/student/${student.id}`);  // Full ULID in URL
```

---

## Key State Management Recommendations

```javascript
// Global state (e.g., Zustand/Redux):
{
  auth: {
    token: string | null,
    refreshToken: string | null,
    userId: string | null,
    profileId: string | null,
    role: "Student" | "Doctor" | "Admin" | "SuperAdmin" | null
  },
  notifications: {
    unreadCount: number,
    items: Notification[],
    lastFetched: Date | null
  },
  signalR: {
    connection: HubConnection | null,
    status: "connected" | "disconnected" | "reconnecting"
  }
}

// Component-level state (React Query / SWR recommended):
// - Student roadmap (cache for 5 minutes, revalidate on focus)
// - Analytics data (cache for 10 minutes)
// - Chat history (no caching — always fresh)
// - Grades (cache for 5 minutes)
```

---

## Recommended Tech Stack for Frontend

| Feature | Recommended Tool |
|---------|----------------|
| Framework | React + Vite or Next.js |
| HTTP Client | TanStack Query + fetch |
| State | Zustand (simple) or Redux Toolkit |
| Forms | React Hook Form + Zod validation |
| Charts | Recharts or Chart.js |
| UI Components | shadcn/ui or Ant Design |
| SignalR | @microsoft/signalr |
| Notifications | react-hot-toast or Sonner |
| Markdown | react-markdown (for AI responses) |
| File Upload | React Dropzone |
| Tables | TanStack Table |
| Date/Time | date-fns |
| RTL Support | direction="rtl" on root + tailwind RTL plugin |

---

## Arabic/RTL Support

The system serves Arabic-speaking users. Frontend must:

```html
<!-- Set direction on html element -->
<html lang="ar" dir="rtl">

<!-- Or dynamically -->
<div dir={isArabic ? "rtl" : "ltr"}>
```

```css
/* Tailwind RTL plugin or manual RTL CSS */
[dir="rtl"] .ml-4 { margin-right: 1rem; margin-left: 0; }
```

AI responses may be in Arabic — ensure the chat component handles RTL text correctly.
