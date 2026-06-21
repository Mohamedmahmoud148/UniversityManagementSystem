# Frontend Integration Report — Complaint Intelligence Platform

> **Date:** 2026-06-21 | **For:** Frontend Team

---

## 1. Endpoint Map by Role

### Student
| Endpoint | When |
|---|---|
| `GET api/complaints/doctor-options` | Load dropdown on complaint form |
| `POST api/complaints` | Submit new complaint |
| `GET api/complaints/my-complaints` | List view |

### Doctor
| Endpoint | When |
|---|---|
| `GET api/complaints/my-reports` | Complaints dashboard |
| `GET api/complaints/clusters?targetType=Doctor&targetId={id}` | Cluster widget |
| `GET api/complaints/clusters/{id}` | Cluster detail modal |
| `PUT api/complaints/{id}/reply` | Reply to single complaint |
| `PUT api/complaints/clusters/{id}/reply` | Reply to whole cluster |

### Admin
| Endpoint | When |
|---|---|
| `GET api/complaints/dashboard` | Main dashboard page load |
| `GET api/complaints/all` | Full list with filters |
| `GET api/complaints/clusters` | Clusters table |
| `GET api/complaints/clusters/{id}` | Cluster detail |
| `PUT api/complaints/clusters/{id}/reply` | Bulk reply |
| `PATCH api/complaints/clusters/{id}/status` | Change cluster status |

---

## 2. Full Request/Response Per Endpoint

### POST `api/complaints` — Student submits complaint

**Request:**
```typescript
interface CreateComplaintRequest {
  title: string;        // max 200
  targetType: string;   // "Doctor" | "Subject" | "Administration" | "Technical"
  targetId?: string;    // ULID — required if targetType = "Doctor"
  message: string;      // 5–2000 chars
}
```

**Response `200`:** `ComplaintDto` (see type definitions below)

**Loading state:** "Submitting your complaint..."
**Success state:** Show "Complaint submitted. Our AI is analyzing it." toast
**Error `400`:** Show validation errors from server

---

### GET `api/complaints/my-complaints` — Student list

**Query params:**
```typescript
interface ComplaintsQuery {
  page?: number;       // default 1
  pageSize?: number;   // default 20, max 100
  from?: string;       // ISO date
  to?: string;         // ISO date
  status?: string;     // "Pending" | "UnderReview" | "Resolved" | "Dismissed"
  targetType?: string;
}
```

**Response `200`:**
```typescript
interface ComplaintsPage {
  totalCount: number;
  page: number;
  pageSize: number;
  items: ComplaintDto[];
}
```

**Empty state:** "You haven't submitted any complaints yet."
**Loading state:** Show 3 skeleton cards

---

### PUT `api/complaints/{id}/reply` — Doctor replies

**Request:**
```typescript
interface ReplyRequest {
  reply: string; // 1–2000 chars
}
```

**Response `200`:** Updated `ComplaintDto`

**Success:** Update card in-place (optimistic update). Student receives SignalR push.
**Error `403`:** "This complaint is not directed at you."
**Error `404`:** "Complaint not found."

---

### PUT `api/complaints/clusters/{clusterId}/reply` — Bulk cluster reply

**Request:**
```typescript
interface ClusterReplyRequest {
  message: string; // max 2000
}
```

**Response `200`:**
```typescript
interface ClusterReplyResponse {
  clusterId: string;
  topic: string;
  affectedStudents: number;
  notificationsSent: number;
  message: string;
  repliedAt: string; // ISO
}
```

**Loading state:** "Sending reply to {count} students..."
**Success:** "Reply sent to {affectedStudents} students successfully."
**Error `404`:** "Cluster not found."

---

### PATCH `api/complaints/clusters/{clusterId}/status` — Update status

**Request:**
```typescript
interface UpdateStatusRequest {
  status: "Open" | "Investigating" | "Resolved" | "Archived";
  reason?: string;
}
```

**Response `204 No Content`**

**Success:** Update cluster card status badge immediately (optimistic).
**Error `400`:** "Invalid status value."

---

### GET `api/complaints/dashboard` — Admin dashboard

**Response:**
```typescript
interface ComplaintDashboard {
  summary: {
    totalComplaints: number;
    pending: number;
    underReview: number;
    resolved: number;
    dismissed: number;
    critical: number;
    totalClusters: number;
  };
  categories: { name: string; count: number }[];
  severities: { severity: string; count: number }[];
  topClusters: EnhancedClusterDto[];
  metrics: {
    averageResolutionHours: number;
    averageSentiment: number;
    trendingClustersCount: number;
  };
  overTime: { date: string; count: number }[];
}
```

**Loading state:** Skeleton cards + chart placeholders
**Empty state:** "No complaints data available for this period."

---

## 3. TypeScript Type Definitions

