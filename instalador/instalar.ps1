# Instala CuotasClub para el usuario actual (sin permisos de administrador).
# Lo ejecuta "Instalar CuotasClub.bat". Los datos no se tocan: viven en %LOCALAPPDATA%\CuotasClub.
$ErrorActionPreference = 'Stop'
$origen  = $PSScriptRoot
$destino = Join-Path $env:LOCALAPPDATA 'Programs\CuotasClub'
try {
    if (Get-Process -Name CuotasClub -ErrorAction SilentlyContinue) {
        throw 'CuotasClub está abierto. Cerralo y volvé a hacer doble clic en "Instalar CuotasClub".'
    }
    Write-Host 'Instalando CuotasClub...'
    New-Item -ItemType Directory -Force -Path $destino | Out-Null
    if ((Resolve-Path $origen).Path.TrimEnd('\') -ne (Resolve-Path $destino).Path.TrimEnd('\')) {
        Copy-Item -Path (Join-Path $origen '*') -Destination $destino -Recurse -Force
    }
    # Archivos bajados de internet: se desbloquean para que Windows no pregunte cada vez.
    Get-ChildItem -Path $destino -Recurse -File | Unblock-File

    $exe = Join-Path $destino 'CuotasClub.exe'
    $shell = New-Object -ComObject WScript.Shell
    $accesos = @(
        (Join-Path ([Environment]::GetFolderPath('Programs')) 'CuotasClub.lnk'),
        (Join-Path ([Environment]::GetFolderPath('Desktop'))  'CuotasClub.lnk')
    )
    foreach ($lnk in $accesos) {
        $a = $shell.CreateShortcut($lnk)
        $a.TargetPath = $exe
        $a.WorkingDirectory = $destino
        $a.IconLocation = "$exe,0"
        $a.Description = 'Cuotas de las clases del club'
        $a.Save()
    }

    # Para que aparezca en Configuración de Windows > Aplicaciones (y se pueda desinstalar desde ahí).
    $reg = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\CuotasClub'
    New-Item -Path $reg -Force | Out-Null
    $desinstalar = Join-Path $destino 'desinstalar.ps1'
    Set-ItemProperty -Path $reg -Name DisplayName     -Value 'CuotasClub'
    Set-ItemProperty -Path $reg -Name DisplayVersion  -Value '1.0.0'
    Set-ItemProperty -Path $reg -Name Publisher       -Value 'CuotasClub'
    Set-ItemProperty -Path $reg -Name DisplayIcon     -Value $exe
    Set-ItemProperty -Path $reg -Name InstallLocation -Value $destino
    Set-ItemProperty -Path $reg -Name UninstallString -Value "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$desinstalar`""
    Set-ItemProperty -Path $reg -Name NoModify -Value 1 -Type DWord
    Set-ItemProperty -Path $reg -Name NoRepair -Value 1 -Type DWord

    Write-Host ''
    Write-Host 'Listo. CuotasClub quedó instalado: lo encontrás en el menú Inicio y en el escritorio.' -ForegroundColor Green
    Write-Host 'Ya podés borrar la carpeta que descomprimiste.'
    Start-Process -FilePath $exe -WorkingDirectory $destino
    Start-Sleep -Seconds 4
}
catch {
    Write-Host ''
    Write-Host "No se pudo instalar: $($_.Exception.Message)" -ForegroundColor Red
    Read-Host 'Tocá Enter para cerrar'
    exit 1
}
