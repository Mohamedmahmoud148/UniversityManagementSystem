# 17 — Page Map

## Complete Route Structure

```
/
├── /auth
│   ├── /auth/login                    All roles
│   ├── /auth/register                 Admin (invite link)
│   └── /auth/change-password          Any authenticated
│
├── /student                           Student only
│   ├── /student/dashboard             Main student home
│   ├── /student/companion             AI Companion hub
│   │   ├── /student/companion/coach   Academic coaching chat
│   │   ├── /student/companion/quiz    Interactive quiz sessions
│   │   ├── /student/companion/flashcards  Flashcard decks
│   │   ├── /student/companion/flashcards/:deckId  Review mode
│   │   ├── /student/companion/progress  Progress reports
│   │   └── /student/companion/insights  AI insights feed
│   ├── /student/exams                 Exam list
│   ├── /student/exams/:id             Exam detail / pre-exam screen
│   ├── /student/exams/:id/take        Active exam taking (fullscreen)
│   ├── /student/exams/:id/result      Post-exam result view
│   ├── /student/assignments           Assignment list
│   ├── /student/assignments/:id       Assignment detail + submit
│   ├── /student/grades                Grades & GPA
│   ├── /student/attendance            Attendance report
│   ├── /student/attendance/scan       QR scanner page
│   ├── /student/materials             Course materials library
│   ├── /student/materials/:offeringId Materials for specific course
│   ├── /student/roadmap               Academic roadmap
│   ├── /student/schedule              Class schedule
│   └── /student/complaints            Complaint management
│       ├── /student/complaints/new    Submit complaint
│       └── /student/complaints/:id    Complaint detail
│
├── /doctor                            Doctor only
│   ├── /doctor/dashboard              Teaching Intelligence Dashboard
│   ├── /doctor/analytics/:offeringId  Class analytics deep dive
│   │   ├── ?tab=overview
│   │   ├── ?tab=students
│   │   ├── ?tab=topics
│   │   └── ?tab=trends
│   ├── /doctor/students/:offeringId   Student intelligence table
│   ├── /doctor/students/:offeringId/:studentId  Individual student
│   ├── /doctor/exams                  Exam management list
│   ├── /doctor/exams/create           Create exam form
│   ├── /doctor/exams/:id              Exam detail
│   ├── /doctor/exams/:id/edit         Edit exam
│   ├── /doctor/exams/:id/results      Exam results table
│   ├── /doctor/exams/:id/analytics    Exam analytics
│   ├── /doctor/assignments            Assignment management
│   ├── /doctor/assignments/create     Create assignment
│   ├── /doctor/assignments/:id        Assignment submissions
│   ├── /doctor/materials              Materials management
│   ├── /doctor/attendance             Attendance management
│   ├── /doctor/attendance/session     Create/active session + QR
│   ├── /doctor/alerts                 Teaching alerts
│   ├── /doctor/notifications          Notifications + send to students
│   └── /doctor/complaints             View/reply complaints
│
├── /admin                             Admin + SuperAdmin
│   ├── /admin/dashboard               System dashboard
│   ├── /admin/structure               University structure management
│   ├── /admin/students                Student management
│   ├── /admin/students/import         Bulk import
│   ├── /admin/students/:id            Student detail/edit
│   ├── /admin/doctors                 Doctor management
│   ├── /admin/doctors/:id             Doctor detail/edit
│   ├── /admin/subjects                Subject management
│   ├── /admin/offerings               Subject offerings
│   ├── /admin/enrollments             Enrollment management
│   ├── /admin/grades                  Grade management / import
│   ├── /admin/schedule                Schedule management
│   ├── /admin/regulations             Academic regulations
│   ├── /admin/analytics               Analytics suite
│   │   ├── ?tab=overview
│   │   ├── ?tab=students
│   │   ├── ?tab=performance
│   │   └── ?tab=risk
│   ├── /admin/complaints              All complaints management
│   ├── /admin/notifications           Notification management
│   ├── /admin/audit-logs              (SuperAdmin only)
│   ├── /admin/delete                  (SuperAdmin only) Safe delete
│   └── /admin/settings                System settings
│
├── /ta                                TeachingAssistant only
│   ├── /ta/attendance                 Attendance management
│   └── /ta/students                   View students
│
├── /chat                              All authenticated roles
│   └── /chat?id={conversationId}      Specific conversation
│
├── /profile                           All authenticated
│   ├── ?tab=profile
│   ├── ?tab=security
│   └── ?tab=preferences
│
├── /notifications                     All authenticated
│
└── /error
    ├── /unauthorized                  403 page
    ├── /not-found                     404 page
    └── /server-error                  500 page
```

---

## Page Count Summary

| Category | Pages |
|---------|-------|
| Auth | 3 |
| Student | 20 |
| Doctor | 18 |
| Admin/SuperAdmin | 16 |
| TeachingAssistant | 2 |
| Shared (Chat, Profile, Notifications) | 4 |
| Error Pages | 3 |
| **Total** | **66** |

---

## Route Guard Rules

```typescript
const routePermissions: Record<string, UserRole[]> = {
  '/student/*': ['Student', 'SuperAdmin'],
  '/doctor/*': ['Doctor', 'SuperAdmin'],
  '/admin/*': ['Admin', 'SuperAdmin'],
  '/ta/*': ['TeachingAssistant', 'SuperAdmin'],
  '/admin/audit-logs': ['SuperAdmin'],
  '/admin/delete': ['SuperAdmin'],
  '/chat': ['Student', 'Doctor', 'Admin', 'SuperAdmin', 'TeachingAssistant'],
  '/profile': ['Student', 'Doctor', 'Admin', 'SuperAdmin', 'TeachingAssistant'],
  '/notifications': ['Student', 'Doctor', 'Admin', 'SuperAdmin', 'TeachingAssistant'],
};
```

---

## Navigation Flow Diagram

```
Login
  ↓
[Role Detection]
  ├── Student → /student/dashboard
  ├── Doctor → /doctor/dashboard  
  ├── Admin → /admin/dashboard
  ├── SuperAdmin → /admin/dashboard
  └── TA → /ta/attendance

Student Dashboard
  ├── Click GPA → /student/grades
  ├── Click Exam → /student/exams/:id
  ├── Click Assignment → /student/assignments/:id
  ├── Click AI Companion → /student/companion
  └── Click Material → /student/materials

Doctor Dashboard
  ├── Click Offering Card → /doctor/analytics/:offeringId
  ├── Click Student → /doctor/students/:offeringId/:studentId
  ├── Click Exam → /doctor/exams/:id
  └── Click Alert → /doctor/alerts

AI Chat → Available from anywhere via navbar icon
```
