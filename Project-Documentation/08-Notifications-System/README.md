# 🔔 Notifications System — Complete Guide

## Overview

The notification system is a **dual-channel architecture**:
1. **Database persistence** — notifications stored in `AppNotifications` table (permanent record)
2. **SignalR real-time push** — instant browser notification without refresh

This means notifications **always reach the user** even if they're offline (they'll see them when they login) AND if they're online they see them **instantly**.

---

## Architecture Diagram

```
An event happens (grade posted, exam scheduled, doctor sends message...)
        │
        ▼
[Service calls INotificationService.SendNotificationAsync()]
        │
        ├──────────────────────────────────────────────────────┐
        ▼                                                      ▼
[Save to AppNotifications table]              [IRealtimeNotifier.PushToUserAsync()]
(Permanent — user sees it when they login)           │
                                              [SignalRNotifier]
                                                      │
                                              [IHubContext<NotificationHub>]
                                                      │
                                              [Browser WebSocket]
                                                      │
                                           User sees toast notification
                                           INSTANTLY (no page refresh!)
```

---

## Notification Flow — Step by Step

### 1. Doctor Posts Assignment
```
Doctor submits POST /api/notification/send-to-my-students
  { title: "Assignment Due", message: "Submit by Friday" }
        │
        ▼
NotificationController resolves doctor's SystemUserId from JWT
        │
        ▼
INotificationService.SendToOfferingStudentsAsync():
  1. Resolve Doctor profile from DoctorSystemUserId
  2. Get all SubjectOfferings where DoctorId = this doctor
  3. Query Enrollments → get distinct Student.SystemUserIds
  4. Create AppNotification record for EACH student
  5. _context.SaveChangesAsync() — all saved in one batch
  6. Task.WhenAll() → push SignalR to each student simultaneously
        │
        ▼
Each student sees:
  - Bell icon count increments (+1)
  - Toast popup: "Assignment Due — Submit by Friday"
  - Clicking opens /materials deep link
```

---

## SignalR Hub Implementation

### How SignalR Groups Work
```
User connects: ws://backend/hubs/notifications?access_token=JWT
        │
        ▼
NotificationHub.OnConnectedAsync():
  userId = Context.UserIdentifier  // From JWT nameid claim
  Groups.AddToGroupAsync(ConnectionId, userId)
        │
        ▼
User is now in group "01HXYZ..." (their SystemUser.Id)
```

When we call:
```csharp
await _hub.Clients.Group(userId).SendAsync("ReceiveNotification", data);
```

ALL browser tabs/devices connected for that user receive the notification simultaneously.

### Frontend Integration
```javascript
import * as signalR from "@microsoft/signalr";

// 1. Create connection
const connection = new signalR.HubConnectionBuilder()
  .withUrl("https://your-backend.railway.app/hubs/notifications", {
    accessTokenFactory: () => localStorage.getItem("auth_token")
  })
  .withAutomaticReconnect([0, 2000, 10000, 30000])  // Retry delays
  .configureLogging(signalR.LogLevel.Warning)
  .build();

// 2. Register event handler BEFORE connecting
connection.on("ReceiveNotification", (notification) => {
  // Show toast
  showToast(notification.title, notification.message);
  
  // Update notification badge count
  setUnreadCount(prev => prev + 1);
  
  // Add to notifications list
  setNotifications(prev => [notification, ...prev]);
});

// 3. Start connection
await connection.start();

// 4. Handle disconnection
connection.onclose(async () => {
  await reconnect();
});
```

---

## Automated Notification Jobs

### Job 1: Academic Risk Alerts (Daily at Midnight)

**Trigger:** Hangfire `Cron.Daily`  
**Purpose:** Detect students in academic danger and notify them

```
Every day at midnight:
        │
        ▼
AcademicRiskJob.RunAsync():
  1. Query StudentGrades WHERE IsFinalized = true
  2. GROUP BY Student.SystemUserId
  3. HAVING AVG(GradePoints) < 2.0
  4. For each at-risk student:
     └── SendNotificationAsync(
           userId: student.SystemUserId,
           title: "تنبيه أكاديمي — انخفاض المعدل",
           message: "معدلك التراكمي 1.75 أقل من 2.0. لديك 2 مادة راسب. ننصحك بمراجعة مرشدك."
         )
```

**Student Experience:**
- Logs in next morning
- Sees notification: "Academic Alert — Your GPA has dropped"
- Clicks → goes to grades page
- Also received in real-time if online at midnight

