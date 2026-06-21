# Frontend UX Guide — Complaint Intelligence Platform

> **Date:** 2026-06-21 | **Version:** 2.0

---

## STUDENT EXPERIENCE

### My Complaints Page

**Route:** `/student/complaints`

**Page Layout:**
```
┌─────────────────────────────────────────────────────┐
│  My Complaints                    [+ New Complaint]  │
│  ─────────────────────────────────────────────────  │
│  Filter: [Status ▼] [Date Range] [Category ▼]       │
│                                                       │
│  ┌─────────────────────────────────────────────┐    │
│  │ 🔴 Unfair Quiz Grade          [High] [Grading]   │
│  │ Submitted: Jun 15, 2026       Status: [Resolved] │
│  │ ────────────────────────────────────────────     │
│  │ 😞 Sentiment: Negative  "Quiz was graded..."     │
│  │ [View Details]                                   │
│  └─────────────────────────────────────────────┘    │
│                                                       │
│  ┌─────────────────────────────────────────────┐    │
│  │ 📋 Attendance Marking Issue   [Normal] [Attendance]│
│  │ Submitted: Jun 10, 2026       Status: [Pending]  │
│  │ ────────────────────────────────────────────     │
│  │ ⏳ AI is analyzing this complaint...            │
│  └─────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────┘
```

---

#### Complaint Card Design

```
┌──────────────────────────────────────────────────────┐
│  [Priority Badge]  Complaint Title         [Status]  │
│  Submitted: Jan 15, 2026        Category: [Badge]    │
│  ──────────────────────────────────────────────────  │
│                                                        │
│  AI Analysis:                                          │
│  😞 Sentiment: ████████░░ Negative (-0.78)            │
│  Summary: "Student disputes midterm grade..."         │
│  Suggested: "Visit office hours to review paper"      │
│                                                        │
│  ──────────────────────────────────────────────────  │
│  Timeline:                                             │
│  ● Submitted ──── ● Analyzing ──── ● Resolved        │
│    Jun 15             Jun 15          Jun 18          │
│                                                        │
│  Resolution Note:                                      │
│  "I reviewed your paper. Grade is correct."           │
│                                                        │
│                              [View Details] [Close]  │
└──────────────────────────────────────────────────────┘
```

---

#### New Complaint Form

**Route:** `/student/complaints/new` or modal

```
┌──────────────────────────────────────────────────────┐
│  Submit a Complaint                                   │
│                                                        │
│  Title *                                              │
│  [________________________________________]           │
│                                                        │
│  Who is this complaint about? *                        │
│  [Doctor ▼]  → shows doctor dropdown                  │
│  [Subject ▼] → shows subject dropdown                 │
│  [Administration]                                      │
│  [Technical Issue]                                     │
│                                                        │
│  Select Doctor *  (if Doctor selected)                │
│  [Dr. Hassan Mohamed — Data Structures    ▼]          │
│                                                        │
│  Describe your issue *                                 │
│  [                                        ]           │
│  [                        ] 0 / 2000 chars            │
│                                                        │
│  ────────────────────────────────────────────         │
│              [Cancel]   [Submit Complaint]            │
└──────────────────────────────────────────────────────┘
```

**Post-submit:** Show "Complaint submitted. AI analysis will be ready shortly." toast. Navigate back to list.

---

#### Status Colors & Badges

```typescript
const STATUS_CONFIG = {
  Pending:     { color: '#F59E0B', bg: '#FEF3C7', label: 'Pending',      icon: '🕐' },
  UnderReview: { color: '#3B82F6', bg: '#EFF6FF', label: 'Under Review',  icon: '🔍' },
  Resolved:    { color: '#10B981', bg: '#D1FAE5', label: 'Resolved',      icon: '✅' },
  Dismissed:   { color: '#6B7280', bg: '#F3F4F6', label: 'Dismissed',     icon: '❌' },
};

const PRIORITY_CONFIG = {
  Normal:   { color: '#6B7280', label: 'Normal'   },
  High:     { color: '#F97316', label: '⚠ High'   },
  Critical: { color: '#EF4444', label: '🚨 Critical' },
};
```

---

#### Sentiment Indicator Component

{% raw %}
```typescript
const SentimentBar = ({ score }: { score: number }) => {
  const pct = ((score + 1) / 2) * 100; // Convert -1..1 to 0%..100%
  const color = score > 0.3 ? '#10B981' : score > -0.3 ? '#F59E0B' : '#EF4444';
  const label = score > 0.3 ? '😊 Positive' : score > -0.3 ? '😐 Neutral' : '😞 Negative';

  return (
    <div>
      <span>{label}</span>
      <div style={{ background: '#E5E7EB', borderRadius: 4, height: 8, width: '100%' }}>
        <div style={{ width: `${pct}%`, background: color, height: '100%', borderRadius: 4 }} />
      </div>
      <span>{score.toFixed(2)}</span>
    </div>
  );
};
```
{% endraw %}

---

## DOCTOR EXPERIENCE

### Doctor Complaints Dashboard

**Route:** `/doctor/complaints`

