# 📣 Complaint System — Complete Guide

## Overview

The complaint system is a **full-cycle student grievance platform** with AI intelligence built in. It's not just a ticket system — it uses Claude AI to automatically analyze every complaint for sentiment, urgency, and category, then clusters patterns to give management actionable insights.

---

## Complaint Lifecycle

```
Student submits complaint
        │
        ▼
Status: "Pending"
Priority: "Normal"
        │
        ▼ (instantly — background)
AI Analysis (Hangfire job):
  • Sentiment: negative/positive/neutral
  • Category: academic_fairness / technical / conduct / ...
  • RiskScore: 0.0 → 1.0
  • Summary: AI-generated short summary
        │
        ├── RiskScore > 0.7 → Priority = "High" → notify admin
        ├── RiskScore > 0.9 → Priority = "Critical" → urgent notify
        │
        ▼
Admin reviews
        │
        ├── Status → "UnderReview"
        │
        ▼
Admin resolves
        │
        └── Status → "Resolved" | "Dismissed"
            ResolutionNote added
```

---

## Complaint Entity

```csharp
public class Complaint : BaseEntity
{
    public Ulid   StudentId      { get; set; }  // SystemUser.Id (who submitted)
    public string Title          { get; set; }  // Short title
    public string Message        { get; set; }  // Body (max 2000 chars)

    // What is being complained about:
    public string TargetType { get; set; }
    // "doctor" | "department" | "administration" | "technical" | "subject"

    public string TargetId  { get; set; }  // ID of the target entity

    // Workflow state:
    public string Status   { get; set; }   // "Pending" | "UnderReview" | "Resolved" | "Dismissed"
    public string Priority { get; set; }   // "Normal" | "High" | "Critical"

    public string? ResolutionNote { get; set; }  // Admin's response

    // AI analysis (1:1):
    public ComplaintAnalysis? Analysis { get; set; }
}
```

---

## Complaint Analysis (AI-Powered)

```csharp
public class ComplaintAnalysis : BaseEntity
{
    public Ulid   ComplaintId { get; set; }  // FK → Complaints (1:1)
    public string Sentiment   { get; set; }  // "positive" | "negative" | "neutral"
    public string Category    { get; set; }  // AI-classified category
    public double RiskScore   { get; set; }  // 0.0 (low) → 1.0 (critical)
    public string Summary     { get; set; }  // AI-generated 1-2 sentence summary
}
```

---

## Complete API Reference

### POST /api/complaints
**Auth:** Student  
**Purpose:** Submit a new complaint  

**Request:**
```json
{
  "title": "Unfair midterm grading",
  "message": "I believe my midterm exam was graded incorrectly. I answered question 3 correctly but received 0 marks. The answer key seems to be wrong.",
  "targetType": "doctor",
  "targetId": "01H...doctor-id..."
}
```

**Validation:**
- `title`: required, max 200 chars
- `message`: required, max 2000 chars
- `targetType`: must be one of the allowed values
- `targetId`: must be a valid ULID

**Response (201 Created):**
```json
{
  "id": "01H...",
  "title": "Unfair midterm grading",
  "status": "Pending",
  "priority": "Normal",
  "createdAt": "2026-05-16T08:00:00Z"
}
```

