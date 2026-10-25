"""Emparejamiento de nodos entre ExampleLanguage/ y SpanishLanguage/ (CAV-36.1).

Lo usan validate_translation.py y find_untranslated.py.

Los hijos de un nodo se emparejan por clave y no por posición:
  clave = (etiqueta, ID, Context, Name)
salvo los atributos que en el inglés llevan ▶ en algún hermano de la misma
etiqueta (son texto traducible, no clave: Name en Naming, Worlds, Relics…).
Los hijos sin clave (p, br, stat, prefix…) se emparejan por orden dentro de su
etiqueta, y solo si hay el mismo número en los dos lados: si no, el orden ya no
es fiable (una página insertada desplaza todas las demás) y el grupo entero
queda sin emparejar, en missing/extra. Los xtag* que solo están en español
(p. ej. <xtagGrammar concord="fs"/>) no se emparejan ni cuentan como sobrantes.
"""
from collections import OrderedDict

MARK = "▶"
KEY_ATTRS = ("ID", "Context", "Name")
CONCORD_VALUES = ("ms", "fs", "mp", "fp", "cs", "cp")


def element_children(elem):
    # Fuera comentarios e instrucciones de proceso: no son contenido traducible
    return [c for c in elem if isinstance(c.tag, str)]


def is_xtag(tag):
    return tag.startswith("xtag")


def _key_attrs_by_tag(en_children):
    marked = {}
    for c in en_children:
        s = marked.setdefault(c.tag, set())
        for a in KEY_ATTRS:
            if MARK in (c.get(a) or ""):
                s.add(a)
    return {t: tuple(a for a in KEY_ATTRS if a not in s) for t, s in marked.items()}


def _key(elem, key_attrs):
    attrs = key_attrs.get(elem.tag, KEY_ATTRS)
    # Context="" equivale a no tener Context
    return (elem.tag, tuple((a, elem.get(a) or "") for a in attrs))


def is_keyed(key):
    return any(v for _, v in key[1])


def key_label(key):
    tag, attrs = key
    parts = [f"{a}='{v}'" for a, v in attrs if v]
    return f"<{tag}> " + " ".join(parts) if parts else f"<{tag}>"


class Pairing:
    """Resultado de emparejar los hijos de un nodo inglés con los del español."""

    def __init__(self):
        self.pairs = []        # (en, es) emparejados
        self.missing = []      # (clave, [en sin pareja])
        self.extra = []        # (clave, [es sin pareja])
        self.duplicates = []   # (clave, n_en, n_es) claves repetidas entre hermanos
        self.new_xtags = []    # xtag* solo en español (se aceptan)
        self.unaligned = []    # (clave, n_en, n_es) grupos sin clave que no cuadran


def pair_children(en_elem, es_elem):
    en_children = element_children(en_elem)
    es_children = element_children(es_elem)
    en_tags = {c.tag for c in en_children}
    key_attrs = _key_attrs_by_tag(en_children)

    res = Pairing()
    en_groups = OrderedDict()
    for c in en_children:
        en_groups.setdefault(_key(c, key_attrs), []).append(c)
    es_groups = OrderedDict()
    for c in es_children:
        if is_xtag(c.tag) and c.tag not in en_tags:
            res.new_xtags.append(c)
            continue
        es_groups.setdefault(_key(c, key_attrs), []).append(c)

    for k, en_list in en_groups.items():
        es_list = es_groups.get(k, [])
        if is_keyed(k) and (len(en_list) > 1 or len(es_list) > 1):
            res.duplicates.append((k, len(en_list), len(es_list)))
        if not is_keyed(k) and len(en_list) != len(es_list):
            res.unaligned.append((k, len(en_list), len(es_list)))
            res.missing.append((k, en_list))
            if es_list:
                res.extra.append((k, es_list))
            continue
        res.pairs.extend(zip(en_list, es_list))
        if len(en_list) > len(es_list):
            res.missing.append((k, en_list[len(es_list):]))
        elif len(es_list) > len(en_list):
            res.extra.append((k, es_list[len(en_list):]))
    for k, es_list in es_groups.items():
        if k not in en_groups:
            if is_keyed(k) and len(es_list) > 1:
                res.duplicates.append((k, 0, len(es_list)))
            res.extra.append((k, es_list))
    return res


def walk_pairs(en_elem, es_elem, on_pairing=None):
    """Recorre en profundidad todos los pares emparejados (incluida la raíz)."""
    stack = [(en_elem, es_elem)]
    while stack:
        en, es = stack.pop()
        yield en, es
        p = pair_children(en, es)
        if on_pairing:
            on_pairing(en, es, p)
        stack.extend(reversed(p.pairs))
