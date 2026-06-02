# 14 — Complaints Platform

## Overview
Students submit complaints about doctors, exams, grades, or other issues. Doctors can reply. Admins manage all complaints.

## APIs
- `POST /api/complaints` — submit (Student)
- `GET /api/complaints/my-complaints` — student's own (Student)
- `GET /api/complaints/my-reports` — complaints about doctor (Doctor)
- `GET /api/complaints/all` — all complaints (Admin)
- `GET /api/complaints/clusters` — complaint clusters (Admin, Doctor)
- `GET /api/complaints/doctor-options` — available doctors to complain about (Student)
- `PUT /api/complaints/{id}/reply` — doctor reply (Doctor)

## Complaint DTO
```typescript
interface ComplaintDto {
  id: string; studentId: string; title: string;
  message: string;
  targetType: 'Doctor' | 'Exam' | 'Grade' | 'Other';
  targetId?: string;
  status: 'Pending' | 'UnderReview' | 'Resolved' | 'Rejected';
  priority: 'Low' | 'Medium' | 'High';
  resolutionNote?: string; doctorReply?: string; createdAt: string;
}
```

## Submit Complaint Form
```
Title: [                        ]
Category: [Doctor ▾] [Exam] [Grade] [Other]
About Doctor: [Search doctor...  ▾]   (if Doctor selected)
Message:
[                                    ]
[                                    ]
                             [Submit Complaint]
```

## Complaint Status Colors
- Pending → Gray
- UnderReview → Amber
- Resolved → Green
- Rejected → Red

## Doctor Reply Flow
Doctor sees: `GET /api/complaints/my-reports`
Click complaint → view details → type reply → `PUT /api/complaints/{id}/reply`
