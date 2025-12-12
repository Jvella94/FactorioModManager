#!/usr/bin/env bash
set -euo pipefail

SRC_ICO="UI/Assets/FMM.ico"
SRC_PNG="UI/Assets/FMM.png"
DST_DIR="./iconset-temp"
DST_ICNS="./AppIcon.icns"

mkdir -p "$DST_DIR"

# Helper to create a png of a specific size using convert or sips
create_png() {
  local input="$1"
  local out="$2"
  local w="$3"
  local h="$4"
  if command -v convert >/dev/null 2>&1; then
    convert "$input" -background none -resize "${w}x${h}" "$out"
  else
    # sips is macOS-native but only works with raster images like PNG
    local input_lower=$(echo "$input" | tr '[:upper:]' '[:lower:]')
    if [[ "$input_lower" == *.ico ]] && [[ -f "$SRC_PNG" ]]; then
      # if source is ico and no convert, use the provided PNG fallback
      sips -z "$h" "$w" "$SRC_PNG" --out "$out" >/dev/null
    else
      sips -z "$h" "$w" "$input" --out "$out" >/dev/null
    fi
  fi
}

# Source selection
if [[ -f "$SRC_ICO" ]]; then
  SRC="$SRC_ICO"
elif [[ -f "$SRC_PNG" ]]; then
  SRC="$SRC_PNG"
else
  echo "Error: No source icon found at $SRC_ICO or $SRC_PNG" >&2
  exit 1
fi

# Required base sizes
sizes=(16 32 128 256 512)
for s in "${sizes[@]}"; do
  base="$DST_DIR/icon_${s}x${s}.png"
  double="$DST_DIR/icon_${s}x${s}@2x.png"
  create_png "$SRC" "$base" "$s" "$s"
  create_png "$SRC" "$double" "$((s*2))" "$((s*2))"
done

# Sanity check
ls -la "$DST_DIR"

# Create ICNS using macOS iconutil
if command -v iconutil >/dev/null 2>&1; then
  iconutil -c icns "$DST_DIR" -o "$DST_ICNS"
  echo "Generated $DST_ICNS"
else
  echo "Error: iconutil not found. This script must be run on macOS." >&2
  exit 2
fi

# Cleanup temp iconset
rm -rf "$DST_DIR"

exit 0
