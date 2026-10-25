// CAV-29.A / CAV-24.E2 · Opción C. DescriptionBuilder con reglas españolas para AddAdjective:
//   1. los adjetivos van DETRÁS del nombre base, en el orden de §4.1 de cav29_concordancia.md
//      (relacional < calificativo < «de …» < «con/en …»);
//   2. concuerdan con el concord del nombre (xtagGrammar), o se usa el texto (respaldo B) si no hay;
//   3. los de la misma clase se coordinan con «y»/«e»; los «con …» se juntan con las WithClause.
// Todo lo demás (base, epítetos, títulos, tags, color, Cutoff) se delega en la clase del juego.
// Build abierta (B.3): de DescriptionBuilder solo se usan los métodos virtuales que se sobrescriben,
// AddWithClause, BaseOnly y Cutoff; Object y PrimaryBase van por reflexión (Juego.cs). Cualquier
// excepción vuelve al comportamiento del juego para ese nombre, y muchas seguidas apagan C (Juego.Apagado).
using System;
using System.Collections.Generic;
using XRL.World;

namespace CavesOfQudEs.Concordancia
{
	public class DescriptionBuilderEspanol : DescriptionBuilder
	{
		/// <summary>OrderAdjust del bloque de adjetivos: AddAdjective(x, 520 + clase) → orden 20 + clase (base = 10, epítetos = 70, «con …» = 600).</summary>
		public const int ORDEN_ADJETIVOS = 520;

		private static TablaConcordancia tabla;

		/// <summary>Carga la tabla la primera vez que hace falta (TraductorEspanol en el juego, el arnés fuera).</summary>
		public static Func<TablaConcordancia> CargarTabla = delegate { return new TablaConcordancia(); };

		public static TablaConcordancia Tabla
		{
			get
			{
				if (tabla == null)
				{
					try
					{
						tabla = CargarTabla();
					}
					catch (Exception ex)
					{
						// sin formas, C no aporta nada: se apaga y los nombres salen exactamente como en el juego (B)
						Juego.Fallo("DescriptionBuilderEspanol: no se pudo cargar la tabla", ex);
						Juego.Apagado = true;
					}
					tabla = tabla ?? new TablaConcordancia();
					if (tabla.NumEntradas == 0 && !Juego.Apagado)
					{
						// sin entradas (no hay XML de formas, o un parche quitó DataManager.YieldXMLStreamsWithRoot) ni siquiera
						// se sabe qué va delante (implante): mejor el juego tal cual
						Juego.Apagado = true;
						Juego.Registrar("TraductorEspanol: la tabla de concordancia está vacía; se desactiva la concordancia en esta sesión (respaldo B)");
					}
				}
				return tabla;
			}
		}

		/// <summary>Vacía la tabla (el arnés la recarga entre pruebas).</summary>
		public static void OlvidarTabla()
		{
			tabla = null;
		}

		/// <summary>
		/// De dónde salen el género y el número del nombre; null = respaldo B.
		/// El arnés lo sustituye porque fuera de Unity no hay GameObject.
		/// </summary>
		public static Func<DescriptionBuilder, Rasgos?> ResolverRasgos = RasgosDelObjeto;

		private struct Adjetivo
		{
			public string Texto;

			public int OrderAdjust;
		}

		private readonly List<Adjetivo> adjetivos = new List<Adjetivo>();

		// Adjetivo de tamaño (ModGigantic): mismo criterio que DescriptionBuilder.ApplySizeAdjective
		// (DescriptionBuilder.cs:203-211), pero guardado aquí para montarlo con los demás.
		private string tamano;

		private int tamanoPrioridad = int.MinValue;

		private int tamanoOrden;

		// true mientras corre base.Resolve(): lo que el juego añada ahí (hoy, nada) va directo al builder del juego
		// en vez de quedarse en la lista ya montada. Protege de un parche que mueva adjetivos a Resolve().
		private bool enBase;

		public override void AddAdjective(string Adjective, int OrderAdjust = 0)
		{
			if (BaseOnly || string.IsNullOrEmpty(Adjective))
			{
				return;
			}
			if (Juego.Apagado || enBase)
			{
				base.AddAdjective(Adjective, OrderAdjust);
				return;
			}
			try
			{
				if (Tabla.EsDelante(Adjective))
				{
					base.AddAdjective(Adjective, OrderAdjust);
					return;
				}
				// CAV-34: un untado de varios líquidos llega como un solo texto; cada fragmento es un adjetivo
				List<string> partes = Tabla.Fragmentos(Adjective) ?? new List<string> { Adjective };
				foreach (string a in partes)
				{
					bool repetido = false;
					for (int i = 0; i < adjetivos.Count; i++)
					{
						repetido |= adjetivos[i].Texto == a;
					}
					if (!repetido)
					{
						adjetivos.Add(new Adjetivo { Texto = a, OrderAdjust = OrderAdjust });
					}
				}
			}
			catch (Exception ex)
			{
				Juego.Fallo("DescriptionBuilderEspanol.AddAdjective", ex);
				base.AddAdjective(Adjective, OrderAdjust);
			}
		}

		public override void ApplySizeAdjective(string Adjective, int Priority = 10, int OrderAdjust = 0)
		{
			if (Juego.Apagado || enBase)
			{
				base.ApplySizeAdjective(Adjective, Priority, OrderAdjust);
				return;
			}
			try
			{
				if (tamano == null || Priority > tamanoPrioridad)
				{
					tamano = Adjective;
					tamanoPrioridad = Priority;
					tamanoOrden = OrderAdjust;
				}
			}
			catch (Exception ex)
			{
				Juego.Fallo("DescriptionBuilderEspanol.ApplySizeAdjective", ex);
				base.ApplySizeAdjective(Adjective, Priority, OrderAdjust);
			}
		}

