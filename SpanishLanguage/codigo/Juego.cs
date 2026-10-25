// CAV-24.E2 · Todo lo que el mod lee del juego y NO es imprescindible para compilar, por reflexión.
// Build abierta (B.3): el juego compila el mod en cada arranque contra su Assembly-CSharp. Si un parche
// renombra algo que el código nombra directamente, la compilación falla y el juego desactiva el mod
// ENTERO en esa sesión, XML incluido (ModInfo.cs:137-139, ModManager.cs:504-526, DataManager.cs:507-513).
// Por eso aquí solo se nombran directamente las clases que hay que heredar (TranslatorBase,
// DescriptionBuilder) y lo que se usa de ellas. Lo demás se busca por nombre: si falta, se registra
// UNA vez en Player.log y el nombre sale con el respaldo B. Lista completa: Juego.Superficie (el arnés
// la comprueba contra el DLL instalado).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;
using XRL.World;

namespace CavesOfQudEs.Concordancia
{
	public static class Juego
	{
		private const BindingFlags INSTANCIA = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		private const BindingFlags ESTATICO = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

		/// <summary>Miembros que se usan por reflexión: «Tipo.Miembro». El arnés comprueba que todos existen.</summary>
		public static readonly string[] Superficie =
		{
			"MetricsManager.LogInfo",
			"XRL.DataManager.YieldXMLStreamsWithRoot",
			"XRL.Language.Strings.TryGetString",
			"XRL.World.GameObjectFactory.Factory",
			"XRL.World.GameObjectFactory.Blueprints",
			"XRL.World.GameObjectBlueprint.DisplayName",
			"XRL.World.GameObjectBlueprint.GetxTag",
			"XRL.World.GameObject.DisplayNameBase",
			"XRL.World.GameObject.GetxTag",
			"XRL.World.GameObject.GetPronounProvider",
			"XRL.World.IPronounProvider.Name",
			"XRL.World.IPronounProvider.Subjective",
			"XRL.World.DescriptionBuilder.Object",
			"XRL.World.DescriptionBuilder.PrimaryBase",
			"XRL.World.PooledEvent`1.Pool",
			"XRL.World.GetDisplayNameEvent.DB",
		};

		private static readonly Assembly ensamblado = typeof(DescriptionBuilder).Assembly;

		private static readonly Dictionary<string, MemberInfo> miembros = new Dictionary<string, MemberInfo>();

		private static readonly HashSet<string> avisados = new HashSet<string>();

		/// <summary>Nombre del campo del pool (PooledEvent.cs:10). El arnés lo cambia para simular un parche que lo quite.</summary>
		public static string CampoPool = "Pool";

		// ---------- registro ----------

		/// <summary>Sale en Player.log (MetricsManager.LogInfo). El arnés lo cambia por la consola.</summary>
		public static Action<string> Salida = SalidaDelJuego;

		public static void Registrar(string s)
		{
			try
			{
				Salida(s);
			}
			catch
			{
			}
		}

		/// <summary>Registra una sola vez por clave (para no llenar Player.log en cada nombre).</summary>
		public static void RegistrarUnaVez(string clave, string s)
		{
			lock (avisados)
			{
				if (!avisados.Add(clave))
				{
					return;
				}
			}
			Registrar(s);
		}

		private static void SalidaDelJuego(string s)
		{
			MethodInfo m = Metodo("MetricsManager", "LogInfo", true, typeof(string));
			if (m != null)
			{
				m.Invoke(null, new object[] { s });
			}
		}

		// ---------- interruptor ----------

		private static int fallos;

		/// <summary>Tras demasiadas excepciones, el proveedor deja de montar y todo sale como en el juego (respaldo B).</summary>
		public const int MAX_FALLOS = 50;

		public static bool Apagado { get; set; }

		/// <summary>
		/// Cuenta un fallo en tiempo de ejecución. Registra UNA línea por tipo de fallo (sitio + tipo de excepción),
		/// no una por nombre, y apaga C si se pasa de MAX_FALLOS. Quien llama ya ha vuelto al respaldo B.
		/// </summary>
		public static void Fallo(string donde, Exception ex)
		{
			int n = System.Threading.Interlocked.Increment(ref fallos);
			Exception causa = ex;
			while (causa is TargetInvocationException && causa.InnerException != null)
			{
				causa = causa.InnerException;
			}
			string tipo = causa == null ? "?" : causa.GetType().Name;
			RegistrarUnaVez("fallo:" + donde + ":" + tipo, "TraductorEspanol: fallo en " + donde + " (" + tipo + ": " + UnaLinea(causa == null ? "" : causa.Message) + LugarDe(causa) + "); esos nombres salen con el respaldo B (aviso único para este fallo)");
			if (n >= MAX_FALLOS && !Apagado)
			{
				Apagado = true;
				Registrar("TraductorEspanol: " + n + " fallos; se desactiva la concordancia en esta sesión (respaldo B)");
			}
		}

