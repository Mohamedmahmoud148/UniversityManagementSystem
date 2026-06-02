# 03 — Role Permissions Matrix

## Legend
- ✅ Full access
- 👁 Read only
- ❌ No access
- 🔒 Own data only
- ➕ Create only
- ✏️ Edit only

---

## Authentication

| Feature | SuperAdmin | Admin | Doctor | TA | Student |
|---------|-----------|-------|--------|-----|---------|
| Login | ✅ | ✅ | ✅ | ✅ | ✅ |
| Change Password | ✅ | ✅ | ✅ | ✅ | ✅ |
| Reset Others Password | ✅ | ✅ | ❌ | ❌ | ❌ |
| Refresh Token | ✅ | ✅ | ✅ | ✅ | ✅ |
| Register Student | ✅ | ✅ | ❌ | ❌ | ❌ |
| Register Doctor | ✅ | ✅ | ❌ | ❌ | ❌ |
| Register Admin | ✅ | ❌ | ❌ | ❌ | ❌ |

---

## University Structure

| Feature | SuperAdmin | Admin | Doctor | TA | Student |
|---------|-----------|-------|--------|-----|---------|
| View Universities | ✅ | ✅ | ✅ | ✅ | ✅ |
| Create/Edit/Delete University | ✅ | ✅ | ❌ | ❌ | ❌ |
| View Colleges | ✅ | ✅ | ✅ | ✅ | ✅ |
| Manage Colleges | ✅ | ✅ | ❌ | ❌ | ❌ |
| View Departments | ✅ | ✅ | ✅ | ✅ | ✅ |
| Manage Departments | ✅ | ✅ | ❌ | ❌ | ❌ |
| View Batches | ✅ | ✅ | ✅ | ✅ | ✅ |
| Manage Batches | ✅ | ✅ | ❌ | ❌ | ❌ |
| View Groups | ✅ | ✅ | ✅ | ✅ | ✅ |
| Manage Groups | ✅ | ✅ | ❌ | ❌ | ❌ |
| Manage Academic Years | ✅ | ✅ | ❌ | ❌ | ❌ |
| Manage Semesters | ✅ | ✅ | ❌ | ❌ | ❌ |

---

## Students

| Feature | SuperAdmin | Admin | Doctor | TA | Student |
|---------|-----------|-------|--------|-----|---------|
| View All Students | ✅ | ✅ | ✅ | ✅ | ❌ |
| Search Students | ✅ | ✅ | ✅ | ✅ | ❌ |
| View Own Profile | — | — | — | — | ✅ |
| Edit Student Profile | ✅ | ✅ | ❌ | ❌ | ❌ |
| Bulk Import Students | ✅ | ✅ | ❌ | ❌ | ❌ |
| View Struggling Students | ✅ | ✅ | ❌ | ❌ | ❌ |

---

## Doctors

| Feature | SuperAdmin | Admin | Doctor | TA | Student |
|---------|-----------|-------|--------|-----|---------|
| View All Doctors | ✅ | ✅ | ✅ | ✅ | ✅ |
| Create Doctor | ✅ | ✅ | ❌ | ❌ | ❌ |
| Edit Doctor | ✅ | ✅ | ❌ | ❌ | ❌ |
| Delete Doctor | ✅ | ✅ | ❌ | ❌ | ❌ |
| Assign to Subject | ✅ | ✅ | ❌ | ❌ | ❌ |

---

## Subject Offerings

| Feature | SuperAdmin | Admin | Doctor | TA | Student |
|---------|-----------|-------|--------|-----|---------|
| Create Offering | ✅ | ✅ | ❌ | ❌ | ❌ |
| View Offerings | ✅ | ✅ | ✅ (own) | ✅ | ✅ |
| Edit/Delete Offering | ✅ | ✅ | ❌ | ❌ | ❌ |
| View Enrolled Students | ✅ | ✅ | ✅ (own) | ✅ | ❌ |

---

## Enrollments

| Feature | SuperAdmin | Admin | Doctor | TA | Student |
|---------|-----------|-------|--------|-----|---------|
| Auto-enroll Self | ✅ | ❌ | ❌ | ❌ | ✅ |
| Enroll in Specific Course | ✅ | ❌ | ❌ | ❌ | ✅ |
| Admin-Enroll Student | ✅ | ✅ | ❌ | ❌ | ❌ |
| View Own Enrollments | ✅ | ❌ | ❌ | ❌ | ✅ |
| View Course Enrollments | ✅ | ✅ | ✅ (own) | ✅ | ❌ |
| Remove Enrollment | ✅ | ✅ | ❌ | ❌ | ❌ |
| Waitlist | ✅ | ❌ | ❌ | ❌ | ✅ |
| View Prerequisites | ✅ | ✅ | ✅ | ❌ | ❌ |
| Manage Prerequisites | ✅ | ✅ | ❌ | ❌ | ❌ |
| View Academic Status | ✅ | ❌ | ❌ | ❌ | ✅ |