**After response:** Hangfire background job is enqueued for AI analysis (student doesn't wait).

---

### GET /api/complaints/my-complaints
**Auth:** Student  
**Returns:** Only this student's complaints
```json
[
  {
    "id": "01H...",
    "title": "Unfair midterm grading",
    "status": "Resolved",
    "priority": "High",
    "targetType": "doctor",
    "resolutionNote": "Grade reviewed and corrected. You now have 8/10 for Q3.",
    "analysis": {
      "sentiment": "negative",
      "category": "academic_fairness",
      "riskScore": 0.78,
      "summary": "Student disputes midterm question 3 grade..."
    },
    "createdAt": "2026-05-16T08:00:00Z"
  }
]
```

---

### GET /api/complaints
**Auth:** Admin, SuperAdmin  
**Query:** `?status=Pending&priority=High&targetType=doctor&page=1&size=20`  
**Returns:** All complaints with AI analysis

---

### PUT /api/complaints/{id}
**Auth:** Admin  
**Purpose:** Update complaint status / add resolution

**Request:**
```json
{
  "status": "Resolved",
  "resolutionNote": "We have reviewed the exam and corrected the grade."
}
```

---

### PUT /api/complaints/{id}/resolve
**Auth:** Admin  
**Shortcut endpoint for resolving**

**Request:**
```json
{
  "resolutionNote": "Issue has been investigated and resolved."
}
```

---

## AI Background Analysis — Deep Dive

```
[AiBackgroundJob runs after every new complaint]
        │
        ▼
Prompt sent to Claude:
"Analyze this student complaint and return a JSON object.

Complaint title: {title}
Complaint message: {message}
Target type: {targetType}

Return ONLY valid JSON in this exact format:
{
  'sentiment': 'positive' | 'negative' | 'neutral',
  'category': one of [academic_fairness, grading, teacher_conduct, 
                       technical_issue, administration, exam_policy, other],
  'riskScore': number between 0.0 and 1.0,
  'summary': 'one or two sentence summary'
}"
        │
        ▼
Claude returns:
{
  "sentiment": "negative",
  "category": "academic_fairness",
  "riskScore": 0.78,
  "summary": "Student disputes Q3 midterm grading, claims correct answer received 0 marks."
}
        │
        ▼
System saves ComplaintAnalysis
  + If riskScore > 0.7 → updates Complaint.Priority = "High"
  + If riskScore > 0.9 → updates Complaint.Priority = "Critical"
                        + Sends admin notification immediately
```

---

## Complaint Intelligence Reports (Daily/Weekly/Monthly)

These are **Hangfire recurring jobs** that run automatically:

### Daily Report
```
Every day:
  1. Fetch all complaints from past 24 hours
  2. Group by TargetType and Category
  3. Calculate: total, high-priority count, avg riskScore
  4. Ask Claude: "Summarize these complaints and identify any patterns"
  5. Send admin notification with summary
  
Example output:
"أمس: 12 شكوى جديدة.
 نمط مكتشف: 5 شكاوى عن تصحيح امتحان CS301.
 توصية: مراجعة درجات الامتحان مع د. أحمد."
```

### Weekly Report
```
Every Monday:
  1. Fetch all complaints from past 7 days
  2. Cluster by similarity (AI clustering)
  3. Identify top-3 recurring issues
  4. Save ComplaintCluster records
  5. Notify admin with trend analysis
```

### Monthly Report
```
Every 1st of month:
  1. Full monthly breakdown per department
  2. Resolution rate statistics
  3. Average response time
  4. Most affected doctors/departments
  5. Recommendations for management
```

---

## Complaint Priority Escalation

| RiskScore | Priority | Action |
|-----------|---------|--------|
| 0.0 – 0.49 | Normal | Standard queue |
| 0.5 – 0.69 | Normal | Standard queue |
| 0.7 – 0.89 | High | Admin notification sent |
| 0.9 – 1.0 | Critical | Urgent admin notification sent |

---

## Target Types Explained

| TargetType | When to Use | TargetId |
|-----------|------------|---------|
| `doctor` | Complaint about a specific professor | Doctor.Id (ULID) |
| `department` | Complaint about department policies | Department.Id (ULID) |
| `administration` | General admin complaints | Admin.Id or empty |
| `technical` | Platform/system technical issues | Can be null or system area |
| `subject` | About a specific course | Subject.Id or SubjectOffering.Id |

---

## Business Rules

| Rule | Behavior |
|------|---------|
| Student can submit multiple complaints | No limit per day |
| Student cannot see other students' complaints | Row-level security by StudentId |
| AI analysis runs async | Student doesn't wait; response returns immediately |
| Admin cannot see who submitted (anonymous?) | Currently NOT anonymous — StudentId stored |
| Doctor cannot see complaints about themselves | Only Admin role can access all complaints |
| Resolved complaints still visible to student | Permanent record with resolution note |
| Dismissed complaints | Remain visible with dismissal reason |

---

## Frontend Implementation Guide

### Submit Complaint Form
```javascript
const targetTypes = [
  { value: 'doctor', label: 'شكوى عن دكتور' },
  { value: 'department', label: 'شكوى عن القسم' },
  { value: 'administration', label: 'شكوى إدارية' },
  { value: 'technical', label: 'مشكلة تقنية' },
  { value: 'subject', label: 'شكوى عن مادة' },
];

async function submitComplaint(form) {
  const response = await fetch('/api/complaints', {
    method: 'POST',
    headers: { 'Authorization': `Bearer ${token}`, 'Content-Type': 'application/json' },
    body: JSON.stringify({
      title: form.title,
      message: form.message,
      targetType: form.targetType,
      targetId: form.targetId
    })
  });

  if (response.status === 201) {
    showSuccess('تم إرسال شكواك بنجاح. سيتم مراجعتها قريباً.');
  }
}
```

### Status Badge Colors
```javascript
const statusColors = {
  'Pending':     'yellow',   // قيد الانتظار
  'UnderReview': 'blue',     // تحت المراجعة
  'Resolved':    'green',    // تم الحل
  'Dismissed':   'gray',     // مرفوضة
};

const priorityColors = {
  'Normal':   'gray',
  'High':     'orange',
  'Critical': 'red',
};
```
