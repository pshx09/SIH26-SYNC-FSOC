param ()

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "      FSOC TRACKING SIMULATOR STOP      " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# ----------------------------------------------------
# 1. FastAPI (Port 8000)
# ----------------------------------------------------
$FastApiPort = 8000
$FastApiListener = Get-NetTCPConnection -LocalPort $FastApiPort -State Listen -ErrorAction SilentlyContinue

if ($FastApiListener) {
    $FastApiPid = $FastApiListener.OwningProcess
    Write-Host "[FastAPI] Found running on port $FastApiPort (PID: $FastApiPid)" -ForegroundColor Yellow
    Write-Host "          Stopping FastAPI..." -ForegroundColor Yellow
    Stop-Process -Id $FastApiPid -Force -ErrorAction SilentlyContinue
    Write-Host "          Stopped." -ForegroundColor Green
} else {
    Write-Host "[FastAPI] Not running on port $FastApiPort." -ForegroundColor DarkGray
}

# ----------------------------------------------------
# 2. React (Port 5173)
# ----------------------------------------------------
$ReactPort = 5173
$ReactListener = Get-NetTCPConnection -LocalPort $ReactPort -State Listen -ErrorAction SilentlyContinue

if ($ReactListener) {
    $ReactPid = $ReactListener.OwningProcess
    Write-Host "[React]   Found running on port $ReactPort (PID: $ReactPid)" -ForegroundColor Yellow
    Write-Host "          Stopping React process..." -ForegroundColor Yellow
    
    # Note: npm run dev spawns child node processes. We can try to kill the node process running on 5173.
    Stop-Process -Id $ReactPid -Force -ErrorAction SilentlyContinue
    Write-Host "          Stopped." -ForegroundColor Green
} else {
    Write-Host "[React]   Not running on port $ReactPort." -ForegroundColor DarkGray
}

# ----------------------------------------------------
# 3. Unity 
# ----------------------------------------------------
$UnityProcesses = Get-CimInstance Win32_Process | Where-Object { $_.Name -match "Unity\.exe$" -and $_.CommandLine -match "FSOC_Tracking_Simulator" }

if ($UnityProcesses) {
    Write-Host "[Unity]   Found Unity project open:" -ForegroundColor Yellow
    foreach ($proc in $UnityProcesses) {
        Write-Host "          PID: $($proc.ProcessId) - $($proc.CommandLine)" -ForegroundColor Yellow
    }
    Write-Host ""
    Write-Host "          WARNING: Automatically killing Unity can corrupt the project or scene." -ForegroundColor Red
    Write-Host "          Please close the Unity Editor manually if it is open." -ForegroundColor Red
    # We do NOT kill Unity automatically.
} else {
    Write-Host "[Unity]   No matching Unity Editor found." -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
