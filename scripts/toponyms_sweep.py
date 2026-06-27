"""Unifica topónimos propios de Qud SOLO en texto visible (entre > y <).

No toca claves (ID/Context/Name son atributos). Reglas decididas tras inspección:
- "Pilar de los Seis Días" / "Sesenta Pilares" / "Seis Pilares" -> Six Day Stilt
- "Escalera de la Luna" -> Moon Stair
- "Huso" (mayúscula, landmark) -> Spindle  [la minúscula 'huso' se respeta:
  versos donde el inglés usa minúscula + un objeto genérico 'huso de verdecaña']
- "Eaters" -> Devoradores (facción, para casar con 'Tumba de los Devoradores')
"""
from pathlib import Path
import re

ES_DIR = Path(__file__).resolve().parent.parent / "SpanishLanguage/languages"
TEXT_SEGMENT = re.compile(r">([^<]*)<", re.DOTALL)

# (patrón, reemplazo, flags)
RULES = [
    (re.compile(r"Pilar de los Seis Días", re.I), "Six Day Stilt"),
    (re.compile(r"Sesenta Pilares", re.I), "Sixty Stilts"),  # variante coloquial del juego
    (re.compile(r"Escalera de la Luna", re.I), "Moon Stair"),
    (re.compile(r"\bHuso\b"), "Spindle"),                 # solo mayúscula
    (re.compile(r"\bEaters\b"), "Devoradores"),
    (re.compile(r"\beaters\b"), "devoradores"),
]

counts = {}
for f in sorted(ES_DIR.glob("*.es.xml")):
    raw = f.read_text(encoding="utf-8")
    n = [0]
    def sub_seg(m):
        inner = m.group(1); new = inner
        for pat, repl in RULES:
            new = pat.sub(repl, new)
        if new != inner:
            n[0] += 1
        return ">" + new + "<"
    out = TEXT_SEGMENT.sub(sub_seg, raw)
    if out != raw:
        f.write_text(out, encoding="utf-8")
        counts[f.name] = n[0]

print("Segmentos modificados por archivo:")
for k, v in sorted(counts.items(), key=lambda x: -x[1]):
    print(f"  {v:4d}  {k}")
print(f"Total archivos: {len(counts)}")
