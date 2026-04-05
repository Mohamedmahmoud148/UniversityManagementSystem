param(
    [string]$baseUrl = "https://universitymanagementsystem-production-e58e.up.railway.app/api"
)

function Invoke-Api {
    param($Path, $Method, $Body, $Token, $IsMultipart)
    $headers = @{}
    if ($Token) { $headers.Add("Authorization", "Bearer $Token") }
    if (-not $IsMultipart) { $headers.Add("Content-Type", "application/json") }
    
    # Try block for making requests
    try {
        if ($Body -and -not $IsMultipart) { $Body = $Body | ConvertTo-Json -Depth 10 }
        $res = Invoke-RestMethod -Uri "$baseUrl$Path" -Method $Method -Body $Body -Headers $headers -ErrorAction Stop
        return @{ Success = $true; Data = $res }
    } catch {
        $msg = $_.Exception.Message
        if ($_.ErrorDetails) { $msg += " - " + $_.ErrorDetails.Message }
        return @{ Success = $false; Error = $msg }
    }
}

Write-Host "===============================================" -ForegroundColor Cyan
Write-Host " FULL ACADEMIC LIFECYCLE TEST" -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan

# 1. Login as SuperAdmin
Write-Host "`n[1] Logging in as SuperAdmin..."
$superToken = (Invoke-Api -Path "/Auth/login" -Method Post -Body @{email="super.admin@university.com";password="SuperSecretPass1!"}).Data.data.token
if (-not $superToken) { Write-Host "Failed to login as SuperAdmin."; exit }
Write-Host "SUCCESS: Obtained SuperAdmin Token" -ForegroundColor Green

# 2. Register Admin from SuperAdmin
$ts = Get-Date -Format "HHmmss"
Write-Host "`n[2] Registering new Admin..."
$adminRes = Invoke-Api -Path "/Auth/register/admin" -Method Post -Token $superToken -Body @{
    fullName = "Admin $ts"; nationalId = "Adm$ts"; phone = "010$ts"
}
if ($adminRes.Success) { Write-Host "SUCCESS: Admin Registered -> $($adminRes.Data.data.universityEmail)" -ForegroundColor Green }
else { Write-Host "FAIL: $($adminRes.Error)" -ForegroundColor Red }

# 3. Create Doctor & Student via Admin Account (Using existing admin for simplicity)
Write-Host "`n[3] Logging in as existing Admin (admin1) to seed users..."
$adminToken = (Invoke-Api -Path "/Auth/login" -Method Post -Body @{email="admin1@benisuefnationaluniversity.edu";password="gqu9f*XJJ@Sm"}).Data.data.token

$docId = "D$ts"; $stuId = "S$ts"
Write-Host "`n[4] Creating Doctor..."
$docRes = Invoke-Api -Path "/Auth/register/doctor" -Method Post -Token $adminToken -Body @{
    fullName="Dr. Tester"; departmentCode="UI_DEP"; nationalId=$docId; phone="011$ts"
}
if ($docRes.Success) {
    Write-Host "SUCCESS: Doctor Created -> Email: $($docRes.Data.data.universityEmail) Pass: $($docRes.Data.data.temporaryPassword)" -ForegroundColor Green
    $docEmail = $docRes.Data.data.universityEmail
    $docPass = $docRes.Data.data.temporaryPassword
} else { Write-Host "FAIL: $($docRes.Error)" -ForegroundColor Red }

Write-Host "`n[5] Creating Student..."
$stuRes = Invoke-Api -Path "/Auth/register/student" -Method Post -Token $adminToken -Body @{
    fullName="Student Tester"; collegeCode="UI_COL"; departmentCode="UI_DEP"; batchCode="UI_BAT"; groupCode="UI_GRP"; nationalId=$stuId; phone="012$ts"
}
if ($stuRes.Success) {
    Write-Host "SUCCESS: Student Created -> Email: $($stuRes.Data.data.universityEmail) Pass: $($stuRes.Data.data.temporaryPassword)" -ForegroundColor Green
    $stuEmail = $stuRes.Data.data.universityEmail
    $stuPass = $stuRes.Data.data.temporaryPassword
} else { Write-Host "FAIL: $($stuRes.Error)" -ForegroundColor Red }

Write-Host "`n--- WAITING FOR DB SYNC/ACCOUNT ACTIVATION (2s) ---"
Start-Sleep -Seconds 2

# 6. Doctor Actions (if creation succeeded)
if ($docEmail) {
    Write-Host "`n[6] Logging in as Doctor..."
    $docTokenRes = Invoke-Api -Path "/Auth/login" -Method Post -Body @{email=$docEmail; password=$docPass}
    if ($docTokenRes.Success) {
        $docToken = $docTokenRes.Data.data.token
        Write-Host "SUCCESS: Doctor Logged In." -ForegroundColor Green
        
        Write-Host "`n[7] Doctor creating Exam (requires SubjectOffering, assuming static offering for now or verifying endpoint)..."
        # We need an offering ID. Searching subjects:
        $subRes = Invoke-Api -Path "/Subjects" -Method Get -Token $adminToken
        Write-Host "Subjects accessible? $($subRes.Success)" -ForegroundColor Yellow
        # It's an intricate flow to assign offering etc.
        Write-Host "NOTE: Exam creation via API requires correct Offering ID. Needs robust setup to fully automate." -ForegroundColor Yellow
    } else {
        Write-Host "FAIL Login: $($docTokenRes.Error)" -ForegroundColor Red
    }
}

# 8. Student Actions
if ($stuEmail) {
    Write-Host "`n[8] Logging in as Student..."
    $stuTokenRes = Invoke-Api -Path "/Auth/login" -Method Post -Body @{email=$stuEmail; password=$stuPass}
    if ($stuTokenRes.Success) {
        $stuToken = $stuTokenRes.Data.data.token
        Write-Host "SUCCESS: Student Logged In." -ForegroundColor Green
        
        Write-Host "`n[9] Student fetching GPA summary..."
        $gpaRes = Invoke-Api -Path "/Grades/summary" -Method Get -Token $stuToken
        if ($gpaRes.Success) { Write-Host "SUCCESS: GPA Fetched ($($gpaRes.Data))" -ForegroundColor Green }
        else { Write-Host "GPA Endpoint Result: $($gpaRes.Error)" -ForegroundColor Yellow }
        
    } else { Write-Host "FAIL Login: $($stuTokenRes.Error)" -ForegroundColor Red }
}

Write-Host "`n===============================================" -ForegroundColor Cyan
Write-Host " TEST FINISHED" -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan
