# Notifications System

> **Last refreshed:** 2026-05-31

---

## 1. Architecture

```
Create Notification
    │
NotificationService.SendNotificationAsync(userId, title, message, actionUrl?)
    │
    ├─ Step 1: INSERT AppNotification → PostgreSQL
    │          (guaranteed persistence even if messaging fails)
    │
    └─ Step 2: Publish NotificationCreatedEvent → RabbitMQ
                    │
             MassTransit Consumer (NotificationConsumer)
                    │
             IRealtimeNotifier.PushToUserAsync(userId, notification)
                    │
             SignalR Hub → user's connected browser group
                    │
             Graceful fallback: DB record survives SignalR failure
```

---

## 2. AppNotification Entity

```csharp
public class AppNotification : BaseEntity {
    public Ulid UserId { get; set; }       // FK to SystemUser
    public string Title { get; set; }
    public string Message { get; set; }
    public bool IsRead { get; set; }       // default false
    public string? ActionUrl { get; set; } // e.g. "/exams", "/assignments"
}
```

---

## 3. SignalR Hub

`NotificationHub.cs`:
- `OnConnectedAsync`: user joins group named by their `userId` claim
- `OnDisconnectedAsync`: user leaves group
- JWTs are validated on WebSocket upgrade — unauthenticated connections rejected

```
Frontend connects: ws://.../hubs/notifications?access_token=<jwt>
Server pushes: hub.Clients.Group(userId).SendAsync("ReceiveNotification", dto)
```

---

## 4. Automated Reminders (Hangfire Jobs)

### ExamReminderJob (every 30 minutes)

```
Find exams: Status=Published AND StartTime > now AND StartTime <= now+24h
For each exam:
  If StartTime <= now+2h  → window = "ساعتين"
  Else                    → window = "يوم"
  Fetch enrolled students → send notification to each SystemUserId
```

### AssignmentReminderJob (every 30 minutes)

```
Find assignments: Deadline > now AND Deadline <= now+24h
For each assignment:
  Fetch students who have NOT yet submitted → send reminder
  (Students who already submitted are excluded — no noise)
  If Deadline <= now+2h → urgent message
```

### AcademicRiskJob (06:00 UTC daily)

Calculates risk for all active students; sends alert notification to at-risk students with AI-generated recommendation.

---

## 5. Notification API

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/notification?unreadOnly=false` | Fetch notifications |
| PUT | `/api/notification/{id}/read` | Mark single as read |
| POST | `/api/notification` | Admin sends to userId(s) |
| DELETE | `/api/notification/{id}` | Admin deletes |
| POST | `/api/notification/send-to-my-students` | Doctor broadcasts to offering |

---

## 6. Doctor Broadcast

Doctors can send custom notifications to all students enrolled in a specific subject offering:

```
POST /api/notification/send-to-my-students
Body: { "subjectOfferingId": "...", "title": "...", "message": "..." }

→ Fetches all active enrollments for the offering
→ Sends notification to each student's SystemUserId
```

---

## 7. Frontend Integration (SignalR)

```javascript
// Connect
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/notifications", { accessTokenFactory: () => token })
  .build();

// Listen
connection.on("ReceiveNotification", (notification) => {
  // Show toast / update badge count
});

await connection.start();
```

---

## 8. Event Schema

```csharp
public class NotificationCreatedEvent {
    public Ulid NotificationId { get; set; }
    public Ulid UserId { get; set; }
    public string Title { get; set; }
    public string Message { get; set; }
    public string? ActionUrl { get; set; }
}
```