		public static int NumFallos
		{
			get { return fallos; }
		}

		private static string UnaLinea(string s)
		{
			return (s ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
		}

		/// <summary>« en Clase.Metodo»: el primer marco de la pila, para saber qué tocar sin volcarla entera.</summary>
		private static string LugarDe(Exception ex)
		{
			string pila = ex == null ? null : ex.StackTrace;
			if (string.IsNullOrEmpty(pila))
			{
				return "";
			}
			string primera = UnaLinea(pila.Split('\n')[0]);
			if (primera.StartsWith("at "))
			{
				primera = primera.Substring(3);
			}
			int i = primera.IndexOf(" in ", StringComparison.Ordinal);
			return " en " + (i > 0 ? primera.Substring(0, i) : primera);
		}

		public static void ReiniciarFallos()
		{
			fallos = 0;
			Apagado = false;
		}

		// ---------- acceso a miembros ----------

		public static Type Tipo(string nombre)
		{
			return ensamblado.GetType(nombre, false);
		}

		/// <summary>Método por nombre cuyos primeros parámetros son los dados (el resto, opcionales). Cacheado.</summary>
		private static MethodInfo Metodo(string tipo, string nombre, bool estatico, params Type[] primeros)
		{
			return Metodo(Tipo(tipo), tipo, nombre, estatico, primeros);
		}

		private static MethodInfo Metodo(Type t, string tipo, string nombre, bool estatico, Type[] primeros)
		{
			string clave = tipo + "." + nombre + "/" + primeros.Length;
			MemberInfo mi;
			lock (miembros)
			{
				if (miembros.TryGetValue(clave, out mi))
				{
					return mi as MethodInfo;
				}
			}
			MethodInfo encontrado = null;
			if (t != null)
			{
				foreach (MethodInfo m in t.GetMethods(estatico ? ESTATICO : INSTANCIA))
				{
					if (m.Name == nombre && Encaja(m.GetParameters(), primeros) && !m.ContainsGenericParameters)
					{
						encontrado = m;
						break;
					}
				}
			}
			if (encontrado == null)
			{
				RegistrarUnaVez(clave, "TraductorEspanol: no encuentro " + tipo + "." + nombre + " (respaldo B donde se use)");
			}
			lock (miembros)
			{
				miembros[clave] = encontrado;
			}
			return encontrado;
		}

		private static bool Encaja(ParameterInfo[] ps, Type[] primeros)
		{
			if (ps.Length < primeros.Length)
			{
				return false;
			}
			for (int i = 0; i < ps.Length; i++)
			{
				if (i < primeros.Length ? ps[i].ParameterType != primeros[i] : !ps[i].IsOptional)
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>Argumentos dados + los valores por defecto de los opcionales que añada un parche.</summary>
		private static object[] Argumentos(MethodInfo m, params object[] dados)
		{
			ParameterInfo[] ps = m.GetParameters();
			object[] args = new object[ps.Length];
			for (int i = 0; i < ps.Length; i++)
			{
				args[i] = i < dados.Length ? dados[i] : ps[i].HasDefaultValue ? ps[i].DefaultValue : null;
			}
			return args;
		}

		/// <summary>Campo o propiedad de instancia (o estático si o es null y se da el tipo). Cacheado por tipo.</summary>
		private static object Leer(Type t, object o, string nombre)
		{
			if (t == null)
			{
				return null;
			}
			string clave = t.FullName + "." + nombre;
			MemberInfo mi;
			bool hay;
			lock (miembros)
			{
				hay = miembros.TryGetValue(clave, out mi);
			}
			if (!hay)
			{
				mi = BuscarCampoOPropiedad(t, nombre, o == null);
				if (mi == null)
				{
					RegistrarUnaVez(clave, "TraductorEspanol: no encuentro " + clave + " (respaldo B donde se use)");
				}
				lock (miembros)
				{
					miembros[clave] = mi;
				}
			}
			FieldInfo f = mi as FieldInfo;
			if (f != null)
			{
				return f.GetValue(o);
			}
			PropertyInfo p = mi as PropertyInfo;
			return p != null ? p.GetValue(o, null) : null;
		}

		private static MemberInfo BuscarCampoOPropiedad(Type t, string nombre, bool estatico)
		{
			BindingFlags flags = estatico ? ESTATICO : INSTANCIA;
			for (Type b = t; b != null; b = b.BaseType)
			{
				FieldInfo f = b.GetField(nombre, flags | BindingFlags.DeclaredOnly);
				if (f != null)
				{
					return f;
				}
				PropertyInfo p = b.GetProperty(nombre, flags | BindingFlags.DeclaredOnly);
				if (p != null && p.GetIndexParameters().Length == 0)
				{
					return p;
				}
			}
			// propiedades de interfaz (IPronounProvider)
			foreach (Type i in t.GetInterfaces())
			{
				PropertyInfo p = i.GetProperty(nombre);
				if (p != null)
				{
					return p;
				}
			}
			return null;
		}

		private static object Llamar(object o, string tipo, string nombre, Type[] primeros, params object[] dados)
		{
			MethodInfo m = o == null ? Metodo(tipo, nombre, true, primeros) : Metodo(o.GetType(), o.GetType().FullName, nombre, false, primeros);
			return m == null ? null : m.Invoke(o, Argumentos(m, dados));
		}

		private static readonly Type[] DOS_TEXTOS = { typeof(string), typeof(string) };

		// ---------- lo que usa el mod ----------

		/// <summary>DescriptionBuilder.Object (el GameObject, como object).</summary>
		public static object ObjetoDe(DescriptionBuilder db)
		{
			return Leer(typeof(DescriptionBuilder), db, "Object");
		}

		/// <summary>DescriptionBuilder.PrimaryBase: la base que puso AddBase/ReplacePrimaryBase.</summary>
		public static string BasePrincipal(DescriptionBuilder db)
		{
			return Leer(typeof(DescriptionBuilder), db, "PrimaryBase") as string;
		}

		private static int hayBasePrincipal;

		/// <summary>
		/// Si existe DescriptionBuilder.PrimaryBase. Sin él no se sabe si la base ha cambiado y el concord del objeto
		/// podría no valer («arma extraña afilado»): quien llama usa B.
		/// </summary>
		public static bool HayBasePrincipal
		{
			get
			{
				if (hayBasePrincipal == 0)
				{
					hayBasePrincipal = BuscarCampoOPropiedad(typeof(DescriptionBuilder), "PrimaryBase", false) != null ? 1 : -1;
				}
				return hayBasePrincipal > 0;
			}
		}

		/// <summary>GameObject.DisplayNameBase (GameObject.cs:882).</summary>
		public static string BaseDelObjeto(object objeto)
		{
			return objeto == null ? null : Leer(objeto.GetType(), objeto, "DisplayNameBase") as string;
		}

		/// <summary>GetxTag(Tag, Value) de un GameObject (GameObject.cs:9666) o de un blueprint (GameObjectBlueprint.cs:488).</summary>
		public static string XTag(object objeto, string tag, string valor)
		{
			if (objeto == null)
			{
				return null;
			}
			return Llamar(objeto, objeto.GetType().FullName, "GetxTag", DOS_TEXTOS, tag, valor) as string;
		}

		/// <summary>Name y Subjective del IPronounProvider del objeto (GameObject.cs:7362), en minúsculas.</summary>
		public static void Pronombres(object objeto, out string nombre, out string sujeto)
		{
			nombre = sujeto = "";
			object p = objeto == null ? null : Llamar(objeto, objeto.GetType().FullName, "GetPronounProvider", Type.EmptyTypes);
			if (p == null)
			{
				return;
			}
			nombre = ((Leer(p.GetType(), p, "Name") as string) ?? "").ToLowerInvariant();
			sujeto = ((Leer(p.GetType(), p, "Subjective") as string) ?? "").ToLowerInvariant();
		}

		/// <summary>Texto de un &lt;string&gt; del idioma activo (Strings.cs:149). null si no está.</summary>
		public static Func<string, string, string> TextoDeStrings = TextoDeStringsDelJuego;

		private static string TextoDeStringsDelJuego(string contexto, string id)
		{
			MethodInfo m = Metodo("XRL.Language.Strings", "TryGetString", true, typeof(string), typeof(string), typeof(string).MakeByRefType());
			if (m == null)
			{
				return null;
			}
			object[] args = Argumentos(m, contexto, id, null);
			return (bool)m.Invoke(null, args) ? args[2] as string : null;
		}

		/// <summary>XML de los mods y del juego con esa raíz (DataManager.cs:640). Cada lector vale hasta el siguiente.</summary>
		public static IEnumerable<XmlReader> XmlConRaiz(string raiz)
		{
			object r = Llamar(null, "XRL.DataManager", "YieldXMLStreamsWithRoot", new[] { typeof(string) }, raiz);
			IEnumerable lista = r as IEnumerable;
			if (lista == null)
			{
				yield break;
			}
			foreach (object x in lista)
			{
				XmlReader xml = x as XmlReader;
				if (xml != null)
				{
					yield return xml;
				}
			}
		}

		/// <summary>Pares (DisplayName, concord) de todos los blueprints con concord (GameObjectFactory.cs:36, :63).</summary>
		public static List<KeyValuePair<string, string>> BasesDeLosBlueprints()
		{
			List<KeyValuePair<string, string>> pares = new List<KeyValuePair<string, string>>();
			Type t = Tipo("XRL.World.GameObjectFactory");
			object fabrica = Leer(t, null, "Factory");
			IDictionary bps = fabrica == null ? null : Leer(t, fabrica, "Blueprints") as IDictionary;
			if (bps == null)
			{
				return pares;
			}
			foreach (object bp in bps.Values)
			{
				string concord = XTag(bp, "Grammar", "concord");
				if (concord != null)
				{
					pares.Add(new KeyValuePair<string, string>(Llamar(bp, bp.GetType().FullName, "DisplayName", Type.EmptyTypes) as string, concord));
				}
			}
			return pares;
		}

		/// <summary>
		/// Los GetDisplayNameEvent del pool (PooledEvent.cs:10, campo protegido) y su campo DB (GetDisplayNameEvent.cs:14).
		/// Devuelve cuántos builders se cambiaron por los de crear().
		/// </summary>
		public static int CambiarBuildersDelPool(Func<DescriptionBuilder> crear, Func<DescriptionBuilder, bool> esNuestro)
		{
			Type evento = Tipo("XRL.World.GetDisplayNameEvent");
			Type pooled = Tipo("XRL.World.PooledEvent`1");
			const string SIN_POOL = "; los eventos que ya estaban en el pool siguen con el builder del juego (respaldo B) y los nuevos usan el nuestro";
			if (evento == null || pooled == null)
			{
				RegistrarUnaVez("pool", "TraductorEspanol: no encuentro PooledEvent<GetDisplayNameEvent>" + SIN_POOL);
				return 0;
			}
			Type cerrado;
			try
			{
				cerrado = pooled.MakeGenericType(evento);
			}
			catch (ArgumentException)
			{
				RegistrarUnaVez("pool", "TraductorEspanol: PooledEvent<GetDisplayNameEvent> ya no es válido" + SIN_POOL);
				return 0;
			}
			// sin Leer(): un campo ausente aquí es UN fallo y lleva UNA línea, la de abajo
			FieldInfo campo = cerrado.GetField(CampoPool, ESTATICO);
			IList pool = campo == null ? null : campo.GetValue(null) as IList;
			if (pool == null)
			{
				RegistrarUnaVez("pool.lista", "TraductorEspanol: PooledEvent<GetDisplayNameEvent>." + CampoPool + " no existe o no es una lista" + SIN_POOL);
				return 0;
			}
			FieldInfo db = evento.GetField("DB", INSTANCIA);
			if (db == null || !typeof(DescriptionBuilder).IsAssignableFrom(db.FieldType))
			{
				RegistrarUnaVez("pool.DB", "TraductorEspanol: no encuentro GetDisplayNameEvent.DB" + SIN_POOL);
				return 0;
			}
			int n = 0;
			foreach (object e in pool)
			{
				if (e != null && !esNuestro(db.GetValue(e) as DescriptionBuilder))
				{
					db.SetValue(e, crear());
					n++;
				}
			}
			return n;
		}

		/// <summary>Para el arnés: «Tipo.Miembro» de Superficie que NO existen en el Assembly-CSharp cargado.</summary>
		public static List<string> Comprobar()
		{
			List<string> faltan = new List<string>();
			foreach (string s in Superficie)
			{
				int punto = s.LastIndexOf('.');
				Type t = Tipo(s.Substring(0, punto));
				string nombre = s.Substring(punto + 1);
				if (t == null || t.GetMember(nombre, MemberTypes.Field | MemberTypes.Property | MemberTypes.Method, INSTANCIA | ESTATICO).Length == 0)
				{
					faltan.Add(s);
				}
			}
			return faltan;
		}
	}
}
