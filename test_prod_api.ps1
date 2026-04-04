$baseUrl = "https://universitymanagementsystem-production-e58e.up.railway.app/api"
$email = "admin1@benisuefnationaluniversity.edu"
$password = "gqu9f*XJJ@Sm"

Write-Host "============================="
Write-Host " TESTING PRODUCTION API      "
Write-Host "============================="
Write-Host ""

# 1. Login
Write-Host "1. Testing Login ($baseUrl/Auth/login)"
$loginBody = @{
    email = $email
    password = $password
} | ConvertTo-Json

try {
    $loginResponse = Invoke-RestMethod -Uri "$baseUrl/Auth/login" -Method Post -Body $loginBody -ContentType "application/json"
    
    if ($loginResponse.success -and $loginResponse.data -and $loginResponse.data.token) {
        $token = $loginResponse.data.token
        Write-Host "   [SUCCESS] Logged in successfully. Token acquired." -ForegroundColor Green
    } else {
        Write-Host "   [FAILED] Login succeeded but no token found." -ForegroundColor Red
        Read-Host "Press Enter to close..."
        exit
    }
} catch {
    Write-Host "   [FAILED] Login failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails) {
        Write-Host "   Details: $($_.ErrorDetails.Message)" -ForegroundColor Red
    }
    Read-Host "Press Enter to close..."
    exit
}

$headers = @{
    Authorization = "Bearer $token"
}

# 2. Check Students
Write-Host "2. Testing GET /api/Students"
try {
    $studentsResponse = Invoke-RestMethod -Uri "$baseUrl/Students" -Method Get -Headers $headers
    Write-Host "   [SUCCESS] Students API is working." -ForegroundColor Green
} catch {
    Write-Host "   [FAILED] Students API failed: $($_.Exception.Message)" -ForegroundColor Red
}

# 3. Check Doctors
Write-Host "3. Testing GET /api/Doctors"
try {
    $doctorsResponse = Invoke-RestMethod -Uri "$baseUrl/Doctors" -Method Get -Headers $headers
    Write-Host "   [SUCCESS] Doctors API is working." -ForegroundColor Green
} catch {
    Write-Host "   [FAILED] Doctors API failed: $($_.Exception.Message)" -ForegroundColor Red
}

# 4. Check Regulations
Write-Host "4. Testing GET /api/Regulations"
try {
    $regResponse = Invoke-RestMethod -Uri "$baseUrl/Regulations" -Method Get -Headers $headers
    Write-Host "   [SUCCESS] Regulations API is working." -ForegroundColor Green
} catch {
    Write-Host "   [FAILED] Regulations API failed: $($_.Exception.Message)" -ForegroundColor Red
}

# 5. Check Colleges
Write-Host "5. Testing GET /api/Colleges"
try {
    $collegesResponse = Invoke-RestMethod -Uri "$baseUrl/Colleges" -Method Get -Headers $headers
    Write-Host "   [SUCCESS] Colleges API is working." -ForegroundColor Green
} catch {
    Write-Host "   [FAILED] Colleges API failed: $($_.Exception.Message)" -ForegroundColor Red
}

# 6. Check FileController GetMyFiles
Write-Host "6. Testing GET /api/File"
try {
    $fileResponse = Invoke-RestMethod -Uri "$baseUrl/File" -Method Get -Headers $headers
    Write-Host "   [SUCCESS] Files API is working." -ForegroundColor Green
} catch {
    Write-Host "   [FAILED] Files API failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "============================="
Write-Host " TESTING COMPLETED           "
Write-Host "============================="
Read-Host "Press Enter to close..."
