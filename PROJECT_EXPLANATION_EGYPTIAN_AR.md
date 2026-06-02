# شرح مشروع University Management System بالمصري

الملف ده معمول كـ script تقدر تمسكه وتشرح بيه للتيم. أنا اعتبرت إن عندنا مشروعين في الـ workspace:

1. مشروع الباك إند: `UniversityManagementSystem` وهو ASP.NET Core API.
2. مشروع مواصفات الفرونت: `frontend-spec` وهو blueprint كامل لبناء React frontend.

مهم: فيه AI service مبني بـ FastAPI مذكور في الدوكيومنتيشن والباك إند بيتكلم معاه، بس كود الـ FastAPI نفسه مش ظاهر جوه الـ workspace الحالي. بالتالي شرحه هنا مبني على الـ docs والـ integrations الموجودة في الباك إند.

---

## 1. الفكرة العامة

المشروع اسمه University Management System أو UniSys. الفكرة مش مجرد نظام جامعة تقليدي فيه طلبة ودكاترة ومواد، هو معمول كمنصة أكاديمية ذكية:

- الطالب يقدر يشوف جدوله، درجاته، GPA، مواده، امتحاناته، واجباته، حضوره، الخطة الدراسية، ويكلم AI academic companion.
- الدكتور يقدر يدير المواد اللي بيدرسها، يعمل امتحانات، يرفع مواد، ياخد حضور، يصحح submissions، ويتابع الطلبة المعرضين للخطر من Teaching Intelligence dashboard.
- الأدمن يقدر يدير هيكل الجامعة، الطلبة، الدكاترة، اللوائح، التسجيل، التحليلات، الشكاوى، الحذف الآمن، والـ audit logs.
- الـ SuperAdmin عنده صلاحيات أعلى من الأدمن، خصوصا في تسجيل Admins، audit logs، وحاجات النظام الحساسة.

يعني لو هنشرحها في جملة واحدة:  
ده نظام ERP أكاديمي للجامعة، فوقه طبقة AI بتفهم بيانات الطالب الحقيقية وتساعد الطالب والدكتور والإدارة بقرارات وتنبيهات ذكية.

---

## 2. الصورة الكبيرة للـ Architecture

النظام متقسم كده:

```text
React Frontend
   |
   | HTTPS + JWT
   v
.NET Backend API
   |
   | PostgreSQL / Redis / RabbitMQ / Hangfire / SignalR / R2
   |
   | HTTP
   v
FastAPI AI Service
   |
   | OpenRouter + ChromaDB + Embeddings
   v
LLM + RAG
```

الفرونت بيكلم .NET API.  
.NET API هو مركز التحكم: authentication، CRUD، business rules، database، notifications، background jobs، files.  
FastAPI AI service بيتنادى من .NET لما نحتاج شات، RAG، exam generation، AI grading، study plans.

---

## 3. Stack المستخدم

الباك إند:

- ASP.NET Core 9.
- Entity Framework Core 9.
- PostgreSQL.
- Redis cache.
- RabbitMQ + MassTransit.
- Hangfire background jobs.
- SignalR real-time notifications.
- Cloudflare R2 لتخزين الملفات.
- JWT Bearer auth.
- Serilog logging.
- Swagger.
- ULID IDs بدل integer IDs.

الفرونت حسب الـ spec:

- React 18 + TypeScript + Vite.
- React Router v6.
- Zustand للـ global state.
- React Query للـ server state.
- Tailwind + Shadcn/UI + Radix.
- Recharts للـ charts.
- React Hook Form + Zod.
- Axios.
- SignalR/WebSocket notifications.
- i18n عربي RTL وإنجليزي LTR.
- SheetJS للـ Excel export.
- react-dropzone للـ uploads.

الـ AI:

- FastAPI Python.
- OpenRouter / GPT-4o-mini حسب الدوكيومنتيشن.
- Embeddings: `text-embedding-3-small`.
- ChromaDB vector store.
- RAG على المحاضرات واللوائح.

---

## 4. الباك إند متقسم إزاي؟

الحل فيه 4 أجزاء رئيسية:

```text
UniversityManagementSystem.Api
UniversityManagementSystem.Core
UniversityManagementSystem.Infrastructure
UniversityManagementSystem.Tests
```

`Api`:

- Controllers.
- Middleware.
- SignalR hubs.
- Swagger.
- Program.cs وفيه DI وsecurity وjobs.

`Core`:

- Entities.
- DTOs.
- Interfaces.
- Constants.
- Exceptions.
- Events.

`Infrastructure`:

- EF DbContext.
- Services implementation.
- Hangfire jobs.
- RabbitMQ consumers.
- Cloudflare R2 storage.

`Tests`:

- Unit tests لبعض الخدمات والكنترولرز زي Auth, Complaints, Grades, Analytics, Audit Logs.

