$baseUrl = "http://localhost:5200" # Adjust port if necessary

# 1. Login as SuperAdmin
$loginUrl = "$baseUrl/api/Auth/login"
$loginBody = @{
    email    = "super.admin@university.com" # Assuming this is the Seeded SuperAdmin
    password = "SuperSecretPass1!"     # Assuming this is the Seeded Password
} | ConvertTo-Json

Write-Host "Logging in..."
try {
    $loginResponse = Invoke-RestMethod -Uri $loginUrl -Method Post -Body $loginBody -ContentType "application/json" -ErrorAction Stop
    $token = $loginResponse.token
    Write-Host "Login Successful. Token received."
    
    # Decode Token
    $payload = $token.Split(".")[1]
    # Add padding if missing
    switch ($payload.Length % 4) {
        2 { $payload += "==" }
        3 { $payload += "=" }
    }
    $decodedBytes = [System.Convert]::FromBase64String($payload)
    $decodedString = [System.Text.Encoding]::UTF8.GetString($decodedBytes)
    Write-Host "Token Payload: $decodedString"
}
catch {
    Write-Host "Login Failed: $_"
    exit
}

# 2. Test Protected Endpoint
$adminUrl = "$baseUrl/api/Auth/register/admin"
# Just testing validation, so body can be minimal/invalid, we just want to NOT see 401
$adminBody = @{
    fullName   = "Test Admin"
    nationalId = "12345678901234"
    phone      = "1234567890"
} | ConvertTo-Json

$headers = @{
    Authorization = "Bearer $token"
}

Write-Host "Testing Protected Endpoint: $adminUrl"
try {
    $response = Invoke-RestMethod -Uri $adminUrl -Method Post -Body $adminBody -Headers $headers -ContentType "application/json" -ErrorAction Stop
    Write-Host "Success! Endpoint accessed (Response might be 200 or 400 validation error, but NOT 401)."
    Write-Host "Response: $($response | ConvertTo-Json -Depth 5)"
}
catch {
    if ($_.Exception.Response.StatusCode -eq "Unauthorized") {
        Write-Error "FAILURE: Received 401 Unauthorized."
    }
    elseif ($_.Exception.Response.StatusCode -eq "BadRequest") {
        Write-Host "Success! Received 400 Bad Request (which means Authorization passed)."
        $streamReader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
        $errorBody = $streamReader.ReadToEnd()
        Write-Host "Validation Errors: $errorBody"
    }
    else {
        Write-Host "Status Code: $($_.Exception.Response.StatusCode)"
        $streamReader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
        $errorBody = $streamReader.ReadToEnd()
        Write-Host "Response Body: $errorBody"
    }
}

# 3. Test GetMe Debug Endpoint
$meUrl = "$baseUrl/api/Auth/me"
Write-Host "Testing GetMe Endpoint: $meUrl"
try {
    $response = Invoke-RestMethod -Uri $meUrl -Method Get -Headers $headers -ContentType "application/json" -ErrorAction Stop
    Write-Host "GetMe Success! Claims:"
    $response | ConvertTo-Json -Depth 5
}
catch {
    Write-Host "GetMe Failed: $($_.Exception.Response.StatusCode)"
    $streamReader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
    Write-Host "Response: $($streamReader.ReadToEnd())"
}
