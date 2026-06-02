# 15 — Analytics Platform

## Admin Analytics Suite

### Available Endpoints
| Endpoint | Data | Chart |
|----------|------|-------|
| `GET /api/analytics/summary` | Totals | Metric cards |
| `GET /api/analytics/student-count-by-department` | Students per dept | Bar chart |
| `GET /api/analytics/student-count-by-batch` | Students per batch | Bar chart |
| `GET /api/analytics/doctor-workload` | Doctor course load | Bar chart |
| `GET /api/analytics/top-enrolled-subjects` | Popular subjects | Horizontal bar |
| `GET /api/analytics/offering-enrollment-stats` | Per-offering stats | Table |
| `GET /api/analytics/attendance/trends` | Weekly attendance | Line chart |
| `GET /api/analytics/grades/distribution` | Grade distribution | Histogram |
| `GET /api/analytics/at-risk-students` | At-risk list | Table |
| `GET /api/analytics/course-performance` | Course stats | Table |
| `GET /api/analytics/department/comparison` | Dept comparison | Grouped bar |

### Admin Analytics Page Layout
Tabs: [Overview] [Students] [Doctors] [Courses] [Risk]

**Overview Tab**:
- 5 metric cards (students, doctors, courses, avg GPA, pass rate)
- Enrollment trend line chart (12 weeks)
- Department distribution pie chart

**Risk Tab**:
- At-risk students table with filters
- Risk level distribution donut chart
- `GET /api/risk/dashboard`
- `GET /api/risk/at-risk-students`

## Student Analytics (Own Data)
- `GET /api/analytics/dashboard/student` → GPA, attendance, enrolled courses
- `GET /api/analytics/student/{id}/performance` → Per-subject performance

## Doctor Analytics
- `GET /api/analytics/dashboard/doctor` → offering count, students, to-grade
- Teaching Intelligence APIs (more detailed)