---

## 5. أهم نقطة في الداتا: ULID وSoft Delete

كل الـ IDs تقريبا ULID strings، مش integers ومش UUID عادي.  
مثال: `01JSCX1234ABC56DEFGH789JKM`.

ليه ULID؟

- آمن كـ string في URLs.
- sortable بالوقت.
- مناسب للأنظمة الكبيرة.

كل entity تقريبا وارث من `BaseEntity`:

```csharp
Id
Code
CreatedAt
DeletedAt
```

`DeletedAt = null` معناها active.  
لو اتمسح، غالبا بيتعمله soft delete بدل ما يروح من الداتابيز، وده مهم لأن البيانات الأكاديمية زي درجات وسجلات طلبة ماينفعش تضيع.

---

## 6. الأدوار الموجودة

عندنا 5 roles:

- `Student`: طالب.
- `Doctor`: دكتور/محاضر.
- `TeachingAssistant`: معيد أو مساعد تدريس.
- `Admin`: مسؤول شؤون أكاديمية/إدارة.
- `SuperAdmin`: صاحب أعلى صلاحيات في النظام.

بعد login، الـ JWT فيه:

- user id.
- role.
- profile id: يعني StudentId أو DoctorId أو AdminId.
- email.

الفرونت بيستخدم الـ role عشان يوجه المستخدم للـ dashboard المناسبة:

- Student -> `/student/dashboard`
- Doctor -> `/doctor/dashboard`
- Admin/SuperAdmin -> `/admin/dashboard`
- TA -> `/ta/attendance`

---

## 7. Authentication وSecurity

الـ auth flow:

1. المستخدم يعمل login.
2. الباك إند يشيك password hash بـ BCrypt.
3. يرجع JWT + refresh token.
4. الفرونت يخزن tokens.
5. أي request بعدها يبعت `Authorization: Bearer <token>`.
6. لو token انتهى، Axios interceptor يعمل refresh token.

الحماية موجودة على كذا مستوى:

- `[Authorize(Roles="...")]` على endpoints.
- service layer بتتأكد إن الطالب يشوف بياناته بس.
- الدكتور يقدر يدير offerings بتاعته هو بس.
- Rate limiting:
  - global: 1000 request/minute.
  - login: 5 attempts/minute.
  - AI endpoints: rate limited.
- AI input sanitizer ضد prompt injection وSQL injection patterns.
- AI service نفسها عندها RBAC على مستوى الـ intent.
- الملفات بتتخزن في R2 بروابط signed URLs بتنتهي.

---

## 8. Response format المهم للفرونت

معظم الردود طالعة في wrapper موحد:

```json
{
  "data": {},
  "success": true,
  "statusCode": 200,
  "timestamp": "..."
}
```

أو في guide تاني:

```json
{
  "success": true,
  "message": "Done.",
  "data": {},
  "errors": null
}
```

لما نبني الفرونت، لازم نعمل normalization في API client بحيث نقرأ `data` ونتعامل مع اختلاف الـ envelope لو موجود.

---

## 9. هيكل الجامعة Academic Structure

النظام بيبني الجامعة كـ hierarchy:

```text
University
  -> College
    -> Department
      -> Batch
        -> Group
```

الأدمن يقدر:

- يعمل جامعة.
- يعمل كليات.
- يعمل أقسام.
- يعمل دفعات.
- يعمل مجموعات.
- يجيب full structure tree مرة واحدة للـ dropdowns.

ده مهم لأن كل حاجة بعد كده مبنية عليه: الطالب مربوط بـ department/batch/group، المادة مربوطة بـ department، الـ offering مربوط بـ semester/batch/doctor.

---

## 10. Academic Years وSemesters

فيه academic years وsemesters:

- AcademicYear: السنة الأكاديمية أو المرحلة.
- Semester: الترم.

الأدمن يعمل السنة، يربط departments بيها، يعمل semesters جوه السنة، ويحدد active.

الـ subject offering بيتربط بـ semester، عشان نعرف المادة دي نسخة أي ترم ومع مين.

---

## 11. Subjects وSubject Offerings

`Subject` هي المادة كتعريف عام:

- name.
- code.
- credit hours.
- department.
- prerequisites.

`SubjectOffering` هي نسخة من المادة في ترم معين:

- subject.
- semester.
- doctor.
- batch/group.
- max capacity.
- grading weights.

مثال:  
`Data Structures` كمادة عامة غير `Data Structures - Fall 2026 - Dr Ahmed - Batch 2024`.

الأوبشنز المهمة في offering:

- maxCapacity.
- midterm max score + weight.
- coursework max score + weight.
- final exam max score + weight.
- platform max score + weight.

لازم الـ weights مجموعها 1.0.

---

