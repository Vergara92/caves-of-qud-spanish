"""Unifica términos de glosario SOLO en el texto de elemento (entre > y <).

Nunca toca atributos (ID/Context/Name/Value...), así que las claves inglesas
quedan intactas. Preserva mayúscula inicial.
"""
from pathlib import Path
import re

ES_DIR = Path(__file__).resolve().parent.parent / "SpanishLanguage/languages"

# (patrón regex, reemplazo canónico) — case-insensitive, se preserva la inicial
REPLACEMENTS = [
    (r"vid acu[áa]tica", "viñagua"),
    (r"watervine", "viñagua"),
    (r"cristal de acero", "crysteel"),
    (r"cristacero", "crysteel"),
    (r"cromel[íi]n", "chromeling"),
    (r"cromadito", "chromeling"),
    (r"artesan[íi]a", "manitas"),
]

TEXT_SEGMENT = re.compile(r">([^<]*)<", re.DOTALL)

def preserve_case(orig, repl):
    return repl[:1].upper() + repl[1:] if orig[:1].isupper() else repl

def fix_text(text):
    for pat, repl in REPLACEMENTS:
        text = re.sub(pat, lambda m: preserve_case(m.group(0), repl), text, flags=re.IGNORECASE)
    return text

total = {}
for f in sorted(ES_DIR.glob("*.es.xml")):
    raw = f.read_text(encoding="utf-8")
    n = [0]
    def sub_seg(m):
        inner = m.group(1)
        new = fix_text(inner)
        if new != inner:
            n[0] += 1
        return ">" + new + "<"
    out = TEXT_SEGMENT.sub(sub_seg, raw)
    if out != raw:
        f.write_text(out, encoding="utf-8")
        total[f.name] = n[0]

print("Segmentos de texto modificados por archivo:")
for k, v in sorted(total.items(), key=lambda x: -x[1]):
    print(f"  {v:4d}  {k}")
print(f"Total archivos tocados: {len(total)}")
