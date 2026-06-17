# 18 — Deployment Guide
## Development, Build, and Production Deployment

---

## 1. Prerequisites

Before you can develop or deploy, you need:

| Tool | Version | How to Get |
|------|---------|-----------|
| Node.js | 18+ LTS | https://nodejs.org |
| npm | 9+ (comes with Node) | `npm -v` to check |
| Firebase CLI | Latest | `npm install -g firebase-tools` |
| Git | Any | https://git-scm.com |

---

## 2. Initial Setup (First Time)

```bash
# 1. Clone the repository
git clone https://github.com/ferashatem/Graduation-Project.git
cd Graduation-Project

# 2. Install frontend dependencies
npm install

# 3. Install Functions dependencies
cd functions
npm install
cd ..

# 4. Login to Firebase
firebase login

# 5. Set the Firebase project
firebase use graduation-project-61aa9

# 6. Create .env file with real values (copy from team member or Firebase Console)
cp .env.example .env
# Edit .env with real Firebase config values
```

---

## 3. Environment Variables Setup

Create a `.env` file in the project root:

```bash
# Firebase Project Configuration
# Get from Firebase Console → Project Settings → Your Apps → Config
REACT_APP_FIREBASE_API_KEY=AIzaSy...
REACT_APP_FIREBASE_AUTH_DOMAIN=graduation-project-61aa9.firebaseapp.com
REACT_APP_FIREBASE_STORAGE_BUCKET=graduation-project-61aa9.appspot.com
REACT_APP_FIREBASE_MESSAGING_SENDER_ID=528199941216
REACT_APP_FIREBASE_APP_ID=1:528199941216:web:...

# FastAPI AI Service
# The URL where the FastAPI service is running
REACT_APP_GENERATE_QUIZ_URL=https://your-fastapi-host.com/api/generate-quiz
```

**Important Notes:**
- Never commit the `.env` file to git
- Add `.env` to `.gitignore` if not already there
- For different environments, use `.env.development` and `.env.production`

---

## 4. Development Server

```bash
# Start the development server
npm start
```

This starts a webpack dev server at `http://localhost:3000` with:
- **Hot Module Replacement** — file saves auto-update the browser
- **Error overlay** — compilation errors shown in browser
- **Source maps** — debuggable code in browser DevTools

### Development Tips

**Firebase Emulator Suite (optional but recommended):**
```bash
# Start local Firebase emulators (Firestore, Auth, Functions, Storage)
firebase emulators:start

# In another terminal — configure app to use emulators
# Add to .env.development:
REACT_APP_USE_EMULATOR=true
```

Using emulators means all development data stays local — no risk of corrupting production data.

**The emulator UI** is available at `http://localhost:4000` — shows Firestore data, Auth users, and Function logs in a visual interface.

---

## 5. Build for Production

```bash
# Create optimized production build
npm run build
```

**What this does:**
1. Webpack bundles all JS files → `build/static/js/main.[hash].js`
2. Generates CSS → `build/static/css/main.[hash].css`
3. Copies `public/` files to `build/`
4. Generates `build/asset-manifest.json`
5. Creates `build/index.html` with correct script/link tags

**Output in `build/` directory:**
```
build/
├── index.html
├── static/
│   ├── js/
│   │   ├── main.abc123.js          ← App code
│   │   ├── main.abc123.js.map      ← Source map
│   │   └── 2.def456.chunk.js       ← Vendor code
│   └── css/
│       └── main.ghi789.css
└── asset-manifest.json
```

**File name hashing** (`abc123` part) enables **cache busting** — when you deploy a new build, browsers automatically download the new files because the URLs change.

---

## 6. Deploying to Firebase Hosting

```bash
# Deploy only the hosting (frontend)
firebase deploy --only hosting

# Deploy everything (hosting + functions + rules + indexes)
firebase deploy

# Preview deployment (doesn't affect production)
firebase hosting:channel:deploy preview-v2
```

### What Gets Deployed

| `firebase deploy --only hosting` | Deploys |
|----------------------------------|---------|
| Frontend | `build/` directory → Firebase CDN |
| Firestore Rules | **NOT** deployed (use `firebase deploy --only firestore:rules`) |
| Storage Rules | **NOT** deployed (use `firebase deploy --only storage`) |
| Functions | **NOT** deployed (use `firebase deploy --only functions`) |

**Full deployment command sequence:**
```bash
npm run build
firebase deploy
```

This deploys hosting + functions + rules + indexes in one command.

---

## 7. Deploying Firebase Functions

After any change to `functions/index.js`:

```bash
# Deploy only functions
firebase deploy --only functions

# Deploy specific function
firebase deploy --only functions:courseAiAssistant
```

