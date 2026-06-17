# 05 — Configuration Guide
## Every Config File Explained — package.json, Firebase, Environment Variables, Tailwind

---

## 1. `package.json` — Project Manifest

The `package.json` is the heartbeat of the project. It defines dependencies, scripts, and metadata.

### Scripts

```json
{
  "scripts": {
    "start":   "react-scripts start",
    "build":   "react-scripts build",
    "test":    "react-scripts test",
    "eject":   "react-scripts eject"
  }
}
```

| Script | Command | What It Does |
|--------|---------|-------------|
| `start` | `npm start` | Starts webpack dev server at `localhost:3000` with hot reload |
| `build` | `npm run build` | Creates production bundle in `build/` (minified, optimized) |
| `test` | `npm test` | Runs Jest test suite (currently minimal tests) |
| `eject` | `npm run eject` | **IRREVERSIBLE** — exposes webpack config files. Don't do this. |

### Dependencies vs devDependencies

| In `dependencies` | In `devDependencies` |
|-------------------|---------------------|
| Ships to production browser bundle | Used only during development/build |
| React, Firebase, MUI, etc. | Tailwind CSS, ESLint, testing libs |

**Critical:** Everything in `dependencies` is included in the JavaScript bundle served to users. Keep it lean. `tailwindcss` is correctly in devDependencies because it generates CSS at build time, not runtime.

### Browser Compatibility

Create React App targets modern browsers by default. To support older browsers, `browserslist` in `package.json` would be configured.

---

## 2. Firebase Configuration

### 2a. `src/firebase/firebaseConfig.js` — SDK Initialization

```javascript
import { initializeApp } from "firebase/app"
import { getFirestore } from "firebase/firestore"
import { getAuth } from "firebase/auth"
import { getStorage } from "firebase/storage"
import { getFunctions } from "firebase/functions"
import { getAnalytics } from "firebase/analytics"

const firebaseConfig = {
  apiKey: process.env.REACT_APP_FIREBASE_API_KEY,
  authDomain: process.env.REACT_APP_FIREBASE_AUTH_DOMAIN,
  projectId: "graduation-project-61aa9",
  storageBucket: process.env.REACT_APP_FIREBASE_STORAGE_BUCKET,
  messagingSenderId: process.env.REACT_APP_FIREBASE_MESSAGING_SENDER_ID,
  appId: process.env.REACT_APP_FIREBASE_APP_ID,
  measurementId: "G-..."
}

const app = initializeApp(firebaseConfig)
export const db        = getFirestore(app)
export const auth      = getAuth(app)
export const storage   = getStorage(app)
export const functions = getFunctions(app)
export const analytics = getAnalytics(app)
```

**What gets exported:** `db`, `auth`, `storage`, `functions`, `analytics` — imported across the entire Firebase API layer.

**Security Note:** Firebase API keys in web apps are NOT secret. They identify the Firebase project but don't grant access. Security is enforced by Firestore Security Rules on the server side.

---

### 2b. `firebase.json` — Project Configuration

```json
{
  "hosting": {
    "public": "build",
    "ignore": ["firebase.json", "**/.*", "**/node_modules/**"],
    "rewrites": [
      { "source": "**", "destination": "/index.html" }
    ]
  },
  "firestore": {
    "rules": "firestore.rules",
    "indexes": "firestore.indexes.json"
  },
  "functions": {
    "source": "functions",
    "predeploy": ["npm --prefix \"$RESOURCE_DIR\" run build"]
  },
  "storage": {
    "rules": "storage.rules"
  }
}
```

**Key settings explained:**

| Setting | Value | Why |
|---------|-------|-----|
| `hosting.public` | `"build"` | Firebase Hosting serves the contents of the `build/` directory |
| `hosting.rewrites` | `** → /index.html` | SPA routing — all paths return `index.html` so React Router handles navigation |
| `firestore.rules` | `"firestore.rules"` | Points to the security rules file |
| `functions.source` | `"functions"` | Cloud Functions code location |

**The SPA rewrite is critical.** Without it, navigating directly to `bsnu.web.app/student/quizzes` would return a 404 from Firebase Hosting because there's no actual file at that path. The rewrite ensures `index.html` is always served, and React Router handles the path.

---

### 2c. `firestore.rules` — Security Rules

Firestore Security Rules are enforced server-side. No matter what code runs in the browser, these rules are the final authority on who can read or write what.

