// CAV-29.A / CAV-24.E2 · Opción C. Proveedor de idioma para «es».
// El juego lo elige en LanguageLoader.LoadLanguages() (LanguageLoader.cs:87-96): recorre los tipos de los
// mods activos y instancia el que lleve [LanguageProvider] con el código del idioma activo.
// Hereda TranslatorBase (el comportamiento inglés) y solo cambia la fábrica de DescriptionBuilder.
// Build abierta (B.3): nada de aquí puede lanzar hacia el juego; si algo falla, el builder del juego (respaldo B).
using System;
using XRL.Language;
using XRL.World;

namespace CavesOfQudEs.Concordancia
{
	[LanguageProvider("es")]
	public class TraductorEspanol : TranslatorBase
	{
		// LoadLanguages puede instanciar el proveedor más de una vez (LanguageLoader.cs:67-96: solo marca
		// Initialized cuando ya está la caché de XML), así que el constructor tiene que ser barato e idempotente.
		// La tabla NO se lee aquí: en ese momento la caché de XML puede no incluir aún los mods
		// (DataManager.cs:503-506). Se lee la primera vez que se monta un nombre con adjetivos.
		// Una excepción aquí saldría por Activator.CreateInstance y cortaría LoadLanguages: se captura todo.
		public TraductorEspanol()
		{
			try
			{
				DescriptionBuilderEspanol.CargarTabla = CargarTablaDelJuego;
				int n = Juego.CambiarBuildersDelPool(delegate { return CreateDescriptionBuilder(); }, delegate (DescriptionBuilder db) { return db is DescriptionBuilderEspanol; });
				if (n > 0)
				{
					Juego.Registrar("TraductorEspanol: " + n + " DescriptionBuilder del pool sustituidos");
				}
			}
			catch (Exception ex)
			{
				Juego.Fallo("TraductorEspanol()", ex);
			}
		}

		public override DescriptionBuilder CreateDescriptionBuilder(int Cutoff = int.MaxValue, bool BaseOnly = false)
		{
			if (!Juego.Apagado)
			{
				try
				{
					// TranslatorBase.cs:182 ignora Cutoff (GetDisplayNameEvent lo fija después); aquí se respeta.
					DescriptionBuilderEspanol db = new DescriptionBuilderEspanol();
					db.Cutoff = Cutoff;
					db.BaseOnly = BaseOnly;
					return db;
				}
				catch (Exception ex)
				{
					Juego.Fallo("TraductorEspanol.CreateDescriptionBuilder", ex);
				}
			}
			return base.CreateDescriptionBuilder(Cutoff, BaseOnly);
		}

		/// <summary>
		/// Formas: los XML de raíz «concordancia» del idioma activo (FORMATO.md). Las entradas &lt;string&gt;
		/// remiten a un &lt;string&gt; de Strings.es.xml por Context + ID, que ya está cargado (StringsLoader).
		/// Bases: DisplayName → concord de todos los blueprints, para cuando la base se cambia en tiempo de ejecución.
		/// </summary>
		public static TablaConcordancia CargarTablaDelJuego()
		{
			TablaConcordancia t = new TablaConcordancia();
			int archivos = 0;
			// un archivo que no se puede leer se salta (lo que ya cargó se queda); sus adjetivos salen con B
			foreach (System.Xml.XmlReader xml in Juego.XmlConRaiz(TablaConcordancia.RAIZ))
			{
				try
				{
					t.CargarFormas(xml, Juego.TextoDeStrings, delegate (string aviso) { Juego.RegistrarUnaVez("formas:" + aviso, "TraductorEspanol: " + aviso); });
					archivos++;
				}
				catch (Exception ex)
				{
					Juego.Fallo("CargarTablaDelJuego (" + xml.BaseURI + ")", ex);
				}
			}
			// Solo se llama desde un nombre con GameObject, así que la fábrica ya está cargada (GameObjectFactory.cs:63-72).
			// Si falla, se devuelve una lista vacía (no null) para no reintentarlo en cada nombre: las bases
			// cambiadas en tiempo de ejecución salen con B.
			t.FuenteBases = delegate
			{
				try
				{
					var pares = Juego.BasesDeLosBlueprints();
					Juego.Registrar("TraductorEspanol: " + pares.Count + " blueprints con concord");
					return pares;
				}
				catch (Exception ex)
				{
					Juego.Fallo("BasesDeLosBlueprints", ex);
					return new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>>();
				}
			};
			Juego.Registrar("TraductorEspanol: " + t.NumEntradas + " entradas (" + t.NumConFormas + " con formas) desde " + archivos + " archivos; " + t.NumAvisos + " avisos");
			return t;
		}
	}
}
