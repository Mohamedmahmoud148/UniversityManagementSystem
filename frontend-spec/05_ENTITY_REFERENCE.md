# 05 — Entity Reference

> Complete TypeScript interface definitions for all data models used in the frontend.

---

## Authentication & Users

```typescript
type UserRole = 'Student' | 'Doctor' | 'Admin' | 'SuperAdmin' | 'TeachingAssistant';

interface AuthResponse {
  token: string;
  refreshToken: string;
  expiresIn: number;
  user: UserProfile;
}

interface UserProfile {
  id: string;
  profileId: string;
  role: UserRole;
  fullName: string;
  universityEmail: string;
  universityStudentId?: string;
  batchId?: string;
  batchName?: string;
  groupId?: string;
  groupName?: string;
  departmentId?: string;
  departmentName?: string;
  collegeId?: string;
  collegeName?: string;
}
```

---

## Academic Structure

```typescript
interface University { id: string; name: string; code: string; }
interface College { id: string; name: string; code: string; universityId: string; }
interface Department { id: string; name: string; code: string; collegeId: string; }
interface Batch { id: string; name: string; code: string; departmentId: string; }
interface Group { id: string; name: string; code: string; batchId: string; }

interface AcademicYear {
  id: string; name: string; collegeId: string;
  order?: number; isActive: boolean; createdAt: string;
}

interface Semester {
  id: string; name: string; order: number;
  startDate: string; endDate: string; academicYearId: string;
}
```

---

## Students

```typescript
interface StudentDto {
  id: string; code: string; fullName: string;
  email: string; phone: string; nationalId: string;
  universityStudentId: string; universityEmail?: string;
  universityId: string; batchId: string; groupId: string;
  isActive: boolean;
}

interface StudentDetailDto extends StudentDto {
  collegeName: string; departmentId: string;
  departmentName: string; batchName: string;
  groupName: string; collegId: string;
}

interface StudentSummaryDto {
  id: string; code: string; fullName: string;
  universityStudentId: string; email: string;
  batchName: string; departmentName: string; collegeName: string;
}

interface StudentGpaDto {
  studentId: string; gpa: number; cgpa: number;
  lastSemesterGpa: number; totalCreditHours: number;
}
```

---

## Doctors

```typescript
interface DoctorDto {
  id: string; code: string; fullName: string;
  email: string; phone: string; universityStaffId: string;
  universityEmail?: string; departmentId: string;
}

interface DoctorSummaryDto {
  id: string; code: string; fullName: string;
  email: string; departmentId: string;
  departmentName: string; collegeName: string;
}
```

---

## Subjects & Offerings

```typescript
interface SubjectDto {
  id: string; name: string; code: string; creditHours: number;
  collegeId: string; collegeName?: string;
  departmentId: string; departmentName?: string;
  batchId?: string; batchName?: string;
  doctorName?: string;
}

interface OfferingSummaryDto {
  id: string; code: string;
  subjectName: string; subjectCode: string;
  doctorName: string; doctorId: string;
  departmentName: string; batchName: string;
  groupName?: string; semesterName: string;
  maxCapacity: number; enrolledCount: number;
}
```

---

## Enrollments

```typescript
interface EnrollmentDto {
  id: string; studentId: string; studentName: string;
  subjectOfferingId: string; subjectCode: string;
  subjectName: string; departmentName: string;
  doctorName: string; semesterName: string;
  enrolledAt: string; isActive: boolean;
}

interface EligibleOfferingDto {
  offeringId: string; subjectName: string;
  doctorName: string; semesterName: string;
  eligibilityStatus: 'eligible' | 'blocked' | 'warning';
  blockers: string[]; warnings: string[];
}

interface AcademicStatusDto {
  studentId: string; studentName: string;
  gpa: number; cgpa: number; lastSemesterGpa: number;
  standing: string; standingColor: 'green' | 'yellow' | 'red';
  earnedHours: number; remainingHours: number;
  totalRequired: number; currentLevel: number;
  maxAllowedHours: number; warningCount: number;
  hasWarning: boolean; warningMessage?: string;
}
```

---

## Grades

```typescript
interface StudentGradeDto {
  subjectName: string; finalScore: number;
  gradeLetter: string; gradePoints: number; isFinalized: boolean;
}

interface GradeDto {
  id: string; studentId: string; subjectOfferingId: string;
  finalScore: number; gradeLetter: string;
  gradePoints: number; isFinalized: boolean; calculatedAt: string;
}
```

---

## Exams

