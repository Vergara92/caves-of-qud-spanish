"""Valida SpanishLanguage/languages/*.es.xml contra ExampleLanguage/*.example.xml.

Uso:
  python3 scripts/validate_translation.py [--resumen] [--formas ARCHIVO ...]
                                          [--en DIR] [--es DIR]

Los hijos se emparejan por clave (etiqueta + ID + Context + Name) y se baja a
todas las entradas aunque no cuadre el número de hijos (ver scripts/qud_xml.py).
--formas valida además XML de formas con raíz <concordancia> que estén fuera de
--es (los de dentro se encuentran solos). --resumen añade el recuento de errores
y avisos por archivo y por tipo.
"""
from collections import Counter
from pathlib import Path
import argparse
import re
import sys

from lxml import etree

from qud_xml import (CONCORD_VALUES, KEY_ATTRS, MARK, element_children,
                     is_keyed, is_xtag, key_label, pair_children)

ROOT = Path(__file__).resolve().parent.parent

EN_DIR = ROOT / "ExampleLanguage"
IT_DIR = ROOT / "SpanishLanguage/languages"

placeholder_pattern = re.compile(r"\{\d+\}|%[sd]")

FORMS_ROOT = "concordancia"
FORMS_ATTRS = ("ms", "fs", "mp", "fp")
FORMS_EXTRA_ATTRS = {"clase"}
STRINGS_FILE = "Strings.es.xml"


class Report:
    def __init__(self):
        self.errors = []    # (tipo, archivo, mensaje)
        self.warnings = []

    def error(self, kind, file, msg):
        self.errors.append((kind, str(file), msg))

    def warn(self, kind, file, msg):
        self.warnings.append((kind, str(file), msg))


def load_xml(path, rep):
    try:
        parser = etree.XMLParser(recover=False)
        return etree.parse(str(path), parser)
    except Exception as e:
        rep.error("Malformed XML", path, f"Malformed XML: {path} -> {e}")
        return None


def check_placeholders(en_text, it_text, file, en_elem, rep):
    en_p = sorted(placeholder_pattern.findall(en_text))
    it_p = sorted(placeholder_pattern.findall(it_text))

    if en_p != it_p:
        rep.error(
            "Placeholder mismatch", file,
            f"[Line {en_elem.sourceline}] "
            f"Placeholder mismatch in {file}\n"
            f"EN: {en_text}\n"
            f"IT: {it_text}"
        )


def check_concord(it_root, file, rep):
    # concord en cualquier xtag* (nuevo o existente): solo valores conocidos
    for el in it_root.iter():
        if isinstance(el.tag, str) and is_xtag(el.tag) and "concord" in el.attrib:
            v = el.get("concord")
            if v not in CONCORD_VALUES:
                parent = el.getparent()
                owner = parent.get("Name") if parent is not None else None
                rep.error(
                    "Invalid concord", file,
                    f"[Line {el.sourceline}] Invalid concord in {file}: "
                    f"'{v}' (object '{owner}'; allowed: {'|'.join(CONCORD_VALUES)})"
                )


