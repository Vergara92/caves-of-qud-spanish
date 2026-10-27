# Caves of Qud — Traducción al español (España)

Traducción **no oficial** de *Caves of Qud* al español de España, construida sobre el
framework de localización de la rama experimental `lang-experimental` de Steam.

**Versión actual:** [descárgala aquí](https://github.com/Vergara92/caves-of-qud-spanish/releases/latest) (archivo `caves-of-qud-spanish-vX.Y.Z.zip`, en «Assets»).

> ⚠️ **Disclaimer**: Las traducciones están hechas con claude code sobre la base inglesa que comparte freehold, en ningún momento pretendo quitarle el valor a las personas que son traductoras, estas traducciones carecen del alma y la precisión que les daría un profesional del secto. lo hago solo como un pequeño aporte para que, a los que como yo, el nivel de inglés que utiliza el juego les hace la experiencia menos disfrutable de lo que nos gustaría y así al menos, podemos matarnos el gusanillo jugando teniendo en mente que las traducciones están lejos de estar en un estado optimo. 

## Instalación (para jugar)

Necesitas el juego en **Steam**: la traducción solo funciona en la rama experimental de idiomas.

1. **Cambia a la rama de idiomas.** En la biblioteca de Steam, clic derecho en *Caves of Qud* → **Propiedades** → **Versiones beta** (o «Versiones y beta del juego») → elige **`lang-experimental`**. Espera a que Steam termine de actualizar.
2. **Descarga la traducción.** Entra en la [última versión](https://github.com/Vergara92/caves-of-qud-spanish/releases/latest) y, en **Assets**, descarga `caves-of-qud-spanish-vX.Y.Z.zip`. Descomprímelo: dentro hay una carpeta llamada **`SpanishLanguage`**.
3. **Copia la carpeta `SpanishLanguage`** (entera, tal cual) a la carpeta de mods del juego. Si la carpeta `Mods` no existe, créala.
   - **Windows:** pega esto en la barra de direcciones del Explorador: `%USERPROFILE%\AppData\LocalLow\Freehold Games\CavesOfQud\Mods`
   - **macOS:** en Finder, menú **Ir → Ir a la carpeta…** y pega: `~/Library/Application Support/com.FreeholdGames.CavesOfQud/Mods`
   - **Linux:** `~/.config/unity3d/Freehold Games/CavesOfQud/Mods`

   Tiene que quedar así: `…/Mods/SpanishLanguage/manifest.json`. Si te queda `…/Mods/SpanishLanguage/SpanishLanguage/…`, sube un nivel la carpeta de dentro.
4. **Abre el juego** y ve a **Options**, apartado **Mods**. Activa **«Allow scripting mods»** (recomendado; ver abajo). Si el juego te pide reiniciar, hazlo.
5. En el selector de idioma (**Language**), elige **Español (España)**. El juego se reinicia en español.
6. Empieza un **personaje nuevo** para ver también en español la historia del mundo que se genera al empezar.

**¿Por qué activar los mods con scripts?** Con la opción activada, los adjetivos de los objetos van detrás del nombre y concuerdan en género y número («espada larga de acero afilada», «pieles pintadas»). Sin ella, la traducción funciona igual, pero esos adjetivos salen en una forma invariable («chal acolchado con pinturas»).

### Actualizar a una versión nueva
Borra la carpeta `SpanishLanguage` de `Mods` (y `SpanishConcordancia`, si la tienes de una prueba antigua) y repite los pasos 2 y 3.

### Si algo no va
- **No aparece «Español» en Language:** comprueba que estás en la rama `lang-experimental` y que la ruta es `Mods/SpanishLanguage/manifest.json`.
- **Ves textos en inglés sueltos:** es normal en algunas habilidades y efectos que el juego escribe dentro de su código; se irán traduciendo en próximas versiones.
- Para avisar de un error, abre una [incidencia](https://github.com/Vergara92/caves-of-qud-spanish/issues) con una captura.

## Para desarrolladores

### Estructura del repo
- `ExampleLanguage/` — plantilla oficial de Freehold (fuente de verdad, inglés). No editar.
- `SpanishLanguage/` — el mod: `manifest.json`, `languages/*.es.xml` (textos) y `codigo/*.cs` (concordancia de adjetivos; solo se carga con los mods con scripts activados).
- `prompts/` — prompts que guían la traducción y la sincronización con IA.
- `scripts/` — `validate_translation.py`, `find_untranslated.py`, instaladores (`install-local.sh`, `install_mod.bat`).
- `CLAUDE.md` — estrategia lingüística (anti-concordancia).

### Cómo trabajar
1. Traducir o corregir `SpanishLanguage/languages/X.es.xml` a partir de `ExampleLanguage/X.example.xml` siguiendo `prompts/prompt-xml.txt`.
2. Validar: `python3 scripts/validate_translation.py` (requiere `lxml`).
3. Al actualizarse el juego, sincronizar con `prompts/prompt-update.txt`.
4. Instalar en local: `./scripts/install-local.sh` (macOS/Linux; ruta forzable con `MODS_DIR=...`) o `scripts\install_mod.bat` (Windows).
