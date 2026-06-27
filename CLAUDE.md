# Caves of Qud — Localización al español (España)

## Contexto
Traducción NO oficial de *Caves of Qud* al español de España, sobre el framework de
localización de la rama `lang-experimental`. El sistema es procedural y usa:
* `HistorySpice.json` (plantillas dinámicas)
* `Strings.es.xml` y demás `*.es.xml` (cadenas localizadas)
* `Genders.es.xml` / `PronounSets.es.xml` (términos por género/pronombre)

Las cadenas se recombinan en tiempo de ejecución: NO son frases estáticas.

## Restricción crítica
* El sistema NO maneja género ni número de forma fiable.
* NO hay helpers de concordancia. NUNCA uses `|pluralize=`.
* El español concuerda en TODO (artículos, adjetivos, participios): por eso la
  estrategia anti-concordancia pesa aquí más que en italiano.

## Objetivo
No traducir literal, sino **reescribir las cadenas para que sean robustas en todas
las combinaciones procedurales**, preservando la sintaxis de plantillas (`=...=`,
`{{C|...}}`).

## Estrategia (resumen; detalle en `prompts/prompt-xml.txt`)
Eliminar la concordancia donde el referente sea desconocido en runtime:
* Adjetivos → sustantivos / sintagmas neutros ("de aspecto antiguo").
* Participios que concuerdan → sustantivo o reformulación ("con roturas").
* Evitar artículos y adjetivos cuando el género/número es variable.
* Preferir verbos, formas impersonales y cuantificadores invariables.
* Donde el género SÍ es conocido (tabla Genders: hombre/mujer, hijo/hija…), usarlo correcto.

## Reglas duras
* NUNCA romper ni quitar la sintaxis de plantilla (`=...=`, `{{...}}`).
* NUNCA hardcodear valores dinámicos.
* Solo modificar el texto tras `▶`; no tocar atributos ID/Context/Name ni la estructura XML.
* Conservar entidades XML (`&#xA;`, `&quot;`…).

## Flujo de trabajo
1. **Escanear**: identificar solo las líneas problemáticas (plantillas, construcciones
   spice, frases que exigen concordancia). Salida: nº de líneas y resumen breve. No volcar archivos.
2. **Arreglar por lotes** (~500 líneas). Usar scripts de Python para diff/índices si ayuda.
3. **Validar**: `python3 scripts/validate_translation.py` debe pasar sin errores.

## Calidad de cada arreglo
* Estable en TODAS las combinaciones.
* Sin concordancia de género/número donde el referente sea variable.
* Plantillas preservadas EXACTAS.
* Tono natural, ligeramente arcaico y evocador.

## Rendimiento
* No volcar archivos enteros. Trabajar incremental. Minimizar salida.

## Estado y decisiones
* Variante objetivo: **español de España** (tú / vosotros, léxico peninsular).
* Código de idioma provisional: `es` (`languages.xml`). **Pendiente de verificar**
  contra lo que espera `lang-experimental` (podría ser `es`, `es-ES` o un slot propio).
* Pendiente: confirmar en el Discord oficial que no haya otro esfuerzo de español
  en curso antes de la traducción masiva.
* `italian-reference/` es el repo italiano clonado, solo como referencia.
