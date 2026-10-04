param ()

$ProjectRoot = "C:\Users\PRIYANSHU SINHA\SIH\FSOC_Tracking_Simulator"
$BackendDir = "$ProjectRoot\dashboard\backend"
$FrontendDir = "$ProjectRoot\dashboard\frontend"
$UnityExe = "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "      FSOC TRACKING SIMULATOR           " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# ----------------------------------------------------
# 1. FastAPI (Port 8000)
# ----------------------------------------------------
$FastApiPort = 8000
$FastApiListener = Get-NetTCPConnection -LocalPort $FastApiPort -State Listen -ErrorAction SilentlyContinue

if ($FastApiListener) {
    Write-Host "[FastAPI] Already running on port $FastApiPort (PID: $($FastApiListener.OwningProcess))" -ForegroundColor Yellow
} else {
    Write-Host "[FastAPI] Starting FastAPI on port $FastApiPort..." -ForegroundColor Green
    Start-Process -FilePath "cmd.exe" -ArgumentList "/k", "cd /d `"$BackendDir`" && .\venv\Scripts\python.exe -m uvicorn main:app --host 127.0.0.1 --port 8000" -WindowStyle Normal
}

# ----------------------------------------------------
# 2. React (Port 5173)
# ----------------------------------------------------
$ReactPort = 5173
$ReactListener = Get-NetTCPConnection -LocalPort $ReactPort -State Listen -ErrorAction SilentlyContinue

if ($ReactListener) {
    Write-Host "[React]   Already running on port $ReactPort (PID: $($ReactListener.OwningProcess))" -ForegroundColor Yellow
} else {
    Write-Host "[React]   Starting React frontend on port $ReactPort..." -ForegroundColor Green
    Start-Process -FilePath "cmd.exe" -ArgumentList "/k", "cd /d `"$FrontendDir`" && npm run dev" -WindowStyle Normal
}

# ----------------------------------------------------
# 3. Unity 
# ----------------------------------------------------
# Check if Unity is already running for this project
$UnityProcesses = Get-CimInstance Win32_Process | Where-Object { $_.Name -match "Unity\.exe$" -and $_.CommandLine -match "FSOC_Tracking_Simulator" }

if ($UnityProcesses) {
    $PidList = ($UnityProcesses | Select-Object -ExpandProperty ProcessId) -join ", "
    Write-Host "[Unity]   Project already open (PID: $PidList)" -ForegroundColor Yellow
} else {
    Write-Host "[Unity]   Launching Unity project (Graphical Mode)..." -ForegroundColor Green
    Start-Process -FilePath $UnityExe -ArgumentList "-projectPath", "`"$ProjectRoot`"" -WindowStyle Normal
}

Write-Host ""
Write-Host "Waiting briefly for processes to initialize..."
Start-Sleep -Seconds 3

# ----------------------------------------------------
# Final Status
# ----------------------------------------------------
Write-Host ""
Write-Host "FSOC STACK STATUS" -ForegroundColor Cyan
Write-Host "-----------------" -ForegroundColor Cyan

$FinalFastApi = Get-NetTCPConnection -LocalPort $FastApiPort -State Listen -ErrorAction SilentlyContinue
if ($FinalFastApi) { Write-Host "FastAPI : RUNNING" -ForegroundColor Green } else { Write-Host "FastAPI : OFFLINE" -ForegroundColor Red }

$FinalReact = Get-NetTCPConnection -LocalPort $ReactPort -State Listen -ErrorAction SilentlyContinue
if ($FinalReact) { Write-Host "React   : RUNNING" -ForegroundColor Green } else { Write-Host "React   : OFFLINE" -ForegroundColor Red }

$FinalUnity = Get-CimInstance Win32_Process | Where-Object { $_.Name -match "Unity\.exe$" -and $_.CommandLine -match "FSOC_Tracking_Simulator" }
if ($FinalUnity) { Write-Host "Unity   : RUNNING" -ForegroundColor Green } else { Write-Host "Unity   : OFFLINE" -ForegroundColor Red }

Write-Host ""
Write-Host "URLs:" -ForegroundColor Cyan
Write-Host "http://127.0.0.1:8000"
Write-Host "http://127.0.0.1:8000/health"
Write-Host "http://127.0.0.1:5173"
Write-Host "========================================" -ForegroundColor Cyan
