# Arquitectura

## Capas

``` text
PowerHistory.Cli  ──────┐
  argumentos, comandos   │
  no interactivos, TUI   │
                         ▼
PowerHistory.Core ───────► PowerHistory.Infrastructure
  modelos, interfaces      lectura/escritura, descubrimiento,
  reglas de limpieza       backups, configuración
```

**Core** no depende de Spectre.Console ni del sistema de archivos. Contiene
los modelos (`HistoryEntry`, `HistoryDocument`, `HistoryFormat`), las
interfaces (`IHistoryFile`, `IHistoryDiscovery`, `IBackupService`) y las
reglas puras (`HistoryQuery`, `HistoryCleaner`).

**Infrastructure** implementa esas interfaces sobre el sistema real:
`FileHistoryRepository`, `SafeFile`, `HistoryFileFormat`,
`PowerShellHistoryDiscovery`, `BackupService`, `OptionsStore`.

**Cli** orquesta: parsea argumentos, resuelve el archivo de historial,
decide entre TUI y comando, y traduce las reglas de Core a una interfaz.

El sentido de la dependencia va hacia adentro. Core no sabe que existen
archivos; Infrastructure no sabe que existe una TUI.

## Modelo de datos

```csharp
HistoryEntry   (Index, Text)              una línea del historial
HistoryFormat  (NewLine, HasBom, EndsWithNewLine)   cómo estabacodificado el archivo
HistoryDocument(SourcePath, Entries, ContentHash, Format)   snapshot inmutable
```

`Index` es la posición dentro del snapshot. Todas las operaciones de
limpieza trabajan sobre ese snapshot: los índices de la selección se calculan
una vez y se aplican de una sola vez, nunca se recalculan después de cada
borrado.

`ContentHash` es el SHA-256 de los bytes leídos. Se usa para detectar que
otro proceso modificó el archivo.

## Preservación del formato

`HistoryFileFormat` decodifica y vuelve a codificar el archivo conservando:

-   la codificación (UTF-8 con o sin BOM, UTF-16 LE/BE),
-   el terminador de línea (`\r\n`, `\n`, `\r`),
-   la presencia o ausencia del salto de línea final,
-   las líneas vacías entre comandos.

Una lectura seguida de una escritura sin cambios devuelve un archivo
byte a byte idéntico al original. Es lo que garantiza el test
`Restore_BringsTheOriginalContentBack` y la verificación end-to-end.

## Escritura segura

`SafeFile` centraliza el patrón de toda escritura:

1.  Se escribe un archivo temporal en el **mismo directorio** que el destino.
2.  Se cierra el stream y se fuerza el volcado a disco.
3.  Se reemplaza el destino con `File.Replace`, que es atómico en NTFS y
    conserva los permisos del archivo original.
4.  Si algo falla, se borra el temporal y el original queda intacto.

`File.Move` se usa solo cuando el destino todavía no existe.

## Detección de cambios externos

El flujo de escritura es siempre el mismo:

``` text
leer (snapshot + hash)
   │
   ├─ el usuario navega, filtra, selecciona
   │
escribir:
   1. recalcular el hash del archivo en disco
   2. si cambió → cancelar, avisar y pedir recarga
   3. crear backup
   4. escribir con SafeFile
```

El paso 2 es una ventana: entre la validación y el reemplazo otro proceso
podría escribir. Se documenta como limitación conocida.

## Descubrimiento

`PowerShellHistoryDiscovery` lanza un proceso hijo por cada host candidato
(`pwsh`, `powershell.exe`) y ejecuta:

``` powershell
$ErrorActionPreference='Stop'
Import-Module PSReadLine -ErrorAction Stop
$o = Get-PSReadLineOption
[pscustomobject]@{p=[string]$o.HistorySavePath; s=[string]$o.HistorySaveStyle; m=[int]$o.MaximumHistoryCount} | ConvertTo-Json -Compress
```

Se carga el perfil del usuario a propósito: un `HistorySavePath` personalizado
suele estar definido ahí, y un host sin perfil reportaría la ruta por defecto
en lugar de la real. Si el host falla con el perfil, se reintenta sin él.

Un host que no está instalado, no tiene PSReadLine o tarda demasiado se
omite sin romper la detección. Las rutas repetidas entre hosts se unifican.

## Backups

`BackupService` copia el archivo a un directorio de datos del usuario con un
nombre `archivo.clave.ruta-y-hora.bak`. La clave es un hash corto de la ruta
completa, para que dos archivos con el mismo nombre no compartan backups.

`Restore` respalda el estado actual antes de restaurar, así una restauración
siempre es reversible.

## Decisiones

| Decisión                                | Motivo                                                                 |
| --------------------------------------- | ---------------------------------------------------------------------- |
| Sin ruta de historial fija             | La ruta real la decide la configuración de PSReadLine del usuario.     |
| Comparación ordinal en búsquedas       | Predecible, independiente de la cultura y sin sorpresas con acentos.   |
| Duplicados exactos (ordinal)           | Es el comportamiento por defecto según la especificación.             |
| Hash SHA-256 del contenido              | Detecta cualquier cambio, no solo cambios de tamaño o fecha.          |
| `File.Replace` en lugar de `File.Write`| Reemplazo atómico que conserva los permisos del archivo original.      |
| Escritura de un solo golpe              | Evita escrituras parciales y la ventana entre leer y escribir.         |
| Configuración como JSON                 | Formato legible y sin dependencias externas.                           |
