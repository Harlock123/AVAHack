#!/usr/bin/env bash
# Turns Screaming Brain Studios' "Old School Dungeon Crawler Pack" and its "Cave Walls Addon" (both CC0,
# https://screamingbrainstudios.itch.io/dungeon-crawler-pack) into the 128×128 textures the ray caster uses.
#
# The pack is drawn for tile-composited crawlers: each set has a flat "Center" wall seen face on, side walls,
# and a floor and ceiling seen in perspective. The ray caster draws its own perspective, so this keeps the
# Center as the wall and un-warps the near floor and ceiling (a 256×64 trapezoid covering half a cell) back
# into a flat half cell, stacked twice to make a whole one.
#
# Usage: tools/import-sbs-textures.sh <folder holding the unpacked "Brick 1".."Brick 3" and "* Cave" folders>
# Needs ImageMagick 7 (magick). Writes src/AVAHack.Render/Assets/Textures/<theme>-{wall,floor,ceiling}.png.
set -euo pipefail

src=${1:?folder with the unpacked pack}
out="$(cd "$(dirname "$0")/.." && pwd)/src/AVAHack.Render/Assets/Textures"
mkdir -p "$out"
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT

# theme -> the pack's set. The region names are AVAHack's (spec 14); the sets are the pack's.
declare -A sets=(
  [cellar]="Brick 3"          # dark brick: the old cellars under Harrowgate
  [works]="Brick 1"           # warm brick: the Deep Works
  [choir]="Brick 2"           # mossy brick: the Drowned Choir
  [heart]="Shale Cave"        # bare violet stone: the Unmade Heart
)

piece() { # set, part (Center|Bottom|Top) -> the Layer 1 file of that part
  find "$src/$1/Layer 1" -maxdepth 1 -name "*$2*.png" | head -n 1
}

# The brick sets lay 16 courses to a cell, too fine for the view's resolution: they keep a quarter of each piece
# (its brick pattern repeats every 16 px, so a 64 px quarter still tiles) at twice the size. The cave sets are
# coarse already. Every texture is then brought to the brightness the code-drawn set had (wall, floor, ceiling),
# since the packs range from near-black to near-white and the view lights them all alike.
declare -A coarse=([cellar]=0 [works]=0 [choir]=0 [heart]=1)
declare -A bright=([wall]=0.43 [floor]=0.34 [ceiling]=0.25)

finish() { # in, kind, theme -> the 128×128 PNG
  local in=$1 kind=$2 theme=$3 mean
  if [[ ${coarse[$theme]} == 0 ]]; then
    magick "$in" -crop 64x64+0+0 +repage -filter Lanczos -resize 128x128 "$tmp/q.png"; in="$tmp/q.png"
  fi
  mean=$(magick "$in" -colorspace gray -format '%[fx:mean]' info:)
  magick "$in" -evaluate multiply "$(awk -v t="${bright[$kind]}" -v m="$mean" 'BEGIN { printf "%.4f", t / m }')" \
    -strip "PNG32:$out/$theme-$kind.png"
}

for theme in "${!sets[@]}"; do
  set=${sets[$theme]}
  wall=$(piece "$set" Center); floor=$(piece "$set" Bottom); ceiling=$(piece "$set" Top)
  [[ -f $wall && -f $floor && -f $ceiling ]] || { echo "missing pieces for $set" >&2; exit 1; }

  finish "$wall" wall "$theme"
  # Floor: far edge 128 wide at the top, near edge 256 wide at the bottom.
  magick "$floor" -virtual-pixel edge -distort Perspective '64,0 0,0 192,0 128,0 0,64 0,64 256,64 128,64' \
    -crop 128x64+0+0 +repage "$tmp/f.png"
  magick "$tmp/f.png" "$tmp/f.png" -append "$tmp/f2.png"
  finish "$tmp/f2.png" floor "$theme"
  # Ceiling: near edge 256 wide at the top, far edge 128 wide at the bottom.
  magick "$ceiling" -virtual-pixel edge -distort Perspective '0,0 0,0 256,0 128,0 64,64 0,64 192,64 128,64' \
    -crop 128x64+0+0 +repage "$tmp/c.png"
  magick "$tmp/c.png" "$tmp/c.png" -append "$tmp/c2.png"
  finish "$tmp/c2.png" ceiling "$theme"
  echo "$theme <- $set"
done
