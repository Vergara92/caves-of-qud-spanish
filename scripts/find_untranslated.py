"""Detecta cadenas que quedaron en inglés (EN==ES) comparando ejemplo vs traducción.

El ejemplo marca lo traducible con ▶. Emparejamos los nodos de ambos árboles
por clave (etiqueta + ID + Context + Name, ver qud_xml.py) y, para cada
texto/atributo marcado con ▶ en el ejemplo, comparamos con la versión española
(que ya no lleva ▶). Si coinciden y el texto contiene palabras inglesas reales,
lo marcamos como NO traducido. Las entradas que no casan no impiden revisar el
resto del archivo: se cuentan aparte (el detalle está en validate_translation.py).
"""
from pathlib import Path
from lxml import etree
import re

from qud_xml import MARK, walk_pairs

ROOT = Path(__file__).resolve().parent.parent
EN_DIR = ROOT / "ExampleLanguage"
ES_DIR = ROOT / "SpanishLanguage/languages"

def strip_mark(s):
    return s.replace(MARK, "").strip() if s else s

# ¿el valor tiene "palabras reales" fuera de plantillas/tags? (para descartar
# números, colores sueltos, puntuación, etc.)
def has_words(s):
    bare = re.sub(r"=[^=]*=", " ", s)          # quita =plantillas=
    bare = re.sub(r"\{\{[^}]*\}\}", " ", bare)  # quita {{tags}}
    bare = re.sub(r"[^A-Za-z]", " ", bare)
    return any(len(w) >= 4 for w in bare.split())

def find_untranslated(en_dir=EN_DIR, es_dir=ES_DIR, log=print):
    """{nombre de archivo sin .example.xml: [(contexto, texto)]}"""
    results = {}
    for en_file in sorted(Path(en_dir).rglob("*.xml")):
        es_file = Path(es_dir) / en_file.name.replace(".example.xml", ".es.xml")
        if not es_file.exists():
            continue
        try:
            en_tree = etree.parse(str(en_file), etree.XMLParser(recover=False))
            es_tree = etree.parse(str(es_file), etree.XMLParser(recover=False))
        except Exception as e:
            log(f"PARSE FAIL {en_file.name}: {e}")
            continue

        unmatched = [0]

        def count_unmatched(_en, _es, p):
            unmatched[0] += sum(len(x) for _, x in p.missing)

        pairs = list(walk_pairs(en_tree.getroot(), es_tree.getroot(), count_unmatched))
        if unmatched[0]:
            log(f"SIN PAREJA {en_file.name}: {unmatched[0]} nodos ingleses (no revisados)")

        hits = []
        for en_el, es_el in pairs:
            # texto del elemento
            if en_el.text and MARK in en_el.text:
                en_v = strip_mark(en_el.text)
                es_v = (es_el.text or "").strip()
                if en_v and en_v == es_v and has_words(en_v):
                    ctx = en_el.get("ID") or en_el.get("Name") or en_el.tag
                    hits.append((ctx, en_v[:70]))
            # atributos
            for attr, val in en_el.attrib.items():
                if MARK in val:
                    en_v = strip_mark(val)
                    es_v = (es_el.get(attr) or "").strip()
                    if en_v and en_v == es_v and has_words(en_v):
                        ctx = f"{en_el.get('ID') or en_el.get('Name') or en_el.tag}@{attr}"
                        hits.append((ctx, en_v[:70]))
        if hits:
            results[en_file.name.replace(".example.xml", "")] = hits
    return results


def main():
    results = find_untranslated()
    total = sum(len(v) for v in results.values())
    print(f"=== Posibles entradas SIN TRADUCIR (EN==ES con palabras reales): {total} ===\n")
    for f, hits in sorted(results.items(), key=lambda x: -len(x[1])):
        print(f"  {len(hits):4d}  {f}")

    # volcado detallado a fichero para los agentes
    out = ROOT / "scratch_untranslated.txt"
    with out.open("w") as fh:
        for f, hits in sorted(results.items(), key=lambda x: -len(x[1])):
            fh.write(f"\n##### {f} ({len(hits)})\n")
            for ctx, txt in hits:
                fh.write(f"  [{ctx}] {txt}\n")
    print(f"\nDetalle en: {out}")


if __name__ == "__main__":
    main()
