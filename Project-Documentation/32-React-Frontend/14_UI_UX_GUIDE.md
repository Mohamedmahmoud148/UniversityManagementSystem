---
render_with_liquid: false
---

# 14 — UI/UX Guide
## Design System, Color Palette, Typography, Responsive Design, and Patterns

---

## 1. Design System Overview

The application uses a **dual-library design system**: Material UI (MUI) for interactive components and Tailwind CSS for layout and spacing. They work together without conflict because:
- MUI uses CSS-in-JS (Emotion) via `sx` prop and styled components
- Tailwind uses utility classes directly in `className`
- Both can coexist on the same element

```jsx
// MUI component with Tailwind layout classes
<Button
  variant="contained"
  className="w-full mt-4"   // Tailwind for sizing/spacing
  sx={{ bgcolor: '#0b2c4a' }}  // MUI sx for MUI-specific overrides
>
  Submit
</Button>
```

---

## 2. Color System

### Primary Brand Color

| Name | Hex | Usage |
|------|-----|-------|
| Primary Navy | `#0b2c4a` | Sidebars, headers, primary buttons |
| Primary Light | `#1a4a7a` | Hover states, secondary buttons |
| Primary Dark | `#061e33` | Active states, pressed |

### Status Colors (Semantic)

| Status | Color | Usage |
|--------|-------|-------|
| Success | `#4caf50` (green) | Passed quiz, attendance "present", success alerts |
| Warning | `#ff9800` (orange) | "Late" attendance, upcoming deadlines |
| Error | `#f44336` (red) | Failed, absent, error alerts |
| Info | `#2196f3` (blue) | Info alerts, loading indicators |
| Neutral | `#9e9e9e` (gray) | Disabled states, secondary text |

### Quiz Status Colors

| Status | Color |
|--------|-------|
| Available | Green `#4caf50` |
| Not Started Yet | Orange `#ff9800` |
| Submitted | Blue `#2196f3` |
| Expired | Red `#f44336` |

### Engagement Status Colors

| Status | Color | Indicator |
|--------|-------|-----------|
| Focused | Green | ● Focused |
| Distracted | Orange | ● Distracted |
| Away | Red | ● Away |
| Idle | Gray | ● Starting... |

---

## 3. Typography

The application uses MUI's default typography system (Roboto font) with the following variants:

| Variant | HTML | Size | Weight | Usage |
|---------|------|------|--------|-------|
| `h1` | `<h1>` | 2.125rem | 300 | Page titles |
| `h2` | `<h2>` | 1.5rem | 400 | Section headers |
| `h3` | `<h3>` | 1.25rem | 400 | Card titles |
| `h4` | `<h4>` | 1.125rem | 400 | Sub-section |
| `h5` | `<h5>` | 1rem | 400 | Component titles |
| `h6` | `<h6>` | 0.875rem | 500 | Small headers |
| `body1` | `<p>` | 1rem | 400 | Main body text |
| `body2` | `<p>` | 0.875rem | 400 | Secondary text |
| `caption` | `<span>` | 0.75rem | 400 | Timestamps, labels |
| `button` | auto | 0.875rem | 500 | Button text (automatic) |

**Arabic Text Support:** The application serves Arabic content (from AI responses, course names, etc.). MUI components support RTL direction, but the app does not globally set `direction: "rtl"` — individual components handle RTL text where needed.

---

## 4. Component Patterns

### 4.1 Card Pattern

Used for: courses, quizzes, buildings, assignments

```jsx
<Card sx={{ borderRadius: 2, boxShadow: 2, '&:hover': { boxShadow: 4 } }}>
  <CardContent>
    <Typography variant="h6">{title}</Typography>
    <Typography variant="body2" color="text.secondary">{subtitle}</Typography>
    <Divider sx={{ my: 1 }} />
    <div className="flex justify-between items-center mt-2">
      <Chip label={status} color={statusColor} size="small" />
      <IconButton onClick={handleEdit}><EditIcon /></IconButton>
    </div>
  </CardContent>
</Card>
```

### 4.2 Modal Pattern

All modals follow this structure:

