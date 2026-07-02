$dllPath = Join-Path $PSScriptRoot "LibreHardwareMonitor\LibreHardwareMonitorLib.dll"
Add-Type -Path $dllPath
$outFile = Join-Path $env:TEMP "cpu-check-result.txt"
$log = @()

$computer = New-Object LibreHardwareMonitor.Hardware.Computer
$computer.IsCpuEnabled = $true
$computer.IsGpuEnabled = $true
$computer.IsMotherboardEnabled = $true
$computer.Open()

for ($i = 0; $i -lt 3; $i++) {
    foreach ($hw in $computer.Hardware) { $hw.Update(); foreach ($sub in $hw.SubHardware) { $sub.Update() } }
    Start-Sleep -Milliseconds 500
}

$cpuTemp = 0
$cpuPackageTemp = 0
$cpuName = ""
foreach ($hw in $computer.Hardware) {
    if ($hw.HardwareType -eq [LibreHardwareMonitor.Hardware.HardwareType]::Cpu) {
        $cpuName = $hw.Name
        foreach ($s in $hw.Sensors) {
            if ($s.SensorType -eq [LibreHardwareMonitor.Hardware.SensorType]::Temperature -and $s.Value -ne $null) {
                $log += "CPU Temp sensor: $($s.Name) = $($s.Value)"
                if ($s.Value -gt 0) {
                    if ($s.Name -like "*CCD*") { $cpuTemp = [math]::Round($s.Value, 1) }
                    elseif ($s.Name -like "*Tctl*") { $cpuPackageTemp = [math]::Round($s.Value, 1) }
                }
            }
        }
        # Fallback: first temp = CPU, second = Package
        if ($cpuTemp -eq 0) {
            foreach ($s in $hw.Sensors) {
                if ($s.SensorType -eq [LibreHardwareMonitor.Hardware.SensorType]::Temperature -and $s.Value -ne $null -and $s.Value -gt 0) {
                    $cpuTemp = [math]::Round($s.Value, 1)
                    break
                }
            }
        }
        foreach ($sub in $hw.SubHardware) {
            foreach ($s in $sub.Sensors) {
                if ($s.SensorType -eq [LibreHardwareMonitor.Hardware.SensorType]::Temperature -and $s.Value -ne $null) {
                    $log += "CPU SubHardware Temp: $($s.Name) = $($s.Value)"
                }
            }
        }
    }
    if ($hw.HardwareType -eq [LibreHardwareMonitor.Hardware.HardwareType]::Motherboard) {
        $log += "Motherboard: $($hw.Name)"
        foreach ($sub in $hw.SubHardware) {
            $log += "  SubHardware: $($sub.Name)"
            foreach ($s in $sub.Sensors) {
                if ($s.Value -ne $null -and $s.Value -ne 0) {
                    $log += "    $($s.SensorType): $($s.Name) = $([math]::Round($s.Value,1))"
                }
            }
        }
    }
}

$log += "CPU: $cpuName"
$log += "CPU Temp: $cpuTemp"
$log += "CPU Package: $cpuPackageTemp"

foreach ($hw in $computer.Hardware) {
    if ($hw.HardwareType -eq [LibreHardwareMonitor.Hardware.HardwareType]::GpuNvidia -or $hw.HardwareType -eq [LibreHardwareMonitor.Hardware.HardwareType]::GpuAmd) {
        foreach ($s in $hw.Sensors) {
            if ($s.SensorType -eq [LibreHardwareMonitor.Hardware.SensorType]::Fan -and $s.Value -ne $null -and $s.Value -gt 0) {
                $log += "FAN: GPU Fan=$([math]::Round($s.Value))"
            }
        }
    }
    foreach ($sub in $hw.SubHardware) {
        foreach ($s in $sub.Sensors) {
            if ($s.SensorType -eq [LibreHardwareMonitor.Hardware.SensorType]::Fan -and $s.Value -ne $null -and $s.Value -gt 0) {
                $log += "FAN: $($s.Name)=$([math]::Round($s.Value))"
            }
        }
    }
}

$log | Out-File $outFile
$computer.Close()
