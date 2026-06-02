# 32 — QA Testing Checklist

## Auth
- [ ] Login with valid credentials → correct role dashboard
- [ ] Login with invalid credentials → error message
- [ ] Token expires → auto-refresh → continue session
- [ ] Refresh token expires → redirect to login
- [ ] Change password → old token invalidated

## Student
- [ ] Dashboard loads all widgets
- [ ] GPA card shows correct value
- [ ] Upcoming exams sorted by date
- [ ] Click exam → pre-exam screen
- [ ] Start exam → timer starts
- [ ] Answer MCQ → saved in state
- [ ] Auto-save fires every 30s
- [ ] Submit exam → result page shows score
- [ ] Submit assignment (file + text)
- [ ] View own submission after submit
- [ ] Attendance report shows per-subject %
- [ ] Academic roadmap shows correct semester status
- [ ] AI companion dashboard loads
- [ ] Generate flashcards → deck created
- [ ] Flip flashcard → shows answer
- [ ] Rate flashcard → updates next review date
- [ ] Start quiz session → questions appear
- [ ] Complete session → score + feedback shown

## Doctor
- [ ] Teaching dashboard loads all offerings
- [ ] Click offering → class analytics page
- [ ] Student table filterable by risk level
- [ ] Sort by risk score works
- [ ] Click student → individual profile
- [ ] Export Excel → file downloads with correct data
- [ ] Create exam (manual) → exam appears in list
- [ ] Generate AI exam → questions appear for review
- [ ] View exam results → table with scores
- [ ] Grade submission manually
- [ ] AI Grade submission → score + feedback
- [ ] Create attendance session → QR code shown
- [ ] Send notification to students
- [ ] Reply to complaint

## Admin
- [ ] Admin dashboard loads stats
- [ ] Create university → appears in structure
- [ ] Create college/department/batch/group
- [ ] Import students (Excel) → success message
- [ ] Student appears in list after import
- [ ] Reset student password → temp password shown
- [ ] Admin-enroll student in course
- [ ] View analytics charts

## AI Chat
- [ ] New conversation created
- [ ] Send message → AI responds
- [ ] Arabic message → Arabic response
- [ ] Typing indicator appears during AI response
- [ ] Suggestion chips work
- [ ] Conversation history loads on open
- [ ] Delete conversation

## Notifications
- [ ] Unread count shown in bell
- [ ] Click notification → marks read
- [ ] Mark all read works
- [ ] Real-time notification appears without refresh

## Role Restrictions
- [ ] Student cannot access /doctor/*
- [ ] Doctor cannot access /admin/*
- [ ] Student cannot see "Create Exam" button
- [ ] Doctor cannot submit complaint
- [ ] Non-SuperAdmin cannot access audit logs

## RTL/Arabic
- [ ] Switch to Arabic → layout flips to RTL
- [ ] Arabic text displays correctly
- [ ] Numbers format correctly in Arabic
- [ ] Dates format in Arabic locale

## Mobile
- [ ] Bottom nav works on mobile
- [ ] Flashcard swipe works
- [ ] QR scanner opens on mobile
- [ ] Chat keyboard handling correct
- [ ] Exam takes on mobile (touch MCQ selection)

## Error States
- [ ] Network error → appropriate toast
- [ ] 404 → not found page
- [ ] 403 → unauthorized toast/page
- [ ] API down → graceful degradation message