---

### Job 2: Exam Reminders (Every 30 Minutes)

**Trigger:** `"*/30 * * * *"` cron  
**Purpose:** Never let a student miss an exam

```
Every 30 minutes:
        │
        ▼
ExamReminderJob.RunAsync():
  1. now = DateTime.UtcNow
  2. Query Exams WHERE:
     - Status = Published
     - StartTime > now
     - StartTime <= now + 24 hours
  3. For each upcoming exam:
     a. Get enrolled students' SystemUserIds
     b. Determine window: is exam < 2h away? → "ساعتين", else → "يوم"
     c. Send notification to each student:
        title: "تذكير بامتحان — Data Structures"
        message: "امتحان 'Data Structures' يبدأ خلال يوم. الوقت: 10:00 UTC"
```

**Coverage:**
- Student enrolled in 5 subjects → gets 5 reminders (one per exam)
- Reminders fire at: 24h before AND 2h before (two separate runs)
- Doctor doesn't need to do anything — fully automatic

---

### Job 3: Complaint Intelligence Reports (Daily/Weekly/Monthly)

**Trigger:** `Cron.Daily`, `Cron.Weekly`, `Cron.Monthly`  
**Purpose:** Aggregate complaint patterns for management

```
Every day:
  ComplaintIntelligenceJob.GenerateDailyReportAsync()
  → Fetches complaints from past 24h
  → AI clusters similar complaints
  → Sends admin notification with summary
  → Saves ComplaintCluster records

Every week:
  ComplaintIntelligenceJob.GenerateWeeklyReportAsync()
  → Broader trend analysis
  → Identifies recurring issues

Every month:
  ComplaintIntelligenceJob.GenerateMonthlyReportAsync()
  → Full monthly report
  → Department-level breakdown
```

---

## Notification Data Model

```csharp
public class AppNotification : BaseEntity
{
    public Ulid UserId { get; set; }      // Who receives it
    public string Title { get; set; }    // Short title (shown in badge)
    public string Message { get; set; }  // Full message
    public bool IsRead { get; set; }     // Read state
    public string? ActionUrl { get; set; }  // Deep link (e.g., "/exams", "/grades")
    public DateTime CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }  // Soft delete
}
```

---

## API Endpoints Summary

| Method | Endpoint | Auth | Description |
|--------|---------|------|-------------|
| GET | `/api/notification` | Any | Get my notifications |
| GET | `/api/notification?unreadOnly=true` | Any | Unread only |
| PUT | `/api/notification/{id}/read` | Any | Mark as read |
| POST | `/api/notification` | Admin | Send to user or broadcast |
| POST | `/api/notification/send-to-my-students` | Doctor | Doctor broadcasts to students |
| DELETE | `/api/notification/{id}` | Admin | Soft-delete notification |

---

## Frontend Notification UX Recommendations

### Notification Bell Component
```
State:
  - unreadCount: number (fetched on mount + updated via SignalR)
  - notifications: Notification[] (paginated list)
  - isOpen: boolean

On mount:
  1. GET /api/notification?unreadOnly=true → set unreadCount
  2. Connect SignalR → listen for "ReceiveNotification"

On bell click:
  1. Open dropdown
  2. GET /api/notification → load full list
  3. Render with "Mark all as read" option

On notification click:
  1. PUT /api/notification/{id}/read
  2. Navigate to notification.actionUrl
  3. Decrement unreadCount
```

### Toast Notification Component
```
On SignalR "ReceiveNotification" event:
  1. Show toast in top-right corner
  2. Auto-dismiss after 5 seconds
  3. Click on toast → navigate to actionUrl
  4. X button → dismiss without navigating
```

---

## Reliability Architecture

### Fire-and-Forget for SignalR
```csharp
// In NotificationService.SendNotificationAsync:
try 
{ 
    await _realtime.PushToUserAsync(userId.ToString(), title, message, actionUrl); 
}
catch 
{ 
    // SignalR failure NEVER breaks the DB write
    // User will see it when they refresh/login
    _logger.LogWarning("SignalR push failed for {UserId}", userId);
}
```

**Why this matters:** The database notification is the **source of truth**. SignalR is a UX enhancement. If the WebSocket fails (user offline, connection drop), they still get the notification from the database.

### Hangfire Retry Policy
```csharp
[AutomaticRetry(Attempts = 2)]  // AcademicRiskJob, ExamReminderJob
// If job fails, Hangfire retries up to 2 times with exponential backoff
```