```javascript
rules_version = '2';
service cloud.firestore {
  match /databases/{database}/documents {

    // Helper functions
    function isSignedIn() {
      return request.auth != null;
    }

    function hasRole(role) {
      return request.auth.token.role == role;
    }

    function isAdmin() {
      return hasRole('admin') || hasRole('super_admin');
    }

    // Colleges — admin can write, anyone signed in can read
    match /colleges/{collegeId} {
      allow read: if isSignedIn();
      allow write: if isAdmin();
    }

    // Quizzes — professors create, students read (if published)
    match /quizzes/{quizId} {
      allow read: if isSignedIn();
      allow create, update: if hasRole('professor');
      allow delete: if hasRole('professor') && resource.data.createdBy == request.auth.uid;
    }

    // Quiz submissions — students write their own, professors read all for their quizzes
    match /quizSubmissions/{subId} {
      allow create: if hasRole('student') && request.resource.data.studentUid == request.auth.uid;
      allow read: if hasRole('professor') || (hasRole('student') && resource.data.studentUid == request.auth.uid);
    }

    // Users — users read/write their own, admins read all
    match /users/{userId} {
      allow read, write: if request.auth.uid == userId || isAdmin();
    }
  }
}
```

**How Rules Work:**
- Rules are evaluated when any client tries to read/write
- If no rule grants access, the request is denied
- Rules can read the requesting user's JWT claims (`request.auth.token.role`)
- Rules can check the existing document (`resource.data`) and the new data (`request.resource.data`)

**Common Pitfall:** Rules are evaluated per-document. A query that returns 1000 documents applies the rule to each document individually — if any document fails the rule, the entire query fails.

---

### 2d. `firestore.indexes.json` — Composite Indexes

When Firestore queries filter on multiple fields or filter + order, a composite index is required.

```json
{
  "indexes": [
    {
      "collectionGroup": "quizSubmissions",
      "queryScope": "COLLECTION",
      "fields": [
        { "fieldPath": "quizId", "order": "ASCENDING" },
        { "fieldPath": "studentUid", "order": "ASCENDING" }
      ]
    },
    {
      "collectionGroup": "courseAssignments",
      "queryScope": "COLLECTION",
      "fields": [
        { "fieldPath": "professorIds", "arrayConfig": "CONTAINS" },
        { "fieldPath": "createdAt", "order": "DESCENDING" }
      ]
    }
  ]
}
```

**When is this needed?** Any query that:
1. Filters on one field AND orders by a different field
2. Filters on multiple fields simultaneously
3. Uses `array-contains` AND orders by another field

**How to add indexes:** If a query fails in development with "index required" error, Firestore provides a direct link to create the index in the Firebase Console. Copy it to `firestore.indexes.json` for deployment.

---

### 2e. `storage.rules` — Storage Security Rules

```javascript
rules_version = '2';
service firebase.storage {
  match /b/{bucket}/o {

    // Lecture materials — professors write, authenticated users read
    match /materials/{professorId}/{courseId}/{fileName} {
      allow read: if request.auth != null;
      allow write: if request.auth != null
                   && request.auth.uid == professorId
                   && request.resource.size < 50 * 1024 * 1024;  // 50MB limit
    }

    // Assignment materials — professors and assistants write
    match /assignmentMaterials/{assignmentId}/{fileName} {
      allow read: if request.auth != null;
      allow write: if request.auth != null
                   && request.resource.size < 50 * 1024 * 1024;
    }
  }
}
```