```typescript
type ExamType = 'Quiz' | 'Midterm' | 'Final';
type ExamStatus = 'Draft' | 'Published' | 'Completed';
type QuestionType = 'MCQ' | 'TrueFalse' | 'Essay' | 'ShortAnswer';

interface ExamDto {
  id: string; code: string; title: string;
  description?: string; type: ExamType;
  totalMarks: number; passingMarks: number;
  startTime: string; endTime: string;
  status: ExamStatus; durationMinutes: number;
  subjectOfferingId: string;
  questions: ExamQuestionDto[];
}

interface ExamQuestionDto {
  id: string; questionText: string;
  questionType: QuestionType; mark: number;
  options?: string[];
  correctAnswer?: string; // only shown to Doctor
}

interface ExamSubmissionResponseDto {
  submissionId: string; score: number;
  isPassed: boolean; feedback?: string;
}

interface ExamAnalyticsDto {
  avgScore: number; passRate: number;
  highestScore: number; lowestScore: number;
  distribution: { label: string; count: number }[];
}
```

---

## Assignments

```typescript
interface AssignmentDto {
  id: string; title: string; description: string;
  instructions?: string; deadline: string;
  maxGrade: number; allowLateSubmission: boolean;
  aiGradingEnabled: boolean; subjectOfferingId: string;
  submissionCount?: number;
}

interface SubmissionDto {
  id: string; studentName: string;
  submittedAt: string; isLate: boolean;
  status: 'Submitted' | 'UnderReview' | 'Graded' | 'Rejected';
  grade?: number; feedback?: string;
  isAiGraded: boolean; fileUrl?: string; textAnswer?: string;
}
```

---

## Attendance

```typescript
interface AttendanceReportDto {
  subjectAttendance: SubjectAttendanceDto[];
}

interface SubjectAttendanceDto {
  subjectName: string; subjectCode: string;
  totalSessions: number; attendedSessions: number;
  attendancePercent: number; status: 'good' | 'warning' | 'critical';
}
```

---

## Materials

```typescript
interface MaterialDto {
  id: string; title: string; description?: string;
  contentType: string; fileSize: number;
  uploadedAt: string; downloadUrl?: string;
}

interface PaginatedMaterialResponseDto {
  data: MaterialDto[]; totalCount: number; page: number; size: number;
}
```

---

## Notifications

```typescript
interface NotificationDto {
  id: string; title: string; message: string;
  isRead: boolean; actionUrl?: string; createdAt: string;
}
```

---

## Complaints

```typescript
type ComplaintTargetType = 'Doctor' | 'Exam' | 'Grade' | 'Other';
type ComplaintStatus = 'Pending' | 'UnderReview' | 'Resolved' | 'Rejected';

interface ComplaintDto {
  id: string; studentId: string; title: string;
  message: string; targetType: ComplaintTargetType;
  targetId?: string; status: ComplaintStatus;
  priority: 'Low' | 'Medium' | 'High';
  resolutionNote?: string; doctorReply?: string;
  createdAt: string;
}
```

---

## AI Companion

```typescript
interface AiCompanionProfileDto {
  userId: string; learningStyle: string;
  currentGoal: string; preferredStudyTime: string;
  weakSubjects: string[]; strongSubjects: string[];
  totalSessions: number; currentStreakDays: number;
  longestStreakDays: number; engagementScore: number;
  lastInteractionAt?: string; activeGoals: string[];
  milestones: string[];
}

interface CompanionDashboardDto {
  profile: AiCompanionProfileDto;
  recentInsights: AiInsightDto[];
  dueFlashcards: FlashcardDto[];
  weeklyProgress: WeeklyProgressDto;
  todayRecommendations: string[];
}

interface WeeklyProgressDto {
  sessionsThisWeek: number; averageAccuracy: number;
  flashcardsReviewed: number; studyMinutes: number;
  dailyActivity: DailyActivityDto[];
}

interface LearningSessionDto {
  id: string; sessionType: string; topicName: string;
  status: string; totalQuestions: number;
  correctAnswers: number; accuracyPercent: number;
  durationMinutes: number; aiFeedback: string;
  startedAt: string; completedAt?: string;
}

interface FlashcardDeckDto {
  id: string; title: string; topicName: string;
  cardCount: number; dueToday: number;
  createdAt: string; cards: FlashcardDto[];
}

interface FlashcardDto {
  id: string; front: string; back: string;
  hint?: string; difficulty: 'Easy' | 'Medium' | 'Hard';
  repetitionCount: number; easeFactor: number;
  nextReviewAt: string;
}

interface AiInsightDto {
  id: string; insightType: string; priority: string;
  title: string; message: string; actionUrl?: string;
  isAcknowledged: boolean; createdAt: string; expiresAt?: string;
}
```

---

## Teaching Intelligence

