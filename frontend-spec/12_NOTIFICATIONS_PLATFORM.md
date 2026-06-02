# 12 — Notifications Platform

## Overview
Two notification systems: REST polling and real-time SignalR push.

## API
- `GET /api/notification` — get all notifications
- `PUT /api/notification/{id}/read` — mark one read
- `POST /api/notification` (Admin) — broadcast
- `POST /api/notification/send-to-my-students` (Doctor) — send to own students
- `DELETE /api/notification/{id}` (Admin) — delete

## Real-time (SignalR)
Hub: `/hubs/notifications`
Event: `ReceiveNotification` → `NotificationDto`

## Notification Bell Component
```tsx
function NotificationBell() {
  const { unreadCount } = useNotificationStore();
  return (
    <Popover>
      <PopoverTrigger>
        <Button variant="ghost" size="icon" className="relative">
          <Bell className="w-5 h-5" />
          {unreadCount > 0 && (
            <span className="absolute -top-1 -right-1 w-5 h-5 bg-red-500 text-white text-xs rounded-full flex items-center justify-center">
              {unreadCount > 99 ? '99+' : unreadCount}
            </span>
          )}
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-80 p-0">
        <NotificationList />
      </PopoverContent>
    </Popover>
  );
}
```

## Notification Types (Inferred from InsightType)

| Source | Type | Visual |
|--------|------|--------|
| AI Background Service | InactivityAlert | 😴 Amber |
| AI Background Service | ExamApproaching | ⚡ Red |
| AI Background Service | AssignmentDeadline | 📌 Orange |
| AI Background Service | StreakMilestone | 🔥 Gold |
| AI Background Service | WeeklyReport | 📊 Blue |
| AI Background Service | RiskAlert | 🚨 Red |
| Teaching Intelligence | ClassPerformanceAlert | 📈 Doctor |
| Manual (Admin/Doctor) | General | 🔔 Default |

## Polling Strategy
```typescript
useQuery({
  queryKey: ['notifications'],
  queryFn: notificationApi.getAll,
  refetchInterval: 60_000, // Every 60 seconds
  staleTime: 30_000,
});
```