---

## Grades

| Feature | SuperAdmin | Admin | Doctor | TA | Student |
|---------|-----------|-------|--------|-----|---------|
| View Own Grades | ✅ | ❌ | ❌ | ❌ | ✅ |
| Import Grades (Excel) | ✅ | ✅ | ✅ | ❌ | ❌ |
| Calculate Grades | ✅ | ❌ | ✅ | ❌ | ❌ |
| Edit Grades | ✅ | ✅ | ❌ | ❌ | ❌ |
| View GPA | ✅ | ✅ | ❌ | ❌ | ✅ (own) |
| Recalculate GPA | ✅ | ✅ | ❌ | ❌ | ❌ |

---

## Exams

| Feature | SuperAdmin | Admin | Doctor | TA | Student |
|---------|-----------|-------|--------|-----|---------|
| Create Exam | ✅ | ❌ | ✅ | ❌ | ❌ |
| Generate AI Exam | ✅ | ❌ | ✅ | ❌ | ❌ |
| Edit/Delete Exam | ✅ | ✅ | ✅ (own) | ❌ | ❌ |
| View Exam | ✅ | ✅ | ✅ | ✅ | ✅ (enrolled) |
| Take Exam | ✅ | ❌ | ❌ | ❌ | ✅ |
| View Results | ✅ | ✅ | ✅ (own) | ❌ | ✅ (own) |
| Grade Submissions | ✅ | ❌ | ✅ | ❌ | ❌ |
| Auto-grade | ✅ | ❌ | ✅ | ❌ | ❌ |
| View Proctoring | ✅ | ✅ | ✅ (own) | ❌ | ❌ |
| View Exam Analytics | ✅ | ❌ | ✅ (own) | ❌ | ❌ |

---

## Assignments

| Feature | SuperAdmin | Admin | Doctor | TA | Student |
|---------|-----------|-------|--------|-----|---------|
| Create Assignment | ✅ | ✅ | ✅ | ❌ | ❌ |
| Delete Assignment | ✅ | ✅ | ✅ (own) | ❌ | ❌ |
| View Assignments | ✅ | ✅ | ✅ | ✅ | ✅ |
| Submit Assignment | ✅ | ❌ | ❌ | ❌ | ✅ |
| View Own Submission | ✅ | ❌ | ❌ | ❌ | ✅ |
| View All Submissions | ✅ | ✅ | ✅ (own) | ❌ | ❌ |
| Grade Submission | ✅ | ✅ | ✅ | ❌ | ❌ |
| AI Grade | ✅ | ✅ | ✅ | ❌ | ❌ |

---

## Attendance

| Feature | SuperAdmin | Admin | Doctor | TA | Student |
|---------|-----------|-------|--------|-----|---------|
| Create Session | ✅ | ✅ | ✅ | ✅ | ❌ |
| Check-in (QR) | ✅ | ❌ | ❌ | ❌ | ✅ |
| View Own Attendance | ✅ | ❌ | ❌ | ❌ | ✅ |
| View Student Attendance | ✅ | ✅ | ✅ | ✅ | ❌ |
| Correct Attendance | ✅ | ✅ | ❌ | ❌ | ❌ |
| Edit Attendance Record | ✅ | ✅ | ❌ | ❌ | ❌ |

---

## Materials

| Feature | SuperAdmin | Admin | Doctor | TA | Student |
|---------|-----------|-------|--------|-----|---------|
| Upload Material | ✅ | ❌ | ✅ | ❌ | ❌ |
| Delete Material | ✅ | ❌ | ✅ (own) | ❌ | ❌ |
| View/Download Materials | ✅ | ✅ | ✅ | ✅ | ✅ |
| AI Summarize Material | ✅ | ❌ | ❌ | ❌ | ✅ |
| Ask AI About Material | ✅ | ❌ | ❌ | ❌ | ✅ |

---

## Notifications

| Feature | SuperAdmin | Admin | Doctor | TA | Student |
|---------|-----------|-------|--------|-----|---------|
| View Own Notifications | ✅ | ✅ | ✅ | ✅ | ✅ |
| Mark as Read | ✅ | ✅ | ✅ | ✅ | ✅ |
| Send to All (Admin) | ✅ | ✅ | ❌ | ❌ | ❌ |
| Send to Own Students | ✅ | ❌ | ✅ | ❌ | ❌ |
| Delete Notification | ✅ | ✅ | ❌ | ❌ | ❌ |

---

## Complaints

