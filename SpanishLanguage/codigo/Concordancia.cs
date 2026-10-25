// CAV-29.A / CAV-24.E2 · Opción C. Lógica de concordancia SIN dependencias del juego:
// se puede probar fuera de Unity y es lo único que habría que tocar si cambia el formato de datos.
// Formato de datos: _equipo/prototipo_c/FORMATO.md (B.4):
//   · género/número del nombre: <xtagGrammar concord="ms|fs|mp|fp|cs|cp" /> en el blueprint;
//   · formas del adjetivo: XML aparte de raíz «concordancia» (concordancia.es.xml). Sus <string> remiten
//     por Context + ID a un <string> de Strings.es.xml, cuyo texto (el respaldo B) es lo que llega a
//     AddAdjective; sus <literal> dan ese texto directamente (literales de C#, adjetivos de Liquids.es.xml).
//     Strings.es.xml no lleva formas: solo el texto B y el OrderAdjust.
// Sintaxis conservadora (C# 7.3) a propósito: el compilador Roslyn del juego es 3.11 (C# ≤ 9).
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace CavesOfQudEs.Concordancia
{
	public enum Genero
	{
		Masculino,
		Femenino
	}

	public struct Rasgos
	{
		public Genero Genero;

		public bool Plural;

		public Rasgos(Genero genero, bool plural)
		{
			Genero = genero;
			Plural = plural;
		}

		// 0 = ms, 1 = fs, 2 = mp, 3 = fp (mismo orden que Entrada.Formas)
		public int Indice => (Plural ? 2 : 0) + (Genero == Genero.Femenino ? 1 : 0);

		/// <summary>
		/// ms/fs/mp/fp → directo; cs/cp → el género sale de los pronombres de la criatura (generoComun);
		/// cualquier otra cosa (null incluido) → null = respaldo B.
		/// </summary>
		public static Rasgos? DeConcord(string concord, Func<Genero> generoComun)
		{
			if (concord == null || concord.Length != 2)
			{
				return null;
			}
			bool plural;
			switch (concord[1])
			{
			case 's':
				plural = false;
				break;
			case 'p':
				plural = true;
				break;
			default:
				return null;
			}
			switch (concord[0])
			{
			case 'm':
				return new Rasgos(Genero.Masculino, plural);
			case 'f':
				return new Rasgos(Genero.Femenino, plural);
			case 'c':
				return new Rasgos(generoComun == null ? Genero.Masculino : generoComun(), plural);
			default:
				return null;
			}
		}

		/// <summary>Femenino si el juego de pronombres es femenino; si no (masculino, neutro, «they»…), masculino genérico.</summary>
		public static Genero GeneroDePronombres(string nombre, string sujeto)
		{
			nombre = (nombre ?? "").ToLowerInvariant();
			sujeto = (sujeto ?? "").ToLowerInvariant();
			if (nombre == "female" || nombre == "feminine" || sujeto == "she" || sujeto == "ella" || sujeto == "ellas")
			{
				return Genero.Femenino;
			}
			return Genero.Masculino;
		}

		public override string ToString()
		{
			return (Genero == Genero.Femenino ? "f" : "m") + (Plural ? "p" : "s");
		}
	}

	/// <summary>Orden de §4.1 de cav29_concordancia.md. «Delante» = se queda donde lo pone el motor.</summary>
	public enum Clase
	{
		Delante = 0,
		Relacional = 1,
		Calificativo = 2,
		De = 3,
		Con = 4
	}

	public class Entrada
	{
		/// <summary>Texto del elemento (respaldo B), con las variables «=…=» si es plantilla.</summary>
		public string Texto;

		/// <summary>Lo que sale sin concordancia: el texto del elemento (para un literal en inglés, su traducción).</summary>
		public string Respaldo;

		/// <summary>ms, fs, mp, fp; null si el adjetivo es invariable.</summary>
		public string[] Formas;

		/// <summary>Atributo opcional clase="…"; null = se deduce.</summary>
		public Clase? ClaseExplicita;

		/// <summary>
		/// El texto tenía «=…=»: cada variable casa con cualquier cosa (también dentro del color,
		/// «{{=liquid.color=|…}}») y en la forma se sustituye por lo que casó.
		/// </summary>
		public bool Plantilla;

		/// <summary>Plantilla → expresión anclada; el grupo «v{i}» es la variable Variables[i].</summary>
		public Regex Patron;

		public List<string> Variables;

		/// <summary>Letras visibles fuera de las variables: con más letras, más específica (se prueba antes).</summary>
		public int Literal;
	}

	/// <summary>
	/// Mapa texto → formas (de los &lt;string&gt;), mapa base → concord (de los blueprints) y reglas de montaje.
	/// </summary>
	public class TablaConcordancia
	{
		private static readonly string[] ATRIBUTOS_FORMA = { "ms", "fs", "mp", "fp" };

		// clave: texto exacto, con marcas de color
		private readonly Dictionary<string, Entrada> porTexto = new Dictionary<string, Entrada>(StringComparer.Ordinal);

		// clave: texto sin marcas, en minúsculas (por si el texto llega con otro color)
		private readonly Dictionary<string, Entrada> porTextoLimpio = new Dictionary<string, Entrada>(StringComparer.Ordinal);

		private readonly List<Entrada> plantillas = new List<Entrada>();

		// variable de plantilla del juego: «=liquid.color=», «=volume.liquid.stain.name#primary=»
		private static readonly Regex VARIABLE = new Regex("=([^=]+)=");

		// base sin marcas, en minúsculas → concord; null = ambigua (dos blueprints con el mismo nombre y distinto concord)
		private Dictionary<string, string> concordPorBase;

		/// <summary>De dónde salen los pares (DisplayName, concord) de los blueprints. Se consulta la primera vez que hace falta.</summary>
		public Func<IEnumerable<KeyValuePair<string, string>>> FuenteBases;

		public int NumEntradas => porTexto.Count + plantillas.Count;

		public int NumConFormas
		{
			get
			{
				int n = 0;
				foreach (Entrada e in porTexto.Values)
				{
					n += e.Formas != null ? 1 : 0;
				}
				foreach (Entrada e in plantillas)
				{
					n += e.Formas != null ? 1 : 0;
				}
				return n;
			}
		}

		/// <summary>Plantillas sin letras visibles fuera de las variables («=x=»): casarían con todo y no se usan.</summary>
		public int NumDescartadas { get; private set; }

		public int NumBases => concordPorBase == null ? 0 : concordPorBase.Count;

		/// <summary>Raíz del XML de formas (FORMATO.md).</summary>
		public const string RAIZ = "concordancia";

		private static readonly string[] ATRIBUTOS_STRING = { "Context", "ID", "respaldo", "ms", "fs", "mp", "fp", "clase" };

		private static readonly string[] ATRIBUTOS_LITERAL = { "Texto", "respaldo", "ms", "fs", "mp", "fp", "clase" };

		/// <summary>Avisos del lector (entradas descartadas o dudosas). También van al registro.</summary>
		public readonly List<string> Avisos = new List<string>();

		public int NumAvisos => Avisos.Count;

		/// <summary>
		/// Lee un XML de formas (raíz «concordancia», FORMATO.md):
		///   &lt;string Context="…" ID="…" [respaldo="…"] [ms fs mp fp] [clase] /&gt;  → lo que llega = texto de ese &lt;string&gt; de Strings
		///   &lt;literal Texto="…" [respaldo="…"] [ms fs mp fp] [clase] /&gt;           → lo que llega = Texto
		/// textoDeStrings(Context, ID): texto del &lt;string&gt; del idioma activo, o null si no está.
		/// Una entrada mal formada se descarta con un aviso; nunca lanza por los datos.
		/// </summary>
		public void CargarFormas(XmlReader r, Func<string, string, string> textoDeStrings, Action<string> aviso)
		{
			string archivo = r.BaseURI;
			Dictionary<string, Entrada> vistas = new Dictionary<string, Entrada>(StringComparer.Ordinal);
			while (r.Read())
			{
				if (r.NodeType != XmlNodeType.Element)
				{
					continue;
				}
				if (r.Depth == 0)
				{
					if (r.LocalName != RAIZ)
					{
						Avisar(aviso, archivo, r, "raíz <" + r.LocalName + "> (se esperaba <" + RAIZ + ">): se ignora el archivo");
						return;
					}
					continue;
				}
				string error;
				Entrada e = LeerEntrada(r, textoDeStrings, out error);
				if (error != null)
				{
					Avisar(aviso, archivo, r, error);
				}
				if (e == null)
				{
					continue;
				}
				// la misma clave dos veces en un archivo (dos Context con el mismo texto): vale si dicen lo mismo
				Entrada previa;
				if (vistas.TryGetValue(e.Texto, out previa) && !MismoContenido(previa, e))
				{
					Avisar(aviso, archivo, r, "«" + e.Texto + "» ya estaba en este archivo con otras formas, clase o respaldo: gana la última");
				}
				vistas[e.Texto] = e;
				Anadir(e);
			}
		}

		private static bool MismoContenido(Entrada a, Entrada b)
		{
			if (a.Respaldo != b.Respaldo || a.ClaseExplicita != b.ClaseExplicita || (a.Formas == null) != (b.Formas == null))
			{
				return false;
			}
			for (int i = 0; a.Formas != null && i < 4; i++)
			{
				if (a.Formas[i] != b.Formas[i])
				{
					return false;
				}
			}
			return true;
		}

		private void Avisar(Action<string> aviso, string archivo, XmlReader r, string texto)
		{
			IXmlLineInfo li = r as IXmlLineInfo;
			string donde = System.IO.Path.GetFileName(archivo ?? "") + (li != null && li.HasLineInfo() ? ":" + li.LineNumber : "");
			string s = donde + ": " + texto;
			Avisos.Add(s);
			if (aviso != null)
			{
				aviso(s);
			}
		}

		private static Entrada LeerEntrada(XmlReader r, Func<string, string, string> textoDeStrings, out string error)
		{
			error = null;
			string elemento = r.LocalName;
			string[] validos = elemento == "string" ? ATRIBUTOS_STRING : elemento == "literal" ? ATRIBUTOS_LITERAL : null;
			if (validos == null)
			{
				error = "elemento <" + elemento + "> desconocido (string|literal): se ignora";
				return null;
			}
			List<string> raros = new List<string>();
			if (r.MoveToFirstAttribute())
			{
				do
				{
					if (Array.IndexOf(validos, r.LocalName) < 0)
					{
						raros.Add(r.LocalName);
					}
				}
				while (r.MoveToNextAttribute());
				r.MoveToElement();
			}
			string texto;
			string desc;
			if (elemento == "string")
			{
				string id = r.GetAttribute("ID");
				string contexto = r.GetAttribute("Context");
				desc = "<string Context=\"" + contexto + "\" ID=\"" + id + "\">";
				if (string.IsNullOrEmpty(id))
				{
					error = "<string> sin ID: se ignora";
					return null;
				}
				texto = textoDeStrings == null ? null : textoDeStrings(contexto, id);
				if (texto == null)
				{
					error = desc + " no está en Strings: se ignora";
					return null;
				}
			}
			else
			{
				texto = r.GetAttribute("Texto");
				desc = "<literal Texto=\"" + texto + "\">";
				if (string.IsNullOrEmpty(texto))
				{
					error = "<literal> sin Texto: se ignora";
					return null;
				}
			}
			if (raros.Count > 0)
			{
				error = desc + " atributos desconocidos: " + string.Join(", ", raros.ToArray());
			}
			string respaldo = r.GetAttribute("respaldo");
			string clase = r.GetAttribute("clase");
			string[] formas = new string[4];
			bool hayFormas = false;
			for (int i = 0; i < 4; i++)
			{
				formas[i] = r.GetAttribute(ATRIBUTOS_FORMA[i]);
				hayFormas |= formas[i] != null;
			}
			Clase? claseExplicita;
			if (!LeerClase(clase, out claseExplicita))
			{
				error = desc + " clase=\"" + clase + "\" desconocida (delante|relacional|calificativo|de|con): se ignora";
				return null;
			}
			if (!hayFormas && claseExplicita == null && respaldo == null)
			{
				error = desc + " sin formas, clase ni respaldo: no hace nada";
				return null;
			}
			if (hayFormas)
			{
				// si falta alguna, se completa: ms ← texto; fs ← ms; mp ← ms; fp ← mp (el validador las exige todas)
				formas[0] = formas[0] ?? texto;
				formas[1] = formas[1] ?? formas[0];
				formas[2] = formas[2] ?? formas[0];
				formas[3] = formas[3] ?? formas[2];
			}
			return new Entrada { Texto = texto, Respaldo = respaldo ?? texto, Formas = hayFormas ? formas : null, ClaseExplicita = claseExplicita };
		}

		private void Anadir(Entrada e)
		{
			if (VARIABLE.IsMatch(e.Texto))
			{
				// plantilla: la variable es un comodín. «{{=liquid.color=|refrigerado por líquido}}» casa con
				// «{{B|refrigerado por líquido}}», y «manchado-de-=volume.liquid.stain.name#primary=» con «manchado-de-{{r|sangre}}»
				if (!Plantilla(e))
				{
					NumDescartadas++;
					return;
				}
				// la misma clave otra vez (otro archivo, cargado después): gana la última, como en porTexto
				plantillas.RemoveAll(delegate (Entrada p) { return p.Texto == e.Texto; });
				int k = 0;
				while (k < plantillas.Count && plantillas[k].Literal >= e.Literal)
				{
					k++;
				}
				plantillas.Insert(k, e);
			}
			else
			{
				porTexto[e.Texto] = e;
				porTextoLimpio[QuitarMarcas(e.Texto).ToLowerInvariant()] = e;
			}
		}

		/// <summary>Compila el patrón de la plantilla; false si fuera de las variables no queda ninguna letra visible.</summary>
		private static bool Plantilla(Entrada e)
		{
			int letras = 0;
			foreach (char c in QuitarMarcas(VARIABLE.Replace(e.Texto, "")))
			{
				if (char.IsLetter(c))
				{
					letras++;
				}
			}
			if (letras == 0)
			{
				return false;
			}
			StringBuilder patron = new StringBuilder("^");
			List<string> variables = new List<string>();
			int desde = 0;
			foreach (Match m in VARIABLE.Matches(e.Texto))
			{
				patron.Append(Regex.Escape(e.Texto.Substring(desde, m.Index - desde)));
				string nombre = m.Groups[1].Value;
				int i = variables.IndexOf(nombre);
				if (i < 0)
				{
					patron.Append("(?<v" + variables.Count + ">.*?)");
					variables.Add(nombre);
				}
				else
				{
					patron.Append("\\k<v" + i + ">");
				}
				desde = m.Index + m.Length;
			}
			patron.Append(Regex.Escape(e.Texto.Substring(desde))).Append("$");
			e.Plantilla = true;
			e.Patron = new Regex(patron.ToString(), RegexOptions.Singleline);
			e.Variables = variables;
			e.Literal = letras;
			return true;
		}

		/// <summary>La forma de una plantilla con cada «=variable=» cambiada por lo que casó en el texto que llegó.</summary>
		private static string Rellenar(Entrada e, string forma, string texto)
		{
			Match m = e.Patron.Match(texto);
			return VARIABLE.Replace(forma, delegate (Match v)
			{
				int i = e.Variables.IndexOf(v.Groups[1].Value);
				return i >= 0 && m.Success ? m.Groups["v" + i].Value : v.Value;
			});
		}

		private static bool LeerClase(string s, out Clase? clase)
		{
			switch (s)
			{
			case null:
				clase = null;
				return true;
			case "delante":
				clase = Clase.Delante;
				return true;
			case "relacional":
				clase = Clase.Relacional;
				return true;
			case "calificativo":
				clase = Clase.Calificativo;
				return true;
			case "de":
				clase = Clase.De;
				return true;
			case "con":
				clase = Clase.Con;
				return true;
			default:
				clase = null;
				return false;
			}
		}

		/// <summary>Entrada del texto, o null si no está en la tabla (= invariable, se usa tal cual).</summary>
		public Entrada Buscar(string texto)
		{
			Entrada e;
			if (porTexto.TryGetValue(texto, out e) || porTextoLimpio.TryGetValue(QuitarMarcas(texto).ToLowerInvariant(), out e))
			{
				return e;
			}
			foreach (Entrada p in plantillas)
			{
				if (p.Patron.IsMatch(texto))
				{
					return p;
				}
			}
			return null;
		}

		/// <summary>
		/// Forma del adjetivo y su clase. Con rasgos y formas → la forma concordada (clase explícita o
		/// calificativo). Si no → el texto tal cual (respaldo B) y la clase se deduce de su primera palabra.
		/// </summary>
		public string Forma(string texto, Rasgos? rasgos, out Clase clase)
		{
			Entrada e = Buscar(texto);
			if (e != null && e.ClaseExplicita == Clase.Delante)
			{
				clase = Clase.Delante;
				return texto;
			}
			if (e != null && e.Formas != null && rasgos.HasValue)
			{
				clase = e.ClaseExplicita ?? Clase.Calificativo;
				string forma = e.Formas[rasgos.Value.Indice];
				return e.Plantilla ? Rellenar(e, forma, texto) : forma;
			}
			// CAV-34: en una plantilla de los datos del mod (ID = texto del juego, texto = respaldo B) el respaldo
			// también se rellena con lo que casó: «manchado-de-{{r|sangre}}» → «con manchas de {{r|sangre}}»
			string respaldo = e == null ? texto : e.Plantilla ? Rellenar(e, e.Respaldo, texto) : e.Respaldo;
			clase = e != null && e.ClaseExplicita.HasValue && e.Formas == null ? e.ClaseExplicita.Value : ClaseDelTexto(respaldo);
			return respaldo;
		}

		/// <summary>
		/// CAV-34: texto que no está en la tabla pero se compone de fragmentos con color que sí están, separados
		/// por espacios («{{w|pegajoso}} {{r|ensangrentado}}», untado de varios líquidos, LiquidVolume.cs:849).
		/// Devuelve los fragmentos, o null si el texto está entero en la tabla o algún fragmento no está.
		/// </summary>
		public List<string> Fragmentos(string texto)
		{
			if (Buscar(texto) != null)
			{
				return null;
			}
			List<string> partes = new List<string>();
			int prof = 0, desde = 0;
			for (int i = 0; i < texto.Length; i++)
			{
				if (i + 1 < texto.Length && texto[i] == '{' && texto[i + 1] == '{')
				{
					prof++;
					i++;
				}
				else if (i + 1 < texto.Length && texto[i] == '}' && texto[i + 1] == '}')
				{
					prof--;
					i++;
				}
				else if (texto[i] == ' ' && prof == 0)
				{
					partes.Add(texto.Substring(desde, i - desde));
					desde = i + 1;
				}
			}
			partes.Add(texto.Substring(desde));
			if (partes.Count < 2)
			{
				return null;
			}
			foreach (string p in partes)
			{
				if (p.Length == 0 || Buscar(p) == null)
				{
					return null;
				}
			}
			return partes;
		}

		public bool EsDelante(string texto)
		{
			Entrada e = Buscar(texto);
			return e != null && e.ClaseExplicita == Clase.Delante;
		}

		/// <summary>«de …» → De; «con …»/«en …» → Con; lo demás → Calificativo.</summary>
		public static Clase ClaseDelTexto(string texto)
		{
			string p = PrimeraPalabraVisible(texto);
			if (p == "de" || p == "del")
			{
				return Clase.De;
			}
			if (p == "con" || p == "en")
			{
				return Clase.Con;
			}
			return Clase.Calificativo;
		}

		/// <summary>
		/// Regla de §1.1: si la base del builder es la del objeto → concord del objeto; si la base se cambió
		/// en tiempo de ejecución (ReplacePrimaryBase) → se busca en la tabla base → concord; si no → null (respaldo).
		/// </summary>
		public Rasgos? Resolver(string primaryBase, string displayNameBase, string concordObjeto, Func<Genero> generoComun)
		{
			string concord;
			if (primaryBase == null || primaryBase == displayNameBase)
			{
				concord = concordObjeto;
			}
			else
			{
				concord = ConcordDeBase(primaryBase);
			}
			return Rasgos.DeConcord(concord, generoComun);
		}

		public void AnadirBase(string displayName, string concord)
		{
			if (concordPorBase == null)
			{
				concordPorBase = new Dictionary<string, string>(StringComparer.Ordinal);
			}
			if (string.IsNullOrEmpty(displayName) || string.IsNullOrEmpty(concord))
			{
				return;
			}
			string clave = QuitarMarcas(displayName).ToLowerInvariant();
			string previo;
			if (concordPorBase.TryGetValue(clave, out previo) && previo != concord)
			{
				concordPorBase[clave] = null;
				return;
			}
			concordPorBase[clave] = concord;
		}

		public string ConcordDeBase(string baseTexto)
		{
			if (concordPorBase == null && FuenteBases != null)
			{
				IEnumerable<KeyValuePair<string, string>> pares = FuenteBases();
				if (pares != null)
				{
					concordPorBase = new Dictionary<string, string>(StringComparer.Ordinal);
					foreach (KeyValuePair<string, string> par in pares)
					{
						AnadirBase(par.Key, par.Value);
					}
				}
			}
			string concord;
			if (concordPorBase != null && concordPorBase.TryGetValue(QuitarMarcas(baseTexto).ToLowerInvariant(), out concord))
			{
				return concord;
			}
			return null;
		}

		/// <summary>«a», «a y b», «a, b y c»; «y» → «e» ante i-/hi- (salvo hie-/hia-).</summary>
		public static string Coordinar(IList<string> partes)
		{
			if (partes.Count == 0)
			{
				return "";
			}
			StringBuilder sb = new StringBuilder(partes[0]);
			for (int i = 1; i < partes.Count; i++)
			{
				if (i < partes.Count - 1)
				{
					sb.Append(", ");
				}
				else
				{
					sb.Append(UsaE(partes[i]) ? " e " : " y ");
				}
				sb.Append(partes[i]);
			}
			return sb.ToString();
		}

		/// <summary>Quita la primera palabra visible si es «con» (y el espacio que la sigue), sin tocar las marcas.</summary>
		public static bool QuitarCon(string texto, out string resto)
		{
			int ini, fin;
			resto = texto;
			if (!PrimeraPalabra(texto, out ini, out fin) || texto.Substring(ini, fin - ini).ToLowerInvariant() != "con")
			{
				return false;
			}
			while (fin < texto.Length && texto[fin] == ' ')
			{
				fin++;
			}
			resto = texto.Substring(0, ini) + texto.Substring(fin);
			return true;
		}

		private static bool UsaE(string siguiente)
		{
			string p = QuitarMarcas(siguiente).ToLowerInvariant();
			if (p.StartsWith("hie") || p.StartsWith("hia"))
			{
				return false;
			}
			return p.StartsWith("i") || p.StartsWith("í") || p.StartsWith("hi") || p.StartsWith("hí");
		}

		private static string PrimeraPalabraVisible(string s)
		{
			int ini, fin;
			return PrimeraPalabra(s, out ini, out fin) ? s.Substring(ini, fin - ini).ToLowerInvariant() : "";
		}

		/// <summary>Quita {{color|…}}, }} y códigos &amp;X / ^X del juego.</summary>
		public static string QuitarMarcas(string s)
		{
			StringBuilder sb = new StringBuilder(s.Length);
			for (int i = 0; i < s.Length; i++)
			{
				char c = s[i];
				if (c == '{' && i + 1 < s.Length && s[i + 1] == '{')
				{
					int barra = s.IndexOf('|', i + 2);
					if (barra > 0)
					{
						i = barra;
						continue;
					}
				}
				if (c == '}' && i + 1 < s.Length && s[i + 1] == '}')
				{
					i++;
					continue;
				}
				if ((c == '&' || c == '^') && i + 1 < s.Length)
				{
					i++;
					continue;
				}
				sb.Append(c);
			}
			return sb.ToString().Trim();
		}

		/// <summary>Localiza la primera palabra visible (letras) saltando las marcas del juego.</summary>
		private static bool PrimeraPalabra(string s, out int ini, out int fin)
		{
			ini = fin = -1;
			for (int i = 0; i < s.Length; i++)
			{
				char c = s[i];
				if (c == '{' && i + 1 < s.Length && s[i + 1] == '{')
				{
					int barra = s.IndexOf('|', i + 2);
					if (barra > 0)
					{
						i = barra;
						continue;
					}
				}
				if ((c == '&' || c == '^') && i + 1 < s.Length)
				{
					i++;
					continue;
				}
				if (char.IsLetter(c))
				{
					ini = i;
					fin = i;
					while (fin < s.Length && char.IsLetter(s[fin]))
					{
						fin++;
					}
					return true;
				}
			}
			return false;
		}
	}
}
