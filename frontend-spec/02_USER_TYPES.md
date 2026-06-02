# 02 — User Types

## Role Overview

| Role | JWT Claim Value | Description | Primary Experience |
|------|----------------|-------------|-------------------|
| `SuperAdmin` | `SuperAdmin` | Full system access | Admin + override capabilities |
| `Admin` | `Admin` | University administrators | System management, user management |
| `Doctor` | `Doctor` | Faculty / professors | Teaching, exams, student monitoring |
| `TeachingAssistant` | `TeachingAssistant` | TAs supporting doctors | Limited teaching access |
| `Student` | `Student` | Enrolled students | Learning, exams, AI companion |

---

## SuperAdmin

**Who**: System-level administrator (IT department, university leadership)

**Capabilities** (beyond Admin):
- Delete any entity
- Register other Admins
- Access audit logs
- Override any role restriction
- Access DevController endpoints
- Full financial/analytics access

**UI Differences from Admin**:
- Shows "SuperAdmin" badge in navbar
- Access to Audit Logs page
- Access to Delete Analysis tool
- Access to all bulk-upload operations
- Can impersonate other users (future feature)

---

## Admin

**Who**: Registrar's office, department secretaries, academic affairs

**Capabilities**:
- Create/manage universities, colleges, departments, batches, groups
- Register students and doctors
- Manage semesters and academic years
- Manage enrollments (admin-enroll, remove)
- Import students via Excel
- Import grades via Excel
- View all analytics
- Manage notifications (broadcast)
- View all complaints
- Reset user passwords

**Primary Pages**:
- System Dashboard (`/admin/dashboard`)
- University Structure (`/admin/structure`)
- User Management (`/admin/users`)
- Enrollment Management (`/admin/enrollments`)
- Grade Management (`/admin/grades`)
- Analytics (`/admin/analytics`)
- Complaints (`/admin/complaints`)
- Notifications (`/admin/notifications`)
- Settings (`/admin/settings`)

---

## Doctor

**Who**: University professors/lecturers

**Capabilities**:
- View own subject offerings
- Create/manage exams
- Grade submissions (manual + AI-assisted)
- View class analytics
- Teaching Intelligence Dashboard
- Create/manage assignments
- Create attendance sessions
- Upload course materials
- View and reply to complaints
- Send notifications to own students
- Use AI Teaching Assistant

**Primary Pages**:
- Teaching Dashboard (`/doctor/dashboard`) ← **Teaching Intelligence**
- Class Analytics (`/doctor/analytics/:offeringId`)
- Student Intelligence (`/doctor/students/:offeringId`)
- Exam Management (`/doctor/exams`)
- Assignment Management (`/doctor/assignments`)
- Materials (`/doctor/materials`)
- Attendance (`/doctor/attendance`)
- Notifications (`/doctor/notifications`)
- AI Chat (`/chat`) — doctor mode
- Profile (`/profile`)

**User Profile Data Available After Login** (from `GET /api/auth/me`):
```json
{
  "id": "ulid",
  "role": "Doctor",
  "profileId": "ulid",  
  "fullName": "Dr. Mohamed Ahmed",
  "universityEmail": "m.ahmed@uni.edu",
  "departmentId": "ulid",
  "departmentName": "Computer Science"
}
```

---

## TeachingAssistant

**Who**: Graduate students or junior faculty supporting a doctor

**Capabilities** (subset of Doctor):
- Create attendance sessions
- Record attendance (QR-based)
- View enrolled students
- View exam results (read-only)
- Cannot create exams
- Cannot grade

**Primary Pages**:
- Attendance Management (`/ta/attendance`)
- Student List (`/ta/students`)
- Schedule (`/ta/schedule`)

---

## Student

**Who**: Enrolled university students

**Capabilities**:
- View own grades and GPA
- Submit assignments
- Take exams
- Check-in to attendance sessions via QR
- Upload files (PDF) for AI analysis
- Use AI Companion (chat, quizzes, flashcards)
- View academic roadmap
- Register for courses (during registration period)
- View course materials
- Submit complaints
- View notifications

**Primary Pages**:
- Student Dashboard (`/student/dashboard`)
- AI Companion (`/student/companion`)
- AI Chat (`/chat`) — student mode
- Exams (`/student/exams`)
- Assignments (`/student/assignments`)
- Grades (`/student/grades`)
- Materials (`/student/materials`)
- Academic Roadmap (`/student/roadmap`)
- Attendance (`/student/attendance`)
- Complaints (`/student/complaints`)
- Profile (`/profile`)
- Settings (`/settings`)

**User Profile Data** (from `GET /api/auth/me`):
```json
{
  "id": "ulid",
  "role": "Student",
  "profileId": "ulid",
  "fullName": "Ahmed Hassan",
  "universityEmail": "a.hassan@uni.edu",
  "universityStudentId": "20230001",
  "batchId": "ulid",
  "batchName": "2023",
  "groupId": "ulid",
  "groupName": "G1",
  "departmentId": "ulid",
  "departmentName": "Computer Science",
  "collegeId": "ulid",
  "collegeName": "Faculty of Computers and Information"
}
```

---

## Role Detection in Frontend

```typescript
// hooks/useRole.ts
export function useRole() {
  const { user } = useAuthStore();
  return {
    isStudent: user?.role === 'Student',
    isDoctor: user?.role === 'Doctor',
    isAdmin: user?.role === 'Admin',
    isSuperAdmin: user?.role === 'SuperAdmin',
    isTA: user?.role === 'TeachingAssistant',
    isAdminOrAbove: ['Admin', 'SuperAdmin'].includes(user?.role || ''),
    isDoctorOrAbove: ['Doctor', 'Admin', 'SuperAdmin'].includes(user?.role || ''),
    hasRole: (roles: string[]) => roles.includes(user?.role || ''),
  };
}
```

---

## Post-Login Redirect Logic

```typescript
function getDefaultRoute(role: string): string {
  switch (role) {
    case 'Student': return '/student/dashboard';
    case 'Doctor': return '/doctor/dashboard';
    case 'Admin': return '/admin/dashboard';
    case 'SuperAdmin': return '/admin/dashboard';
    case 'TeachingAssistant': return '/ta/attendance';
    default: return '/auth/login';
  }
}
```

---

## User Context Available Throughout App

After successful login, the following is stored in Zustand and available everywhere:

```typescript
interface UserProfile {
  id: string;            // SystemUserId
  profileId: string;     // Student.Id / Doctor.Id / Admin.Id
  role: UserRole;
  fullName: string;
  universityEmail: string;
  // Role-specific:
  universityStudentId?: string;   // Students only
  batchId?: string;               // Students only
  batchName?: string;             // Students only
  groupId?: string;               // Students only
  groupName?: string;             // Students only
  departmentId?: string;          // All roles
  departmentName?: string;        // All roles
  collegeId?: string;             // All roles
  collegeName?: string;           // All roles
}
```
