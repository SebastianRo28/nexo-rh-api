<#
.SYNOPSIS
  Levanta Nexo RH en modo demo: compila el frontend y la API en una COPIA APARTE (artifacts/demo) y la sirve en
  http://localhost:5281 (un solo puerto), lista para compartirla con un túnel temporal
  (cloudflared tunnel --url http://localhost:5281).

.DESCRIPTION
  - La demo corre desde artifacts/demo: puedes seguir programando y compilando sin afectar lo que ve el cliente.
    Para mostrarle los cambios, vuelve a ejecutar este script (el túnel puede quedar abierto: la dirección no cambia).
  - Usa la base de desarrollo con los datos de ejemplo (empresa ficticia "Comercial Andina", marzo de 2026).
  - Swagger queda apagado y la API toma la IP real de cada visitante a través del túnel (límite de intentos de login).
  - Con -Limpia borra la base de desarrollo antes de arrancar, para empezar con los datos de ejemplo intactos.
  Detén la demo con Ctrl+C en esta ventana.

.EXAMPLE
  .\tools\demo.ps1 -Limpia
#>
param(
  [string]$Frontend = (Join-Path $PSScriptRoot '..\..\retail-web'),
  [switch]$Limpia,
  [switch]$SinCompilar
)
$ErrorActionPreference = 'Stop'
$api = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$Frontend = (Resolve-Path $Frontend).Path
$destino = Join-Path $api 'artifacts\demo'
$destApi = Join-Path $destino 'api'
$destWeb = Join-Path $destino 'web'

if (-not $SinCompilar) {
  Write-Host 'Compilando el frontend (ng build)...' -ForegroundColor Cyan
  Push-Location $Frontend
  try { npx ng build --configuration production; if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación del frontend.' } }
  finally { Pop-Location }

  Write-Host 'Publicando la API en la copia de la demo...' -ForegroundColor Cyan
  if (Test-Path $destino) { Remove-Item $destino -Recurse -Force }
  dotnet publish (Join-Path $api 'src\NexoRh.Api') -c Release -o $destApi --nologo -v quiet
  if ($LASTEXITCODE -ne 0) { throw 'Falló la publicación de la API.' }
  Copy-Item (Join-Path $Frontend 'dist\retail-web') $destWeb -Recurse
} elseif (-not (Test-Path (Join-Path $destApi 'NexoRh.Api.dll'))) {
  throw 'No hay una copia de la demo todavía: ejecuta el script sin -SinCompilar.'
}

if ($Limpia) {
  Write-Host 'Borrando las bases de la demo (se vuelven a crear con los datos de ejemplo)...' -ForegroundColor Yellow
  foreach ($bd in 'NexoRhDemo', 'NexoRhDemoCatalogo') {
    sqlcmd -S '.\SQLEXPRESS' -E -b -Q "IF DB_ID('$bd') IS NOT NULL BEGIN ALTER DATABASE [$bd] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$bd]; END"
    if ($LASTEXITCODE -ne 0) { throw "No se pudo borrar la base $bd. ¿Está detenida la demo?" }
  }
}

# Misma configuración que el perfil "demo" de launchSettings, aplicada a la copia publicada.
# La demo usa su PROPIA base (NexoRhDemo y su catálogo): lo que pruebes en desarrollo no la toca.
$env:ConnectionStrings__Nexo = 'Server=.\SQLEXPRESS;Database=NexoRhDemo;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=False'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://localhost:5281'
$env:Asistencia__FechaCorte = '16/03/2026'
$env:Asistencia__HoraCorte = '11:45'
$env:Web__Carpeta = $destWeb
$env:Swagger__Habilitado = 'false'
$env:Proxy__Habilitado = 'true'
$env:Plataforma__Dominio = 'localhost'
$env:Plataforma__TenantPorDefecto = 'comercialandina'

Write-Host ''
Write-Host 'Nexo RH en modo demo: http://localhost:5281' -ForegroundColor Green
Write-Host 'En otra ventana, comparte el sitio con:  cloudflared tunnel --url http://localhost:5281' -ForegroundColor Green
Write-Host ''
Push-Location $destApi
try { dotnet NexoRh.Api.dll }
finally { Pop-Location }
