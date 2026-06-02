# 08 — Admin Experience

## Overview
Admin experience focuses on system management: university structure, user management, analytics, and operations. SuperAdmin has additional capabilities (audit logs, deletion tool, register other admins).

---

## Page: Admin Dashboard (`/admin/dashboard`)

### Layout
```
┌─────────────────────────────────────────────────────────────┐
│  SYSTEM OVERVIEW                                            │
│  ┌────────┐ ┌────────┐ ┌────────┐ ┌────────┐ ┌────────┐  │
│  │ 2,450  │ │  142   │ │  318   │ │  89%   │ │  23    │  │
│  │Students│ │Doctors │ │Courses │ │ PassR. │ │At Risk │  │
│  └────────┘ └────────┘ └────────┘ └────────┘ └────────┘  │
│                                                             │
│  ┌───────────────────────┐  ┌───────────────────────────┐  │
│  │ Enrollment Trends      │  │ Department Distribution   │  │
│  │ [Line Chart: 12 weeks] │  │ [Pie Chart by dept]       │  │
│  └───────────────────────┘  └───────────────────────────┘  │
│                                                             │
│  ┌───────────────────────┐  ┌───────────────────────────┐  │
│  │ At-Risk Students       │  │ Recent Activity           │  │
│  │ Top 5 at risk          │  │ • 23 new enrollments      │  │
│  │ [View All Risk →]      │  │ • 5 grades imported       │  │
│  └───────────────────────┘  └───────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

**APIs**: `GET /api/analytics/dashboard/admin`, `GET /api/analytics/summary`, `GET /api/risk/at-risk-students`

---

## Page: University Structure (`/admin/structure`)

### Hierarchical Tree View
```
University
└── Faculty of Computers (College)
    ├── Computer Science (Department)
    │   ├── 2023 Batch
    │   │   ├── Group A (G1)
    │   │   └── Group B (G2)
    │   └── 2024 Batch
    └── Information Systems (Department)
        └── 2023 Batch
```

### Tabs: [Universities] [Colleges] [Departments] [Batches] [Groups] [Academic Years] [Semesters]

Each tab has:
- Searchable/filterable data table
- [+ Add] button → modal form
- Row actions: Edit (pencil), Delete (trash with confirmation)

---

## Page: Student Management (`/admin/students`)

### Features
- Searchable, paginated table
- Filters: department, batch, group, college, active/inactive
- Bulk import via Excel
- Individual add/edit
- Password reset

### Table Columns
```
[Photo] Name | ID | University Email | Batch | Department | Status | Actions
```

### Import Wizard
```
Step 1: Upload Excel
  [📎 Drop Excel file or browse]
  [Download Template]

Step 2: Preview (first 10 rows shown)
  [Name] [ID] [Phone] [Batch] [Group] ...

Step 3: Confirm
  Total: 120 rows
  Valid: 115 ✅
  Errors: 5 ⚠️ (shown inline)

[Import 115 Students]
```

---

## Page: Doctor Management (`/admin/doctors`)
- Same structure as Student Management
- Filters: department, college
- Assign to subjects inline

---

## Page: Enrollment Management (`/admin/enrollments`)

### View All Enrollments
```
Filter: [Offering ▾] [Batch ▾] [Status ▾] [Semester ▾]

Student Name    | Course        | Semester  | Status   | Actions
─────────────── ─────────────── ──────────  ────────── ─────────
Ahmed Hassan    | DB Systems    | Sem 5     | Active   | [Remove]
Sara Mohamed    | Networks      | Sem 5     | Active   | [Remove]
```

- Admin-enroll student: `POST /api/enrollments/{offeringId}/admin-enroll`
- Remove: `DELETE /api/enrollments/{id}`

---

## Page: Grade Management (`/admin/grades`)

### Import Grades
- Upload Excel per offering
- `POST /api/grades/import/{offeringId}`
- Shows import result (success/errors)

### Recalculate
- Select offering → Recalculate GPA for all students
- `POST /api/grades/calculate/{offeringId}`

---

## Page: Analytics (`/admin/analytics`)

### Sub-pages
1. **Overview** — `GET /api/analytics/summary`
2. **Student Distribution** — `GET /api/analytics/student-count-by-department`
3. **Doctor Workload** — `GET /api/analytics/doctor-workload`
4. **Course Performance** — `GET /api/analytics/offering-enrollment-stats`
5. **Risk Analysis** — `GET /api/risk/dashboard`

### Charts Used
- Bar chart: students per department
- Donut: grade distribution
- Line: enrollment trend
- Heatmap: attendance by week (future)
- Table with sorting: course performance

---

## Page: Complaint Management (`/admin/complaints`)

```
Filter: [Status ▾] [Type ▾] [Date ▾]  [Search...]

┌────────────────────────────────────────────────────────────┐
│ Ahmed Hassan vs Dr. Sara     Grade Complaint  Pending      │
│ "My midterm grade was wrongly recorded..."                 │
│ 3 days ago                  [View] [Assign] [Resolve]      │
└────────────────────────────────────────────────────────────┘
```

---

## Page: Notifications (`/admin/notifications`)

### Broadcast Notification
```
Send Notification to:
[All Students ▾]

Title: [              ]
Message: [                    ]
Action URL (optional): [      ]

[Send Notification]
```

---

## Page: Subject Offerings (`/admin/offerings`)

- Create/edit offerings
- Assign doctors
- Set max capacity
- View enrollment counts

---

## Page: Regulations (`/admin/regulations`)

- Upload regulation PDF
- Attach subjects to regulation (JSON)
- Activate/deactivate regulation
- View academic roadmaps per student

---

## Page: Audit Logs (`/admin/audit-logs`) — SuperAdmin Only

```
Date/Time       User            Action              Entity
─────────────── ─────────────── ─────────────────── ───────────
2024-12-01 10:23 admin@uni.edu  Created Student      Ahmed Hassan
2024-12-01 09:45 doctor@uni.edu Graded Submission    Exam #123
2024-12-01 09:12 admin@uni.edu  Imported 50 students Batch 2024
```

---

## Page: Delete Tool (`/admin/delete`) — SuperAdmin Only

Safe entity deletion with dependency analysis:
1. Select entity type + search
2. `POST /api/deletion/analyze` → shows dependencies
3. If safe: `POST /api/deletion/execute` with reason

---

## Admin Navigation Sidebar

```
📊 Dashboard
─────────────
👥 Users
  ├─ Students
  ├─ Doctors
  └─ Admins (SuperAdmin)
─────────────
🏛️ Structure
  ├─ Universities
  ├─ Colleges
  ├─ Departments
  ├─ Batches & Groups
  ├─ Academic Years
  └─ Semesters
─────────────
📚 Academic
  ├─ Subjects
  ├─ Offerings
  ├─ Enrollments
  ├─ Schedule
  └─ Regulations
─────────────
📊 Analytics
  ├─ Overview
  ├─ Performance
  └─ Risk Analysis
─────────────
📧 Notifications
💬 Complaints
📋 Audit Logs (SuperAdmin)
🗑️ Delete Tool (SuperAdmin)
⚙️ Settings
```