## 12. إدارة الطلبة

الأدمن يقدر:

- يضيف طالب يدوي.
- يعدل بياناته.
- يعمله active/deactive.
- يبحث ويفلتر الطلبة.
- يجيب الطلبة حسب batch/group/department/offering.
- يعمل bulk import من Excel.
- ينزل template Excel.
- يرفع Excel ويرجع credentials للطلبة.

النظام بيولد:

- university student id.
- university email.
- temporary password.

وفي Excel import فيه:

- successful students sheet.
- failed rows sheet.
- summary sheet.
- validation errors/warnings.

الطالب يقدر:

- يشوف profile بتاعه.
- يشوف grades/GPA.
- يشوف roadmap.
- يشوف enrollments.

---

## 13. إدارة الدكاترة والـ TAs

الأدمن يقدر:

- يضيف دكتور.
- يعدل بياناته.
- يبحث ويفلتر الدكاترة.
- يربط دكتور بمادة أو offering.
- يرفع دكاترة bulk upload.

الدكتور بعد login يشوف:

- offerings بتاعته.
- schedule.
- enrolled students.
- exams.
- assignments.
- materials.
- teaching intelligence.

الـ TeachingAssistant صلاحياته أقل:

- حضور.
- يشوف طلبة.
- يشوف schedule.
- لا يعمل exams ولا grading حسب الـ spec.

---

## 14. Smart Registration والتسجيل في المواد

فيه طريقتين للتسجيل:

1. Legacy enrollments: `/api/enrollments`.
2. Smart registration: `/api/registration` ودي الأفضل للفرونت.

الطالب قبل ما يسجل:

1. يجيب academic status.
2. يجيب eligible offerings.
3. يشوف blockers/warnings.
4. يسجل أو يدخل waitlist.

الـ academic status بيرجع:

- GPA.
- CGPA.
- standing.
- earned hours.
- remaining hours.
- max allowed hours.
- warning/probation state.

الـ eligible offerings بتقول:

- المادة متاحة ولا لأ.
- full ولا فيها capacity.
- prerequisites مكتملة ولا لأ.
- الطالب عدى المادة قبل كده ولا لأ.
- credit hours limit هيتكسر ولا لأ.

لو offering full:

- الطالب يدخل waitlist.
- النظام يرجع waitlist position.

الأدمن يقدر يدير:

- prerequisites.
- academic policy.
- thresholds زي honor/warning/probation max hours.

---

## 15. Regulations والـ Academic Roadmap

الـ Regulation هي اللائحة الأكاديمية:

- اسم اللائحة.
- نوعها: Academic / Conduct / Exam / General.
- محتوى نصي.
- ملف PDF/Word/Excel.
- department.
- subjects mapping.

`RegulationSubject` بتحدد:

- المادة.
- semester number.
- required ولا elective.

الطالب عنده endpoint مهم جدا:  
`GET /api/regulations/my-roadmap`

ده بيرجع:

- إجمالي الساعات المطلوبة.
- الساعات اللي خلصها.
- الساعات المتبقية.
- المواد اللي عداها.
- المواد اللي ساقط فيها ولازم يعيدها.
- المواد المسجل فيها حاليا.
- المواد المقترحة بعد كده.
- GPA الحالي.
- breakdown semester by semester.

الـ roadmap هو مصدر أساسي للـ AI study plan والـ academic advisor.

---

## 16. Grades وGPA

الدكتور أو الأدمن يقدر:

- يرفع grades من Excel.
- يحسب grades للـ offering.
- يعدل درجات.
- يحذف/يعيد حساب grade.

الطالب يقدر:

- يشوف درجاته.
- يشوف GPA.

الـ GPA بيتحسب من finalized grades، والـ grade له:

- midterm.
- coursework.
- final.
- platform.
- total/final score.
- letter grade.
- grade points.

بعد حساب grades، النظام يقدر يحدّث `StudentAcademicStatus`.

---

## 17. Schedule

الأدمن يعمل schedule entries:

- subjectOfferingId.
- batchId.
- groupId optional.
- dayOfWeek.
- startTime.
- endTime.
- type: Lecture / Section / Lab.
- location.
- weekType: All / Odd / Even.

الطالب يشوف:

- schedule بتاع batch.
- today classes.

الدكتور يشوف:

- my schedule.
- my today.

---

## 18. Attendance

الدكتور أو TA أو Admin يعمل attendance session:

- offering/subject.
- date.
- start/end.
- QR content/session id.

الطالب يعمل check-in بالـ QR أو الكود.

الأدمن يقدر:

- يصحح حضور.
- يعدل record.
- يحذف record.

الدكتور/TA/Admin يقدروا يشوفوا attendance report:

- total sessions.
- attended sessions.
- attendance percentage.
- records per session.

