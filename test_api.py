import requests
import json

BASE = "https://universitymanagementsystem-production-e58e.up.railway.app"
RESULTS = []

def test(name, method, url, token=None, body=None, expected_status=200):
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    try:
        if method == "GET":
            r = requests.get(url, headers=headers, timeout=15)
        elif method == "POST":
            r = requests.post(url, headers=headers, json=body, timeout=15)
        elif method == "DELETE":
            r = requests.delete(url, headers=headers, timeout=15)
        elif method == "PUT":
            r = requests.put(url, headers=headers, json=body, timeout=15)

        ok = "✅" if r.status_code == expected_status else "❌"
        try:
            data = r.json()
        except:
            data = r.text[:200]
        RESULTS.append({"test": name, "status": r.status_code, "ok": ok, "data": data})
        print(f"{ok} [{r.status_code}] {name}")
        return r.status_code, data
    except Exception as e:
        RESULTS.append({"test": name, "status": "ERR", "ok": "❌", "data": str(e)})
        print(f"❌ [ERR] {name}: {e}")
        return None, None

# ─── LOGIN ───────────────────────────────────────────────────────────────────
print("\n═══ AUTH ═══")
_, resp = test("Login SuperAdmin", "POST", f"{BASE}/api/auth/login",
               body={"email": "super.admin@university.com", "password": "SuperSecretPass1!"})
SA_TOKEN = resp.get("data", {}).get("token", "") if isinstance(resp.get("data"), dict) else ""

_, resp = test("Login Doctor", "POST", f"{BASE}/api/auth/login",
               body={"email": "ahmed@university.com", "password": "Pass123!"})
DOC_TOKEN = resp.get("data", {}).get("token", "") if isinstance(resp.get("data"), dict) else ""

_, resp = test("Login Student", "POST", f"{BASE}/api/auth/login",
               body={"email": "ali@university.com", "password": "Pass123!"})
STU_TOKEN = resp.get("data", {}).get("token", "") if isinstance(resp.get("data"), dict) else ""

print(f"  SA_TOKEN:  {'OK' if SA_TOKEN else 'MISSING'} ({len(SA_TOKEN)} chars)")
print(f"  DOC_TOKEN: {'OK' if DOC_TOKEN else 'MISSING'} ({len(DOC_TOKEN)} chars)")
print(f"  STU_TOKEN: {'OK' if STU_TOKEN else 'MISSING'} ({len(STU_TOKEN)} chars)")

test("Bad login → invalid creds msg", "POST", f"{BASE}/api/auth/login",
     body={"email": "super.admin@university.com", "password": "WRONG"})
test("No auth → 401", "GET", f"{BASE}/api/Colleges", expected_status=401)

# ─── STRUCTURE GETs ──────────────────────────────────────────────────────────
print("\n═══ STRUCTURE — GETs ═══")
_, uni_r     = test("GET /University/structure",   "GET", f"{BASE}/api/University/structure",   SA_TOKEN)
_, col_r     = test("GET /Colleges",               "GET", f"{BASE}/api/Colleges?pageSize=10",   SA_TOKEN)
_, dept_r    = test("GET /Departments",            "GET", f"{BASE}/api/Departments?pageSize=10",SA_TOKEN)
_, batch_r   = test("GET /Batches",                "GET", f"{BASE}/api/Batches?pageSize=10",    SA_TOKEN)
_, group_r   = test("GET /Groups",                 "GET", f"{BASE}/api/Groups?pageSize=10",     SA_TOKEN)
_,           test("GET /University/full-structure","GET", f"{BASE}/api/University/full-structure",SA_TOKEN)

# Extract seeded IDs
uni_id = college_id = dept_id = batch_id = group_id = None
try:
    uni_data = uni_r.get("data", [])
    if isinstance(uni_data, list) and uni_data:
        uni_id = str(uni_data[0]["id"])
    elif isinstance(uni_data, dict):
        uni_id = str(uni_data["id"])