		public override void Resolve()
		{
			// el tamaño entra en el montaje como un adjetivo más (si es un «con …», llega a las WithClause a tiempo)
			if (tamano != null)
			{
				string t = tamano;
				int orden = tamanoOrden;
				tamano = null;
				tamanoPrioridad = int.MinValue;
				tamanoOrden = 0;
				AddAdjective(t, orden);
			}
			if (adjetivos.Count > 0)
			{
				List<KeyValuePair<string, int>> bloques = null;
				List<string> conClausula = null;
				if (!Juego.Apagado)
				{
					try
					{
						Montar(out bloques, out conClausula);
					}
					catch (Exception ex)
					{
						Juego.Fallo("DescriptionBuilderEspanol.Resolve", ex);
						bloques = null;
					}
				}
				if (bloques != null)
				{
					// solo se toca el builder cuando el montaje entero ha salido bien
					foreach (string w in conClausula)
					{
						AddWithClause(w);
					}
					foreach (KeyValuePair<string, int> b in bloques)
					{
						base.AddAdjective(b.Key, b.Value);
					}
				}
				else
				{
					// respaldo: comportamiento del juego (orden del motor, texto del XML)
					foreach (Adjetivo a in adjetivos)
					{
						base.AddAdjective(a.Texto, a.OrderAdjust);
					}
				}
				adjetivos.Clear();
			}
			enBase = true;
			try
			{
				base.Resolve();
			}
			finally
			{
				enBase = false;
			}
		}

		private struct Montado
		{
			public string Texto;

			public Clase Clase;

			public int OrderAdjust;
		}

		/// <summary>Calcula los bloques (texto, OrderAdjust) y las WithClause sin tocar el builder.</summary>
		private void Montar(out List<KeyValuePair<string, int>> bloques, out List<string> conClausula)
		{
			Rasgos? rasgos = ResolverRasgos(this);
			TablaConcordancia t = Tabla;
			List<Montado> montados = new List<Montado>(adjetivos.Count);
			foreach (Adjetivo a in adjetivos)
			{
				Clase clase;
				string forma = t.Forma(a.Texto, rasgos, out clase);
				montados.Add(new Montado { Texto = forma, Clase = clase, OrderAdjust = a.OrderAdjust });
			}
			montados.Sort(Comparar);
			bloques = new List<KeyValuePair<string, int>>();
			conClausula = new List<string>();
			List<string> grupo = new List<string>();
			for (int c = (int)Clase.Relacional; c <= (int)Clase.Con; c++)
			{
				grupo.Clear();
				foreach (Montado m in montados)
				{
					if ((int)m.Clase != c)
					{
						continue;
					}
					string sinCon;
					if (m.Clase == Clase.Con && TablaConcordancia.QuitarCon(m.Texto, out sinCon))
					{
						// «con mira» + WithClause «blindaje …» → «con blindaje … y mira» (Strings.es.xml:1806)
						conClausula.Add(sinCon);
					}
					else
					{
						grupo.Add(m.Texto);
					}
				}
				if (grupo.Count > 0)
				{
					bloques.Add(new KeyValuePair<string, int>(TablaConcordancia.Coordinar(grupo), ORDEN_ADJETIVOS + c));
				}
			}
		}

		/// <summary>
		/// El juego implementa AddHonorific llamando a AddAdjective(Honorific, 60) (DescriptionBuilder.cs:221-227):
		/// sin esto, los honoríficos (Honorifics.cs, HeroMaker.cs:217) se irían detrás. Se dejan delante,
		/// en su orden de siempre, hasta que Traducción decida otra cosa.
		/// </summary>
		public override void AddHonorific(string Honorific, int OrderAdjust = 0)
		{
			base.AddAdjective(Honorific, 60);
		}

		public override void Clear()
		{
			base.Clear();
			adjetivos.Clear();
			tamano = null;
			tamanoPrioridad = int.MinValue;
			tamanoOrden = 0;
		}

		// mismo criterio que el motor dentro de una clase: OrderAdjust y, a igualdad, alfabético (DescriptionBuilder.cs:481-489)
		private static int Comparar(Montado a, Montado b)
		{
			int n = a.OrderAdjust.CompareTo(b.OrderAdjust);
			if (n != 0)
			{
				return n;
			}
			return string.Compare(TablaConcordancia.QuitarMarcas(a.Texto), TablaConcordancia.QuitarMarcas(b.Texto), StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// En el juego: concord del blueprint (GameObject.cs:9666-9669), con la regla de base reemplazada
		/// (DescriptionBuilder.cs:185-193) y, para cs/cp, el género de los pronombres (GameObject.cs:7362).
		/// </summary>
		public static Rasgos? RasgosDelObjeto(DescriptionBuilder db)
		{
			object o = Juego.ObjetoDe(db);
			if (o == null || !Juego.HayBasePrincipal)
			{
				return null;
			}
			return Tabla.Resolver(Juego.BasePrincipal(db), Juego.BaseDelObjeto(o), Juego.XTag(o, "Grammar", "concord"), delegate { return GeneroDePronombres(o); });
		}

		/// <summary>Femenino si el juego de pronombres es femenino; si no (masculino, neutro, «they»…), masculino genérico.</summary>
		public static Genero GeneroDePronombres(object o)
		{
			string nombre, sujeto;
			Juego.Pronombres(o, out nombre, out sujeto);
			return Rasgos.GeneroDePronombres(nombre, sujeto);
		}
	}
}