الحضور داخل في risk scoring وteaching intelligence.

---

## 19. Materials والملفات

الدكتور يرفع course material:

- PDF.
- DOC/DOCX.
- PPT/PPTX.
- XLSX.
- images.
- videos.
- ZIP.
- TXT.

المواد لحد 500 MB حسب الدوكيومنتيشن.  
التخزين في Cloudflare R2.  
التحميل بيرجع signed URL صالح تقريبا 60 دقيقة.

الطالب والدكتور والأدمن يقدروا يشوفوا materials حسب offering أو subject.

كل material ممكن يتعمله RAG indexing عشان AI يجاوب من المحتوى نفسه.

---

## 20. RAG System

RAG معناها Retrieval-Augmented Generation.  
الفكرة إن الـ AI مايجاوبش من دماغه في أسئلة المحاضرات أو اللوائح، لكن يدور في الملفات المرفوعة.

الـ index flow:

1. الدكتور يرفع material.
2. الباك إند يخزن الملف في R2.
3. `RagIndexingJob` أو endpoint manual يعمل indexing.
4. FastAPI يستخرج text.
5. يقسمه chunks حوالي 500 tokens.
6. يعمل embeddings.
7. يخزن vectors في ChromaDB.
8. يخزن metadata/chunks في PostgreSQL.

الـ query flow:

1. الطالب يسأل: "اشرحلي stack من المحاضرة".
2. AI يكتشف intent = material_qa.
3. يعمل embedding للسؤال.
4. يبحث في ChromaDB عن أقرب chunks.
5. يبعت chunks للـ LLM.
6. الـ LLM يجاوب بس من chunks ويذكر source/citation.

لو مفيش chunk مناسب، المفروض يقول إن المعلومة مش موجودة في المواد المتاحة بدل ما يخمن.

---

## 21. Exams System

الامتحانات من أكبر modules في المشروع.

الدكتور يقدر يعمل exam:

- Structured manual exam.
- AI-generated exam.
- Questions from PDF/material.

أنواع الامتحانات:

- Quiz.
- Midterm.
- Final.

حالات الامتحان:

- Draft: مش ظاهر للطلبة.
- Published: متاح.
- Closed: اتقفل.

أنواع الأسئلة:

- MCQ.
- True/False.
- Short Answer.
- Essay.

الأوبشنز المهمة:

- total marks.
- start time.
- end time.
- duration.
- subject offering.
- randomized exam.
- questions per student.

Randomization:

- كل طالب ممكن يشوف ترتيب أسئلة مختلف.
- MCQ options ممكن تتلخبط لكل طالب.
- الطالب له `StudentExamVariant`.

Student exam flow:

1. يشوف امتحاناته.
2. يدخل pre-screen.
3. يبدأ session.
4. يجاوب.
5. autosave كل فترة.
6. proctoring events تتبعت.
7. submit.
8. يشوف النتيجة لما تتاح.

Doctor exam flow:

1. يعمل exam.
2. يضيف questions أو يولد بـ AI.
3. ينشر exam.
4. يشوف submissions/results.
5. يعمل auto-grade أو manual grade.
6. يشوف analytics.

Proctoring:

- tab switch.
- focus loss.
- window blur.
- events بتتخزن ويراجعها الدكتور.

Grading:

- MCQ/TrueFalse automatic.
- Essay/Short Answer ممكن AI grade.
- الدكتور يراجع ويfinalize.

فيه job reminders كل 30 دقيقة يبعث تنبيه للطلبة قبل الامتحان بـ 24 ساعة أو ساعتين.

---

## 22. Assignments System

الدكتور/الأدمن يعمل assignment:

- title.
- description.
- instructions.
- deadline.
- max grade.
- allow late submission.
- AI grading enabled.
- grading rubric.

الطالب يقدم:

- text answer.
- file up to 100 MB.

السيستم يحدد:

- submitted at.
- late ولا لأ.
- status.

الحالات:

- Submitted.
- UnderReview.
- Graded.
- Rejected.

التصحيح:

- الدكتور يصحح يدوي.
- أو يستخدم AI grade.

AI grading بيرجع:

- score.
- feedback.
- strengths.
- weaknesses.
- confidence.

فيه AssignmentReminderJob كل 30 دقيقة:

- يدور على واجبات موعدها خلال 24 ساعة.
- يبعث تنبيه بس للطلبة اللي لسه ما سلموش.
- لو أقل من ساعتين يبقى urgent.

---

## 23. AI Chat System

الشات مش chatbot عادي. هو conversation-based:

- create conversation.
- list conversations.
- send message.
- get messages.
- rename/delete conversation.

.NET ChatService بيبني academic context للمستخدم ويبعت لـ FastAPI.

FastAPI فيه PlannerAgent:

