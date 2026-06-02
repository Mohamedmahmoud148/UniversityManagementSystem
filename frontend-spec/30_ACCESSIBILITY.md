# 30 — Accessibility

## WCAG 2.1 AA Compliance

### Color Contrast
- Text on background: minimum 4.5:1 ratio
- Large text (18px+): minimum 3:1
- Use CSS variable system — tested for contrast in both themes

### Keyboard Navigation
- All interactive elements reachable via Tab
- Focus visible at all times (ring-2 ring-primary)
- Modal: trap focus inside, Escape to close
- Exam: keyboard shortcuts for MCQ (1-4 keys)
- Chat: Ctrl+Enter to send

### ARIA Labels
```tsx
// Notification bell
<button aria-label={`Notifications, ${unreadCount} unread`}>

// Risk badge
<span role="status" aria-label={`Risk level: ${riskLevel}, score ${riskScore}`}>

// Chart
<div role="img" aria-label="Grade distribution chart: A 27%, B 40%, C 18%, D 11%, F 4%">

// Table sort
<th aria-sort="ascending">Name</th>
```

### Screen Reader Support
- All images have alt text
- Loading states announce: "Loading..." via aria-live
- Form errors linked to fields via aria-describedby
- Toast notifications use aria-live="polite"

### RTL Support
- All flex/grid directions use logical properties
- `dir="rtl"` on `<html>` for Arabic
- Text alignment: `text-start` (not left/right)
- Margins/paddings: use `ms-` / `me-` (not left/right)
- Icons flip appropriately with RTL context

### Focus Management
- After modal open: focus first interactive element
- After modal close: return focus to trigger button
- After route change: focus page title heading
- After form submit success: focus success message

### Reduced Motion
```css
@media (prefers-reduced-motion: reduce) {
  *, *::before, *::after {
    animation-duration: 0.01ms !important;
    animation-iteration-count: 1 !important;
    transition-duration: 0.01ms !important;
  }
}
```
