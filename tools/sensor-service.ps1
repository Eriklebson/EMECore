# sensor-service.ps1 - Runs elevated in background, writes sensor data every 1 second
# Started by main.ts when monitor opens, killed when monitor closes
# Writes to: %TEMP%\lhm-sensors-cache.json

$dllDir = Join-Path $PSScriptRoot "LibreHardwareMonitor"
$dllPath = Join-Path $dllDir "LibreHardwareMonitorLib.dll"
$outFile = Join-Path $env:TEMP "lhm-sensors-cache.json"
$stopFile = Join-Path $env:TEMP "lhm-sensor-service.stop"

# ASUS TUF GAMING B550M-PLUS fan channel mapping (confirmed via Armoury Crate)
$FanNameMap = @{
    "Fan #1" = "Cha_1"
    "Fan #2" = "CPU"
    "Fan #3" = "Cha_2"
    "Fan #4" = "Cha_3"
    "Fan #5" = "AIO"
    "Fan #6" = "Bomba WC"
    "Fan #7" = "Bomba WC"
}

# Max RPM for pump-type fans (used to calculate relative duty)
# Headers report 100% because ASUS BIOS sets DC full power for pumps
$PumpMaxRpm = @{
    "Bomba WC" = 4300
    "AIO" = 4300
}

# Remove stop file on start
Remove-Item -Path $stopFile -Force -ErrorAction SilentlyContinue

if (-not (Test-Path $dllPath)) { exit 1 }
try { Add-Type -Path $dllPath -ErrorAction Stop } catch { exit 1 }

# Keep Computer open for the entire lifetime
$computer = New-Object LibreHardwareMonitor.Hardware.Computer
$computer.IsMotherboardEnabled = $true
$computer.IsCpuEnabled = $true
$computer.IsGpuEnabled = $true
try { $computer.Open() } catch { exit 1 }

# Main loop - update sensors every 1 second
while ($true) {
    if (Test-Path $stopFile) { break }

    try {
        # Update all hardware (re-reads sensor values)
        foreach ($hw in $computer.Hardware) {
            $hw.Update()
            foreach ($sub in $hw.SubHardware) { $sub.Update() }
        }

        $cpuName = ""; $cpuTemp = 0; $cpuPackageTemp = 0
        $gpuName = ""; $gpuTemp = $null
        $fans = @()

        foreach ($hw in $computer.Hardware) {
            if ($hw.HardwareType -eq [LibreHardwareMonitor.Hardware.HardwareType]::Cpu) {
                $cpuName = $hw.Name
                foreach ($s in $hw.Sensors) {
                    if ($s.SensorType -eq [LibreHardwareMonitor.Hardware.SensorType]::Temperature -and $s.Value -ne $null) {
                        $v = [double]$s.Value
                        if ($v -le 0) { continue }
                        # CCD1 (Tdie) - actual die temperature (lower)
                        if ($s.Name -match "^CCD\d") {
                            if ($v -gt $cpuTemp) { $cpuTemp = $v }
                        }
                        # Core (Tctl/Tdie) - package/thermal limit temperature (higher)
                        if ($s.Name -match "^Core") {
                            if ($v -gt $cpuPackageTemp) { $cpuPackageTemp = $v }
                        }
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
                }
                foreach ($s in $hw.Sensors) {
                    if ($s.SensorType -eq [LibreHardwareMonitor.Hardware.SensorType]::Fan -and $s.Value -ne $null) {
                        $rpm = [math]::Round([double]$s.Value)
                        if ($rpm -gt 0) { $fans += @{ name = "GPU Fan"; rpm = $rpm; hardware = "GPU" } }
                    }
                }
                foreach ($s in $hw.Sensors) {
                    if ($s.SensorType -eq [LibreHardwareMonitor.Hardware.SensorType]::Control -and $s.Value -ne $null) {
                        $duty = [math]::Round([double]$s.Value)
                        for ($i = 0; $i -lt $fans.Count; $i++) {
                            if ($fans[$i].hardware -eq "GPU") { $fans[$i].duty = $duty; break }
                        }
                    }
                }
            }

            foreach ($sub in $hw.SubHardware) {
                $subFans = @()
                foreach ($s in $sub.Sensors) {
                    if ($s.SensorType -eq [LibreHardwareMonitor.Hardware.SensorType]::Fan -and $s.Value -ne $null) {
                        $rpm = [math]::Round([double]$s.Value)
                        $name = if ($FanNameMap.ContainsKey($s.Name)) { $FanNameMap[$s.Name] } else { $s.Name }
                        $subFans += @{ name = $name; rpm = $rpm; rawName = $s.Name }
                    }
                }
                foreach ($s in $sub.Sensors) {
                    if ($s.SensorType -eq [LibreHardwareMonitor.Hardware.SensorType]::Control -and $s.Value -ne $null) {
                        $duty = [math]::Round([double]$s.Value)
                        for ($i = 0; $i -lt $subFans.Count; $i++) {
                            if ($subFans[$i].rawName -eq $s.Name) { $subFans[$i].duty = $duty; break }
                        }
                    }
                }
            foreach ($f in $subFans) {
                if ($f.rpm -gt 0) {
                    # Calculate relative duty for pump-type fans
                    $duty = $f.duty
                    if ($PumpMaxRpm.ContainsKey($f.name) -and $duty -eq 100) {
                        $maxRpm = $PumpMaxRpm[$f.name]
                        $duty = [math]::Min(100, [math]::Round(($f.rpm / $maxRpm) * 100))
                    }
                    $fans += @{ name = $f.name; rpm = $f.rpm; duty = $duty; hardware = "Motherboard" }
                }
            }
            }
        }

        # Deduplicate: if two fans have same name, keep the one with higher RPM
        $deduped = @()
        $seenNames = @{}
        foreach ($f in $fans) {
            if ($seenNames.ContainsKey($f.name)) {
                if ($f.rpm -gt $seenNames[$f.name].rpm) {
                    $deduped = $deduped | Where-Object { $_.name -ne $f.name }
                    $deduped += $f
                    $seenNames[$f.name] = $f
                }
            } else {
                $deduped += $f
                $seenNames[$f.name] = $f
            }
        }
        $fans = $deduped

        # Always write both cpu temps, even if one is 0
        $cpuObj = $null
        if ($cpuName -and ($cpuTemp -gt 0 -or $cpuPackageTemp -gt 0)) {
            $cpuObj = @{ name = $cpuName; temp = [math]::Round($cpuTemp); packageTemp = [math]::Round($cpuPackageTemp) }
        }

        $output = [PSCustomObject]@{
            cpu = $cpuObj
            gpu = if ($gpuName -and $gpuTemp -gt 0) { @{ name = $gpuName; temp = $gpuTemp } } else { $null }
            fans = $fans
        }
        $output | ConvertTo-Json -Compress -Depth 5 | Set-Content -Path $outFile -NoNewline -ErrorAction SilentlyContinue
    } catch {}

    Start-Sleep -Seconds 1

    if (Test-Path $stopFile) { break }
}

# Cleanup
$computer.Close()
Remove-Item -Path $stopFile -Force -ErrorAction SilentlyContinue