**Layout:**
```
┌──────────────────────────────────────────────────────┐
│  Complaint Management                                 │
│                                                        │
│  ┌────────┐  ┌─────────┐  ┌─────────┐  ┌─────────┐  │
│  │ 12     │  │  3      │  │  8      │  │  2      │  │
│  │ Total  │  │ Critical│  │ Resolved│  │Trending │  │
│  └────────┘  └─────────┘  └─────────┘  └─────────┘  │
│                                                        │
│  TRENDING TOPICS                                      │
│  ┌──────────────────────────────────────────────┐    │
│  │ 📈 Grading — 47 complaints    [Investigating]│    │
│  │ "Multiple students report unfair quiz..."    │    │
│  │ Recommendations: Review rubric | Office hrs  │    │
│  │                [View Cluster] [Reply All]    │    │
│  └──────────────────────────────────────────────┘    │
│                                                        │
│  INDIVIDUAL COMPLAINTS                                │
│  [Status ▼] [Priority ▼] [Date ▼]                   │
│  ┌──────────────────────────────────────────────┐    │
│  │ 🔴 Unfair quiz   [High] [Grading]            │    │
│  │ Jun 15 · 😞 -0.85 · "Student disputes..."   │    │
│  │ Suggested: "Review paper with student"       │    │
│  │          [Reply] [Mark Investigating]        │    │
│  └──────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────┘
```

---

#### Reply Modal (Doctor → Single Complaint)

```
┌──────────────────────────────────────────────────────┐
│  Reply to Complaint                              [×]  │
│  ────────────────────────────────────────────────    │
│  Complaint: "Unfair quiz grade"                       │
│  Student: Anonymous (privacy protected)               │
│  AI Summary: Student disputes midterm grade...        │
│  AI Suggests: Review exam paper with student          │
│                                                        │
│  Your Reply *                                          │
│  [                                             ]      │
│  [                             ] 0 / 2000 chars       │
│                                                        │
│              [Cancel]         [Send Reply]            │
└──────────────────────────────────────────────────────┘
```

---

#### Cluster Detail Modal (Doctor → Cluster Reply)

```
┌──────────────────────────────────────────────────────┐
│  Cluster: Grading Issues                         [×]  │
│  Status: [Investigating 🔵]  Trend: [📈 Increasing]  │
│  ────────────────────────────────────────────────    │
│  47 complaints   |   5 critical   |   😞 -0.72 avg   │
│                                                        │
│  AI Summary:                                           │
│  "47 students reported unfair grading..."             │
│                                                        │
│  AI Recommendations:                                   │
│  📋 Review quiz grading rubric with all TAs           │
│  📋 Schedule open office hours for affected students  │
│  📋 Audit last 3 quiz correction batches              │
│                                                        │
│  ──── Reply to All Students ────────────────────     │
│  Message *                                             │
│  [                                             ]      │
│  Sends to 47 students individually.                   │
│                                                        │
│  Status History:                                       │
│  Open → Investigating (Jun 19) "Multiple critical"    │
│                                                        │
│  Previous Replies:                                     │
│  Jun 20 · "We are looking into this" → 47 students    │
│                                                        │
│        [Cancel]    [Send Reply to All 47 Students]   │
└──────────────────────────────────────────────────────┘
```

---

## ADMIN EXPERIENCE

### Admin Complaint Intelligence Dashboard

**Route:** `/admin/complaints`

**Layout:**
```
┌──────────────────────────────────────────────────────────────────┐
│  Complaint Intelligence Dashboard          [Export] [Refresh]    │
│                                                                    │
│  ┌────────┐ ┌────────┐ ┌────────┐ ┌────────┐ ┌──────────────┐  │
│  │  541   │ │   67   │ │   8    │ │  447   │ │    4.2 hrs   │  │
│  │ Total  │ │Pending │ │Critical│ │Resolved│ │ Avg Resolve  │  │
│  └────────┘ └────────┘ └────────┘ └────────┘ └──────────────┘  │
│                                                                    │
│  ┌─────────────────────────┐  ┌──────────────────────────────┐  │
│  │ Complaints by Category  │  │   Complaints Over Time       │  │
│  │ ████ Grading       154  │  │   /\    /\                   │  │
│  │ ███  Attendance     97  │  │  /  \  /  \                  │  │
│  │ ██   Technical      62  │  │ /    \/    \___              │  │
│  │ █    Other          28  │  │ Jun 1           Jun 21       │  │
│  └─────────────────────────┘  └──────────────────────────────┘  │
│                                                                    │
│  ┌─────────────────────────┐  ┌──────────────────────────────┐  │
│  │  Severity Distribution  │  │   Sentiment Distribution     │  │
│  │ ● Low      201 (37%)    │  │   😞 Negative  62%           │  │
│  │ ● Medium   189 (35%)    │  │   😐 Neutral   25%           │  │
│  │ ● High     123 (23%)    │  │   😊 Positive  13%           │  │
│  │ 🔴 Critical  28 (5%)   │  │   Avg: -0.38                 │  │
│  └─────────────────────────┘  └──────────────────────────────┘  │
│                                                                    │
│  TOP CLUSTERS                                                      │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │ Topic       │ Count │ Trend    │ Status       │ Actions    │  │
│  │ Grading     │  47   │ 📈 +12% │ Investigating│ [View][Reply]│ │
│  │ Attendance  │  23   │ ➡ Stable│ Open         │ [View][Reply]│ │
│  │ Technical   │  11   │ 📉 -5%  │ Resolved     │ [View]      │ │
│  └────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────┘
```

