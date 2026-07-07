# E.M.E Core Hardware Collector
# Runs in background, collects all hardware data to a cache file

$outFile = Join-Path $env:TEMP "eme_hw_cache.json"

while ($true) {
    try {
        $data = @{}

        # CPU
        $cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
        $data.cpuName = $cpu.Name.Trim()
        $data.cpuLoad = [int]$cpu.LoadPercentage

        # GPU
        $gpu = Get-CimInstance Win32_VideoController | Select-Object -First 1
        $data.gpuName = $gpu.Name
        $data.gpuLoad = 0
        $data.gpuTemp = 0
        
        # GPU usage via WMI (works on any GPU)
        if ($gpu.LoadPercentage) { $data.gpuLoad = [int]$gpu.LoadPercentage }
        
        # GPU temp via nvidia-smi (NVIDIA only, fails silently otherwise)
        if ($gpu.Name -match "NVIDIA") {
            try {
                $smi = & "$env:SystemRoot\System32\nvidia-smi.exe" --query-gpu=utilization.gpu,temperature.gpu --format=csv,noheader 2>$null
                if ($smi -match '(\d+) %, (\d+)') {
                    $data.gpuLoad = [int]$Matches[1]
                    $data.gpuTemp = [int]$Matches[2]
                }
            } catch { }
        }

        # RAM
        $os = Get-CimInstance Win32_OperatingSystem
        $ramModules = Get-CimInstance Win32_PhysicalMemory
        $ram = $ramModules | Select-Object -First 1
        $data.ramTotal = [math]::Round([long]$os.TotalVisibleMemorySize * 1024 / 1GB, 1)
        $data.ramFree = [math]::Round([long]$os.FreePhysicalMemory * 1024 / 1GB, 1)
        $data.ramUsed = [math]::Round($data.ramTotal - $data.ramFree, 1)
        $data.ramPct = if ($data.ramTotal -gt 0) { [math]::Round($data.ramUsed / $data.ramTotal * 100, 0) } else { 0 }
        $data.ramModel = "$($ram.Manufacturer) $($ram.PartNumber)".Trim()
        $data.ramSpeed = [int]$ram.ConfiguredClockSpeed
        $data.ramModuleCount = $ramModules.Count
        $data.ramModuleSize = [math]::Round([long]$ram.Capacity / 1GB, 0)
        $data.ramVoltage = if ($ram.ConfiguredVoltage) { [math]::Round($ram.ConfiguredVoltage / 1000.0, 1) } else { 0 }

        # Motherboard
        $mb = Get-CimInstance Win32_BaseBoard
        $data.mbModel = $mb.Product

        # Disk C:
        $disk = Get-CimInstance Win32_LogicalDisk | Where-Object { $_.DeviceID -eq 'C:' }
        if ($disk) {
            $data.diskTotal = [math]::Round([long]$disk.Size / 1GB, 1)
            $data.diskFree = [math]::Round([long]$disk.FreeSpace / 1GB, 1)
            $data.diskUsed = [math]::Round($data.diskTotal - $data.diskFree, 1)
            $data.diskPct = if ($data.diskTotal -gt 0) { [math]::Round($data.diskUsed / $data.diskTotal * 100, 0) } else { 0 }
        }

        # Disk Name
        $data.diskName = (Get-PhysicalDisk | Select-Object -First 1).Model

        # Disk Speed
        $diskPerf = Get-CimInstance Win32_PerfFormattedData_PerfDisk_PhysicalDisk | Where-Object { $_.Name -eq '_Total' }
        if ($diskPerf) {
            $data.diskReadKB = [math]::Round([long]$diskPerf.DiskReadBytesPersec / 1KB, 1)
            $data.diskWriteKB = [math]::Round([long]$diskPerf.DiskWriteBytesPersec / 1KB, 1)
        }

        # Network - find the active non-virtual adapter
        $net = Get-NetAdapter | Where-Object { 
            $_.Status -eq 'Up' -and 
            $_.InterfaceDescription -notmatch 'Loopback|Tunnel|VPN|Virtual|Remote NDIS' -and
            $_.MediaType -ne 'Unspecified'
        } | Select-Object -First 1
        
        # Fallback: if no adapter found, try any active adapter
        if (-not $net) {
            $net = Get-NetAdapter | Where-Object { $_.Status -eq 'Up' } | Select-Object -First 1
        }
        
        if ($net) {
            $data.netName = $net.InterfaceDescription
            $netStats = Get-NetAdapterStatistics | Where-Object { $_.Name -eq $net.Name } | Select-Object -First 1
            if ($netStats) {
                $data.netReceived = [long]$netStats.ReceivedBytes
                $data.netSent = [long]$netStats.SentBytes
            }
        }

        # Temperatures via MSAcpi_ThermalZoneTemperature (requires admin)
        try {
            $thermalZones = Get-CimInstance MSAcpi_ThermalZoneTemperature -Namespace "root/wmi" -ErrorAction SilentlyContinue
            if ($thermalZones) {
                $cpuTemp = ($thermalZones | Select-Object -First 1).CurrentTemperature
                if ($cpuTemp -and $cpuTemp -gt 2732) {
                    $data.cpuTemp = [math]::Round(($cpuTemp - 2732) / 10.0, 1)
                }
            }
        } catch {}

        # Convert to JSON and save
        $json = $data | ConvertTo-Json -Compress
        [System.IO.File]::WriteAllText($outFile, $json, [System.Text.Encoding]::UTF8)
    }
    catch {
        # Silently continue on error
    }

    Start-Sleep -Seconds 3
}
