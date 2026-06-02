# 20 — Design System

## Design Philosophy
Premium SaaS quality. References: Stripe (spacing, typography), Linear (speed, tables), Duolingo (gamification, streaks), Notion (structured content), GitHub (status indicators).

---

## Color Palette

### CSS Variables (globals.css)
```css
:root {
  /* Brand */
  --primary:        220 90% 56%;      /* #2563EB — Brilliant Blue */
  --primary-hover:  220 90% 48%;
  --primary-foreground: 0 0% 100%;
  
  /* Semantic Colors */
  --success:        142 76% 36%;      /* #16A34A */
  --warning:        38 92% 50%;       /* #F59E0B */
  --danger:         0 84% 60%;        /* #EF4444 */
  --info:           199 89% 48%;      /* #0EA5E9 */
  
  /* Risk Level Colors */
  --risk-low:       142 76% 36%;      /* Green */
  --risk-medium:    38 92% 50%;       /* Amber */
  --risk-high:      25 95% 53%;       /* Orange */
  --risk-critical:  0 84% 60%;        /* Red */
  
  /* AI Companion Brand */
  --ai-gradient-from: 220 90% 56%;    /* Blue */
  --ai-gradient-to:   270 70% 60%;    /* Purple */
  
  /* Teaching Intelligence Brand */
  --ti-gradient-from: 14 100% 57%;    /* Orange */
  --ti-gradient-to:   0 84% 60%;      /* Red */
  
  /* Neutral */
  --background:     0 0% 100%;
  --foreground:     222 47% 11%;
  --muted:          210 40% 96%;
  --muted-foreground: 215 16% 47%;
  --border:         214 32% 91%;
  --card:           0 0% 100%;
  --card-foreground: 222 47% 11%;
}

.dark {
  --background:     222 47% 11%;
  --foreground:     210 40% 98%;
  --muted:          217 32% 17%;
  --muted-foreground: 215 20% 65%;
  --border:         217 32% 17%;
  --card:           222 47% 13%;
}
```

---

## Typography

```css
/* fonts.css */
@import url('https://fonts.googleapis.com/css2?family=IBM+Plex+Sans+Arabic:wght@300;400;500;600;700&family=Inter:wght@400;500;600;700&display=swap');

:root {
  --font-arabic: 'IBM Plex Sans Arabic', sans-serif;
  --font-latin:  'Inter', sans-serif;
}

html[lang="ar"] {
  font-family: var(--font-arabic);
}

html[lang="en"] {
  font-family: var(--font-latin);
}
```

### Type Scale
```
h1: 2.25rem (36px) / font-semibold / tracking-tight
h2: 1.875rem (30px) / font-semibold
h3: 1.5rem (24px) / font-semibold
h4: 1.25rem (20px) / font-medium
h5: 1.125rem (18px) / font-medium
body-lg: 1.125rem (18px) / font-normal
body: 1rem (16px) / font-normal
body-sm: 0.875rem (14px) / font-normal
caption: 0.75rem (12px) / font-normal / text-muted-foreground
code: 0.875rem / font-mono / bg-muted rounded px-1
```

---

## Spacing System (Tailwind 4px base)

```
0 → 0px
1 → 4px
2 → 8px
3 → 12px
4 → 16px
5 → 20px
6 → 24px
8 → 32px
10 → 40px
12 → 48px
16 → 64px
20 → 80px
24 → 96px
```

### Page Layout Spacing
- Page padding: `px-4 py-6 md:px-8 md:py-8`
- Card gap: `gap-4 md:gap-6`
- Section margin: `mb-8`
- Form field gap: `gap-4`

---

## Layout Grid

```css
/* Desktop: 12-column grid with 24px gutters */
.container {
  max-width: 1440px;
  margin: 0 auto;
  padding: 0 32px;
}

/* Dashboard grid */
.dashboard-grid {
  display: grid;
  grid-template-columns: repeat(12, 1fr);
  gap: 24px;
}

/* Stat cards: 3 per row on desktop */
.stat-card { grid-column: span 4; }
/* Main content: 8 cols, sidebar: 4 cols */
.main-content { grid-column: span 8; }
.sidebar { grid-column: span 4; }
```

---

## Component Specifications

