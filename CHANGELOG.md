# Changelog

Todas las novedades de PowerHistory.

El formato sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y
el versionado es [SemVer](https://semver.org/lang/es/).

Las notas de cada release salen de la sección que coincide con el tag.

## [0.3.0] - 2026-11-13

### Agregado

-   Filtro exacto en la TUI (`e`) y `--exact` en `clean`, para que `node` no
    coincida con `node --run dev`.
-   `Ctrl+F` copia al filtro el comando donde está el cursor.
-   Vista de duplicados (`Ctrl+D`): muestra solo los comandos repetidos,
    agrupados y con la cantidad de apariciones.
-   Marcar con `Shift` en la TUI: el recorrido alterna el estado de la fila donde
    arranca (una sola vez) y de cada fila por la que pasa, así que retroceder por
    el mismo tramo lo deshace. Las flechas solas no tocan la selección.

### Cambiado

-   El filtro se aplica después de una pausa corta al escribir, no en cada
    tecla.
-   Los filtros de `clean` se combinan con Y en lugar de O.

### Corregido

-   Mantener apretada la flecha o `PgDn` degradaba la navegación: la altura del
    frame cambiaba con cada comando y obligaba a redibujarlo entero. Ahora la
    altura es fija y un frame cuesta un solo render.
-   Las repeticiones de una tecla mantenida se aplican de a una y se dibujan en
    un solo frame, en lugar de redibujar la pantalla completa por cada
    repetición.
-   `clean --duplicates --text` borraba todos los duplicados del archivo, no
    solo los que coincidían con el filtro.

## [0.2.0] - 2026-11-13

### Agregado

-   Versionado del release tomado de la etiqueta: el ejecutable publicado
    reporta la versión del tag y no un valor fijo.
-   El zip del release incluye el ejecutable, el README, la licencia y el
    archivo `SHA256SUMS.txt` con el hash del binario.
-   `CHANGELOG.md` como fuente de las notas de cada release.
-   Hash SHA-256 del zip publicado en el resumen del workflow, para reutilizarlo
    en el manifest de WinGet.

### Cambiado

-   La TUI se reescribió sobre `AnsiConsole.Live` y la pantalla alternativa del
    terminal: el redibujado ocurre en el lugar, sin parpadeo y sin acumular
    frames en el historial de scroll.
-   La búsqueda es inline: `/` abre un campo de edición y el filtro se aplica
    mientras se escribe. `Enter` lo confirma y `Esc` restaura el filtro previo.
-   La confirmación de borrado es inline: `d` muestra una barra con la cantidad
    de entradas y las primeras en eliminar; `y` confirma.
-   La lista se ajusta al tamaño de la ventana: el alto real del frame se mide
    y la tabla se encoge hasta entrar, en lugar de estimarlo.
-   `PageSize` pasó a ser el máximo de entradas visibles; con `0` (por defecto)
    la lista llena la terminal.
-   La versión mostrada en el encabezado de la TUI ya no incluye el hash del
    commit.

### Corregido

-   El filtro se recalculaba en cada tecla, varias veces por pulsación. Ahora
    la vista filtrada se cachea y solo se rehace cuando cambia el filtro o el
    archivo.
-   Con una terminal angosta el final del frame se recortaba y quedaban fuera
    el pie, los avisos de acción y la confirmación de borrado.

## [0.1.0] - 2026-11-12

### Agregado

-   Descubrimiento del historial de PSReadLine consultando la configuración real
    de `pwsh` y `powershell.exe`.
-   Lectura y escritura que conservan el formato original: codificación, BOM,
    fin de línea y salto final.
-   Escritura atómica con archivo temporal en el mismo directorio.
-   Backups con detección de cambios externos por hash SHA-256.
-   Comandos `list`, `search`, `stats`, `clean`, `backup`, `backups` y
    `restore`.
-   TUI inicial con listado, búsqueda, selección y borrado seguro.
-   Publicación self-contained single-file para `win-x64`.

[0.3.0]: https://github.com/hodygmd/PowerHistory/releases/tag/v0.3.0
[0.2.0]: https://github.com/hodygmd/PowerHistory/releases/tag/v0.2.0
[0.1.0]: https://github.com/hodygmd/PowerHistory/releases/tag/v0.1.0
