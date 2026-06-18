# 04 — Technology Stack Documentation
## Every Library Explained — Why, How, and What Would Break Without It

---

## 1. React 18

**Package:** `react@18`, `react-dom@18`

### What It Is
React is a JavaScript library for building user interfaces. It uses a component model where UI is broken into reusable, isolated pieces. React 18 introduced Concurrent Mode and the new root API.

### Why Chosen
- Industry standard for SPAs (Single Page Applications)
- Massive ecosystem of compatible libraries
- Component model maps perfectly to the role-based UI needed (Student component ≠ Professor component)
- Hooks API makes state management clean without class syntax

### How It Works in This Project
```javascript
// index.js — React 18 root API
import { createRoot } from 'react-dom/client'
const root = createRoot(document.getElementById('root'))
root.render(<App />)
```

Every UI element is a React function component. State lives in hooks (`useState`, `useEffect`, `useContext`). The component tree re-renders when state changes.

### What Would Break Without It
Everything. React is the foundation. The entire UI depends on it.

### Version Note
React 18 requires `react-dom/client` instead of `react-dom`. Using the old `ReactDOM.render()` still works but shows a deprecation warning.

---

## 2. React Router DOM v7

**Package:** `react-router-dom@7`

### What It Is
The standard routing library for React. Enables navigation between pages without full browser refreshes (Single Page App behavior).

### Why Chosen
- De facto standard for React routing
- v7 adds improved TypeScript support and a cleaner data API
- Nested routes + layout system is perfect for role-specific layouts

### Key APIs Used

**`<BrowserRouter>`** — Wraps the app with HTML5 history API routing
```javascript
<BrowserRouter>
  <Routes>
    <Route path="/student" element={<StudentLayout />}>
      <Route index element={<StudentHome />} />
      <Route path="courses" element={<StudentCoursesPage />} />
    </Route>
  </Routes>
</BrowserRouter>
```

**`<Outlet>`** — In a layout component, renders the matched child route
{% raw %}
```javascript
// StudentLayout.jsx
return (
  <div>
    <StudentSidebar />
    <Outlet context={{ user, profile }} />   // child page renders here
  </div>
)
```
{% endraw %}

**`useOutletContext()`** — In a child page, gets data from parent layout
```javascript
const { user, profile } = useOutletContext()
```

**`useNavigate()`** — Programmatic navigation
```javascript
const navigate = useNavigate()
navigate('/student/quizzes')
```

**`useParams()`** — Extracts URL parameters
```javascript
const { quizId } = useParams()  // for route /student/quizzes/:quizId
```

**`<Navigate>`** — Redirects to another route
```javascript
<Route path="materials" element={<Navigate to="/prof/courses" replace />} />
```

### What Would Break Without It
All navigation. The app would be a single page with no routing.

---

## 3. Material UI (MUI) v5

**Package:** `@mui/material@5`, `@emotion/react`, `@emotion/styled`, `@mui/x-data-grid@5`

### What It Is
A comprehensive React component library implementing Google's Material Design system. Provides 100+ pre-built, accessible, and themeable components.

### Why Chosen
- Saves months of building UI from scratch
- Accessibility (ARIA attributes) built into every component
- Consistent design language across all pages
- `DataGrid` component handles table sorting, filtering, and pagination automatically

### Key Components Used

| MUI Component | Used For |
|--------------|---------|
| `<Button>` | All buttons with variant control (contained, outlined, text) |
| `<TextField>` | Form inputs with label, validation error display |
| `<Dialog>` | Modal windows (create/edit forms) |
| `<DataGrid>` | Admin tables with sorting and filtering |
| `<Chip>` | Status badges (quiz status, role labels) |
| `<Alert>` | Error and success messages |
| `<CircularProgress>` | Loading spinners |
| `<Avatar>` | User profile pictures |
| `<Tabs>` | Section navigation within pages |
| `<Accordion>` | Collapsible sections |
| `<LinearProgress>` | Progress bars |
| `<Select>` | Dropdown menus |
| `<Autocomplete>` | Searchable dropdowns |
| `<Snackbar>` | Toast notifications |
| `<IconButton>` | Icon-only buttons with proper touch targets |
| `<Tooltip>` | Hover explanations |
| `<Divider>` | Visual separators |
| `<Grid>` | Responsive grid layout system |
| `<Card>` + `<CardContent>` | Content cards |
| `<Table>` | Basic HTML tables with MUI styling |

### Theme Customization
MUI supports a custom theme via `createTheme()`. The app uses the primary color `#0b2c4a` (dark navy blue) applied through MUI's theme system.