def compare_elements(en_elem, it_elem, file, rep):
    en_children = element_children(en_elem)

    if en_elem.tag != it_elem.tag:
        rep.error(
            "Tag mismatch", file,
            f"[Line {en_elem.sourceline}] "
            f"Tag mismatch in {file}: "
            f"{en_elem.tag} != {it_elem.tag}"
        )

    en_attr_keys = set(en_elem.attrib.keys())
    it_attr_keys = set(it_elem.attrib.keys())
    # Ignore missing attrs that are empty-valued in source (e.g. Context="")
    missing_attrs = {a for a in en_attr_keys - it_attr_keys if en_elem.get(a)}
    extra_attrs = it_attr_keys - en_attr_keys
    if is_xtag(en_elem.tag):
        extra_attrs -= {"concord"}
    if missing_attrs:
        rep.error(
            "Attribute mismatch", file,
            f"[Line {en_elem.sourceline}] "
            f"Attribute mismatch in {file}: "
            f"{en_elem.tag} missing attrs {sorted(missing_attrs)}"
        )
    elif extra_attrs:
        rep.warn(
            "IT-only attributes", file,
            f"[Line {en_elem.sourceline}] "
            f"IT-only attributes in {file}: "
            f"{en_elem.tag} extra attrs {sorted(extra_attrs)}"
        )

    for attr in KEY_ATTRS:
        en_val = en_elem.get(attr)
        it_val = it_elem.get(attr)
        # Treat empty-string Context as equivalent to missing Context
        en_norm = en_val or ''
        it_norm = it_val or ''
        # Attributes starting with ▶ are display text (translatable), not keys
        if en_norm and not en_norm.startswith(MARK) and en_norm != it_norm:
            rep.error(
                "Key attribute changed", file,
                f"[Line {en_elem.sourceline}] "
                f"Key attribute '{attr}' changed in {file}: "
                f"'{en_val}' -> '{it_val}'"
            )

    if en_elem.text and it_elem.text:
        check_placeholders(en_elem.text, it_elem.text, file, en_elem, rep)

        en_text = en_elem.text.strip()
        it_text = it_elem.text.strip()
        en_len = len(en_text)
        it_len = len(it_text)

        if en_len > 0:
            growth = ((it_len-en_len)/en_len)*100
            if growth > 50:
                rep.warn(
                    "Long translation", file,
                    f"[Line {en_elem.sourceline}] "
                    f"Long translation (+{growth:.0f}%) "
                    f"in {file}: {it_text[:60]}"
                )

        if len(en_children) == 0:
            # Skip strings where EN text == ID: these are intentional passthroughs
            # (proper nouns, game-specific terms that need no translation)
            en_id = en_elem.get("ID", "")
            en_text_bare = en_text.lstrip(MARK).strip()
            passthrough = en_id and en_text_bare == en_id.strip()
            if (
                not passthrough
                and en_text
                and it_text
                and len(en_text) > 3
                and re.search(r"[A-Za-z]", en_text)
                and en_text == it_text
            ):
                rep.warn(
                    "Possibly untranslated string", file,
                    f"[Line {en_elem.sourceline}] "
                    f"Possibly untranslated string in {file}: "
                )

    p = pair_children(en_elem, it_elem)

    for k, n_en, n_it in p.duplicates:
        rep.warn(
            "Duplicate key", file,
            f"[Line {en_elem.sourceline}] "
            f"Duplicate key in {file}: {key_label(k)} (EN {n_en}, ES {n_it})"
        )
    for k, n_en, n_it in p.unaligned:
        msg = (f"[Line {en_elem.sourceline}] "
               f"Node count mismatch in {file}: <{k[0]}> EN {n_en} != ES {n_it} "
               f"(not compared)")
        if n_it > n_en:
            # IT has more entries than source: may be intentional (e.g. an extra book page)
            rep.warn("Extra IT entries", file, msg)
        else:
            rep.error("Node count mismatch", file, msg)
    for k, left in p.missing:
        if is_keyed(k):
            for e in left:
                rep.error(
                    "Missing in IT translation", file,
                    f"[Line {e.sourceline}] "
                    f"Missing in IT translation {file}: {key_label(k)}"
                )
    for k, left in p.extra:
        if is_keyed(k):
            for e in left:
                rep.warn(
                    "Extra in IT translation", file,
                    f"[Line {en_elem.sourceline} / ES {e.sourceline}] "
                    f"Extra in IT translation {file}: {key_label(k)}"
                )
        elif k not in {u[0] for u in p.unaligned}:
            rep.warn(
                "Extra IT entries", file,
                f"[Line {en_elem.sourceline}] "
                f"Extra IT entries in {file}: {len(left)} additional <{k[0]}>"
            )

    for ec, ic in p.pairs:
        compare_elements(ec, ic, file, rep)


def strings_keys(it_dir):
    path = Path(it_dir) / STRINGS_FILE
    if not path.exists():
        return None
    root = etree.parse(str(path)).getroot()
    keys = {}
    for s in root.iter("string"):
        keys.setdefault(s.get("ID") or "", set()).add(s.get("Context") or "")
    return keys


def check_forms_file(path, rep, skeys, label=None):
    """XML aparte de formas (raíz <concordancia>). Formato provisional hasta CAV-36.2:
    el de _equipo/prototipo_c/mod/datos/ (<string ID Context ms fs mp fp [clase]>B</string>)."""
    file = label or path
    tree = load_xml(path, rep)
    if tree is None:
        return
    raw = Path(path).read_bytes()
    root = tree.getroot()
    if root.tag != FORMS_ROOT:
        rep.error("Forms root", file, f"Forms file {file}: root <{root.tag}> != <{FORMS_ROOT}>")
        return
    if root.get("Lang") != "es":
        rep.error("Root Lang", file, f"Root Lang in {file}: '{root.get('Lang')}' != 'es'")
    if raw.startswith(b"\xef\xbb\xbf") and (tree.docinfo.encoding or "").lower() not in ("utf-8", "utf8"):
        rep.error("BOM", file, f"BOM in {file} but encoding '{tree.docinfo.encoding}'")

    seen = Counter()
    for el in element_children(root):
        line = f"[Line {el.sourceline}]"
        if el.tag != "string":
            rep.warn("Forms entry", file, f"{line} Unknown element in forms file {file}: <{el.tag}>")
            continue
        id_, ctx = el.get("ID"), el.get("Context") or ""
        if not id_:
            rep.error("Forms entry", file, f"{line} Forms entry without ID in {file}")
            continue
        seen[(id_, ctx)] += 1
        if seen[(id_, ctx)] == 2:
            rep.warn("Duplicate key", file, f"{line} Duplicate key in {file}: ID='{id_}' Context='{ctx}'")
        if not (el.text or "").strip():
            rep.error("Forms entry", file, f"{line} Forms entry without fallback text (B) in {file}: ID='{id_}'")
        present = [a for a in FORMS_ATTRS if el.get(a) is not None]
        if present and len(present) != len(FORMS_ATTRS):
            rep.error("Forms entry", file,
                      f"{line} Incomplete forms in {file}: ID='{id_}' has {present}")
        for a in present:
            if not el.get(a).strip():
                rep.error("Forms entry", file, f"{line} Empty form '{a}' in {file}: ID='{id_}'")
        unknown = set(el.attrib) - {"ID", "Context"} - set(FORMS_ATTRS) - FORMS_EXTRA_ATTRS
        if unknown:
            rep.warn("Forms entry", file, f"{line} Unknown attrs in {file}: ID='{id_}' {sorted(unknown)}")
        if skeys is None:
            continue
        if id_ not in skeys:
            rep.warn("Forms key not in Strings", file,
                     f"{line} Forms key not in {STRINGS_FILE} ({file}): ID='{id_}' Context='{ctx}'")
        elif ctx not in skeys[id_]:
            rep.warn("Forms key not in Strings", file,
                     f"{line} Forms key not in {STRINGS_FILE} ({file}): ID='{id_}' Context='{ctx}' "
                     f"(ID exists with Context {sorted(skeys[id_])})")