### Metric Card
```
┌──────────────────────────────────────┐
│  Icon  Title                         │
│        ─────────────────────────     │
│        Value (large, bold)           │
│        Subtitle / change indicator   │
└──────────────────────────────────────┘

Props:
- icon: LucideIcon
- title: string
- value: string | number
- subtitle?: string
- trend?: { value: number; direction: 'up' | 'down' | 'neutral' }
- variant: 'default' | 'success' | 'warning' | 'danger'
```

```tsx
// Usage
<MetricCard
  icon={Users}
  title="Total Students"
  value="2,450"
  subtitle="+12% from last month"
  trend={{ value: 12, direction: 'up' }}
  variant="success"
/>
```

### Risk Badge
```tsx
type RiskLevel = 'Low' | 'Medium' | 'High' | 'Critical';

const riskConfig = {
  Low:      { bg: 'bg-green-100',  text: 'text-green-800',  dot: 'bg-green-500' },
  Medium:   { bg: 'bg-amber-100',  text: 'text-amber-800',  dot: 'bg-amber-500' },
  High:     { bg: 'bg-orange-100', text: 'text-orange-800', dot: 'bg-orange-500' },
  Critical: { bg: 'bg-red-100',    text: 'text-red-800',    dot: 'bg-red-500' },
};

function RiskBadge({ level }: { level: RiskLevel }) {
  const config = riskConfig[level];
  return (
    <span className={`inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full text-xs font-medium ${config.bg} ${config.text}`}>
      <span className={`w-1.5 h-1.5 rounded-full ${config.dot}`} />
      {level}
    </span>
  );
}
```

### Progress Bar
```tsx
function ProgressBar({ value, max = 100, variant = 'default', showLabel = true }) {
  const percent = (value / max) * 100;
  const color = percent >= 75 ? 'bg-green-500' : percent >= 50 ? 'bg-amber-500' : 'bg-red-500';
  
  return (
    <div className="flex items-center gap-2">
      <div className="flex-1 bg-muted rounded-full h-2">
        <div className={`h-2 rounded-full transition-all ${color}`} style={{ width: `${percent}%` }} />
      </div>
      {showLabel && <span className="text-sm text-muted-foreground w-10">{percent.toFixed(0)}%</span>}
    </div>
  );
}
```

### AI Message Bubble
```tsx
function AiMessage({ content, suggestions }: { content: string; suggestions?: string[] }) {
  return (
    <div className="flex items-start gap-3">
      <div className="w-8 h-8 rounded-full bg-gradient-to-br from-blue-500 to-purple-600 flex items-center justify-center text-white text-xs font-bold flex-shrink-0">
        AI
      </div>
      <div className="flex flex-col gap-2 max-w-lg">
        <div className="bg-muted rounded-2xl rounded-tl-sm px-4 py-3 text-sm">
          <ReactMarkdown className="prose prose-sm dark:prose-invert max-w-none">
            {content}
          </ReactMarkdown>
        </div>
        {suggestions && (
          <div className="flex flex-wrap gap-2">
            {suggestions.map((s, i) => (
              <button key={i} className="px-3 py-1 border rounded-full text-xs hover:bg-muted transition-colors">
                {s}
              </button>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
```

---

## Streak Component (Duolingo-style)
```tsx
function StreakBadge({ days }: { days: number }) {
  return (
    <div className="flex items-center gap-1.5 px-3 py-1.5 bg-amber-50 border border-amber-200 rounded-full">
      <span className="text-orange-500 text-base">🔥</span>
      <span className="font-bold text-amber-700">{days}</span>
      <span className="text-amber-600 text-sm">day streak</span>
    </div>
  );
}
```

---

## Data Table Component

```tsx
interface Column<T> {
  key: keyof T;
  header: string;
  sortable?: boolean;
  render?: (value: any, row: T) => React.ReactNode;
}

function DataTable<T>({ data, columns, onRowClick, isLoading, emptyMessage }) {
  // Sorting state
  const [sort, setSort] = useState<{ key: string; dir: 'asc' | 'desc' }>();
  
  return (
    <div className="border rounded-lg overflow-hidden">
      <table className="w-full">
        <thead className="bg-muted/50">
          <tr>
            {columns.map(col => (
              <th className="px-4 py-3 text-start text-sm font-medium text-muted-foreground">
                {col.header}
                {col.sortable && <SortIcon />}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {isLoading ? <TableSkeleton rows={5} cols={columns.length} /> :
           data.length === 0 ? <EmptyRow message={emptyMessage} /> :
           data.map((row, i) => (
            <tr key={i} className="border-t hover:bg-muted/30 cursor-pointer transition-colors" onClick={() => onRowClick?.(row)}>
              {columns.map(col => (
                <td className="px-4 py-3 text-sm">
                  {col.render ? col.render(row[col.key], row) : String(row[col.key])}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
```