### What Would Break Without It
The visual design would collapse. All forms, tables, dialogs, and buttons are MUI components.

---

## 4. Tailwind CSS v3

**Package:** `tailwindcss@3` (dev dependency)

### What It Is
A utility-first CSS framework. Instead of writing CSS classes, you apply utility classes directly in HTML/JSX: `className="flex items-center gap-4 text-sm font-bold"`.

### Why Chosen
- Complements MUI for custom layouts and spacing
- Much faster than writing custom CSS
- PurgeCSS integration removes unused styles in production (tiny bundle)

### How It Works
Tailwind scans all files in `src/` for class names and generates only the CSS that's actually used. The output is a single small CSS file.

### Usage Pattern in This Project
```jsx
<div className="flex flex-col items-start gap-2 p-4 rounded-lg shadow-md">
  <h2 className="text-2xl font-bold text-gray-800">{title}</h2>
</div>
```

### When to Use MUI vs Tailwind
- **MUI**: For interactive components (buttons, forms, dialogs, tables)
- **Tailwind**: For layout (flex, grid, spacing, positioning) and custom visual styling

### What Would Break Without It
Layout and custom spacing would fall apart. Pages would lose their visual structure.

---

## 5. Firebase SDK v9

**Package:** `firebase@9`

### What It Is
Firebase is Google's Backend-as-a-Service (BaaS) platform. The SDK v9 uses a modular, tree-shakeable API that imports only what's needed.

### Services Used

#### 5a. Firebase Authentication
```javascript
import { getAuth, onAuthStateChanged, signInWithEmailAndPassword } from 'firebase/auth'
```

- Manages user login/logout
- Persists session in browser storage (survives page refresh)
- Provides `getIdTokenResult()` to read JWT custom claims (role)
- **Custom claims** are set server-side by Cloud Functions and arrive in the JWT

#### 5b. Cloud Firestore
```javascript
import { getFirestore, collection, doc, getDoc, setDoc, onSnapshot } from 'firebase/firestore'
```

- Primary database for classroom operations
- Real-time listeners (`onSnapshot`) update UI automatically when data changes
- Hierarchical document model (collections contain documents, documents can have subcollections)
- Offline cache means app works with stale data when network is down

**Key Firestore API functions used:**

| Function | Purpose |
|----------|---------|
| `getDoc(ref)` | Fetch a single document once |
| `getDocs(query)` | Fetch all matching documents once |
| `setDoc(ref, data)` | Create or overwrite a document |
| `addDoc(colRef, data)` | Create a document with auto-ID |
| `updateDoc(ref, partial)` | Partially update a document |
| `deleteDoc(ref)` | Delete a document |
| `onSnapshot(query, callback)` | Subscribe to real-time updates |
| `query(colRef, ...constraints)` | Build a filtered/ordered query |
| `where(field, op, value)` | Filter constraint |
| `orderBy(field, dir)` | Sort constraint |
| `runTransaction(db, fn)` | Atomic multi-document operation |
| `writeBatch(db)` | Batch multiple writes atomically |
| `serverTimestamp()` | Server-generated timestamp |

#### 5c. Firebase Storage
```javascript
import { getStorage, ref, uploadBytesResumable, getDownloadURL } from 'firebase/storage'
```

- Stores lecture PDF files
- Uploads go directly from browser to Google Cloud Storage
- `getDownloadURL()` returns a public URL with optional expiry

#### 5d. Firebase Cloud Functions
```javascript
import { getFunctions, httpsCallable } from 'firebase/functions'
const fn = httpsCallable(functions, 'functionName')
const result = await fn(payload)
```

- Calls server-side Node.js functions
- Authentication is automatic — the current user's token is attached to every call
- Functions can be called with any JSON-serializable payload

### Why Firebase Over a Custom Backend?
- Real-time sync is built-in (critical for quizzes and attendance)
- No server management
- Authentication, storage, and serverless compute in one platform
- Scales automatically

### What Would Break Without It
The entire data layer. No login, no data, no file uploads.

---

## 6. ApexCharts + react-apexcharts

**Packages:** `apexcharts@4`, `react-apexcharts@1`

### What It Is
ApexCharts is a feature-rich charting library. `react-apexcharts` is its React wrapper.

### Why Chosen
- Beautiful animations and interactive tooltips out of the box
- Wide variety of chart types (bar, line, pie, donut, area)
- Responsive and mobile-friendly

