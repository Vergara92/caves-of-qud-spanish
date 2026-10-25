"""Pruebas de CAV-36.1 (validador y find_untranslated) sobre fixtures pequeños.

  python3 -m unittest discover -s scripts/tests -v
"""
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from find_untranslated import find_untranslated  # noqa: E402
from validate_translation import validate  # noqa: E402

FIX = Path(__file__).resolve().parent / "fixtures"


def run(case, forms=()):
    rep = validate(FIX / case / "en", FIX / case / "es", forms)
    return [m for _, _, m in rep.errors], [m for _, _, m in rep.warnings]


def kinds(rep_items, kind):
    return [m for k, _, m in rep_items if k == kind]


class Clave(unittest.TestCase):
    """1 · Empareja por ID+Context aunque el orden cambie; duplicados con aviso propio."""

    def setUp(self):
        self.rep = validate(FIX / "clave/en", FIX / "clave/es")

    def test_orden_distinto_no_da_errores_de_clave(self):
        self.assertEqual(kinds(self.rep.errors, "Key attribute changed"), [])
        self.assertEqual(kinds(self.rep.errors, "Missing in IT translation"), [])
        self.assertEqual(kinds(self.rep.warnings, "Extra in IT translation"), [])

    def test_mismo_id_otro_context_son_entradas_distintas(self):
        # Si se emparejara solo por ID, «sword/Verb» casaría con «espada»
        self.assertEqual(len(kinds(self.rep.errors, "Placeholder mismatch")), 1)

    def test_duplicado_de_clave_avisa(self):
        dup = kinds(self.rep.warnings, "Duplicate key")
        self.assertEqual(len(dup), 1)
        self.assertIn("ID='dup' Context='Twice'", dup[0])
        self.assertIn("(EN 2, ES 2)", dup[0])


class Placeholders(unittest.TestCase):
    """4 · Un placeholder que falta da error, no NameError."""

    def test_placeholder_da_error_y_no_excepcion(self):
        errors, _ = run("clave")
        ph = [e for e in errors if "Placeholder mismatch" in e]
        self.assertEqual(len(ph), 1)
        self.assertIn("You hit {0}.", ph[0])


class Descenso(unittest.TestCase):
    """2 · Baja a todas las entradas aunque no cuadre el número de hijos."""

    def setUp(self):
        self.errors, self.warnings = run("descenso")

    def test_sobrantes_y_faltantes_con_su_clave(self):
        self.assertTrue(any("Missing in IT translation" in e and "ID='c'" in e for e in self.errors))
        extra = [w for w in self.warnings if "Extra in IT translation" in w]
        self.assertEqual(len(extra), 2)
        self.assertTrue(any("ID='solo_es'" in w for w in extra))

    def test_sigue_comparando_las_que_casan(self):
        # «b» pierde el {0}: antes no se veía porque la raíz no cuadraba (4 != 3)
        self.assertTrue(any("Placeholder mismatch" in e and "beta {0}" in e for e in self.errors))

    def test_grupo_sin_clave_descuadrado_no_se_empareja_por_orden(self):
        pages = [w for w in self.warnings if "<page> EN 2 != ES 3" in w]
        self.assertEqual(len(pages), 1)


class Concord(unittest.TestCase):
    """3 · concord se acepta sin aviso; valor desconocido = error; el resto se sigue validando."""

    def setUp(self):
        self.errors, self.warnings = run("concord")

    def test_xtag_nuevo_y_concord_sin_aviso(self):
        self.assertEqual([w for w in self.warnings if "xtag" in w or "concord" in w], [])
        self.assertEqual([w for w in self.warnings if "Extra" in w], [])

    def test_concord_fuera_de_rango_es_error(self):
        bad = [e for e in self.errors if "Invalid concord" in e]
        self.assertEqual(len(bad), 1)
        self.assertIn("'xx'", bad[0])
        self.assertIn("Club", bad[0])

    def test_se_sigue_bajando_a_las_partes(self):
        # Club: <part Name="Render"> sin DisplayName, en un objeto con xtag nuevo
        self.assertTrue(any("missing attrs ['DisplayName']" in e for e in self.errors))
        self.assertEqual(len(self.errors), 2)


class Formas(unittest.TestCase):
    """6 · XML aparte de formas (raíz <concordancia>): no rompe el validador y se comprueba."""

    def test_se_encuentra_solo_y_se_valida(self):
        rep = validate(FIX / "formas/en", FIX / "formas/es")
        err = kinds(rep.errors, "Forms entry")
        self.assertTrue(any("Incomplete forms" in e for e in err))
        self.assertTrue(any("without fallback text" in e for e in err))
        self.assertEqual(len(err), 2)
        nokey = kinds(rep.warnings, "Forms key not in Strings")
        self.assertEqual(len(nokey), 1)
        self.assertIn("ID='keen'", nokey[0])
        self.assertEqual(len(kinds(rep.warnings, "Duplicate key")), 2)  # Strings y formas

    def test_lang_y_xml_mal_formado(self):
        fuera = FIX / "formas/fuera"
        rep = validate(FIX / "formas/en", FIX / "formas/es",
                       [fuera / "concordancia.es.xml", fuera / "rota.es.xml"])
        self.assertEqual(len(kinds(rep.errors, "Root Lang")), 1)
        self.assertEqual(len(kinds(rep.errors, "Malformed XML")), 1)


class FindUntranslated(unittest.TestCase):
    """5 · find_untranslated no se salta archivos cuyo número de nodos no cuadra."""

    def test_revisa_archivos_descuadrados(self):
        log = []
        res = find_untranslated(FIX / "descenso/en", FIX / "descenso/es", log.append)
        self.assertEqual(res.get("Strings"), [("a", "alpha")])
        self.assertTrue(any("SIN PAREJA Strings" in m for m in log))
        self.assertTrue(any("SIN PAREJA Books" in m for m in log))


if __name__ == "__main__":
    unittest.main()
