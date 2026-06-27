# Caves of Qud — Traducción al español (España)

Traducción **no oficial** de *Caves of Qud* al español de España, construida sobre el
framework de localización de la rama experimental `lang-experimental` de Steam.

> **Estado:** andamiaje montado. Falta la traducción masiva de los ~40 archivos de idioma.

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
3. Abrir el juego, habilitar las scripted mods si lo pide, y en **Language** elegir el español. El juego se reinicia.

## Pendientes conocidos
- Verificar el código de idioma correcto (`es` / `es-ES` / slot propio) en `lang-experimental`.
- Confirmar en el [Discord oficial](https://discord.com/invite/cavesofqud) que no haya otro español en curso.
