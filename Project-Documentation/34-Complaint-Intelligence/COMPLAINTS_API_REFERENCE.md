# Complaints API Reference

> **Base URL:** `api/complaints` | **Auth:** `Authorization: Bearer <JWT>` (all endpoints)

---

## Existing Endpoints (v1 — unchanged)

### POST `api/complaints`
**Role:** Student, SuperAdmin

**Request:**
```json
{
  "title": "Unfair quiz grade",
  "targetType": "Doctor",
  "targetId": "01HN...",
  "message": "My grade was marked incorrectly in the midterm..."
}
```

| Field | Type | Required | Rules |
|---|---|---|---|
| `title` | string | ✅ | Max 200 |
| `targetType` | string | ✅ | `"Doctor"` / `"Subject"` / `"Administration"` / `"Technical"` |
| `targetId` | ULID string | ❌ | Required if targetType = Doctor |
| `message` | string | ✅ | Min 5, Max 2000 |

**Response `200`:**
```json
{
  "id": "01HN...",
  "studentId": "01HN...",
  "title": "Unfair quiz grade",
  "targetType": "Doctor",
  "targetId": "01HN...",
  "message": "My grade was marked incorrectly...",
  "status": "Pending",
  "priority": "Normal",
  "resolutionNote": null,
  "createdAt": "2026-06-21T10:00:00Z",
  "resolvedAt": null,
  "analysis": null
}
```
> `analysis` is null immediately — AI analysis runs in background (Hangfire job). Poll after ~5 seconds.

---

### GET `api/complaints/my-complaints`
**Role:** Student, SuperAdmin

**Query Params:**
| Param | Type | Default |
|---|---|---|
| `page` | int | 1 |
| `pageSize` | int | 20 (max 100) |
| `from` | ISO DateTime | — |
| `to` | ISO DateTime | — |
| `status` | string | — |
| `targetType` | string | — |
| `targetId` | ULID string | — |

**Response `200`:**
```json
{
  "totalCount": 5,
  "page": 1,
  "pageSize": 20,
  "items": [
    {
      "id": "01HN...",
      "studentId": "01HN...",
      "student": null,
      "title": "Unfair quiz grade",
      "targetType": "Doctor",
      "targetId": "01HN...",
      "message": "...",
      "status": "Resolved",
      "priority": "High",
      "resolutionNote": "I reviewed your paper — please see office hours.",
      "createdAt": "2026-06-15T10:00:00Z",
      "resolvedAt": "2026-06-18T14:00:00Z",
      "analysis": {
        "sentimentScore": -0.78,
        "category": "Grading",
        "severity": "high",
        "aiSummary": "Student disputes midterm grade, claims incorrect marking",
        "suggestedAction": "Review exam paper with student during office hours"
      }
    }
  ]
}
```

---

### GET `api/complaints/my-reports`
**Role:** Doctor, SuperAdmin

**Query Params:** same as `my-complaints`

**Response:** Same shape. `studentId` = `"HIDDEN"`, `student` = `null` (privacy).

---

### GET `api/complaints/all`
**Role:** Admin, SuperAdmin

**Query Params:** same as `my-complaints`

**Response:** Same shape but `student` is fully populated:
```json
"student": {
  "id": "01HN...",
  "fullName": "Ahmed Ali",
  "nationalId": "30001234567890",
  "email": "ahmed@benisuef.edu.eg",
  "phoneNumber": "01012345678",
  "academicCode": "CS2021001"
}
```

---

### GET `api/complaints/clusters`
**Role:** Admin, SuperAdmin, Doctor

**Query Params:**
| Param | Type |
|---|---|
| `targetType` | string (optional) |
| `targetId` | ULID string (optional) |

**Response `200`:** `Array<ComplaintClusterDto>` (basic shape, see v2 for enhanced)

---

### GET `api/complaints/doctor-options`
**Role:** Student, SuperAdmin

**Response `200`:**
```json
[
  { "id": "01HN...", "name": "Dr. Hassan Mohamed" }
]
```

---

### PUT `api/complaints/{id}/reply`
**Role:** Doctor, SuperAdmin

**Request:**
```json
{ "reply": "I reviewed your quiz. The grade is correct. Visit office hours." }
```

**Response `200`:** Updated `ComplaintDto` with `status = "Resolved"` and `resolutionNote` set.

**Errors:** `404` not found | `403` not directed at you

---

## New Endpoints (v2.0)

---

### PUT `api/complaints/clusters/{clusterId}/reply`
**Role:** Admin, SuperAdmin, Doctor

**اللي بيعمله:** يرد على كل طلاب الـ cluster بنفس الرسالة — كل طالب يوصله notification فردي.

**Request:**
```json
{
  "message": "The grading issue has been reviewed. Corrections have been applied to all affected students."
}
```

| Field | Type | Required | Rules |
|---|---|---|---|
| `message` | string | ✅ | Max 2000 |

**Response `200`:**
```json
{
  "clusterId": "01HN...",
  "topic": "Grading",
  "affectedStudents": 47,
  "notificationsSent": 47,
  "message": "The grading issue has been reviewed...",
  "repliedAt": "2026-06-21T14:00:00Z"
}
```

