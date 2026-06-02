# 16 — Dashboard Specifications

## Student Dashboard (`/student/dashboard`)

### Widget Inventory (prioritized top-to-bottom, left-to-right)

| # | Widget | API | Size | Priority |
|---|--------|-----|------|---------|
| 1 | GPA Card (current + CGPA) | `/api/gpa/my-gpa` | 1/3 | P0 |
| 2 | Attendance Overview | `/api/attendance/student/{id}/report` | 1/3 | P0 |
| 3 | AI Companion Quick Access | `/api/companion/dashboard` | 1/3 | P0 |
| 4 | Upcoming Exams (next 7 days) | `/api/exams/my-enrolled-exams` | 1/2 | P0 |
| 5 | Due Assignments | `/api/assignments/offering/{id}` | 1/2 | P0 |
| 6 | My Courses Grid | `/api/enrollments/my-enrollments` | Full | P1 |
| 7 | AI Insights Feed | `/api/companion/insights?unreadOnly=true` | 1/2 | P1 |
| 8 | Today's Study Plan | `/api/companion/dashboard` | 1/2 | P1 |
| 9 | Streak Badge | `/api/companion/dashboard` | Inline in GPA | P1 |

---

## Doctor Dashboard (`/doctor/dashboard`)

### Widget Inventory

| # | Widget | API | Size | Priority |
|---|--------|-----|------|---------|
| 1 | Stats Row (students, at-risk, avg grade, attendance) | TI Dashboard | Full | P0 |
| 2 | AI Recommendations | TI Dashboard `.aiRecommendations` | 1/3 | P0 |
| 3 | My Offerings Health Cards | TI Dashboard `.offerings` | 2/3 | P0 |
| 4 | At-Risk Students Table | TI `.atRiskStudents` | Full | P0 |
| 5 | Weak Topics List | TI `.weakTopics` | 1/2 | P1 |
| 6 | Class Comparison Chart | TI `.classComparisons` | 1/2 | P1 |
| 7 | Recent Teaching Alerts | TI `.recentAlerts` | Full | P1 |
| 8 | Submissions to Grade | Doctor dashboard | 1/3 | P2 |

---

## Admin Dashboard (`/admin/dashboard`)

### Widget Inventory

| # | Widget | API | Size | Priority |
|---|--------|-----|------|---------|
| 1 | System Stats (students, doctors, courses, pass rate) | `/analytics/dashboard/admin` | Full row | P0 |
| 2 | Enrollment Trend Chart | `/analytics/summary` | 2/3 | P0 |
| 3 | Department Distribution Pie | `/analytics/student-count-by-department` | 1/3 | P1 |
| 4 | At-Risk Students List | `/risk/at-risk-students` | 1/2 | P1 |
| 5 | Recent Activity Feed | Audit logs summary | 1/2 | P2 |

---

## Dashboard Data Loading Pattern

All dashboards use a single "dashboard query" that fetches all data at once:

```typescript
function useDashboard(role: UserRole) {
  const queryMap = {
    Student: {
      key: ['dashboard', 'student'],
      fn: () => Promise.all([
        analyticsApi.getStudentDashboard(),
        gpaApi.getMyGpa(),
        examsApi.getMyEnrolledExams(),
        companionApi.getDashboard(),
        notificationApi.getAll(),
      ]),
    },
    Doctor: {
      key: ['dashboard', 'doctor'],
      fn: () => teachingApi.getDashboard(),
    },
    Admin: {
      key: ['dashboard', 'admin'],
      fn: () => Promise.all([
        analyticsApi.getAdminDashboard(),
        riskApi.getAtRiskStudents(),
        analyticsApi.getSummary(),
      ]),
    },
  };
  
  return useQuery({
    queryKey: queryMap[role].key,
    queryFn: queryMap[role].fn,
    staleTime: 5 * 60 * 1000,
  });
}
```