### Usage in This Project
Used in `ProfessorDashboard` to display:
- Assignment counts per course (bar chart)
- Course performance comparison (column chart)
- Student distribution (pie chart)

{% raw %}
```jsx
import ReactApexChart from 'react-apexcharts'

<ReactApexChart
  type="bar"
  series={[{ name: "Students", data: [30, 45, 28] }]}
  options={{ xaxis: { categories: ["CS101", "MATH201", "PHY101"] } }}
  height={300}
/>
```
{% endraw %}

### What Would Break Without It
Dashboard charts would disappear. Pages would still function.

---

## 7. Google MediaPipe (`@mediapipe/tasks-vision`)

**Package:** `@mediapipe/tasks-vision@0.10.12`

### What It Is
Google MediaPipe is a machine learning framework for real-time AI tasks. The `tasks-vision` package provides:
- `FaceDetector` — detects faces in a video frame
- `FaceLandmarker` — maps 478 3D landmarks on a detected face

### Why Chosen
- Runs entirely in the browser using WebAssembly + WebGL
- No video data sent to any server — privacy-preserving
- Fast enough for real-time (1 sample/second at 30fps video)

### How It Works in `EngagementTracker.jsx`

```javascript
// 1. Load models from CDN
const landmarker = await FaceLandmarker.createFromOptions(vision, {
  baseOptions: { modelAssetPath: CDN_URL }
})

// 2. Every 1000ms, analyze current video frame
const result = landmarker.detectForVideo(videoElement, Date.now())

// 3. Read nose-tip position (landmark index 1 or 0)
const noseX = result.faceLandmarks[0][1].x  // 0.0 = left, 1.0 = right

// 4. Classify engagement
if (!result.faceLandmarks.length) status = "away"
else if (Math.abs(noseX - 0.5) > 0.15) status = "distracted"
else status = "focused"
```

### Privacy Architecture
- Webcam stream stays in the browser (never uploaded)
- Only aggregated counts (focused: 8, distracted: 2, away: 0) are sent to Firebase
- No images, no video frames, no biometric data stored

### What Would Break Without It
EngagementTracker would fail to initialize. The rest of the app is unaffected.

---

## 8. Axios

**Package:** `axios@1.10`

### What It Is
Promise-based HTTP client for making REST API requests.

### Why Chosen
- Cleaner API than native `fetch`
- Request/response interceptors for adding auth headers
- Automatic JSON serialization/deserialization

### Current Usage in This Project
```javascript
// src/services/http.js
export const http = axios.create({
  baseURL: process.env.VITE_API_BASE || "https://coowned-api-dev-*.run.app/api"
})
```

**Currently only used for FastAPI quiz generation:**
```javascript
// In ProfessorQuizzesPage.jsx — AI quiz from PDF
const formData = new FormData()
formData.append('file', pdfFile)
const response = await fetch(process.env.REACT_APP_GENERATE_QUIZ_URL, {
  method: 'POST', body: formData
})
```

Note: Quiz generation actually uses native `fetch`, not Axios. Axios is configured for future `.NET backend` integration.

### What Would Break Without It
Future `.NET backend` calls. Current app uses Firebase for all data — Axios is not actively called yet.

---

## 9. xlsx

**Package:** `xlsx@0.18`

### What It Is
A JavaScript library for reading and writing Excel files (`.xlsx`, `.xls`, `.csv`).

### Usage in This Project

**Reading (BulkImportUsersPage):**
```javascript
const workbook = XLSX.read(arrayBuffer, { type: 'array' })
const sheet = workbook.Sheets[workbook.SheetNames[0]]
const rows = XLSX.utils.sheet_to_json(sheet, { header: 1 })
```

**Writing (template download):**
```javascript
const ws = XLSX.utils.aoa_to_sheet([['username','email','phone','password','role']])
const wb = XLSX.utils.book_new()
XLSX.utils.book_append_sheet(wb, ws, 'Users')
XLSX.writeFile(wb, 'users_template.xlsx')
```

### What Would Break Without It
Bulk user import would not work. Admin could not download the template.

---

## 10. jsPDF

**Package:** `jspdf@3`

### What It Is
A client-side PDF generation library. Creates PDFs entirely in JavaScript without any server.

### Usage in This Project
Used in `StudentCoursesPage` to export the student's course schedule as a PDF document.

```javascript
const doc = new jsPDF()
doc.text("My Course Schedule", 20, 20)
doc.autoTable({ head: [['Course', 'Room', 'Day', 'Time']], body: rows })
doc.save('schedule.pdf')
```