except: pass
try: college_id = str(col_r["data"]["data"][0]["id"])
except: pass
try: dept_id    = str(dept_r["data"]["data"][0]["id"])
except: pass
try: batch_id   = str(batch_r["data"]["data"][0]["id"])
except: pass
try: group_id   = str(group_r["data"]["data"][0]["id"])
except: pass

print(f"  Seeded → uni:{uni_id and uni_id[:14]} college:{college_id and college_id[:14]} dept:{dept_id and dept_id[:14]} batch:{batch_id and batch_id[:14]} group:{group_id and group_id[:14]}")

if dept_id:
    test("GET /Batches/by-department/{id}", "GET", f"{BASE}/api/Batches/by-department/{dept_id}", SA_TOKEN)
if batch_id:
    test("GET /Groups/by-batch/{id}",       "GET", f"{BASE}/api/Groups/by-batch/{batch_id}",      SA_TOKEN)

# ─── INVALID IDs ─────────────────────────────────────────────────────────────
print("\n═══ STRUCTURE — Invalid IDs → 400 ═══")
test("DELETE College not-a-ulid → 400",  "DELETE", f"{BASE}/api/Colleges/not-a-ulid",  SA_TOKEN, expected_status=400)
test("DELETE Batch not-a-ulid → 400",   "DELETE", f"{BASE}/api/Batches/not-a-ulid",   SA_TOKEN, expected_status=400)
test("DELETE Group not-a-ulid → 400",   "DELETE", f"{BASE}/api/Groups/not-a-ulid",    SA_TOKEN, expected_status=400)
test("DELETE unknown college → 404",    "DELETE", f"{BASE}/api/Colleges/01AAAAAAAAAAAAAAAAAAAAAA", SA_TOKEN, expected_status=404)

# ─── CREATE flow ─────────────────────────────────────────────────────────────
print("\n═══ STRUCTURE — CREATE flow ═══")
new_college_id = new_dept_id = new_batch_id = new_group_id = None

if uni_id:
    _, r = test("POST /Colleges", "POST", f"{BASE}/api/Colleges", SA_TOKEN,
                body={"name":"Test College","code":"TSTCOL","universityId": uni_id})
    try: new_college_id = str(r["data"]["id"])
    except: pass
    test("POST /Colleges dup code → 409","POST", f"{BASE}/api/Colleges", SA_TOKEN,
         body={"name":"Dup","code":"TSTCOL","universityId": uni_id}, expected_status=409)

if new_college_id:
    _, r = test("POST /Departments", "POST", f"{BASE}/api/Departments", SA_TOKEN,
                body={"name":"Test Dept","code":"TSTDEP","collegeId": new_college_id})
    try: new_dept_id = str(r["data"]["id"])
    except: pass

if new_dept_id:
    _, r = test("POST /Batches", "POST", f"{BASE}/api/Batches", SA_TOKEN,
                body={"name":"Test Batch","code":"TSTBAT","departmentId": new_dept_id})
    try: new_batch_id = str(r["data"]["id"])
    except: pass

if new_batch_id:
    _, r = test("POST /Groups", "POST", f"{BASE}/api/Groups", SA_TOKEN,
                body={"name":"Test Group","code":"TSTGRP","batchId": new_batch_id})
    try: new_group_id = str(r["data"]["id"])
    except: pass

# PUT (update)
if new_college_id:
    test("PUT /Colleges/{id}", "PUT", f"{BASE}/api/Colleges/{new_college_id}", SA_TOKEN,
         body={"name":"Test College Updated","code":"TSTCOL","universityId": uni_id},
         expected_status=204)
if new_batch_id:
    test("PUT /Batches/{id}", "PUT", f"{BASE}/api/Batches/{new_batch_id}", SA_TOKEN,
         body={"name":"Test Batch Updated","code":"TSTBAT","departmentId": new_dept_id},
         expected_status=204)
if new_group_id:
    test("PUT /Groups/{id}", "PUT", f"{BASE}/api/Groups/{new_group_id}", SA_TOKEN,
         body={"name":"Test Group Updated","code":"TSTGRP","batchId": new_batch_id},
         expected_status=204)

