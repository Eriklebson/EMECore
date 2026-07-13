# EME Core - Build para Microsoft Store
# Gera pacote MSIX para submissao no Partner Center

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $scriptDir

$projectPath = Join-Path $scriptDir "src\EMECore.WinUI\EMECore.WinUI.csproj"
$csprojBackup = Join-Path $scriptDir "src\EMECore.WinUI\EMECore.WinUI.csproj.bak"
$outputDir = Join-Path $scriptDir "store-build"
$appPackagesDir = Join-Path $scriptDir "src\EMECore.WinUI\AppPackages"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " E.M.E Core - Build para Store" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. Backup e preparar csproj para MSIX
Write-Host "[1/5] Preparando projeto para MSIX..." -ForegroundColor Yellow
Copy-Item $projectPath $csprojBackup -Force
$csproj = Get-Content $projectPath -Raw
$csproj = $csproj -replace '<WindowsPackageType>None</WindowsPackageType>', '<WindowsPackageType>MSIX</WindowsPackageType>'
$csproj = $csproj -replace '<EnableMsixTooling>false</EnableMsixTooling>', '<EnableMsixTooling>true</EnableMsixTooling>'
Set-Content $projectPath $csproj -NoNewline

try {
    # 2. Clean
    Write-Host "[2/5] Limpando builds anteriores..." -ForegroundColor Yellow
    if (Test-Path $outputDir) { Remove-Item $outputDir -Recurse -Force }
    if (Test-Path $appPackagesDir) { Remove-Item $appPackagesDir -Recurse -Force }
    Remove-Item "src\EMECore.WinUI\bin" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item "src\EMECore.WinUI\obj" -Recurse -Force -ErrorAction SilentlyContinue

    # 3. Build x64
    Write-Host "[3/5] Build x64 (MSIX)..." -ForegroundColor Yellow
    dotnet build $projectPath `
        -c Release `
        -p:Platform=x64 `
        -p:GenerateAppxPackageOnBuild=true `
        -p:AppxPackageSigningEnabled=false `
        -p:PublishReadyToRun=false
    if ($LASTEXITCODE -ne 0) { throw "Build x64 falhou" }

    # 4. Build x86
    Write-Host "[4/5] Build x86 (MSIX)..." -ForegroundColor Yellow
    dotnet build $projectPath `
        -c Release `
        -p:Platform=x86 `
        -p:GenerateAppxPackageOnBuild=true `
        -p:AppxPackageSigningEnabled=false `
        -p:PublishReadyToRun=false
    if ($LASTEXITCODE -ne 0) { throw "Build x86 falhou" }

    # 5. Copiar .msix e gerar bundle
    Write-Host "[5/5] Gerando pacotes..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

    $msixX64 = Get-ChildItem $appPackagesDir -Filter "*x64.msix" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    $msixX86 = Get-ChildItem $appPackagesDir -Filter "*x86.msix" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1

    if (-not $msixX64 -or -not $msixX86) {
        Write-Host "ERRO: Arquivos .msix nao encontrados em AppPackages" -ForegroundColor Red
        exit 1
    }

    Write-Host "  x64: $($msixX64.Name)" -ForegroundColor Gray
    Write-Host "  x86: $($msixX86.Name)" -ForegroundColor Gray

    Copy-Item $msixX64.FullName "$outputDir\EMECore_x64.msix"
    Copy-Item $msixX86.FullName "$outputDir\EMECore_x86.msix"

    # Procurar MakeAppx
    $makeAppx = Get-ChildItem "C:\Program Files*\Windows Kits\*\bin\*\x64\MakeAppx.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $makeAppx) {
        Write-Host "MakeAppx.exe nao encontrado - gerando apenas arquivos .msix individuais." -ForegroundColor Yellow
    } else {
        Write-Host "  Gerando EMECore.msixbundle..." -ForegroundColor Gray
        & $makeAppx.FullName bundle /d "$outputDir" /p "$outputDir\EMECore.msixbundle" /o
        if ($LASTEXITCODE -ne 0) { throw "MakeAppx bundle falhou" }

        Remove-Item "$outputDir\EMECore_x64.msix" -Force -ErrorAction SilentlyContinue
        Remove-Item "$outputDir\EMECore_x86.msix" -Force -ErrorAction SilentlyContinue

        $bundleSize = [math]::Round((Get-Item "$outputDir\EMECore.msixbundle").Length / 1MB, 1)
        Write-Host ""
        Write-Host "========================================" -ForegroundColor Green
        Write-Host " Build concluido com sucesso!" -ForegroundColor Green
        Write-Host " Tamanho: ${bundleSize} MB" -ForegroundColor Green
        Write-Host "========================================" -ForegroundColor Green
        Write-Host ""
        Write-Host "Arquivo: $outputDir\EMECore.msixbundle" -ForegroundColor White
    }

    Write-Host ""
    Write-Host "Proximo passo:" -ForegroundColor Yellow
    Write-Host "  1. Acesse https://partner.microsoft.com/dashboard" -ForegroundColor White
    Write-Host "  2. Apps e jogos > EME Core > Atualizacoes" -ForegroundColor White
    Write-Host "  3. Nova submissao > Envie o pacote MSIX" -ForegroundColor White

} finally {
    Write-Host ""
    Write-Host "Restaurando csproj original..." -ForegroundColor Gray
    Copy-Item $csprojBackup $projectPath -Force
    Remove-Item $csprojBackup -Force -ErrorAction SilentlyContinue
}
