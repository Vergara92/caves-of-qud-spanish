#!/bin/bash
set -e

# Instala el pack de idioma en la carpeta de mods de Caves of Qud.
# Detecta macOS / Linux. La ruta exacta puede variar según versión/tienda:
# verifícala una vez (ver README) y, si hace falta, exporta MODS_DIR a mano:
#   MODS_DIR="/ruta/a/CavesOfQud/Mods" ./scripts/install-local.sh

SRC="$(cd "$(dirname "$0")/.." && pwd)/SpanishLanguage"

if [[ -z "$MODS_DIR" ]]; then
  case "$(uname -s)" in
    Darwin)
      MODS_DIR="$HOME/Library/Application Support/com.FreeholdGames.CavesOfQud/Mods"
      ;;
    Linux)
      MODS_DIR="$HOME/.config/unity3d/Freehold Games/CavesOfQud/Mods"
      ;;
    *)
      echo "SO no reconocido. Define MODS_DIR a mano."; exit 1 ;;
  esac
fi

DEST="$MODS_DIR/SpanishLanguage"
echo "Origen:  $SRC"
echo "Destino: $DEST"

if [[ ! -d "$SRC" ]]; then echo "ERROR: no existe $SRC"; exit 1; fi
mkdir -p "$MODS_DIR"
rm -rf "$DEST"
cp -R "$SRC" "$DEST"
echo "Hecho. Abre el juego, habilita las scripted mods si lo pide y elige el idioma en Language."