| Feature | SuperAdmin | Admin | Doctor | TA | Student |
|---------|-----------|-------|--------|-----|---------|
| Submit Complaint | ✅ | ❌ | ❌ | ❌ | ✅ |
| View Own Complaints | ✅ | ❌ | ❌ | ❌ | ✅ |
| View Complaints About Me | ✅ | ✅ | ✅ | ❌ | ❌ |
| View All Complaints | ✅ | ✅ | ❌ | ❌ | ❌ |
| Reply to Complaint | ✅ | ❌ | ✅ | ❌ | ❌ |
| View Complaint Clusters | ✅ | ✅ | ✅ | ❌ | ❌ |

---

## Analytics & Dashboard

| Feature | SuperAdmin | Admin | Doctor | TA | Student |
|---------|-----------|-------|--------|-----|---------|
| Admin Dashboard | ✅ | ✅ | ❌ | ❌ | ❌ |
| Doctor Dashboard | ✅ | ✅ | ✅ | ❌ | ❌ |
| Student Dashboard | ✅ | ❌ | ❌ | ❌ | ✅ |
| System Analytics | ✅ | ✅ | ❌ | ❌ | ❌ |
| Risk Analysis | ✅ | ✅ | ✅ | ❌ | ❌ |
| At-risk Students | ✅ | ✅ | ✅ | ❌ | ❌ |
| Audit Logs | ✅ | ❌ | ❌ | ❌ | ❌ |

---

## AI Companion Features

| Feature | SuperAdmin | Admin | Doctor | TA | Student |
|---------|-----------|-------|--------|-----|---------|
| AI Chat | ✅ | ✅ | ✅ | ✅ | ✅ |
| Companion Profile | ✅ | ❌ | ✅ | ❌ | ✅ |
| Companion Dashboard | ✅ | ❌ | ✅ | ❌ | ✅ |
| Learning Sessions | ✅ | ❌ | ❌ | ❌ | ✅ |
| Flashcard Generation | ✅ | ❌ | ❌ | ❌ | ✅ |
| Flashcard Review | ✅ | ❌ | ❌ | ❌ | ✅ |
| View Insights | ✅ | ❌ | ✅ | ❌ | ✅ |
| Class Analytics (Companion) | ✅ | ❌ | ✅ | ❌ | ❌ |

---

## Teaching Intelligence

| Feature | SuperAdmin | Admin | Doctor | TA | Student |
|---------|-----------|-------|--------|-----|---------|
| Teaching Dashboard | ✅ | ❌ | ✅ | ❌ | ❌ |
| Class Analytics | ✅ | ❌ | ✅ (own) | ❌ | ❌ |
| Student Intelligence | ✅ | ❌ | ✅ (own) | ❌ | ❌ |
| Topic Analytics | ✅ | ❌ | ✅ (own) | ❌ | ❌ |
| At-risk Students | ✅ | ❌ | ✅ (own) | ❌ | ❌ |
| Teaching Alerts | ✅ | ❌ | ✅ (own) | ❌ | ❌ |
| Export Excel | ✅ | ❌ | ✅ (own) | ❌ | ❌ |

---

## Regulations

| Feature | SuperAdmin | Admin | Doctor | TA | Student |
|---------|-----------|-------|--------|-----|---------|
| View Regulations | ✅ | ✅ | ✅ | ✅ | ✅ |
| Manage Regulations | ✅ | ✅ | ❌ | ❌ | ❌ |
| View Academic Roadmap | ✅ | ❌ | ❌ | ❌ | ✅ |

---

## Schedule

| Feature | SuperAdmin | Admin | Doctor | TA | Student |
|---------|-----------|-------|--------|-----|---------|
| View Schedule | ✅ | ✅ | ✅ | ✅ | ✅ |
| Create/Edit Schedule | ✅ | ✅ | ❌ | ❌ | ❌ |
| View My Schedule | ✅ | ❌ | ✅ | ❌ | ✅ |

---

## UI Visibility Rules

Use this matrix to show/hide UI elements:

```typescript
// hooks/usePermissions.ts
export function usePermissions() {
  const { user } = useAuthStore();
  const role = user?.role;
  
  return {
    canViewTeachingIntelligence: ['Doctor', 'SuperAdmin'].includes(role),
    canManageExams: ['Doctor', 'SuperAdmin'].includes(role),
    canTakeExam: role === 'Student',
    canSubmitComplaint: role === 'Student',
    canReplyToComplaint: ['Doctor', 'SuperAdmin'].includes(role),
    canImportStudents: ['Admin', 'SuperAdmin'].includes(role),
    canViewAuditLogs: role === 'SuperAdmin',
    canManageStructure: ['Admin', 'SuperAdmin'].includes(role),
    canViewAllAnalytics: ['Admin', 'SuperAdmin'].includes(role),
    canUseAiCompanion: ['Student', 'Doctor', 'SuperAdmin'].includes(role),
    canExportExcel: ['Doctor', 'SuperAdmin'].includes(role),
  };
}
```