---

#### Cluster Status Dropdown

```typescript
const CLUSTER_STATUS_CONFIG = {
  Open:          { color: '#F59E0B', bg: '#FEF3C7', icon: '🔵', label: 'Open'          },
  Investigating: { color: '#3B82F6', bg: '#EFF6FF', icon: '🔍', label: 'Investigating' },
  Resolved:      { color: '#10B981', bg: '#D1FAE5', icon: '✅', label: 'Resolved'      },
  Archived:      { color: '#6B7280', bg: '#F3F4F6', icon: '📦', label: 'Archived'      },
};

const TREND_CONFIG = {
  Increasing:  { color: '#EF4444', icon: '📈', label: 'Increasing'  },
  Stable:      { color: '#3B82F6', icon: '➡',  label: 'Stable'      },
  Decreasing:  { color: '#10B981', icon: '📉', label: 'Decreasing'  },
};
```

---

#### Bulk Reply Confirmation Dialog

Before sending a cluster reply, show a confirmation:

```
┌──────────────────────────────────────────────────┐
│  ⚠ Confirm Bulk Reply                        [×] │
│  ─────────────────────────────────────────────   │
│  You are about to send a reply to:               │
│                                                    │
│       47 students                                 │
│  in cluster: "Grading Issues"                     │
│                                                    │
│  Each student will receive an individual          │
│  notification with your message.                  │
│                                                    │
│  All complaints in this cluster will be           │
│  marked as Resolved.                              │
│                                                    │
│         [Cancel]    [Yes, Send to 47 Students]   │
└──────────────────────────────────────────────────┘
```

---

## UI REQUIREMENTS

### Colors Reference

```css
/* Status */
--status-pending:     #F59E0B;
--status-under-review:#3B82F6;
--status-resolved:    #10B981;
--status-dismissed:   #6B7280;

/* Priority */
--priority-normal:    #6B7280;
--priority-high:      #F97316;
--priority-critical:  #EF4444;

/* Trend */
--trend-increasing:   #EF4444;
--trend-stable:       #3B82F6;
--trend-decreasing:   #10B981;

/* Sentiment */
--sentiment-positive: #10B981;
--sentiment-neutral:  #F59E0B;
--sentiment-negative: #EF4444;
```

---

### Skeleton Loading Components

```typescript
// Complaint Card Skeleton
const ComplaintCardSkeleton = () => (
  <div className="skeleton-card">
    <div className="skeleton-line w-3/4 h-5" />
    <div className="skeleton-line w-1/4 h-4 mt-2" />
    <div className="skeleton-line w-full h-4 mt-4" />
    <div className="skeleton-line w-2/3 h-4 mt-2" />
  </div>
);

// Show 3 skeletons while loading
const LoadingState = () => (
  <div>
    {[1, 2, 3].map(i => <ComplaintCardSkeleton key={i} />)}
  </div>
);
```

---

### Optimistic Updates

For status changes and replies, update the UI immediately before the API responds:

```typescript
// Optimistic update pattern
const handleReply = async (complaintId: string, reply: string) => {
  // 1. Update state immediately
  setComplaints(prev => prev.map(c =>
    c.id === complaintId
      ? { ...c, status: 'Resolved', resolutionNote: reply }
      : c
  ));

  // 2. Send to API
  try {
    await api.put(`/api/complaints/${complaintId}/reply`, { reply });
  } catch (error) {
    // 3. Rollback on failure
    setComplaints(prev => prev.map(c =>
      c.id === complaintId
        ? { ...c, status: 'Pending', resolutionNote: null }
        : c
    ));
    showErrorToast('Failed to send reply. Please try again.');
  }
};
```

---

### Accessibility

- All status badges have `aria-label` with full text
- Sentiment bar has `role="progressbar"` with `aria-valuenow`
- Reply forms have proper `aria-required` and `aria-describedby`
- Keyboard navigation: Tab through complaint cards, Enter to expand
- Mobile: Cards stack vertically, full-width reply textarea
- Minimum touch target size: 44×44px for all buttons
- Color is never the only indicator (always paired with icon/text)

---

### Mobile Responsive

```
Mobile (< 768px):
  - KPI cards: 2×2 grid
  - Charts: hidden or collapsible
  - Cluster table: card view instead of table
  - Reply button: full width at bottom of card
  - Bulk reply: full-screen modal

Tablet (768–1024px):
  - KPI cards: 4×1 row
  - Charts: 2-column
  - Table: horizontal scroll

Desktop (> 1024px):
  - Full layout as designed above
```