**Key Rules:**
- Professors can only upload to their own `materials/{professorId}/` path
- File size limits enforced at the storage rule level (can't be bypassed)
- Any authenticated user can read (download) materials

---

## 3. Environment Variables (`.env`)

The project uses Create React App's environment variable system. Variables must be prefixed with `REACT_APP_` to be accessible in the browser.

### Required Variables

Create a `.env` file in the project root:

```bash
# Firebase Project Configuration
REACT_APP_FIREBASE_API_KEY=AIzaSy...
REACT_APP_FIREBASE_AUTH_DOMAIN=graduation-project-61aa9.firebaseapp.com
REACT_APP_FIREBASE_STORAGE_BUCKET=graduation-project-61aa9.appspot.com
REACT_APP_FIREBASE_MESSAGING_SENDER_ID=528199941216
REACT_APP_FIREBASE_APP_ID=1:528199941216:web:...

# AI Service
REACT_APP_GENERATE_QUIZ_URL=https://your-fastapi-service.com/api/generate-quiz
```

### Variable Files

| File | Environment | Purpose |
|------|-------------|---------|
| `.env` | All | Default values |
| `.env.local` | Local dev | Overrides `.env`, not committed to git |
| `.env.development` | `npm start` | Dev-specific values |
| `.env.production` | `npm run build` | Production values |

**NEVER commit `.env` files with real API keys to git.** Add `.env.local` to `.gitignore`.

### Accessing Variables in Code

```javascript
// Correct — CRA prefix
const url = process.env.REACT_APP_GENERATE_QUIZ_URL

// Wrong — Vite prefix (WILL be undefined in CRA project)
const url = process.env.VITE_API_BASE  // ← Bug in http.js
```

**Known Bug:** `src/services/http.js` uses `process.env.VITE_API_BASE` which is always `undefined` in a CRA project. This means the Axios instance always uses the hardcoded fallback URL.

---

## 4. Tailwind CSS Configuration

### `tailwind.config.js`

```javascript
module.exports = {
  content: [
    "./src/**/*.{js,jsx,ts,tsx}",   // scan all source files
    "./public/index.html",
  ],
  theme: {
    extend: {
      colors: {
        primary: '#0b2c4a',   // Dark navy — main brand color
      }
    }
  },
  plugins: [],
}
```

**`content` array** tells Tailwind which files to scan for class names. Only classes found in these files are included in the final CSS. This keeps the bundle small.

**`theme.extend`** adds custom values without replacing Tailwind defaults. Adding `primary: '#0b2c4a'` allows using `text-primary`, `bg-primary`, `border-primary` etc.

### `src/index.css` — Tailwind Directives

```css
@tailwind base;       /* CSS reset + base styles */
@tailwind components; /* Component class utilities */
@tailwind utilities;  /* All utility classes (flex, grid, etc.) */
```

These three directives must be in the root CSS file. Tailwind replaces them with generated CSS at build time.

---

## 5. Functions Configuration (`functions/package.json`)

Firebase Cloud Functions has its own `package.json` in the `functions/` directory. This is separate from the frontend's `package.json`.

```json
{
  "name": "functions",
  "scripts": {
    "serve": "firebase emulators:start --only functions",
    "deploy": "firebase deploy --only functions",
    "logs": "firebase functions:log"
  },
  "dependencies": {
    "firebase-admin": "^13",
    "firebase-functions": "^6"
  }
}
```

**Key Dependencies:**
- `firebase-admin` — Server-side Admin SDK (bypasses security rules, full access)
- `firebase-functions` — Declares callable and trigger functions

**Deploying functions separately:**
```bash
firebase deploy --only functions
```

This is important — after editing any function in `functions/index.js`, you must run this command to push the change to Firebase.

---

## 6. Build Output

Running `npm run build` creates:

```
build/
├── index.html              ← Entry point (single file)
├── static/
│   ├── js/
│   │   ├── main.chunk.js   ← App code (largest file)
│   │   ├── vendor.chunk.js ← Third-party libraries
│   │   └── runtime.js      ← Webpack runtime
│   ├── css/
│   │   └── main.chunk.css  ← All CSS (MUI + Tailwind generated)
│   └── media/              ← Images and fonts
└── asset-manifest.json     ← Mapping of all build artifacts
```

### Build Optimizations Done by CRA

| Optimization | What It Does |
|-------------|-------------|
| Minification | Removes whitespace/comments from JS and CSS |
| Tree shaking | Removes unused exports (e.g., unused MUI components) |
| Code splitting | Separates vendor libraries from app code |
| Asset hashing | File names include content hash (e.g., `main.a3f9c2.js`) for cache busting |
| Source maps | Optional `.map` files for debugging production |

---

## 7. Secondary Firebase App for Admin User Creation

In `CreateAdminUser.jsx`, a **secondary Firebase app** is initialized:

```javascript
const secondaryApp = initializeApp(firebaseConfig, "secondary")
const secondaryAuth = getAuth(secondaryApp)
```

**Why?** When an admin creates a new user via `createUserWithEmailAndPassword()`, Firebase Authentication automatically signs in the newly created user and signs out the current admin. To prevent this, a second Firebase app instance is used for user creation — it's separate from the main app's auth state, so the admin stays logged in.

This is a standard Firebase pattern for admin-side user creation.