**Errors:** `400` invalid ID | `404` cluster not found

**Side effects:**
- All complaints in cluster → `status = "Resolved"`, `resolutionNote = message`
- Each unique student → receives SignalR push notification
- `ClusterReply` record created (reply history)
- Cluster `status` → `"Resolved"`, `resolvedAt` = now

---

### GET `api/complaints/clusters/{clusterId}`
**Role:** Admin, SuperAdmin, Doctor

**اللي بيعمله:** يجيب تفاصيل cluster واحد كاملة مع تاريخ الردود وتاريخ تغيير الـ status.

**Response `200`:**
```json
{
  "id": "01HN...",
  "topic": "Grading",
  "targetType": "Doctor",
  "targetId": "01HN...",
  "complaintCount": 47,
  "criticalCount": 5,
  "aiSummary": "47 students reported unfair grading across multiple quizzes...",
  "aiRecommendations": [
    "Review quiz grading rubric with all TAs",
    "Schedule open office hours for affected students",
    "Audit last 3 quiz correction batches"
  ],
  "status": "Investigating",
  "trendDirection": "Increasing",
  "averageSentiment": -0.72,
  "firstComplaintAt": "2026-06-01T08:00:00Z",
  "lastUpdated": "2026-06-21T10:00:00Z",
  "resolvedAt": null,
  "replies": [
    {
      "id": "01HN...",
      "message": "We are looking into this issue.",
      "affectedStudents": 47,
      "notificationsSent": 47,
      "repliedAt": "2026-06-20T09:00:00Z"
    }
  ],
  "statusHistory": [
    {
      "oldStatus": "Open",
      "newStatus": "Investigating",
      "reason": "Multiple critical complaints detected",
      "changedAt": "2026-06-19T11:00:00Z"
    }
  ]
}
```

**Errors:** `400` invalid ID | `404` not found

---

### PATCH `api/complaints/clusters/{clusterId}/status`
**Role:** Admin, SuperAdmin

**اللي بيعمله:** يغير status الـ cluster يدوياً مع حفظ تاريخ التغيير.

**Request:**
```json
{
  "status": "Investigating",
  "reason": "Multiple critical complaints detected — escalating"
}
```

| Field | Type | Required | Values |
|---|---|---|---|
| `status` | string | ✅ | `"Open"` / `"Investigating"` / `"Resolved"` / `"Archived"` |
| `reason` | string | ❌ | Max 500 chars |

**Response `204 No Content`**

**Errors:** `400` invalid status / invalid ID | `404` not found

---

### GET `api/complaints/dashboard`
**Role:** Admin, SuperAdmin

**اللي بيعمله:** داشبورد كامل مع كل المقاييس والإحصاءات.

**Response `200`:**
```json
{
  "summary": {
    "totalComplaints": 541,
    "pending": 67,
    "underReview": 12,
    "resolved": 447,
    "dismissed": 15,
    "critical": 8,
    "totalClusters": 23
  },
  "categories": [
    { "name": "Grading",    "count": 154 },
    { "name": "Attendance", "count": 97  },
    { "name": "Technical",  "count": 62  }
  ],
  "severities": [
    { "severity": "low",      "count": 201 },
    { "severity": "medium",   "count": 189 },
    { "severity": "high",     "count": 123 },
    { "severity": "critical", "count": 28  }
  ],
  "topClusters": [
    {
      "id": "01HN...",
      "topic": "Grading",
      "complaintCount": 47,
      "criticalCount": 5,
      "status": "Investigating",
      "trendDirection": "Increasing",
      "averageSentiment": -0.72,
      "aiRecommendations": ["Review grading rubric", "..."],
      "lastUpdated": "2026-06-21T10:00:00Z"
    }
  ],
  "metrics": {
    "averageResolutionHours": 4.2,
    "averageSentiment": -0.38,
    "trendingClustersCount": 3
  },
  "overTime": [
    { "date": "2026-05-22T00:00:00Z", "count": 12 },
    { "date": "2026-05-23T00:00:00Z", "count": 8  }
  ]
}
```

---

## Value Reference

### Complaint Status
| Value | Meaning |
|---|---|
| `"Pending"` | Submitted, not yet reviewed |
| `"UnderReview"` | Being investigated |
| `"Resolved"` | Replied and closed |
| `"Dismissed"` | Rejected / not valid |

### Complaint Priority
| Value | Set By |
|---|---|
| `"Normal"` | Default on creation |
| `"High"` | AI severity = high |
| `"Critical"` | AI severity = critical |

### Cluster Status
| Value | Meaning |
|---|---|
| `"Open"` | New cluster, not yet actioned |
| `"Investigating"` | Team is working on it |
| `"Resolved"` | Reply sent to all students |
| `"Archived"` | Closed for reference |

### TrendDirection
| Value | Meaning |
|---|---|
| `"Increasing"` | Complaint count growing |
| `"Stable"` | Roughly constant |
| `"Decreasing"` | Complaints reducing |

### SentimentScore
| Range | Meaning |
|---|---|
| `> 0.3` | Positive |
| `-0.3 to 0.3` | Neutral |
| `< -0.3` | Negative |
| `< -0.7` | Very Negative (urgent) |
