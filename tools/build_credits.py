#!/usr/bin/env python3
"""Regenerates CREDITS.md from assets.manifest.json, the single record of every third-party asset (spec 15.3)."""
import json, os

root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
manifest = json.load(open(os.path.join(root, "assets.manifest.json")))
lines = ["# Credits", "",
         "Generated from `assets.manifest.json` by `tools/build_credits.py`. Every third-party asset the game ships is "
         "listed here.", ""]
for s in manifest["sources"]:
    commit = f" at commit `{s['commit'][:12]}`" if "commit" in s else ""
    lines += [f"## {s['title']}", "",
              f"- Source: [{s['url']}]({s['url']}){commit} (retrieved {s['retrieved']})",
              f"- Licence: {s['licence']} (`{s['licenceFile']}`)",
              f"- Authors: {s['authors']}",
              f"- Changes: {s['changes']}",
              f"- Files: {len(s['files'])} (listed in `assets.manifest.json`)", ""]
lines += ["## Everything else", "",
          "The creatures, items, portraits, doors, levers, pits, plates, stairs and markings are drawn in code. Every sound "
          "effect and ambience loop is synthesised by `tools/make_sounds.py`. Both are original to AVAHack, and the code is "
          "under the licence in `LICENSE`.", ""]
open(os.path.join(root, "CREDITS.md"), "w").write("\n".join(lines))
print(f"{len(manifest['sources'])} sources")