# ─── DELETE cascade (empty = no students) ────────────────────────────────────
print("\n═══ STRUCTURE — DELETE cascade (empty) ═══")
if new_group_id:  test("DELETE Group  (empty) → 204",  "DELETE", f"{BASE}/api/Groups/{new_group_id}",   SA_TOKEN, expected_status=204)
if new_batch_id:  test("DELETE Batch  (empty) → 204",  "DELETE", f"{BASE}/api/Batches/{new_batch_id}",  SA_TOKEN, expected_status=204)
if new_dept_id:   test("DELETE Dept   (empty) → 204",  "DELETE", f"{BASE}/api/Departments/{new_dept_id}",SA_TOKEN, expected_status=204)
if new_college_id:test("DELETE College(empty) → 204",  "DELETE", f"{BASE}/api/Colleges/{new_college_id}",SA_TOKEN, expected_status=204)

# ─── DELETE blocked (seeded data has students) ───────────────────────────────
print("\n═══ STRUCTURE — DELETE blocked (has students) ═══")
if group_id:  test("DELETE seeded Group → 400",  "DELETE", f"{BASE}/api/Groups/{group_id}",  SA_TOKEN, expected_status=400)
if batch_id:  test("DELETE seeded Batch → 400",  "DELETE", f"{BASE}/api/Batches/{batch_id}", SA_TOKEN, expected_status=400)

# ─── ACADEMIC YEARS ──────────────────────────────────────────────────────────
print("\n═══ ACADEMIC YEARS ═══")
_, ay_r = test("GET /academic-years",      "GET", f"{BASE}/api/academic-years",      SA_TOKEN)
ay_id = None
try:
    ay_id = str(ay_r["data"][0]["id"])
    print(f"  First AY: {ay_r['data'][0]}")
except: pass
if ay_id:
    _, dept_mapping = test(f"GET /academic-years/{ay_id[:8]}../departments", "GET",
                            f"{BASE}/api/academic-years/{ay_id}/departments", SA_TOKEN)
    try:
        first = dept_mapping["data"][0] if dept_mapping and dept_mapping.get("data") else None
        if first:
            has_code = "departmentCode" in first
            print(f"  DepartmentCode in response: {'✅ YES' if has_code else '❌ MISSING'} → {first}")
    except: pass

# ─── STUDENTS ────────────────────────────────────────────────────────────────
print("\n═══ STUDENTS ═══")
test("GET /Students (admin)",                "GET", f"{BASE}/api/Students?pageSize=5",         SA_TOKEN)
test("GET /Students (no auth) → 401",        "GET", f"{BASE}/api/Students",                    expected_status=401)
test("GET /Students (student role)",         "GET", f"{BASE}/api/Students?pageSize=5",         STU_TOKEN)

# ─── DOCTORS ─────────────────────────────────────────────────────────────────
print("\n═══ DOCTORS ═══")
test("GET /Doctors (admin)", "GET", f"{BASE}/api/Doctors?pageSize=5", SA_TOKEN)
test("GET /Doctors (doctor)", "GET", f"{BASE}/api/Doctors?pageSize=5", DOC_TOKEN)

# ─── SUBJECTS ────────────────────────────────────────────────────────────────
print("\n═══ SUBJECTS ═══")
test("GET /Subjects", "GET", f"{BASE}/api/Subjects?pageSize=5", SA_TOKEN)

# ─── SUBJECT OFFERINGS ───────────────────────────────────────────────────────
print("\n═══ SUBJECT OFFERINGS ═══")
test("GET /SubjectOfferings",              "GET", f"{BASE}/api/SubjectOfferings?pageSize=5",    SA_TOKEN)
test("GET /SubjectOfferings/my-enrollments (student)", "GET", f"{BASE}/api/Enrollments/my-enrollments", STU_TOKEN)

