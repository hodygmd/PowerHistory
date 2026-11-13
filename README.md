# PowerHistory

Administrador interactivo del historial de PowerShell. Permite consultar,
buscar, limpiar y respaldar los archivos de historial sin editarlos a mano.

PowerHistory **no reemplaza a PSReadLine**. PSReadLine sigue capturando y
guardando los comandos; PowerHistory administra los archivos de forma
explícita, con vista previa, confirmación y backup.

## Requisitos

-   Windows (probado en Windows 11).
-   Para el uso interactivo: Windows Terminal o la terminal integrada del
    editor.
-   No requiere instalar .NET: se distribuye como ejecutable autónomo.

## Instalación

1.  Descargar el zip de la sección [Releases](https://github.com/hodygmd/PowerHistory/releases).
2.  Verificar el hash del ejecutable contra `SHA256SUMS.txt`:

    ``` powershell
    Get-FileHash .\histo.exe -Algorithm SHA256
    ```

3.  Colocar `histo.exe` en una carpeta incluida en `PATH`.
4.  Ejecutar `histo`.

Para probarlo sin tocar el `PATH`:

``` powershell
.\histo.exe
```

## Uso

### Interfaz interactiva

``` powershell
histo
```

| Tecla           | Acción                                          |
| --------------- | ----------------------------------------------- |
| `↑` `↓`         | Moverse por la lista                            |
| `PgUp` `PgDn`   | Moverse una página                              |
| `Home` `End`    | Ir al principio o al final                      |
| `Espacio`       | Seleccionar o deseleccionar la entrada          |
| `Ctrl+A`        | Seleccionar todas las entradas visibles         |
| `/`             | Filtrar; el resultado se actualiza al escribir  |
| `Enter`         | Confirmar el filtro                             |
| `Esc`           | Cancelar el filtro; sin filtro, salir           |
| `c`             | Limpiar el filtro                               |
| `d`             | Eliminar la selección (pide confirmación)       |
| `y`             | Confirmar la eliminación                        |
| `r`             | Recargar el archivo                             |
| `q`             | Salir                                           |

La interfaz usa la pantalla alternativa del terminal, así que el contenido previo
se restaura al salir y la lista no deja rastros en el historial de scroll.

### Comandos

``` powershell
histo list [--file <ruta>]        # lista las entradas
histo search <texto>              # busca un texto
histo stats                       # muestra estadísticas
histo backup                      # crea un backup
histo backups                     # lista los backups
histo restore <id>                # restaura un backup
histo clean --duplicates --yes    # elimina duplicados exactos
```

`histo clean` acepta los filtros `--duplicates`, `--text`, `--regex`,
`--empty` y `--keep-last <n>`, y se pueden combinar. Sin `--yes` solo muestra
la vista previa y no modifica nada.

Si no se indica `--file`, se usa el historial detectado en la máquina. Para
trabajar sobre otro archivo:

``` powershell
histo list --file C:\ruta\a\historial.txt
```

## Detección del historial

PowerHistory no tiene rutas fijas. Consulta a cada host de PowerShell
instalado (`pwsh` y `powershell.exe`) mediante `Get-PSReadLineOption` y usa
la ruta que ese host reporta realmente.

El perfil del usuario se carga durante la consulta, porque es ahí donde
normalmente se define un `HistorySavePath` personalizado. Los hosts que no
están instalados, fallan o no tienen PSReadLine se omiten sin romper la
detección.

## Configuración

Archivo opcional en `%APPDATA%\PowerHistory\config.json`:

``` json
{
  "DefaultHistoryPath": null,
  "BackupDirectory": null,
  "PageSize": 0,
  "RequireConfirmation": true,
  "CaseSensitiveSearch": false
}
```

Los backups se guardan en `%LOCALAPPDATA%\PowerHistory\backups` por defecto,
separados del historial.

`PageSize` es el máximo de entradas que muestra la lista interactiva. Con `0`
(por defecto) la lista se ajusta a la ventana de la terminal.

## Seguridad

-   Nada se modifica sin un backup previo.
-   Nada se modifica sin confirmación explícita.
-   Antes de escribir se verifica que el archivo no haya cambiado desde la
    lectura. Si cambió, la operación se cancela.
-   La escritura pasa por un archivo temporal en el mismo directorio y
    reemplaza el original de forma atómica. Si algo falla, el original
    queda intacto.
-   El contenido del historial nunca se envía a ningún servicio externo ni
    se escribe en logs.

## Limitaciones conocidas

-   **Sesiones abiertas.** Otras sesiones de PSReadLine pueden tener el
    historial en memoria y volver a guardarlo. Cerrá las otras sesiones que
    usen el mismo archivo antes de aplicar cambios.
-   **Carrera entre validación y escritura.** La comprobación de cambios
    externos ocurre antes de escribir, no de forma atómica con la escritura.
    Un proceso externo que escriba exactamente en ese instante no es
    detectado.
-   **Sin marcas de tiempo.** El historial de PSReadLine es texto plano. No
    hay fechas por comando, así que no se puede limpiar por fecha.
-   **Solo los archivos elegidos.** PowerHistory administra los archivos que
    vos indiques. No garantiza que un comando desaparezca de otros registros
    del sistema.
-   **Windows.** La distribución actual es solo para Windows x64.

## Desarrollo

``` powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project src/PowerHistory.Cli
```

Publicar el ejecutable autónomo:

``` powershell
dotnet publish src/PowerHistory.Cli -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Más detalle en [docs/architecture.md](docs/architecture.md) y
[docs/testing.md](docs/testing.md). El historial de cambios está en
[CHANGELOG.md](CHANGELOG.md).

## Licencia

[MIT](LICENSE).
