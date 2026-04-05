$baseUrl = "https://universitymanagementsystem-production-e58e.up.railway.app/api"
$email = "admin1@benisuefnationaluniversity.edu"
$password = "gqu9f*XJJ@Sm"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " TESTING AUTH REGISTRATION & EXAMS/LOCATIONS" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Login
$loginBody = @{ email = $email; password = $password } | ConvertTo-Json
try {
    $authResp = Invoke-RestMethod -Uri "$baseUrl/Auth/login" -Method Post -Body $loginBody -ContentType "application/json"
    $token = $authResp.data.token
    Write-Host "[SUCCESS] Logged in successfully." -ForegroundColor Green
} catch {
    Write-Host "[FAILED] Login failed: $($_.Exception.Message)" -ForegroundColor Red
    exit
}

$headers = @{
    Authorization = "Bearer $token"
    "Content-Type" = "application/json"
}

$timestamp = Get-Date -Format "HHmmss"
$natId = "2" + (Get-Date -Format "yyMMddHHmmss") + "1"
$phone = "010" + (Get-Date -Format "ddHHmmss")

# We will use existing valid codes from the DB to test the endpoints.
# The previous script created these:
$colCode = "UI_COL"
$depCode = "UI_DEP"
$batchCode = "UI_BAT"
$groupCode = "UI_GRP"

# 1. Register Admin
Write-Host "`n1. Testing POST /api/Auth/register/admin..."
$adminBody = @{
    fullName = "Test Admin $timestamp"
    nationalId = $natId
    phone = $phone
} | ConvertTo-Json
try {
    $res = Invoke-RestMethod -Uri "$baseUrl/Auth/register/admin" -Method Post -Body $adminBody -Headers $headers
    Write-Host "   [SUCCESS] Admin registered. Assigned Email: $($res.data.universityEmail)" -ForegroundColor Green
} catch {
    Write-Host "   [FAILED] $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails) { Write-Host "   $($_.ErrorDetails.Message)" -ForegroundColor Red }
}

# 2. Register Doctor
Write-Host "`n2. Testing POST /api/Auth/register/doctor..."
$docNatId = "289" + (Get-Date -Format "ddHHmmss") + "12"
$docBody = @{
    fullName = "Dr. Test $timestamp"
    departmentCode = $depCode
    nationalId = $docNatId
    phone = "011" + (Get-Date -Format "ddHHmmss")
} | ConvertTo-Json
try {
    $res = Invoke-RestMethod -Uri "$baseUrl/Auth/register/doctor" -Method Post -Body $docBody -Headers $headers
    Write-Host "   [SUCCESS] Doctor registered. Assigned Email: $($res.data.universityEmail)" -ForegroundColor Green
} catch {
    Write-Host "   [FAILED] $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails) { Write-Host "   $($_.ErrorDetails.Message)" -ForegroundColor Red }
}

# 3. Register Student
Write-Host "`n3. Testing POST /api/Auth/register/student..."
$stuNatId = "300" + (Get-Date -Format "ddHHmmss") + "99"
$stuBody = @{
    fullName = "Auth Student $timestamp"
    collegeCode = $colCode
    departmentCode = $depCode
    batchCode = $batchCode
    groupCode = $groupCode
    nationalId = $stuNatId
    phone = "012" + (Get-Date -Format "ddHHmmss")
} | ConvertTo-Json
try {
    $res = Invoke-RestMethod -Uri "$baseUrl/Auth/register/student" -Method Post -Body $stuBody -Headers $headers
    Write-Host "   [SUCCESS] Student registered. Assigned Email: $($res.data.universityEmail)" -ForegroundColor Green
} catch {
    Write-Host "   [FAILED] $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails) { Write-Host "   $($_.ErrorDetails.Message)" -ForegroundColor Red }
}

Write-Host "`n==========================================" -ForegroundColor Cyan
Write-Host " REGISTRATION TEST FINISHED " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