1. يصنف الرسالة لـ intent.
2. يعمل deterministic keyword override للعربي لو التصنيف غلط.
3. يشيك RBAC.
4. ينفذ module مناسب.
5. يرجع رد طبيعي.

أمثلة intents:

- study_plan.
- academic_advice.
- material_qa.
- material_explanation.
- regulation.
- result_query.
- generate_exam.
- assignment_query.
- backend_api_query.
- action_execute.
- complaint_submit.
- complaint_summary.
- summarization.
- file_extraction.
- file_processing.
- cv_analysis.
- general_chat.

الـ AI يقدر يعمل حاجات زي:

- يقول للطالب GPA ووضعه.
- يشرح اللائحة.
- يعمل study plan.
- يجاوب من المحاضرات.
- يساعد الدكتور يعمل exam.
- يلخص شكاوى للدكتور/الأدمن.
- ينفذ action بعد confirmation.

---

## 24. AI Companion Platform

دي experience خاصة للطالب، وممكن الدكتور يشوف class analytics منها.

في dashboard الطالب:

- profile summary.
- learning style.
- goal.
- streak.
- engagement score.
- due flashcards.
- weekly progress.
- today recommendations.
- recent sessions.
- insights feed.
- weak subjects.

Features:

### Academic Coach

شات مخصص للطالب، يستخدم بياناته الحقيقية:

- grades.
- attendance.
- roadmap.
- assignments.

ويجاوب على أسئلة زي:

- "وضعي في مادة OS عامل إيه؟"
- "أحسن معدلي إزاي؟"
- "أذاكر إيه الأسبوع ده؟"

### Study Partner / Quiz

الطالب يختار:

- topic.
- difficulty.
- session type.

session types:

- Quiz.
- Active Recall.
- Concept Check.
- Exam Prep.
- Weakness Review.
- Free Study.

بعد الجلسة:

- score.
- accuracy.
- duration.
- AI feedback.
- next steps.

### Flashcards

الطالب يولد deck:

- topic.
- card count.
- difficulty.
- optional offering.

المراجعة بـ SM-2 spaced repetition:

- quality 0 to 5.
- next review date بيتحدث.
- due cards تظهر للطالب.

### Insights

أنواع insights:

- inactivity alert.
- exam approaching.
- assignment deadline.
- streak milestone.
- improvement detected.
- weakness detected.
- weekly/monthly report.
- risk alert.

فيه background service كل 6 ساعات يطلع follow-ups:

- طالب inactive 7 أيام.
- exam خلال 7 أيام.
- assignment خلال 48 ساعة.
- grade أقل من 50.
- streak milestones.
- weekly report يوم الاتنين.

---

## 25. Teaching Intelligence Platform

دي dashboard الدكتور الأساسية في الـ frontend-spec.

الفكرة: الدكتور بدل ما يشوف raw data بس، يشوف intelligence عن الكلاس.

البيانات جاية من snapshots بتتحدث hourly:

- StudentIntelligenceSnapshot.
- TeachingIntelligenceBackgroundService.

Doctor dashboard بيرجع:

- offerings summary.
- overall stats.
- top at-risk students.
- weak topics.
- class comparisons.
- recent alerts.
- AI recommendations.

Risk score من 0 لـ 100:

- Low: 0-29.
- Medium: 30-54.
- High: 55-74.
- Critical: 75-100.

المعادلة حسب spec:

- Grade risk: 0-35.
- Attendance risk: 0-30.
- Assignment risk: 0-20.
- Engagement risk: 0-15.

الدكتور يقدر:

- يفتح offering analytics.
- يشوف student intelligence table.
- يفلتر riskLevel/trend/atRiskOnly.
- يشوف weak topics.
- يشوف most improved students.
- يعمل export Excel.
- يعمل manual refresh للsnapshots.
- يشوف alerts ويعلمها read.

Excel export فيه 29 columns:

- student identity.
- hierarchy.
- subject.
- grades.
- attendance.
- assignments.
- exams.
- risk.
- AI activity.

---

## 26. Academic Risk وProactive Alerts

فيه job يومي 6 صباحا:

- يحلل كل active offerings.
- يحسب attendance percent.
- يحسب average grade.
- يحدد risk level.
- يعمل AI recommendation.
- يخزن/يحدث AcademicRiskScore.
- يبعث notification للطلبة من Medium وطالع.

thresholds:

- Critical: attendance < 50 أو average grade < 40.
- High: attendance < 65 أو average grade < 55.
- Medium: attendance < 75 أو average grade < 65.
- Low: غير كده.

الدكتور/الأدمن يقدروا يشوفوا:

- at-risk students.
- risk dashboard.
- risk per student.
- trigger manual analysis.

ده مهم جدا في presentation لأنه بيوضح إن النظام proactive مش reactive.

---

## 27. Notifications System

النوتيفيكيشنز معمولة production-style:

```text
NotificationService
  -> save AppNotification in PostgreSQL
  -> publish NotificationCreatedEvent to RabbitMQ
  -> MassTransit consumer
  -> SignalR push to user group
```

لو SignalR وقع، notification محفوظة في DB والفرونت يجيبها polling.

أنواع notifications:

- admin broadcast.
- doctor send to students.
- exam reminders.
- assignment reminders.
- academic risk alerts.
- AI companion insights.

الفرونت:

- notification bell.
- unread count.
- toast on ReceiveNotification.
- mark as read.

---

## 28. Complaints System

الطالب يقدر يقدم complaint:

- title.
- message.
- target type: Doctor / Department / Admin / Technical / Subject.
- target id optional.
- priority.

الحالات:

- Pending.
- UnderReview.
- Resolved.
- Dismissed.

الدكتور يقدر يشوف complaints اللي تخصه ويرد.  
الأدمن يشوف كل complaints ويديرها.

فيه AI intelligence:

- daily report.
- weekly report.
- monthly report.
- clustering للشكاوى حسب themes.
- priority breakdown.
- top concerns.

الطالب كمان ممكن يقدم شكوى من خلال AI chat، والـ AI يستخرج target/message ويبعت complaint API.

---

## 29. Analytics وDashboards

فيه dashboards حسب الدور.

Student dashboard:

- current GPA.
- overall attendance.
- enrolled courses.
- subject details.
- upcoming exams.
- due assignments.
- AI companion quick access.
- notifications.

Doctor dashboard:

- total offerings.
- total students.
- average grade.
- course list.
- teaching intelligence.
- at-risk students.
- weak topics.
- alerts.

Admin dashboard:

- total students.
- total doctors.
- active courses.
- total enrollments.
- colleges/departments/batches.
- average GPA.
- pass rate.
- at-risk count.
- students by department.
- top enrolled subjects.

Analytics endpoints:

- student count by department.
- student count by batch.
- doctor workload.
- top enrolled subjects.
- offering enrollment stats.
- grade distribution.
- attendance trends.
- department comparison.
- student performance.

---

## 30. Intelligent Deletion

دي feature مهمة جدا للإدارة.

بدل ما الأدمن يدوس delete وخلاص، فيه بروتوكول مرحلتين:

1. Analyze.
2. Execute.

Analyze:

- يقولك إيه اللي هيتأثر.
- يحسب dependencies.
- يطلع warnings.
- يحدد risk level.
- يحدد confirmation steps.
- يقول canDelete ولا blocked.

Risk levels:

- Low.
- Medium.
- High.
- Critical.
- Catastrophic.

Delete types:

- SoftDelete.
- HardDelete.
- Restricted.
- ArchiveOnly.
- ImmutableBlocked.

أمثلة:

- College/University/Department = Catastrophic.
- Student/Doctor/Subject = Critical.
- Exam/Batch/Regulation = High.
- Group/Material/Complaint = Medium.
- Notification/RefreshToken/ChatMessage = Low.

فيه blockers:

- finalized grades immutable.
- published/closed exam مينفعش يتمسح.
- exam عليه submissions مينفعش يتمسح بسهولة.
- active enrollment restricted.
- audit logs immutable.

الفرونت لازم:

- دايما ينادي analyze الأول.
- لو blocked يخفي زرار execute.
- لو High يطلب typed phrase.
- لو Critical يطلب phrase + password.
- لو Catastrophic يطلب phrase + password + second admin style confirmation.

---

## 31. Audit Logs

الـ Audit Logs بتسجل:

- مين عمل إيه.
- على أي entity.
- امتى.
- old values.
- new values.
- action type.

الوصول غالبا SuperAdmin/Admin حسب الكود والدوكيومنتيشن، لكن الـ frontend-spec مدي Audit Logs لـ SuperAdmin فقط. في العرض قول: دي شاشة system audit للعمليات الحساسة، وأعلى صلاحية هي اللي تستخدمها.

---

## 32. Background Jobs كلها

أهم jobs:

- `ExamReminderJob`: كل 30 دقيقة، تنبيهات امتحانات.
- `AssignmentReminderJob`: كل 30 دقيقة، تنبيهات واجبات للي ماسلموش.
- `AcademicRiskJob`: يوميا 6 صباحا، risk analysis.
- `RagIndexingJob`: يوميا، index materials غير المفهرسة.
- `ComplaintIntelligenceJob`: daily/weekly/monthly reports.
- `AiFollowUpBackgroundService`: كل 6 ساعات، insights/follow-ups.
- `TeachingIntelligenceBackgroundService`: يحدث snapshots للدكاترة.
- `BulkUploadJob`: background import.

---

## 33. Frontend Spec: الصفحات