---

## Charts Specifications

All charts use **Recharts**. Standard config:

```tsx
const chartTheme = {
  colors: {
    primary: '#2563EB',
    success: '#16A34A',
    warning: '#F59E0B',
    danger: '#EF4444',
    muted: '#94A3B8',
  },
  tooltip: {
    contentStyle: {
      background: 'white',
      border: '1px solid #E2E8F0',
      borderRadius: '8px',
      boxShadow: '0 4px 6px -1px rgba(0,0,0,0.1)',
    }
  }
};
```

### Grade Distribution (Donut)
```tsx
<PieChart width={280} height={280}>
  <Pie data={gradeData} innerRadius={70} outerRadius={110} paddingAngle={3}>
    {gradeData.map((entry) => <Cell key={entry.name} fill={entry.color} />)}
  </Pie>
  <Tooltip />
  <Legend />
</PieChart>
```

### Performance Trend (Line)
```tsx
<LineChart data={trendData}>
  <CartesianGrid strokeDasharray="3 3" stroke="#E2E8F0" />
  <XAxis dataKey="label" />
  <YAxis domain={[0, 100]} />
  <Line type="monotone" dataKey="avg" stroke="#2563EB" strokeWidth={2} dot={{ r: 4 }} />
  <Line type="monotone" dataKey="passRate" stroke="#16A34A" strokeWidth={2} strokeDasharray="5 5" />
  <Tooltip />
</LineChart>
```

### Attendance Bar Chart
```tsx
<BarChart data={attendanceData}>
  <Bar dataKey="attended" fill="#2563EB" radius={[4, 4, 0, 0]} />
  <Bar dataKey="total" fill="#E2E8F0" radius={[4, 4, 0, 0]} />
</BarChart>
```

---

## Shadcn/UI Component Customizations

```tsx
// Custom Button variants
const buttonVariants = cva(
  "inline-flex items-center justify-center rounded-lg font-medium transition-all focus-visible:outline-none disabled:opacity-50",
  {
    variants: {
      variant: {
        default: "bg-primary text-primary-foreground hover:bg-primary/90 shadow-sm",
        outline: "border border-input bg-background hover:bg-muted",
        ghost: "hover:bg-muted",
        danger: "bg-red-500 text-white hover:bg-red-600",
        ai: "bg-gradient-to-r from-blue-500 to-purple-600 text-white hover:opacity-90",
      },
      size: {
        sm: "h-8 px-3 text-xs",
        default: "h-10 px-4 text-sm",
        lg: "h-12 px-6 text-base",
      },
    },
  }
);
```

---

## Responsive Breakpoints

```
xs: 375px   (iPhone SE)
sm: 640px   (mobile landscape)
md: 768px   (tablet portrait)  
lg: 1024px  (tablet landscape / small laptop)
xl: 1280px  (desktop)
2xl: 1536px (wide desktop)
```

### Sidebar Behavior
- `< md`: Drawer (slides in from side)
- `md – lg`: Collapsible (icon-only mode)
- `>= lg`: Always visible (full sidebar)

---

## Dark Mode

```tsx
// ThemeProvider using next-themes or similar
<ThemeProvider attribute="class" defaultTheme="light" enableSystem>
  <App />
</ThemeProvider>
```

Toggle in settings: Light / Dark / System

---

## Icon Conventions

All icons from **Lucide React**:
- Danger/Delete: `Trash2`, `AlertTriangle`
- Edit: `Pencil`, `Edit2`
- View: `Eye`, `ExternalLink`
- AI: `Sparkles`, `Bot`, `Brain`
- Analytics: `BarChart3`, `TrendingUp`, `TrendingDown`
- Student: `GraduationCap`, `BookOpen`
- Doctor: `Stethoscope`, `Users`
- Admin: `Shield`, `Settings`
- Risk: `AlertOctagon`, `ShieldAlert`
- Success: `CheckCircle`, `ThumbsUp`
- Streak: Use emoji `🔥` (not Lucide)