**Functions deployment takes 1-3 minutes.** You'll see:
```
✔  functions[courseAiAssistant(us-central1)]: Successful update operation
✔  functions[setAttendance(us-central1)]: Successful update operation
...
✔  Deploy complete!
```

---

## 8. Deploying Firestore Rules and Indexes

```bash
# Deploy security rules
firebase deploy --only firestore:rules

# Deploy composite indexes
firebase deploy --only firestore:indexes

# Deploy both
firebase deploy --only firestore
```

**Index deployment takes 2-10 minutes** as Firebase builds the index. During this time, queries requiring the new index may return errors.

---

## 9. Live URL and Hosting

| Environment | URL |
|-------------|-----|
| Production | `https://bsnu.web.app` |
| Production (alt domain) | `https://graduation-project-61aa9.web.app` |
| Local dev | `http://localhost:3000` |

Firebase Hosting is served from Google's global CDN, so users get fast load times from their nearest edge node.

---

## 10. Previewing Before Deploy

```bash
# Create a preview channel (doesn't affect production)
firebase hosting:channel:deploy staging --expires 7d
```

This creates a temporary URL like `https://graduation-project-61aa9--staging-abc123.web.app` that expires in 7 days. Share with reviewers before deploying to production.

---

## 11. Rolling Back a Deployment

Firebase Hosting keeps deployment history. To roll back:

1. Go to **Firebase Console → Hosting → Deployment history**
2. Find the previous deployment
3. Click **"Roll back to this version"**

Or via CLI:
```bash
# List hosting versions
firebase hosting:versions:list

# Roll back to a specific version
firebase hosting:clone graduation-project-61aa9:live graduation-project-61aa9:live \
  --from-version <version-id>
```

---

## 12. Environment-Specific Deployments

### Production Deployment

```bash
# Build with production environment
npm run build  # uses .env.production or .env

# Deploy
firebase deploy --only hosting
```

### Staging Deployment

```bash
# Create staging Firebase project (recommended)
# Or use a channel on the same project

firebase hosting:channel:deploy staging
```

---

## 13. Post-Deployment Verification

After deployment, verify:

```bash
# Test the live site
curl https://bsnu.web.app

# Check Firebase Functions are running
firebase functions:log --limit 20

# Verify Firestore rules are deployed
firebase firestore:rules
```

**Manual verification checklist:**
- [ ] Sign in works (student, professor, admin)
- [ ] Student can see quizzes
- [ ] Professor can upload a material
- [ ] Admin can create a user
- [ ] AI chat works (calls Firebase Function)
- [ ] Quiz generation works (calls FastAPI)

---

## 14. Common Deployment Issues

### Issue: `npm run build` fails

**Cause 1:** Missing environment variables
```
Creating an optimized production build...
Failed to compile: process.env.REACT_APP_FIREBASE_API_KEY is undefined
```
**Fix:** Add all `REACT_APP_*` variables to `.env` before building.

**Cause 2:** ESLint errors treated as build failures
```
Treating warnings as errors because process.env.CI = true
```
**Fix:** Either fix the ESLint errors or add `DISABLE_ESLINT_PLUGIN=true` to `.env`.

### Issue: Firebase deploy fails with 403

```
Error: HTTP Error: 403, Request had insufficient authentication scopes
```
**Fix:** `firebase login --reauth`

### Issue: Functions deployment takes too long

Functions have a 540-second deployment timeout. If a function is complex with many npm dependencies, pre-build the functions:
```bash
cd functions && npm run build && cd ..
firebase deploy --only functions
```

### Issue: Deployed site shows old version

**Cause:** Browser cached old files. The hash in filenames should prevent this, but service workers can interfere.

**Fix:** 
1. Hard refresh: `Ctrl+Shift+R` (Windows) or `Cmd+Shift+R` (Mac)
2. Open DevTools → Application → Service Workers → Unregister

---

## 15. Monitoring Production

**Firebase Console** provides:
- **Hosting:** Traffic, bandwidth, requests
- **Authentication:** User count, sign-ins per day
- **Firestore:** Reads/writes/deletes per day, storage used
- **Functions:** Invocations, errors, latency
- **Storage:** Files stored, bandwidth
- **Analytics:** User sessions, page views (requires Google Analytics setup)

**Cost Monitoring:**
Firebase Spark (free tier) limits:
- Firestore: 50K reads/day, 20K writes/day, 20K deletes/day
- Functions: 2M invocations/month
- Storage: 5GB stored, 1GB/day download
- Hosting: 10GB/month transfer

For a growing university, you'll need the **Blaze** (pay-as-you-go) plan.