الـ frontend-spec بيحدد حوالي 66 صفحة:

Auth:

- login.
- register/invite.
- change password.

Student:

- dashboard.
- companion hub.
- coach.
- quiz.
- flashcards.
- flashcard review.
- progress.
- insights.
- exams list/detail/take/result.
- assignments list/detail.
- grades.
- attendance.
- QR scan.
- materials.
- roadmap.
- schedule.
- complaints.

Doctor:

- teaching dashboard.
- class analytics.
- student intelligence.
- student profile.
- exams management.
- create/edit exam.
- exam results.
- exam analytics.
- assignments.
- submissions.
- materials.
- attendance.
- alerts.
- notifications.
- complaints.

Admin:

- dashboard.
- structure.
- students.
- student import.
- doctors.
- subjects.
- offerings.
- enrollments.
- grades.
- schedule.
- regulations.
- analytics.
- complaints.
- notifications.
- audit logs.
- safe delete.
- settings.

Shared:

- chat.
- profile.
- notifications.
- error pages.

---

## 34. Frontend Architecture المقترحة

الفرونت المفروض feature-based:

```text
src/
  api/
  features/
    exams/
    companion/
    teaching/
    admin/
    assignments/
  components/
  store/
  hooks/
  types/
  router/
  i18n/
```

React Query:

- لكل API query.
- staleTime حسب الـ spec.
- invalidation بعد mutations.
- polling للنوتيفيكيشنز/alerts.

Zustand:

- auth store.
- notification store.
- UI preferences.

Routing:

- ProtectedRoute.
- role guards.
- route groups student/doctor/admin/ta.

Localization:

- default Arabic RTL.
- English LTR.
- `document.documentElement.dir` يتغير حسب اللغة.

---

## 35. أهم User Flows للشرح

### Student login

1. يدخل login.
2. يبعت credentials.
3. ياخد token + user profile.
4. يتحول لـ `/student/dashboard`.
5. dashboard تعمل parallel queries للـ GPA/attendance/exams/companion/notifications.

### Student takes exam

1. يشوف exam في dashboard.
2. يدخل pre-screen.
3. يبدأ exam في fullscreen.
4. autosave.
5. proctoring events.
6. submit.
7. result/feedback.

### Doctor creates AI exam

1. يفتح exams.
2. يختار Generate with AI.
3. يحدد offering/topics/count/difficulty.
4. AI يولد الأسئلة.
5. الدكتور يراجع ويعدل.
6. ينشر.
7. الطلاب يوصلهم notification.

### Student AI companion

1. يدخل companion.
2. يبدأ quiz/session.
3. AI يسأل.
4. الطالب يجاوب.
5. AI يقيم.
6. session complete.
7. streak/engagement/insights تتحدث.

### Doctor handles at-risk student

1. dashboard يظهر at-risk count.
2. الدكتور يفتح student intelligence.
3. يشوف risk factors: grade/attendance/assignments/engagement.
4. يبعث support notification.

### Admin imports students

1. ينزل template.
2. يرفع Excel.
3. النظام validates/imports.
4. يرجع imported/failed/warnings.
5. ينزل credentials report.

### Smart registration

1. الطالب يشوف academic status.
2. يشوف eligible offerings.
3. يشوف blockers/warnings.
4. enroll أو waitlist.

### Safe delete

1. الأدمن يضغط delete.
2. frontend ينادي analyze.
3. يعرض impact.
4. يجمع confirmations.
5. execute.
6. refresh list + audit log.

---

## 36. أهم APIs حسب module

Auth:

- `POST /api/auth/login`
- `POST /api/auth/refresh-token`
- `GET /api/auth/me`
- `POST /api/auth/change-password`
- `POST /api/auth/admin/reset-password/{userId}`

Students:

- `GET /api/students/filter`
- `GET /api/students/me`
- `POST /api/students`
- `POST /api/students/import-excel`
- `GET /api/students/import-excel/template`

Registration:

- `GET /api/registration/academic-status`
- `GET /api/registration/eligible-offerings`
- `POST /api/registration/enroll/{offeringId}`
- `POST /api/registration/waitlist/{offeringId}`

Exams:

- `POST /api/exams`
- `POST /api/exams/generate-ai`
- `GET /api/exams/my-exams`
- `GET /api/exams/my-enrolled-exams`
- `POST /api/exams/{id}/submit`
- `POST /api/exams/{id}/save-progress`
- `GET /api/exams/{id}/results`
- `POST /api/exams/{id}/auto-grade`

Assignments:

- `POST /api/assignments`
- `GET /api/assignments/offering/{offeringId}`
- `POST /api/assignments/{id}/submit`
- `GET /api/assignments/{id}/submissions`
- `POST /api/assignments/submissions/{id}/grade`
- `POST /api/assignments/submissions/{id}/ai-grade`

