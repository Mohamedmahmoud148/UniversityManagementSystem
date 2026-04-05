$baseUrl = "https://universitymanagementsystem-production-e58e.up.railway.app/api"
$email = "admin1@benisuefnationaluniversity.edu"
$password = "gqu9f*XJJ@Sm"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " SEEDING TEST DATA (Full Admin flow)    " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Starting tests against: $baseUrl"

# 1. Login
Write-Host "`n1. Authenticating as SuperAdmin..."
$loginBody = @{
    email = $email
    password = $password
} | ConvertTo-Json -Depth 10

try {
    $loginResponse = Invoke-RestMethod -Uri "$baseUrl/Auth/login" -Method Post -Body $loginBody -ContentType "application/json"
    $token = $loginResponse.data.token
    Write-Host "   [SUCCESS] Logged in successfully." -ForegroundColor Green
} catch {
    Write-Host "   [FAILED] Login failed: $($_.Exception.Message)" -ForegroundColor Red
    exit
}

$headers = @{
    Authorization = "Bearer $token"
    "Content-Type" = "application/json"
}

# Generate unique codes for this test run
$timestamp = Get-Date -Format "syyyyMMddHHmmss"
$uniCode = "UNI_$timestamp"
$colCode = "COL_$timestamp"
$depCode = "DEP_$timestamp"
$batchCode = "BAT_$timestamp"
$groupCode = "GRP_$timestamp"
$natId = "29" + (Get-Date -Format "yyMMddHHmmss") # 14 digits pseudo-random valid format
$phone = "0100" + (Get-Date -Format "HHmmss") + "1"

# 2. Create University
Write-Host "`n2. Creating University [$uniCode]..."
$uniBody = @{
    name = "Test University ($timestamp)"
    code = $uniCode
} | ConvertTo-Json -Depth 10

try {
    $uniResp = Invoke-RestMethod -Uri "$baseUrl/University" -Method Post -Body $uniBody -Headers $headers
    Write-Host "   [SUCCESS] University created. ID: $($uniResp.id)" -ForegroundColor Green
} catch {
    Write-Host "   [FAILED] Create University: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails) { Write-Host "   Details: $($_.ErrorDetails.Message)" -ForegroundColor Red }
    exit
}

# 3. Create College
Write-Host "`n3. Creating College [$colCode] linked to [$uniCode]..."
$colBody = @{
    name = "Test College ($timestamp)"
    code = $colCode
    universityCode = $uniCode
} | ConvertTo-Json -Depth 10

try {
    $colResp = Invoke-RestMethod -Uri "$baseUrl/Colleges" -Method Post -Body $colBody -Headers $headers
    Write-Host "   [SUCCESS] College created. ID: $($colResp.id)" -ForegroundColor Green
} catch {
    Write-Host "   [FAILED] Create College: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails) { Write-Host "   Details: $($_.ErrorDetails.Message)" -ForegroundColor Red }
    exit
}

# 4. Create Department
Write-Host "`n4. Creating Department [$depCode] linked to [$colCode]..."
$depBody = @{
    name = "Test Department ($timestamp)"
    code = $depCode
    collegeCode = $colCode
} | ConvertTo-Json -Depth 10

try {
    $depResp = Invoke-RestMethod -Uri "$baseUrl/Departments" -Method Post -Body $depBody -Headers $headers
    Write-Host "   [SUCCESS] Department created. ID: $($depResp.id)" -ForegroundColor Green
} catch {
    Write-Host "   [FAILED] Create Department: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails) { Write-Host "   Details: $($_.ErrorDetails.Message)" -ForegroundColor Red }
    exit
}

# 5. Create Batch
Write-Host "`n5. Creating Batch [$batchCode] linked to [$depCode]..."
$batchBody = @{
    name = "Test Batch ($timestamp)"
    code = $batchCode
    departmentCode = $depCode
} | ConvertTo-Json -Depth 10

try {
    $batchResp = Invoke-RestMethod -Uri "$baseUrl/Batches" -Method Post -Body $batchBody -Headers $headers
    Write-Host "   [SUCCESS] Batch created. ID: $($batchResp.id)" -ForegroundColor Green
} catch {
    Write-Host "   [FAILED] Create Batch: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails) { Write-Host "   Details: $($_.ErrorDetails.Message)" -ForegroundColor Red }
    exit
}

# 6. Create Group
Write-Host "`n6. Creating Group [$groupCode] linked to [$batchCode]..."
$groupBody = @{
    name = "Test Group ($timestamp)"
    code = $groupCode
    batchCode = $batchCode
} | ConvertTo-Json -Depth 10

try {
    $groupResp = Invoke-RestMethod -Uri "$baseUrl/Groups" -Method Post -Body $groupBody -Headers $headers
    Write-Host "   [SUCCESS] Group created. ID: $($groupResp.id)" -ForegroundColor Green
} catch {
    Write-Host "   [FAILED] Create Group: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails) { Write-Host "   Details: $($_.ErrorDetails.Message)" -ForegroundColor Red }
    exit
}

# 7. Create Student
Write-Host "`n7. Creating Student linked to Batch:[$batchCode] and Group:[$groupCode]..."
$stuBody = @{
    fullName = "Test Student ($timestamp)"
    nationalId = $natId
    phone = $phone
    batchCode = $batchCode
    groupCode = $groupCode
} | ConvertTo-Json -Depth 10

try {
    $stuResp = Invoke-RestMethod -Uri "$baseUrl/Students" -Method Post -Body $stuBody -Headers $headers
    Write-Host "   [SUCCESS] Student created successfully." -ForegroundColor Green
    Write-Host "   [INFO] Student FullName: $($stuResp.fullName)" -ForegroundColor Cyan
    Write-Host "   [INFO] Student University ID: $($stuResp.universityStudentId)" -ForegroundColor Cyan
    Write-Host "   [INFO] Student University Email: $($stuResp.universityEmail)" -ForegroundColor Cyan
} catch {
    Write-Host "   [WARNING] Create Student failed or timed out: $($_.Exception.Message)" -ForegroundColor Yellow
    if ($_.ErrorDetails) { Write-Host "   Details: $($_.ErrorDetails.Message)" -ForegroundColor Yellow }
    Write-Host "   Note: Sometimes this fails locally because of Email Service timeouts, but the DB insertion might have succeeded." -ForegroundColor DarkGray
}

Write-Host "`n==========================================" -ForegroundColor Cyan
Write-Host " STRUCTURE AND DATA SEEDING COMPLETE    " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Read-Host "Press Enter to exit..."
