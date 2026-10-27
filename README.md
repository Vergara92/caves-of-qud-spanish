# Caves of Qud — Traducción al español (España)

Traducción **no oficial** de *Caves of Qud* al español de España, construida sobre el
framework de localización de la rama experimental `lang-experimental` de Steam.

> ⚠️ **Disclaimer**: Las traducciones están hechas con claude code sobre la base inglesa que comparte freehold, en ningún momento pretendo quitarle el valor a las personas que son traductoras, estas traducciones carecen del alma y la precisión que les daría un profesional del secto. lo hago solo como un pequeño aporte para que, a los que como yo, el nivel de inglés que utiliza el juego les hace la experiencia menos disfrutable de lo que nos gustaría y así al menos, podemos matarnos el gusanillo jugando teniendo en mente que las traducciones están lejos de estar en un estado optimo. 

## Estructura del repo
- `ExampleLanguage/` — plantilla oficial de Freehold (fuente de verdad, inglés). No editar.
- `SpanishLanguage/` — el pack de idioma que produce este proyecto.
  - `manifest.json`, `languages/languages.xml`
  - `languages/Genders.es.xml`, `languages/PronounSets.es.xml` (hechos a mano)
  - `languages/*.es.xml` — un archivo por cada `*.example.xml` (pendientes de generar)
- `prompts/` — prompts que guían la traducción/sincronización con IA.
- `scripts/` — `validate_translation.py`, `create-release.sh`, instaladores.
- `CLAUDE.md` — estrategia lingüística (anti-concordancia).
- `italian-reference/` — repo italiano clonado, solo referencia.

## Cómo trabajar
1. Generar cada `SpanishLanguage/languages/X.es.xml` a partir de `ExampleLanguage/X.example.xml`
   siguiendo `prompts/prompt-xml.txt` (y `prompt-history.txt` para `HistorySpice`).
2. Validar: `python3 scripts/validate_translation.py` (requiere `lxml`).
3. Al actualizarse el juego, sincronizar con `prompts/prompt-update.txt`.

## Instalación en el juego
1. En Steam: clic derecho en Caves of Qud → Propiedades → "Versiones y beta del juego"
   → seleccionar la rama **`lang-experimental`**.
2. Copiar la carpeta `SpanishLanguage/` a la carpeta de mods:
   - **Windows:** `%USERPROFILE%\AppData\LocalLow\Freehold Games\CavesOfQud\Mods` (o `scripts\install_mod.bat`)
   - **macOS:** `~/Library/Application Support/com.FreeholdGames.CavesOfQud/Mods` (o `./scripts/install-local.sh`)
   - **Linux:** `~/.config/unity3d/Freehold Games/CavesOfQud/Mods`
   > La ruta exacta puede variar según versión/tienda: verifícala una vez. Puedes forzarla con `MODS_DIR=... ./scripts/install-local.sh`.
3. Abrir el juego y, en **Opciones**, activar **«Permitir mods con scripts»** (recomendado). Con la opción activada, los adjetivos de los objetos van detrás del nombre y concuerdan («espada larga de acero afilada»). Sin ella, la traducción funciona igual, con adjetivos en forma invariable («chal acolchado con pinturas»).
4. En **Language** elegir el español. El juego se reinicia.
> Si tenías instalada una versión anterior, borra antes la carpeta `SpanishLanguage` (y `SpanishConcordancia`, si existe) de la carpeta de mods.

## Pendientes conocidos
- Verificar el código de idioma correcto (`es` / `es-ES` / slot propio) en `lang-experimental`.
- Confirmar en el [Discord oficial](https://discord.com/invite/cavesofqud) que no haya otro español en curso.