```typescript
interface TeachingDashboardDto {
  offerings: DoctorOfferingSummaryDto[];
  overallStats: DashboardStatsDto;
  atRiskStudents: StudentIntelligenceDto[];
  weakTopics: WeakTopicDto[];
  classComparisons: ClassComparisonDto[];
  recentAlerts: TeachingAlertDto[];
  aiRecommendations: string[];
}

interface DoctorOfferingSummaryDto {
  offeringId: string; subjectName: string; subjectCode: string;
  batchName: string; groupName: string;
  departmentName: string; collegeName: string; semesterName: string;
  totalStudents: number; atRiskCount: number;
  averageGrade: number; averageAttendance: number;
  assignmentCompletionRate: number;
  overallHealth: 'excellent' | 'good' | 'concerning' | 'critical';
}

interface StudentIntelligenceDto {
  studentId: string; studentName: string; studentUniversityId: string;
  batchName: string; groupName: string;
  departmentName: string; collegeName: string;
  finalScore?: number; midtermScore?: number;
  attendancePercent: number; assignmentCompletionRate: number;
  avgExamScore?: number; avgQuizScore?: number;
  lateSubmissions: number; missingAssignments: number;
  aiSessionCount: number; learningStreakDays: number;
  engagementScore: number; riskScore: number;
  riskLevel: 'Low' | 'Medium' | 'High' | 'Critical';
  riskFactors: string[]; riskExplanation: string;
  recommendedAction: string;
  overallTrend: 'improving' | 'stable' | 'declining';
  gradeTrend: number; attendanceTrend: number;
  computedAt: string;
}

interface StudentExcelRowDto {
  universityId: string; studentName: string;
  batchName: string; groupName: string;
  departmentName: string; collegeName: string; subjectName: string;
  finalScore?: number; midtermScore?: number;
  courseworkScore?: number; finalExamScore?: number;
  gradeCategory: 'Pass' | 'Fail' | 'Pending';
  totalSessions: number; attendedSessions: number; attendancePercent: number;
  totalAssignments: number; submittedAssignments: number;
  missingAssignments: number; assignmentCompletionRate: number;
  totalExams: number; avgExamScore?: number; avgQuizScore?: number;
  riskScore: number; riskLevel: string; riskFactors: string;
  aiSessions: number; studyMinutes: number; streakDays: number;
}

interface WeakTopicDto {
  topicName: string; sourceType: string;
  averageScore: number; errorRate: number;
  affectedStudents: number;
  severity: 'low' | 'medium' | 'high' | 'critical';
  aiRecommendation: string;
}

interface TeachingAlertDto {
  alertId: string; alertType: string; severity: string;
  title: string; message: string;
  studentId?: string; studentName?: string;
  offeringId?: string; isRead: boolean; createdAt: string;
}
```

---

## Regulations & Roadmap

```typescript
interface AcademicRoadmapDto {
  regulationId: string; regulationTitle: string;
  departmentName: string; collegeName: string; batchName: string;
  totalSemesters: number; totalCreditHours: number;
  completedCreditHours: number; remainingCreditHours: number;
  totalSubjects: number; passedSubjects: number;
  failedSubjects: number; currentlyEnrolled: number;
  currentGpa: number;
  semesters: SemesterRoadmapDto[];
  recommendedNext: SubjectStatusDto[];
  mustRetake: SubjectStatusDto[];
}

interface SemesterRoadmapDto {
  semesterNumber: number;
  status: 'completed' | 'in_progress' | 'upcoming';
  totalSubjects: number; passedSubjects: number;
  totalCreditHours: number; earnedCreditHours: number;
  subjects: SubjectStatusDto[];
}

interface SubjectStatusDto {
  subjectId: string; subjectName: string; subjectCode: string;
  creditHours: number; isRequired: boolean;
  status: 'passed' | 'failed' | 'enrolled' | 'available' | 'locked';
  gradeLetter?: string; finalScore?: number;
}
```

---

## Analytics

```typescript
interface AdminDashboardDto {
  totalStudents: number; totalDoctors: number;
  activeCourses: number; totalEnrollments: number;
  totalColleges: number; totalDepartments: number;
  totalBatches: number; avgGpa: number;
  passRate: number; atRiskCount: number;
}

interface StudentRiskDto {
  studentId: string; studentCode: string; fullName: string;
  departmentName: string; gpa?: number;
  attendanceRate: number; failingSubjects: number;
  riskLevel: 'Low' | 'Medium' | 'High' | 'Critical';
}
```

---

## Pagination Pattern

```typescript
interface PagedResult<T> {
  data: T[];
  totalCount: number;
  page: number;
  size: number;
}
```

---

## API Error Format

```typescript
interface ApiError {
  status: number;
  message: string;
  errors?: Record<string, string[]>;
  timestamp?: string;
}
```
