"""Regenerate tokens.json from tokens.css:  python3 design-system/build-tokens.py"""
import re, json, pathlib
here = pathlib.Path(__file__).parent
css = (here / "tokens.css").read_text(encoding="utf-8")
root = css[css.index(":root {"):css.index("}", css.index(":root {"))]
vals = dict(re.findall(r"--abr-([\w-]+):\s*([^;]+);", root))
def resolve(v, d=0):
    m = re.fullmatch(r"var\(--abr-([\w-]+)\)", v.strip())
    return resolve(vals[m.group(1)], d + 1) if m and d < 5 else v.strip()
out = {}
for k, v in vals.items():
    out[k] = {"$value": resolve(v), "$extensions": {"cssVar": f"--abr-{k}", "alias": v.strip()}}
(here / "tokens.json").write_text(json.dumps(out, ensure_ascii=False, indent=2), encoding="utf-8")
print(f"{len(out)} tokens written")
