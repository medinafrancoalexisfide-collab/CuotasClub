# Desinstala CuotasClub. Tus datos (alumnos, pagos, copias, recibos) NO se borran.
$destino = $PSScriptRoot
Add-Type -AssemblyName System.Windows.Forms
$r = [System.Windows.Forms.MessageBox]::Show(
    "¿Desinstalar CuotasClub?`n`nTus datos no se borran: quedan en $env:LOCALAPPDATA\CuotasClub y las copias y recibos en Documentos\CuotasClub. Si lo volvés a instalar, aparecen solos.",
    'CuotasClub', 'YesNo', 'Question')
if ($r -ne 'Yes') { exit 0 }
if (Get-Process -Name CuotasClub -ErrorAction SilentlyContinue) {
    [System.Windows.Forms.MessageBox]::Show('CuotasClub está abierto. Cerralo y volvé a desinstalar.', 'CuotasClub') | Out-Null
    exit 1
}
Remove-Item -Force -ErrorAction SilentlyContinue -Path `
    (Join-Path ([Environment]::GetFolderPath('Programs')) 'CuotasClub.lnk'),
    (Join-Path ([Environment]::GetFolderPath('Desktop'))  'CuotasClub.lnk')
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\CuotasClub'
Set-Location $env:TEMP
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue -Path $destino
[System.Windows.Forms.MessageBox]::Show('CuotasClub se desinstaló. Tus datos siguen guardados.', 'CuotasClub') | Out-Null
