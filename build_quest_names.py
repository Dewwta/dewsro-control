#!/usr/bin/env python3
"""
build_quest_names.py

Reads textquest_speech&name.txt (UTF-16 LE with BOM) and extracts quest display
names from lines 486-1612 (inclusive, 1-indexed).

Output: vsro-panel/src/lib/data/questNames.ts
  Exports QUEST_NAMES: Record<string, string>
  Keys match QuestParser.QuestName exactly (SN_ prefix stripped).
  e.g.  "QNO_CH_SMITH_1" -> "Weapon Dealer's Letter"

Usage:
  python build_quest_names.py
  Run from the repo root (the folder containing VSRO_CONTROL_API/ and vsro-panel/).
"""

import pathlib

TEXTDATA   = pathlib.Path("VSRO_CONTROL_API/textquest_speech&name.txt")
OUTPUT     = pathlib.Path("vsro-panel/src/lib/data/questNames.ts")
LINE_START = 486   
LINE_END   = 1612


def main():
    if not TEXTDATA.exists():
        raise FileNotFoundError(f"Not found: {TEXTDATA}\nRun from the repo root.")

    text  = TEXTDATA.read_text(encoding="utf-16")
    lines = text.splitlines()

    mapping: dict[str, str] = {}

    for i, line in enumerate(lines, start=1):
        if i < LINE_START:
            continue
        if i > LINE_END:
            break

        parts = line.split("\t")
        if len(parts) < 2:
            continue

        sn_code      = parts[1].strip()
        display_name = parts[-1].strip()

        if not sn_code or not display_name:
            continue

        # Strip leading "SN_" so keys match QuestParser's QuestName field
        key = sn_code[3:] if sn_code.startswith("SN_") else sn_code

        if key and display_name:
            mapping[key] = display_name

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)

    out_lines = [
        "// Auto-generated from textquest_speech&name.txt — do not edit manually.",
        "// Regenerate: python build_quest_names.py  (run from repo root)",
        f"// {len(mapping)} quests mapped (source lines {LINE_START}–{LINE_END})",
        "",
        "export const QUEST_NAMES: Record<string, string> = {",
    ]

    for key in sorted(mapping):
        name     = mapping[key]
        safe_key = key.replace("\\", "\\\\").replace('"', '\\"')
        safe_val = name.replace("\\", "\\\\").replace('"', '\\"')
        out_lines.append(f'  "{safe_key}": "{safe_val}",')

    out_lines.append("};")
    out_lines.append("")

    OUTPUT.write_text("\n".join(out_lines), encoding="utf-8")
    print(f"OK — wrote {len(mapping)} entries to {OUTPUT}")


if __name__ == "__main__":
    main()
