$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$baseUrl = "https://universitymanagementsystem-production-e58e.up.railway.app/api"
$adminEmail = "admin1@benisuefnationaluniversity.edu"
$adminPass = "gqu9f*XJJ@Sm"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " FULL LIFECYCLE QA TEST (ADMIN -> DOCTOR -> STUDENT) " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Login as Admin
$loginBody = @{ email = $adminEmail; password = $adminPass } | ConvertTo-Json
try {
    $adResp = Invoke-RestMethod -Uri "$baseUrl/Auth/login" -Method Post -Body $loginBody -ContentType "application/json"
    $adToken = $adResp.data.token
    Write-Host "[SUCCESS] Logged in as Admin ($adminEmail)." -ForegroundColor Green
} catch {
    Write-Host "[FAILED] Admin login failed. $($_.Exception.Message)" -ForegroundColor Red
    exit
}
$adHeaders = @{ Authorization = "Bearer $adToken"; "Content-Type" = "application/json" }

$ts = Get-Date -Format "yyMMddHHmmss"

# 2. Setup Academic Records 
Write-Host "`n-- Setting up Academic Records --"
$depCode = "UI_DEP"
$batchCode = "UI_BAT"
$groupCode = "UI_GRP"
$colCode = "UI_COL"

# Academic Year
try {
    $yrBody = @{ name = "Year $ts"; isActive = $true } | ConvertTo-Json
    $yrRes = Invoke-RestMethod -Uri "$baseUrl/AcademicYears" -Method Post -Body $yrBody -Headers $adHeaders
    $yrId = $yrRes.id
} catch { Write-Host "[FAILED] AcademicYears: $($_.Exception.Message)" -ForegroundColor Red; exit }