Companion:

- `GET /api/companion/dashboard`
- `POST /api/companion/sessions/start`
- `POST /api/companion/sessions/{id}/complete`
- `POST /api/companion/flashcards/generate`
- `GET /api/companion/flashcards/due`
- `POST /api/companion/flashcards/cards/{cardId}/review`
- `GET /api/companion/insights`

Teaching Intelligence:

- `GET /api/teaching-intelligence/dashboard`
- `GET /api/teaching-intelligence/offerings/{id}/analytics`
- `GET /api/teaching-intelligence/offerings/{id}/students`
- `GET /api/teaching-intelligence/students/at-risk`
- `GET /api/teaching-intelligence/offerings/{id}/export`
- `POST /api/teaching-intelligence/offerings/{id}/refresh`

RAG:

- `POST /api/rag/index/{materialId}`
- `GET /api/rag/status/{materialId}`
- `POST /api/rag/search`
- `GET /api/rag/search/offering/{offeringId}`

Notifications:

- `GET /api/notification`
- `PUT /api/notification/{id}/read`
- `POST /api/notification`
- `POST /api/notification/send-to-my-students`

Deletion:

- `POST /api/deletion/analyze`
- `POST /api/deletion/execute`

---

## 37. أهم Demo Talking Points

لو هتعرض المشروع للتيم، ركز على دول:

1. مش CRUD system بس، ده academic intelligence platform.
2. الطالب عنده roadmap حقيقي مبني على اللائحة والدرجات.
3. التسجيل ذكي: prerequisites، GPA limits، waitlist، warnings.
4. الامتحانات فيها AI generation، randomization، autosave، proctoring، grading.
5. المواد بتتخزن في R2 وتتعملها RAG، فالطالب يسأل من المحاضرة نفسها.
6. AI chat فاهم 17 intent ومش generic chatbot.
7. AI Companion بيعمل sessions وflashcards وinsights وstudy plan.
8. Teaching Intelligence بتدي الدكتور risk score وweak topics وExcel reports.
9. Notifications real-time بـ SignalR وفي نفس الوقت persistent في DB.
10. Background jobs بتخلي النظام proactive: reminders، risk alerts، reports.
11. Intelligent deletion بيحمي البيانات الأكاديمية من الحذف الغلط.
12. Audit logs وrate limiting وJWT وRBAC بيدوا production readiness.

---

## 38. ملاحظات مهمة قبل التنفيذ أو العرض

- الكود الحالي TargetFramework هو `.NET 9`، رغم إن بعض frontend-spec كاتب `.NET 8`.
- `frontend-spec` مش implementation React، هو مواصفات تفصيلية للبناء.
- FastAPI AI service موثق ومتكامل من خلال .NET، لكن كوده مش ظاهر في نفس الـ workspace.
- بعض docs بتقول 300+ endpoints وبعض project docs بتقول 120+ endpoints. مصدر الحقيقة الأفضل هنا هو الكود + Swagger عند التشغيل.
- routes في الـ backend أحيانا casing مختلف: مثل `Notification` و`Chat` حسب controller route. الفرونت لازم يستخدم API map موحد أو Swagger generated client لتفادي اللخبطة.
- كل IDs strings، ممنوع parseInt.
- file uploads كلها multipart/form-data.
- signed URLs بتنتهي، فلازم تتطلب وقت التحميل مش تتخزن forever.

---

## 39. طريقة شرح مختصرة للتيم

ابدأ كده:

"المشروع عبارة عن University ERP ذكي. عندنا باك إند .NET 9 معمول Clean Architecture تقريبا: API, Core, Infrastructure. الداتا في PostgreSQL، والملفات في Cloudflare R2، والنوتيفيكيشنز real-time بـ SignalR ومعاها RabbitMQ. فيه Hangfire jobs للتنبيهات والتحليل. فوق ده فيه AI service بـ FastAPI بتتعامل مع الشات، RAG، study plans، exam generation، وAI grading.

المستخدمين خمسة: طالب، دكتور، TA، أدمن، SuperAdmin. كل دور له dashboard وصلاحيات. الطالب يقدر يسجل مواد بذكاء، يشوف roadmap، يمتحن، يقدم واجبات، يستخدم companion. الدكتور يدير offerings وامتحانات وواجبات ومواد وحضور، ويشوف teaching intelligence. الأدمن يدير الهيكل والناس واللوائح والتحليلات والشكاوى والحذف الآمن.

أقوى features هي AI Companion للطالب، Teaching Intelligence للدكتور، RAG على المحاضرات واللوائح، Smart Registration، Exam platform، والـ Intelligent Deletion."

دي الجملة الافتتاحية، وبعدها ادخل في كل module بالتفصيل من sections اللي فوق.

