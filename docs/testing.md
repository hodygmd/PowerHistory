# Estrategia de pruebas

## Ejecutar

``` powershell
dotnet test
```

Tres proyectos, 95 tests:

| Proyecto                        | Qué cubre                                            |
| ------------------------------- | ---------------------------------------------------- |
| `PowerHistory.Core.Tests`       | Reglas puras: búsqueda y limpieza                     |
| `PowerHistory.Infrastructure.Tests` | Archivos, formato, backups, descubrimiento, opciones |
| `PowerHistory.Cli.Tests`        | Parser de argumentos, comandos y TUI interactiva      |

## Aislamiento

**Ningún test toca el historial real.** Todos los archivos de prueba se
crean en un directorio temporal bajo `%TEMP%\PowerHistoryTests` o
`%TEMP%\PowerHistoryCliTests` y se eliminan al terminar.

Los backups de los tests van al directorio temporal del test, nunca a
`%LOCALAPPDATA%`. Por eso `CliApplication` acepta la ruta de configuración y
el directorio de backups por constructor.

Los tests de la CLI reemplazan la consola global, así que el proyecto
desactiva la ejecución en paralelo.

## Qué se prueba

### Unitarias (Core)

-   Búsqueda con y sin sensibilidad a mayúsculas.
-   Búsqueda exacta: `node` no coincide con `node --run dev`.
-   Orden de los resultados: se conserva el del archivo.
-   Búsqueda vacía: devuelve todo.
-   Eliminación por índices sobre el snapshot, no sobre índices recalculados.
-   Duplicados exactos, conservando la primera o la última aparición.
-   Duplicados exactos: `"ls -la"` y `"ls -la "` son entradas distintas.
-   Eliminación por texto, por expresión regular y de líneas vacías.
-   Conservar las últimas N entradas.
-   Patrón de expresión regular inválido.

### Integración (Infrastructure)

-   Lectura que preserva contenido y orden.
-   Archivo vacío: cero entradas.
-   Archivo con un solo salto de línea: una entrada vacía.
-   Líneas vacías entre comandos.
-   Detección de `\r\n`.
-   Caracteres especiales, acentos y espacios.
-   Escritura que conserva BOM, terminador y salto final.
-   Archivo sin salto de línea final.
-   Detección de cambios externos (modificado, borrado, sin cambios).
-   Destino bloqueado: la escritura falla y el original queda intacto.
-   No quedan archivos temporales después de escribir.
-   Backups: copia, nombre único, listado por origen, restauración, backup del
    estado actual antes de restaurar.
-   Descubrimiento: no lanza, devuelve rutas absolutas y sin duplicados.
-   Configuración: defaults, round trip y archivo corrupto.

### Interfaz (Cli)

-   Parser: comando, posicionales, opciones, flags, `--key=value`.
-   Comandos: `list`, `search`, `stats`, `clean`, `backup`, `backups`,
    `restore`, `--help`, `--version`, comando desconocido, archivo inexistente.
-   `clean` sin `--yes` no modifica nada y devuelve el código de salida 4.
-   `clean --duplicates --text` solo borra los duplicados que coinciden con el
    texto; los duplicados de otros comandos quedan.
-   `clean --text --exact` distingue `node` de `node --run dev`.
-   TUI: render de entradas, filtro en vivo, `Esc` que restaura el filtro
    anterior, selección con espacio, selección total con `Ctrl+A`, borrado con
    confirmación, borrado cancelado, versión sin el hash del commit.
-   TUI: el filtro se aplica después de la pausa de escritura.
-   TUI: modo exacto, `Ctrl+F` sobre el comando actual, vista de duplicados con
    `Ctrl+D`.
-   TUI: un comando de 5000 caracteres no desborda el frame.
-   TUI: marcado con `Shift`. Se verifica que las flechas solas no toquen la
    selección, que el recorrido incluya la fila desde la que arranca, que volver
    sobre lo marcado lo desmarque y que recorrer el mismo tramo en sentido
    inverso lo deshaga por completo. Se comprueba borrando y mirando qué líneas
    quedan en el archivo.

Las pruebas de TUI usan `Spectre.Console.Testing`: empujan teclas reales
contra `TestConsoleInput` y verifican tanto lo que se renderiza como el
contenido final del archivo.

La vista se renderiza con `AnsiConsole.Live` sobre la pantalla alternativa, así
que `TestConsole` acumula un frame por refresco. Las aserciones sobre texto se
hacen sobre el último frame (`Output` a partir del último `Filter:`), nunca
sobre la salida completa.

El test del debounce no empuja teclas después del texto: deja que la pausa real
transcurra y el filtro se aplique antes de que la entrada se agote.

## Pruebas manuales

No se automatizan porque dependen del terminal:

-   Windows Terminal y terminal integrada del editor.
-   `Shift+flechas`: que el terminal entregue el modificador y que mantenerlo
    apretado pinte el rango sin redibujar por cada repetición.
-   Windows PowerShell 5.1 y PowerShell 7.
-   Historial con miles de entradas.
-   Dos sesiones abiertas sobre el mismo archivo.
-   Cierre inesperado del proceso durante una operación.

## Verificación end-to-end

El flujo completo se validó sobre una **copia** del historial real:

``` powershell
Copy-Item $env:APPDATA\Microsoft\Windows\PowerShell\PSReadLine\ConsoleHost_history.txt $copy
histo clean --duplicates --yes --file $copy
histo restore <id> --yes --file $copy
```

El hash SHA-256 del archivo restaurado coincide con el del original: el
ciclo leer → escribir → restaurar no altera ni un byte.