# Semester
try {
    $semBody = @{ name = "Semester $ts"; academicYearId = $yrId; startDate = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssZ"); endDate = (Get-Date).AddMonths(4).ToString("yyyy-MM-ddTHH:mm:ssZ") } | ConvertTo-Json
    $semRes = Invoke-RestMethod -Uri "$baseUrl/Semesters" -Method Post -Body $semBody -Headers $adHeaders
    $semId = $semRes.id
} catch { Write-Host "[FAILED] Semesters: $($_.Exception.Message)" -ForegroundColor Red; exit }

# Subject
try {
    $subCode = "CS_$ts"
    $subBody = @{ name = "Computer Science $ts"; code = $subCode; departmentCode = $depCode; batchCode = $batchCode; collegeCode = $colCode } | ConvertTo-Json
    $subRes = Invoke-RestMethod -Uri "$baseUrl/Subjects" -Method Post -Body $subBody -Headers $adHeaders
    $subId = $subRes.id
} catch { Write-Host "[FAILED] Subjects: $($_.Exception.Message)" -ForegroundColor Red; exit }

Write-Host "[SUCCESS] Created Year, Semester, and Subject." -ForegroundColor Green

# 3. Create Doctor and Student
Write-Host "`n-- Creating Doctor and Student --"
try {
    $docBody = @{ fullName = "Dr. Robot $ts"; departmentCode = $depCode; nationalId = "2$ts"; phone = "010$ts".Substring(0,11) } | ConvertTo-Json
    $docRes = Invoke-RestMethod -Uri "$baseUrl/Auth/register/doctor" -Method Post -Body $docBody -Headers $adHeaders
    $docEmail = $docRes.data.universityEmail
    $docPass = $docRes.data.temporaryPassword
    $docId = $docRes.data.userId

    $stuBody = @{ fullName = "Student Bot $ts"; collegeCode = $colCode; departmentCode = $depCode; batchCode = $batchCode; groupCode = $groupCode; nationalId = "3$ts"; phone = "011$ts".Substring(0,11) } | ConvertTo-Json
    $stuRes = Invoke-RestMethod -Uri "$baseUrl/Auth/register/student" -Method Post -Body $stuBody -Headers $adHeaders
    $stuEmail = $stuRes.data.universityEmail
    $stuPass = $stuRes.data.temporaryPassword
    $stuId = $stuRes.data.userId
    Write-Host "[SUCCESS] Created Doctor ($docEmail) and Student ($stuEmail)." -ForegroundColor Green
} catch { Write-Host "[FAILED] Auth Register: $($_.Exception.Message)" -ForegroundColor Red; exit }

# 4. Create Subject Offering
Write-Host "`n-- Creating Subject Offering --"
try {
    $offBody = @{ subjectId = $subId; semesterId = $semId; doctorId = $docId; departmentId = $subRes.departmentId; batchId = $subRes.batchId; maxCapacity = 100 } | ConvertTo-Json
    $offRes = Invoke-RestMethod -Uri "$baseUrl/SubjectOfferings" -Method Post -Body $offBody -Headers $adHeaders
    $offId = $offRes.id
    Write-Host "[SUCCESS] SubjectOffering Created." -ForegroundColor Green
} catch { Write-Host "[FAILED] SubjectOfferings: $($_.Exception.Message)" -ForegroundColor Red; exit }

# 5. Enroll Student
Write-Host "`n-- Enrolling Student --"
try {
    $enrBody = @{ studentId = $stuId; subjectOfferingId = $offId } | ConvertTo-Json
    $enrRes = Invoke-RestMethod -Uri "$baseUrl/Enrollments" -Method Post -Body $enrBody -Headers $adHeaders
    Write-Host "[SUCCESS] Student Enrolled." -ForegroundColor Green
} catch { Write-Host "[FAILED] Enrollments: $($_.Exception.Message)" -ForegroundColor Red; exit }

# 6. Doctor Login and Upload Material
Write-Host "`n-- Doctor Flow: Upload Material --"
try {
    $docLoginBody = @{ email = $docEmail; password = $docPass } | ConvertTo-Json
    $docLogin = Invoke-RestMethod -Uri "$baseUrl/Auth/login" -Method Post -Body $docLoginBody -ContentType "application/json"
    $docToken = $docLogin.data.token
    $docHeaders = @{ Authorization = "Bearer $docToken" }

    $boundary = [System.Guid]::NewGuid().ToString()
    $LF = "`r`n"
    $bodyLines = (
        "--$boundary",
        "Content-Disposition: form-data; name=`"OfferingId`"",
        "",
        "$offId",
        "--$boundary",
        "Content-Disposition: form-data; name=`"File`"; filename=`"dummy_video.mp4`"",
        "Content-Type: video/mp4",
        "",
        "DUMMY_BINARY_DATA_CORRUPT_PROOF",
        "--$boundary--"
    ) -join $LF

    $matRes = Invoke-RestMethod -Uri "$baseUrl/Materials/upload" -Method Post -Body $bodyLines -ContentType "multipart/form-data; boundary=$boundary" -Headers $docHeaders
    Write-Host "[SUCCESS] Doctor Uploaded Video Material (ID: $($matRes.id))." -ForegroundColor Green
} catch { Write-Host "[FAILED] Material upload failed: $($_.Exception.Message)" -ForegroundColor Red }

# 7. Doctor Creates Exam
Write-Host "`n-- Doctor Flow: Create Exam --"
try {
    $examBody = @{
        title = "Midterm $ts"
        type = 0
        startTime = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssZ")
        endTime = (Get-Date).AddHours(2).ToString("yyyy-MM-ddTHH:mm:ssZ")
        status = 1
        questions = @( @{ questionText = "What is 1+1?"; correctAnswer = "2"; mark = 10 } )
    } | ConvertTo-Json
    $exmRes = Invoke-RestMethod -Uri "$baseUrl/Exams?subjectOfferingId=$offId" -Method Post -Body $examBody -Headers $docHeaders -ContentType "application/json"
    $examId = $exmRes.id
    Write-Host "[SUCCESS] Doctor Created Exam (ID: $examId)." -ForegroundColor Green
} catch { Write-Host "[FAILED] Exam creation failed: $($_.Exception.Message)" -ForegroundColor Red }

# 8. Student Login and View Materials/Exams
Write-Host "`n-- Student Flow: View Materials and Exams --"
try {
    $stuLoginBody = @{ email = $stuEmail; password = $stuPass } | ConvertTo-Json
    $stuLogin = Invoke-RestMethod -Uri "$baseUrl/Auth/login" -Method Post -Body $stuLoginBody -ContentType "application/json"
    $stuToken = $stuLogin.data.token
    $stuHeaders = @{ Authorization = "Bearer $stuToken"; "Content-Type" = "application/json" }

    $matList = Invoke-RestMethod -Uri "$baseUrl/Materials/by-offering/$offId" -Method Get -Headers $stuHeaders
    Write-Host "[SUCCESS] Student retrieved $($matList.data.Count) materials for the offering." -ForegroundColor Green

    $exmList = Invoke-RestMethod -Uri "$baseUrl/Exams/by-offering/$offId" -Method Get -Headers $stuHeaders
    Write-Host "[SUCCESS] Student retrieved exams successfully." -ForegroundColor Green
} catch { Write-Host "[FAILED] Student Flow failed: $($_.Exception.Message)" -ForegroundColor Red }

# 9. Admin Calculates GPA
Write-Host "`n-- Admin Flow: Calculate GPA --"
try {
    $gpaRes = Invoke-RestMethod -Uri "$baseUrl/Gpa/student/$stuId/recalculate" -Method Post -Headers $adHeaders -ContentType "application/json" -Body "{}"
    Write-Host "[SUCCESS] GPA Recalculated: $($gpaRes.cumulativeGpa)" -ForegroundColor Green
} catch { Write-Host "[FAILED] GPA calculation failed: $($_.Exception.Message)" -ForegroundColor Red }

Write-Host "`n==========================================" -ForegroundColor Cyan
Write-Host " INTEGRATION TEST FINISHED " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
