# ✨ Frontend Polish & WOW Factor

> This document is about making the project LOOK like a real production system.
> Small animations and smooth interactions make the biggest impression on reviewers.

---

## Core Principle

> A committee member who doesn't understand code WILL notice:
> - Smooth animations
> - Instant feedback
> - Professional layout
> - Clear status indicators

Make the UI feel alive.

---

## Registration Page Animations

### Card Entry Animation
When the registration page loads, animate cards in with a stagger:
```css
/* Each card enters with a slight upward slide + fade */
@keyframes slideUpFade {
  from { opacity: 0; transform: translateY(16px); }
  to   { opacity: 1; transform: translateY(0); }
}

.offering-card {
  animation: slideUpFade 0.3s ease-out;
  animation-fill-mode: both;
}

/* Stagger: card 1 = 0ms, card 2 = 50ms, card 3 = 100ms, etc. */
.offering-card:nth-child(1) { animation-delay: 0ms; }
.offering-card:nth-child(2) { animation-delay: 50ms; }
.offering-card:nth-child(3) { animation-delay: 100ms; }
```

### Capacity Bar Fill Animation
```css
.capacity-bar-fill {
  width: 0%;
  transition: width 0.8s ease-out;
}
/* On mount: set width to actual value → bar animates from 0 to real value */
```

### Credit Hours Counter Animation
When enrolled hours increase after enrollment:
```
Before: "9 / 18 hours"   →   After: "12 / 18 hours"
         ↑ animate number counting up from 9 to 12
```
Use a counting animation library or simple `requestAnimationFrame` counter.

### Enroll Button States
```css
/* Spin during API call */
.btn-loading .spinner {
  animation: spin 0.8s linear infinite;
}

/* Success pulse */
.btn-success {
  animation: successPulse 0.4s ease-out;
}
@keyframes successPulse {
  0%   { transform: scale(1); }
  50%  { transform: scale(1.05); }
  100% { transform: scale(1); }
}
```

---

## GPA Dashboard Animations

### GPA Number Count-Up
On page load, animate the GPA number from 0 to real value:
```javascript
function animateValue(element, start, end, duration) {
  const range = end - start;
  const startTime = performance.now();

  function update(currentTime) {
    const elapsed = currentTime - startTime;
    const progress = Math.min(elapsed / duration, 1);
    const eased = 1 - Math.pow(1 - progress, 3); // ease-out cubic
    element.textContent = (start + range * eased).toFixed(2);
    if (progress < 1) requestAnimationFrame(update);
  }
  requestAnimationFrame(update);
}
// Call: animateValue(gpaElement, 0, 3.42, 1200)
```

### GPA Donut/Circle Progress
```javascript
// SVG circle with stroke-dasharray animation
// 0 → (gpa/4.0 * circumference) over 1.2 seconds
const circumference = 2 * Math.PI * 54; // for r=54
circle.style.strokeDasharray = `0 ${circumference}`;
// on mount:
circle.style.transition = 'stroke-dasharray 1.2s ease-out';
circle.style.strokeDasharray = `${(gpa/4)*circumference} ${circumference}`;
```

### Credit Hours Progress Bar
```javascript
// Animate from 0% to earnedHours/totalRequired on mount
bar.style.transition = 'width 1s ease-out';
bar.style.width = `${(earnedHours / totalRequired) * 100}%`;
```

### Warning Banner Shake Animation
When a Warning banner appears:
```css
@keyframes shake {
  0%, 100% { transform: translateX(0); }
  20%       { transform: translateX(-6px); }
  40%       { transform: translateX(6px); }
  60%       { transform: translateX(-4px); }
  80%       { transform: translateX(4px); }
}
.warning-banner-appear {
  animation: shake 0.5s ease-out;
}
```

---

## Skeleton Loaders (while API fetches)

```html
<!-- Skeleton offering card -->
<div class="animate-pulse rounded-lg border p-4">
  <div class="h-5 bg-gray-200 rounded w-3/4 mb-2"></div>
  <div class="h-4 bg-gray-200 rounded w-1/2 mb-4"></div>
  <div class="h-3 bg-gray-200 rounded w-full mb-1"></div>
  <div class="h-3 bg-gray-200 rounded w-4/5 mb-3"></div>
  <div class="h-9 bg-gray-200 rounded w-32 ml-auto"></div>
</div>
```

Show 6 skeleton cards while `GET /api/registration/eligible-offerings` is loading.

---

## Toast Notifications

Use a toast library (e.g., `react-hot-toast`, `vue-toastification`, `sonner`).

### Placement: top-right

```javascript
// Success enrollment
toast.success("✅ Enrolled in Data Structures!", { duration: 4000 });

// Waitlist
toast("🕐 Added to waitlist for Algorithms — you are #4", {
  icon: "🕐",
  style: { background: "#fffbeb", border: "1px solid #f59e0b" },
  duration: 5000
});

// Blocked enrollment
toast.error("Cannot enroll: Prerequisite not completed: Operating Systems", {
  duration: 6000
});

// Import success
toast.success("✅ 142 students imported successfully!", { duration: 5000 });
```

---

## Smooth State Transitions on Cards

When a card changes state (e.g., "Eligible" → "Enrolled"), don't hard-reset:

```css
.offering-card {
  transition: border-color 0.3s ease, background-color 0.3s ease;
}
```

The border color and background smoothly transition from green to blue when enrolled.

---

## Mobile Responsiveness

### Registration Page
- Cards: full width on mobile, 2-column grid on tablet, 3-column on desktop.
- Credit Hours Banner: stack vertically on mobile.
- Blocker pills: wrap to multiple lines.

### GPA Dashboard
- Stat cards: 2×2 grid on mobile, 4 in a row on desktop.
- Progress bars: full width on all sizes.

---

## Loading States Checklist

| Action | Loading State |
|---|---|
| Page load (eligible offerings) | 6 skeleton cards |
| Clicking Enroll | Button spinner + disabled |
| Clicking Join Waitlist | Button spinner + disabled |
| GPA dashboard load | Skeleton number boxes |
| Import upload | Progress spinner + "Analyzing file..." |
| Import execute | Progress bar animation |

---

## Final Polish Items

| Feature | Effect |
|---|---|
| Capacity bar | Animate fill on card entry |
| GPA number | Count-up animation (0 → real value) |
| Standing badge | Fade in with scale(0.8) → scale(1) |
| Warning banner | Slide down from top + slight shake |
| Enrolled button | Green pulse on success |
| Waitlist button | Amber bounce on join |
| Card entry | Staggered slide-up-fade |
| Error pills | Fade in sequentially |
| Enrollment count update | Animated number counter |
| Toast notifications | Smooth slide in from right |

---

## Color Quick Reference

```
Green  (#22c55e) — Eligible, Success, Good GPA
Blue   (#3b82f6) — Already Enrolled
Amber  (#f59e0b) — Warning, Waitlist, Almost Full
Orange (#f97316) — Probation, Near Limit
Red    (#ef4444) — Blocked, Full, Error, Suspended
Gray   (#9ca3af) — Disabled, Expelled
Purple (#8b5cf6) — Honor Tier (GPA ≥ 3.5)
```