```typescript
interface ComplaintDto {
  id: string;
  studentId: string;          // "HIDDEN" when doctor views
  student: StudentInfo | null; // null for doctor view, populated for admin
  title: string;
  targetType: string;
  targetId: string;
  message: string;
  status: ComplaintStatus;
  priority: ComplaintPriority;
  resolutionNote: string | null;
  createdAt: string;          // ISO 8601
  resolvedAt: string | null;  // ISO 8601
  analysis: ComplaintAnalysis | null;  // null while AI is processing
}

interface ComplaintAnalysis {
  sentimentScore: number;     // -1.0 to +1.0
  category: string;           // "Grading" | "Attendance" | "Technical" | ...
  severity: string;           // "low" | "medium" | "high" | "critical"
  aiSummary: string;
  suggestedAction: string;
}

type ComplaintStatus = "Pending" | "UnderReview" | "Resolved" | "Dismissed";
type ComplaintPriority = "Normal" | "High" | "Critical";

interface EnhancedClusterDto {
  id: string;
  topic: string;
  targetType: string;
  targetId: string;
  complaintCount: number;
  criticalCount: number;
  aiSummary: string;
  aiRecommendations: string[];
  status: ClusterStatus;
  trendDirection: TrendDirection;
  averageSentiment: number;
  firstComplaintAt: string;
  lastUpdated: string;
  resolvedAt: string | null;
  replies: ClusterReplyDto[];
  statusHistory: StatusHistoryDto[];
}

type ClusterStatus = "Open" | "Investigating" | "Resolved" | "Archived";
type TrendDirection = "Increasing" | "Stable" | "Decreasing";

interface ClusterReplyDto {
  id: string;
  message: string;
  affectedStudents: number;
  notificationsSent: number;
  repliedAt: string;
}

interface StatusHistoryDto {
  oldStatus: string;
  newStatus: string;
  reason: string | null;
  changedAt: string;
}
```

---

## 4. Pagination Pattern

All list endpoints use the same pattern:

```typescript
// State
const [page, setPage] = useState(1);
const PAGE_SIZE = 20;

// Fetch
const { data } = useQuery(['complaints', page, filters], () =>
  api.get(`/api/complaints/my-complaints`, {
    params: { page, pageSize: PAGE_SIZE, ...filters }
  })
);

// Pagination controls
const totalPages = Math.ceil(data?.totalCount / PAGE_SIZE);
```

---

## 5. analysis = null Handling

The AI analysis runs in a background job. On complaint creation, `analysis` will be `null` for the first few seconds.

```typescript
// Pattern for showing analysis or loading indicator
const AnalysisSection = ({ analysis }: { analysis: ComplaintAnalysis | null }) => {
  if (!analysis) return (
    <div className="analyzing">
      <Spinner size="sm" />
      <span>AI is analyzing this complaint...</span>
    </div>
  );
  return <SentimentCard analysis={analysis} />;
};
```

To auto-refresh after creation, poll `GET /my-complaints` every 5 seconds until `analysis !== null`.

---

## 6. Error Handling Reference

| HTTP Status | Meaning | UI Action |
|---|---|---|
| `400` | Validation error | Show field errors from `ModelState` |
| `401` | Expired token | Redirect to login |
| `403` | Wrong role / not your complaint | Show "Access denied" toast |
| `404` | Not found | Show "Item not found" inline |
| `500` | Server error | Show "Something went wrong. Try again." toast |

---

## 7. Real-Time Notifications (SignalR)

After a doctor/admin replies (single or cluster), affected students receive a SignalR push. The frontend should listen and update the complaint card automatically.

```typescript
// In useEffect after login
connection.on('ReceiveNotification', (notification) => {
  if (notification.type === 'complaint_resolved') {
    // Refetch my-complaints to show updated status + resolutionNote
    queryClient.invalidateQueries(['complaints']);
    showToast(`Your complaint "${notification.title}" has been answered.`);
  }
});
```

---

## 8. Loading and Empty States

| State | Text |
|---|---|
| Loading complaints list | Skeleton cards (3 placeholders) |
| Loading clusters | Skeleton table rows |
| Loading dashboard | Skeleton KPI cards + chart placeholders |
| Analyzing complaint | "AI is analyzing this complaint..." with spinner |
| Generating recommendations | "Generating recommendations..." |
| Empty complaints (student) | "You haven't submitted any complaints yet." |
| Empty complaints (admin) | "No complaints match your filters." |
| Empty clusters | "No complaint clusters found." |
| No recommendations | "No recommendations available yet." |
| No analysis yet | "Analysis in progress..." |

---

## 9. Permissions Matrix

| Feature | Student | Doctor | Admin | SuperAdmin |
|---|---|---|---|---|
| Submit complaint | ✅ | ❌ | ❌ | ✅ |
| View own complaints | ✅ | ❌ | ❌ | ✅ |
| View complaints about them | ❌ | ✅ (masked) | ❌ | ✅ |
| View all complaints | ❌ | ❌ | ✅ | ✅ |
| Reply to complaint | ❌ | ✅ (own) | ❌ | ✅ |
| View clusters | ❌ | ✅ (own) | ✅ | ✅ |
| Reply to cluster | ❌ | ✅ | ✅ | ✅ |
| Update cluster status | ❌ | ❌ | ✅ | ✅ |
| View dashboard | ❌ | ❌ | ✅ | ✅ |