# ─── SEMESTERS ───────────────────────────────────────────────────────────────
print("\n═══ SEMESTERS ═══")
test("GET /Semesters", "GET", f"{BASE}/api/Semesters", SA_TOKEN)

# ─── GPA & GRADES ────────────────────────────────────────────────────────────
print("\n═══ GPA & GRADES ═══")
test("GET /gpa/my-gpa (student)", "GET", f"{BASE}/api/gpa/my-gpa", STU_TOKEN)
test("GET /Grades (admin)",       "GET", f"{BASE}/api/Grades?pageSize=5", SA_TOKEN)

# ─── REGULATIONS ─────────────────────────────────────────────────────────────
print("\n═══ REGULATIONS ═══")
test("GET /Regulations",             "GET", f"{BASE}/api/Regulations?pageSize=5",  SA_TOKEN)
test("GET /Regulations/my-roadmap",  "GET", f"{BASE}/api/Regulations/my-roadmap",  STU_TOKEN)

# ─── EXAMS ───────────────────────────────────────────────────────────────────
print("\n═══ EXAMS ═══")
test("GET /Exams/my-exams (doctor)",  "GET", f"{BASE}/api/Exams/my-exams",  DOC_TOKEN)
test("GET /Exams (admin)",            "GET", f"{BASE}/api/Exams?pageSize=5", SA_TOKEN)

# ─── ATTENDANCE ──────────────────────────────────────────────────────────────
print("\n═══ ATTENDANCE ═══")
test("GET /Attendance/sessions (doctor)", "GET", f"{BASE}/api/Attendance/sessions", DOC_TOKEN)

# ─── MATERIALS ───────────────────────────────────────────────────────────────
print("\n═══ MATERIALS ═══")
test("GET /Materials (admin)", "GET", f"{BASE}/api/Materials?pageSize=5", SA_TOKEN)

# ─── NOTIFICATIONS ───────────────────────────────────────────────────────────
print("\n═══ NOTIFICATIONS ═══")
test("GET /Notification (student)", "GET", f"{BASE}/api/Notification?pageSize=5", STU_TOKEN)
test("GET /Notification (doctor)",  "GET", f"{BASE}/api/Notification?pageSize=5", DOC_TOKEN)

# ─── COMPLAINTS ──────────────────────────────────────────────────────────────
print("\n═══ COMPLAINTS ═══")
test("GET /Complaints/my-complaints (student)", "GET", f"{BASE}/api/Complaints/my-complaints", STU_TOKEN)
test("GET /Complaints/all (admin)",             "GET", f"{BASE}/api/Complaints/all?pageSize=5", SA_TOKEN)

# ─── DASHBOARD ───────────────────────────────────────────────────────────────
print("\n═══ DASHBOARD ═══")
test("GET /Dashboard/stats", "GET", f"{BASE}/api/Dashboard/stats", SA_TOKEN)

# ─── AUDIT LOGS ──────────────────────────────────────────────────────────────
print("\n═══ AUDIT LOGS ═══")
test("GET /AuditLogs (admin)", "GET", f"{BASE}/api/AuditLogs?pageSize=5", SA_TOKEN)

# ─── SCHEDULE ────────────────────────────────────────────────────────────────
print("\n═══ SCHEDULE ═══")
test("GET /Schedule (admin)", "GET", f"{BASE}/api/Schedule?pageSize=5", SA_TOKEN)

# ─────────────────────────────────────────────────────────────────────────────
print("\n" + "═"*60)
print("SUMMARY")
print("═"*60)
passed = sum(1 for r in RESULTS if r["ok"] == "✅")
failed = sum(1 for r in RESULTS if r["ok"] == "❌")
print(f"✅ Passed: {passed}  ❌ Failed: {failed}  Total: {len(RESULTS)}")
print()
if failed:
    print("FAILURES DETAIL:")
    for r in RESULTS:
        if r["ok"] == "❌":
            data_str = str(r['data'])[:200] if r['data'] else ''
            print(f"  ❌ [{r['status']}] {r['test']}")
            print(f"       {data_str}")