```jsx
<Modal open={open} onClose={handleClose}>
  <Box sx={{
    position: 'absolute',
    top: '50%', left: '50%',
    transform: 'translate(-50%, -50%)',
    width: { xs: '90%', sm: 500 },  // responsive
    bgcolor: 'background.paper',
    borderRadius: 2,
    boxShadow: 24,
    p: 4,
    maxHeight: '90vh',
    overflow: 'auto'
  }}>
    <Typography variant="h6" gutterBottom>Modal Title</Typography>
    <IconButton
      onClick={handleClose}
      sx={{ position: 'absolute', top: 8, right: 8 }}
    >
      <CloseIcon />
    </IconButton>

    {/* Form fields */}
    <TextField ... />

    {/* Error display */}
    {error && <Alert severity="error">{error}</Alert>}

    {/* Actions */}
    <div className="flex justify-end gap-2 mt-4">
      <Button onClick={handleClose} disabled={loading}>Cancel</Button>
      <Button variant="contained" onClick={handleSubmit} disabled={loading}>
        {loading ? <CircularProgress size={20} /> : 'Save'}
      </Button>
    </div>
  </Box>
</Modal>
```

### 4.3 Data Table Pattern (MUI DataGrid)

```jsx
<DataGrid
  rows={data}
  columns={[
    { field: 'name', headerName: 'Name', flex: 1 },
    { field: 'email', headerName: 'Email', flex: 1 },
    {
      field: 'actions',
      headerName: '',
      width: 120,
      renderCell: (params) => (
        <div className="flex gap-1">
          <IconButton size="small" onClick={() => handleEdit(params.row)}>
            <EditIcon fontSize="small" />
          </IconButton>
          <IconButton size="small" color="error" onClick={() => handleDelete(params.row)}>
            <DeleteIcon fontSize="small" />
          </IconButton>
        </div>
      )
    }
  ]}
  pageSize={10}
  rowsPerPageOptions={[10, 25, 50]}
  disableSelectionOnClick
  autoHeight
  sx={{ border: 0 }}
/>
```

### 4.4 Loading State Pattern

```jsx
// Full-page loading
if (loading) return (
  <div className="flex justify-center items-center h-64">
    <CircularProgress sx={{ color: '#0b2c4a' }} />
  </div>
)

// In-button loading
<Button disabled={loading}>
  {loading ? <CircularProgress size={20} color="inherit" /> : 'Submit'}
</Button>

// Skeleton loading (for card grids)
{loading ? (
  Array.from({ length: 6 }).map((_, i) => (
    <Skeleton key={i} variant="rectangular" height={150} sx={{ borderRadius: 2 }} />
  ))
) : (
  courses.map(c => <CourseCard key={c.id} course={c} />)
)}
```

### 4.5 Empty State Pattern

```jsx
{!loading && items.length === 0 && (
  <div className="flex flex-col items-center justify-center py-16 text-gray-500">
    <InboxIcon sx={{ fontSize: 64, mb: 2, opacity: 0.3 }} />
    <Typography variant="h6">No items yet</Typography>
    <Typography variant="body2">Click "Add" to create your first item</Typography>
  </div>
)}
```

### 4.6 Error State Pattern

```jsx
{error && (
  <Alert
    severity="error"
    action={
      <Button color="inherit" size="small" onClick={reload}>
        Retry
      </Button>
    }
  >
    {error}
  </Alert>
)}
```

---

## 5. Responsive Design

The application uses a **mobile-first responsive** approach with breakpoints from MUI:

| Breakpoint | Size | Usage |
|-----------|------|-------|
| `xs` | 0-600px | Mobile phones |
| `sm` | 600-900px | Tablets portrait |
| `md` | 900-1200px | Tablets landscape, small laptops |
| `lg` | 1200-1536px | Standard desktops |
| `xl` | 1536px+ | Large monitors |

### Responsive Patterns Used

**Sidebar:** On mobile, sidebar is a `<Drawer>` (overlay). On desktop, it's always visible.
```jsx
<Drawer
  variant={isMobile ? 'temporary' : 'permanent'}
  open={sidebarOpen}
  onClose={() => setSidebarOpen(false)}
>
  <SidebarContent />
</Drawer>
```

