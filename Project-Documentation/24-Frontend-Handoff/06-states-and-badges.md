# 🏷️ States, Badges & Error Messages — Complete Reference

---

## Academic Standing States

| API Value | Display Label | Badge Style | Icon |
|---|---|---|---|
| `"Good"` | Good Standing | `bg-green-100 text-green-800 border-green-300` | ✅ |
| `"Warning"` | Academic Warning | `bg-yellow-100 text-yellow-800 border-yellow-400` | ⚠️ |
| `"Probation"` | Academic Probation | `bg-orange-100 text-orange-800 border-orange-400` | 🔶 |
| `"Suspended"` | Suspended | `bg-red-100 text-red-800 border-red-500` | 🚫 |
| `"Graduated"` | Graduated | `bg-blue-100 text-blue-800 border-blue-300` | 🎓 |
| `"Expelled"` | Expelled | `bg-gray-100 text-gray-600 border-gray-400` | ❌ |

---

## Offering Eligibility States

| Condition | Card Style | Button | Left Border |
|---|---|---|---|
| `isEligible: true, !isFull` | Normal white | Green "Enroll →" | `border-l-4 border-green-500` |
| `isEligible: true, isFull` | Amber tint | Amber "Join Waitlist" | `border-l-4 border-amber-400` |
| `isEligible: false` | Red tint | Gray disabled "Cannot Enroll" | `border-l-4 border-red-400` |
| Already enrolled | Blue tint | Blue static "✓ Enrolled" | `border-l-4 border-blue-400` |
| On waitlist | Amber tint | Amber "🕐 Waitlist #N" | `border-l-4 border-amber-400` |
| `warnings.length > 0` | Yellow tint | Green "Enroll →" (still enabled) | `border-l-4 border-yellow-400` |

---

## Enrollment Result States

| API Response | Toast Type | Toast Message |
|---|---|---|
| `success: true` | `✅ Success (green)` | "Enrolled in {subjectName} successfully!" |
| `addedToWaitlist: true` | `🕐 Info (amber)` | "Added to waitlist for {subjectName} — position #N" |
| `HTTP 409 + errors[0]` | `❌ Error (red)` | Show `errors[0]` verbatim |
| Network error | `⚠️ Warning (yellow)` | "Connection error. Please try again." |

---

## Blocker Pill Styles

Each blocker message in the card:
```html
<span class="inline-flex items-center px-2 py-1 rounded-full text-xs 
             bg-red-100 text-red-700 border border-red-200 mt-1">
  ❌ {blocker message}
</span>
```

Warning pills:
```html
<span class="inline-flex items-center px-2 py-1 rounded-full text-xs 
             bg-yellow-50 text-yellow-700 border border-yellow-200 mt-1">
  ⚠️ {warning message}
</span>
```

---

## GPA Value Colors

| GPA Range | Color | Tailwind Class |
|---|---|---|
| 3.5 – 4.0 | Green | `text-green-600` |
| 3.0 – 3.49 | Teal | `text-teal-600` |
| 2.5 – 2.99 | Blue | `text-blue-600` |
| 2.0 – 2.49 | Yellow | `text-yellow-600` |
| 1.5 – 1.99 | Orange | `text-orange-600` |
| 0.0 – 1.49 | Red | `text-red-600` |

---

## Capacity Progress Bar Colors

| Fill % | Bar Color | Tailwind Class |
|---|---|---|
| 0–59% | Green | `bg-green-500` |
| 60–84% | Amber | `bg-yellow-500` |
| 85–99% | Orange | `bg-orange-500` |
| 100% | Red | `bg-red-600` |

---

## All Possible Blocker Messages (show exactly as-is)

```
"You are already enrolled in this offering."
"You have already passed this subject."
"Prerequisite not completed: {SubjectName}"
"Must PASS prerequisite: {SubjectName} (currently: {Grade})"
"Minimum score {N} required in {SubjectName} (scored {X.X})"
"Credit hours limit exceeded: {current} + {adding} = {total} > max {max} (GPA: {gpa})"
"Your account is suspended. Contact academic affairs."
"Offering not found in eligible list."
"Already enrolled."
```

---

## All Possible Warning Messages (show in yellow)

```
"⚠️ Academic Warning: GPA {X.XX}. Max {N} hours."
"⚠️ Probation: GPA {X.XX} < {threshold}. Max {N} hours."
"Offering almost full ({enrolled}/{max})"
"Offering is full ({enrolled}/{max}). You can join the waitlist (position {N})."
```

---

## Import Row Error Severity

| `severity` field | Color | Meaning |
|---|---|---|
| `"Error"` | Red — row is skipped | Fatal, row not imported |
| `"Warning"` | Amber — row proceeds | Non-fatal, value adjusted |
| `"Info"` | Blue — informational | Auto-generated value used |

---

## HTTP Error Status Codes

| Code | When | Frontend Action |
|---|---|---|
| `200` | Success | Process `data` field |
| `400` | Bad request (invalid ID, missing param) | Show `message` as error |
| `401` | Not authenticated | Redirect to login |
| `403` | Wrong role | Show "Access denied" |
| `404` | Entity not found | Show "Not found" |
| `409` | Business logic conflict (blocked enrollment) | Show `errors[]` in red |
| `500` | Server error | Show "Server error, try again" |