def validate(en_dir=EN_DIR, it_dir=IT_DIR, forms_files=(), verbose=False):
    en_dir, it_dir = Path(en_dir), Path(it_dir)
    rep = Report()
    files = sorted(en_dir.rglob('*.xml'))
    if verbose:
        print(f"Found {len(files)} English XML files")

    for en_file in files:
        if verbose:
            print(f"Checking: {en_file}")

        rel = en_file.relative_to(en_dir)
        translated_name = en_file.name.replace(".example.xml", ".es.xml")
        it_file = it_dir / en_file.parent.relative_to(en_dir) / translated_name

        if not it_file.exists():
            rep.error("Missing translated file", rel, f"Missing translated file: {rel}")
            continue

        en_tree = load_xml(en_file, rep)
        it_tree = load_xml(it_file, rep)
        if en_tree is None or it_tree is None:
            continue

        it_root = it_tree.getroot()
        if it_root.get("Lang") != "es":
            rep.error("Root Lang", rel, f"Root Lang in {rel}: '{it_root.get('Lang')}' != 'es'")
        check_concord(it_root, rel, rep)
        compare_elements(en_tree.getroot(), it_root, rel, rep)

    # XML de formas: los que haya en it_dir (por raíz) y los pasados a mano
    skeys = strings_keys(it_dir)
    forms = []
    for f in sorted(it_dir.rglob("*.xml")):
        try:
            with open(f, "rb") as fh:
                for _, el in etree.iterparse(fh, events=("start",)):
                    if el.tag == FORMS_ROOT:
                        forms.append(f)
                    break
        except etree.XMLSyntaxError:
            if f.name.startswith(FORMS_ROOT):
                forms.append(f)
    forms += [Path(f) for f in forms_files if Path(f).resolve() not in {x.resolve() for x in forms}]
    for f in forms:
        check_forms_file(f, rep, skeys, label=f.relative_to(it_dir) if it_dir in f.parents else f)
    return rep


def print_summary(rep):
    for title, items in (("ERRORES", rep.errors), ("AVISOS", rep.warnings)):
        print(f"\n{title} por tipo:")
        for k, n in Counter(k for k, _, _ in items).most_common():
            print(f"  {n:5d}  {k}")
        print(f"{title} por archivo y tipo:")
        for (f, k), n in sorted(Counter((f, k) for k, f, _ in items).items()):
            print(f"  {n:5d}  {f}  ·  {k}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--en", default=EN_DIR)
    ap.add_argument("--es", default=IT_DIR)
    ap.add_argument("--formas", nargs="*", default=[])
    ap.add_argument("--resumen", action="store_true")
    args = ap.parse_args(argv)

    print(f"Scanning English directory: {Path(args.en).resolve()}")
    print(f"Scanning Spanish directory: {Path(args.es).resolve()}")
    rep = validate(args.en, args.es, args.formas, verbose=True)

    if rep.warnings:
        print("\nWARNINGS:")
        for _, _, w in rep.warnings:
            print(w)
    if rep.errors:
        print("\nERRORS:")
        for _, _, e in rep.errors:
            print(e)
    if args.resumen:
        print_summary(rep)

    print("DEBUG SUMMARY")
    print(f"Files checked: {len(list(Path(args.en).rglob('*.xml')))}")
    print(f"Warnings: {len(rep.warnings)}")
    print(f"Errors: {len(rep.errors)}")
    if rep.errors:
        return 1
    print("All validations passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
