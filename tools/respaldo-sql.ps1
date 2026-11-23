<#
.SYNOPSIS
  Copia de seguridad completa de la base de Nexo RH, con borrado de las copias más antiguas.

.DESCRIPTION
  Ejecuta BACKUP DATABASE con verificación (CHECKSUM + RESTORE VERIFYONLY) y deja un archivo
  NexoRh_AAAAMMDD_HHmm.bak en la carpeta indicada (por defecto, la carpeta de respaldos de la instancia).
  Borra los .bak de esa base con más de -DiasRetencion días. Pensado para una tarea programada diaria
  (Programador de tareas de Windows o el agente de SQL Server).

  La carpeta debe estar en el servidor de SQL Server y la cuenta del servicio de SQL Server debe poder escribir en ella.
  Copia también los archivos a otro lugar (otro disco o almacenamiento en la nube): una copia en el mismo servidor no
  protege si se pierde el servidor.

.EXAMPLE
  .\respaldo-sql.ps1 -Servidor '.\SQLEXPRESS' -Carpeta 'D:\Respaldos\NexoRh' -DiasRetencion 30
#>
param(
  [string]$Servidor = '.\SQLEXPRESS',
  [string]$BaseDatos = 'NexoRh',
  [string]$Carpeta = '',
  [int]$DiasRetencion = 30
)
$ErrorActionPreference = 'Stop'

if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) { throw 'No se encontró sqlcmd. Instala las herramientas de línea de comandos de SQL Server.' }
if ($BaseDatos -notmatch '^[A-Za-z0-9_]+$') { throw 'Nombre de base de datos no válido.' }

if (-not $Carpeta) {
  $Carpeta = (sqlcmd -S $Servidor -E -b -h -1 -W -Q "SET NOCOUNT ON; SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(400))").Trim()
}
$archivo = Join-Path $Carpeta ("{0}_{1}.bak" -f $BaseDatos, (Get-Date -Format 'yyyyMMdd_HHmm'))
$ruta = $archivo.Replace("'", "''")

Write-Host "Respaldando $BaseDatos en $archivo ..."
sqlcmd -S $Servidor -E -b -Q "BACKUP DATABASE [$BaseDatos] TO DISK = N'$ruta' WITH INIT, CHECKSUM, NAME = N'$BaseDatos completo'; RESTORE VERIFYONLY FROM DISK = N'$ruta' WITH CHECKSUM;"
if ($LASTEXITCODE -ne 0) { throw "La copia de seguridad falló (código $LASTEXITCODE)." }

if ((Test-Path $Carpeta) -and $DiasRetencion -gt 0) {
  $limite = (Get-Date).AddDays(-$DiasRetencion)
  Get-ChildItem -Path $Carpeta -Filter "${BaseDatos}_*.bak" | Where-Object { $_.LastWriteTime -lt $limite } | ForEach-Object {
    Write-Host "Borrando copia antigua $($_.Name)"
    Remove-Item $_.FullName
  }
}
Write-Host 'Copia de seguridad terminada y verificada.'