### What Would Break Without It
The "Export to PDF" button on the student courses page.

---

## 11. react-icons

**Package:** `react-icons@5`

### What It Is
Unified icon package including Font Awesome, Material Design, Heroicons, and more.

### Why Chosen
- Single package, multiple icon families
- Tree-shakeable — only imported icons are in the bundle

### Usage Pattern
```javascript
import { HiOutlinePencil, HiTrash, HiPlus } from 'react-icons/hi'
<HiTrash className="text-red-500 w-5 h-5" />
```

### What Would Break Without It
Icons would disappear — buttons and navigation would be text-only.

---

## 12. react-firebase-hooks

**Package:** `react-firebase-hooks@5`

### What It Is
Custom React hooks for Firebase operations — `useAuthState`, `useCollection`, `useDocument`.

### Status in This Project
Imported but largely **unused**. Most components implement their own `useEffect` + `onSnapshot` subscriptions manually instead of using this library's hooks.

### What Would Break Without It
Nothing currently — it's an unused dependency that could be removed.

---

## 13. react-top-loading-bar

**Package:** `react-top-loading-bar@3`

### What It Is
A slim progress bar at the top of the screen, similar to YouTube's loading indicator.

### Usage
Shown during navigation transitions to provide visual feedback that something is happening.

### What Would Break Without It
Top-of-page loading indicator disappears. Navigation still works.

---

## 14. file-saver

**Package:** `file-saver@2`

### What It Is
`saveAs(blob, filename)` — saves a Blob or File object as a download in the browser.

### Usage
Used alongside `xlsx` to trigger the download of the generated Excel template.

```javascript
import { saveAs } from 'file-saver'
const blob = new Blob([excelBuffer], { type: EXCEL_TYPE })
saveAs(blob, 'users_template.xlsx')
```

### What Would Break Without It
Excel template download. `xlsx` alone can use `writeFile` which is a direct alternative.

---

## 15. jszip

**Package:** `jszip@3`

### What It Is
Creates ZIP archives in the browser.

### Status in This Project
Imported/available but not actively used in current features. Likely added for future "bulk export" features.

---

## 16. Create React App (Build Tooling)

**Dev Dependency:** `react-scripts@5`

### What It Is
Zero-configuration webpack + Babel + ESLint setup from Facebook. `npm start` → dev server. `npm run build` → production bundle.

### What It Provides
- **Webpack** — bundles all JS, CSS, images into optimized files
- **Babel** — transpiles modern JavaScript (JSX, ES2022) to browser-compatible JS
- **ESLint** — code quality checks
- **dev server** — hot module replacement at `localhost:3000`
- **Build optimization** — code splitting, minification, tree shaking

### Environment Variables
CRA supports `REACT_APP_*` prefix for environment variables:
```
REACT_APP_GENERATE_QUIZ_URL=https://...
REACT_APP_FIREBASE_API_KEY=AIzaSy...
```

**Note:** The `http.js` service uses `VITE_API_BASE` (Vite syntax) instead of `REACT_APP_API_BASE` (CRA syntax). This is a bug — `VITE_API_BASE` will always be `undefined` in a CRA project, so the fallback URL is always used.

### What Would Break Without It
Build and development server. Would need to replace with Vite or another build tool.

---

## Complete Dependency Summary

| Package | Category | Actively Used | Critical |
|---------|----------|--------------|---------|
| react, react-dom | Framework | ✅ | ✅ |
| react-router-dom | Routing | ✅ | ✅ |
| firebase | Database/Auth/Storage | ✅ | ✅ |
| @mui/material | UI Components | ✅ | ✅ |
| @mui/x-data-grid | Tables | ✅ | ✅ |
| @emotion/react, @emotion/styled | MUI dependency | ✅ | ✅ |
| tailwindcss | Styling | ✅ | High |
| @mediapipe/tasks-vision | Engagement tracking | ✅ | Medium |
| apexcharts, react-apexcharts | Charts | ✅ | Low |
| axios | HTTP (future) | Configured | Low |
| xlsx | Excel import/export | ✅ | Medium |
| jspdf | PDF export | ✅ | Low |
| react-icons | Icons | ✅ | Low |
| file-saver | File download | ✅ | Low |
| react-firebase-hooks | Firebase hooks | ❌ unused | None |
| react-top-loading-bar | Nav indicator | ✅ | Low |
| jszip | ZIP creation | ❌ unused | None |
| firebase-admin | Admin SDK (functions/) | ✅ server-side | High (functions) |
