# read-sensors.ps1 - Quick fallback read (GPU only, no admin needed)
# The background sensor-service.ps1 handles CPU + motherboard fans via elevation
# This script is used only as fallback when service isn't running

param([switch]$Elevated)

$dllDir = Join-Path $PSScriptRoot "LibreHardwareMonitor"
$dllPath = Join-Path $dllDir "LibreHardwareMonitorLib.dll"

if (-not (Test-Path $dllPath)) {
    Write-Output '{"cpu":null,"gpu":null,"fans":[]}'
    exit 0
}
try { Add-Type -Path $dllPath -ErrorAction Stop } catch {
    Write-Output '{"cpu":null,"gpu":null,"fans":[]}'
    exit 0
}

$computer = New-Object LibreHardwareMonitor.Hardware.Computer
$computer.IsCpuEnabled = $true
$computer.IsGpuEnabled = $true
try { $computer.Open() } catch {
    Write-Output '{"cpu":null,"gpu":null,"fans":[]}'
    exit 0
}

$cpuName = ""; $cpuTemp = 0
$gpuName = ""; $gpuTemp = $null; $gpuFan = $null; $gpuDuty = $null

foreach ($hw in $computer.Hardware) {
    $hw.Update()
    if ($hw.HardwareType -eq [LibreHardwareMonitor.Hardware.HardwareType]::Cpu) {
        $cpuName = $hw.Name
        foreach ($s in $hw.Sensors) {
            if ($s.SensorType -eq [LibreHardwareMonitor.Hardware.SensorType]::Temperature -and $s.Value -ne $null) {
                $v = [double]$s.Value
                if ($v -gt $cpuTemp) { $cpuTemp = $v }
            }
        }
    }
    $isGpu = ($hw.HardwareType -eq [LibreHardwareMonitor.Hardware.HardwareType]::GpuNvidia) -or
             ($hw.HardwareType -eq [LibreHardwareMonitor.Hardware.HardwareType]::GpuAmd) -or
             ($hw.HardwareType -eq [LibreHardwareMonitor.Hardware.HardwareType]::GpuIntel)
    if ($isGpu) {
        $gpuName = $hw.Name
        foreach ($s in $hw.Sensors) {
            if ($s.SensorType -eq [LibreHardwareMonitor.Hardware.SensorType]::Temperature -and $s.Value -ne $null) {
                $v = [double]$s.Value
                if ($v -gt 0 -and ($gpuTemp -eq $null -or $v -gt $gpuTemp)) { $gpuTemp = [math]::Round($v) }
            }
            if ($s.SensorType -eq [LibreHardwareMonitor.Hardware.SensorType]::Fan -and $s.Value -ne $null) {
                $r = [math]::Round([double]$s.Value)
                if ($r -gt 0) { $gpuFan = $r }
            }
            if ($s.SensorType -eq [LibreHardwareMonitor.Hardware.SensorType]::Control -and $s.Value -ne $null) {
                $gpuDuty = [math]::Round([double]$s.Value)
            }
        }
    }
}
$computer.Close()

$fans = @()
if ($gpuFan -gt 0) {
    $f = @{ name = "GPU Fan"; rpm = $gpuFan; hardware = "GPU" }
    if ($gpuDuty -ne $null) { $f.duty = $gpuDuty }
    $fans += $f
}

$output = [PSCustomObject]@{
    cpu = if ($cpuName -and $cpuTemp -gt 0) { [PSCustomObject]@{ name = $cpuName; temp = [math]::Round($cpuTemp) } } else { $null }
    gpu = if ($gpuName -and $gpuTemp -gt 0) { [PSCustomObject]@{ name = $gpuName; temp = $gpuTemp } } else { $null }
    fans = $fans
}
Write-Output ($output | ConvertTo-Json -Compress -Depth 5)