**Grid Layout:**
```jsx
<Grid container spacing={2}>
  <Grid item xs={12} sm={6} md={4} lg={3}>
    <CourseCard />
  </Grid>
</Grid>
```

**Modal Width:**
```jsx
sx={{ width: { xs: '95%', sm: 500, md: 600 } }}
```

**Typography Sizing:**
```jsx
<Typography
  variant="h4"
  sx={{ fontSize: { xs: '1.25rem', md: '2.125rem' } }}
>
```

---

## 6. Navigation Design

### Sidebar Structure (Student Example)

```
┌─────────────────────────┐
│  [Avatar] Mohamed Ahmed │  ← Profile section
│  Computer Science       │  ← College
│  Year 2                 │  ← Year
├─────────────────────────┤
│  🏠 Home                │  ← Nav items
│  📚 My Courses          │
│  📝 Quizzes             │
├─────────────────────────┤
│  🚪 Sign Out            │  ← Bottom action
└─────────────────────────┘
```

### Topbar Structure

```
┌──────────────────────────────────────┐
│  ≡  [Page Title]        [Search] 👤 │
└──────────────────────────────────────┘
```

Active nav item highlighted with background color `#1a4a7a` (lighter navy).

---

## 7. Countdown Timer UI (Student Quiz)

The timer is one of the most critical UI elements — it directly affects student behavior.

```jsx
const minutes = Math.floor(timeRemaining / 60)
const seconds = timeRemaining % 60

const isWarning = timeRemaining <= 120  // < 2 minutes
const isDanger  = timeRemaining <= 60   // < 1 minute

<div className={`
  flex items-center gap-2 px-4 py-2 rounded-lg font-mono text-xl font-bold
  ${isDanger  ? 'bg-red-600 text-white animate-pulse' :
    isWarning ? 'bg-orange-500 text-white' :
                'bg-blue-600 text-white'}
`}>
  <TimerIcon />
  {String(minutes).padStart(2, '0')}:{String(seconds).padStart(2, '0')}
</div>
```

**Visual States:**
- Normal: Blue background
- Warning (< 2 min): Orange background
- Danger (< 1 min): Red background + pulse animation

---

## 8. Schedule Grid UI (Room Management)

The room schedule page displays a 6-column (days) × 4-row (time slots) grid:

```
         Sat    Sun    Mon    Tue    Wed    Thu
09-11   [CS101  ]  [    ]  [MATH]  [    ]  [    ]  [    ]
11-13   [    ]  [PHY ]  [    ]  [    ]  [CS102]  [    ]
13-15   [    ]  [    ]  [    ]  [CHEM]  [    ]  [    ]
15-17   [    ]  [    ]  [    ]  [    ]  [    ]  [DB  ]
```

Each cell is colored:
- Empty: Light gray (clickable to book)
- Booked: Primary navy with course code
- Hovered: Slightly darker

---

## 9. Accessibility Considerations

### What's Done
- MUI components include ARIA attributes by default
- All interactive elements have accessible labels
- Color contrast ratios meet WCAG AA for primary/secondary text
- Form inputs have associated labels (via MUI TextField's `label` prop)
- Icons inside IconButtons have Tooltip labels

### What Could Be Improved
- No `aria-live` regions for dynamic content updates (quiz timer, real-time data)
- No keyboard navigation shortcuts for the schedule grid
- No skip-to-content link for screen readers
- No reduced-motion preferences respected for pulse animations

---

## 10. Animation System

Animations are minimal by design — fast, purposeful, non-distracting:

| Animation | How | Duration |
|-----------|-----|---------|
| Modal appear | MUI default fade + scale | 200ms |
| Card hover shadow | CSS transition on `box-shadow` | 150ms |
| Sidebar slide | MUI Drawer slide | 225ms |
| Timer pulse (danger) | Tailwind `animate-pulse` | 2s loop |
| Page transition | None (instant) | — |
| Snackbar enter | MUI slide from bottom | 300ms |

**No page transition animations** — navigating between pages is instant. This is intentional for a productivity app where users navigate frequently.
