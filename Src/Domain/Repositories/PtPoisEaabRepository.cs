using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using EAABAddIn.Src.Core.Entities;
using EAABAddIn.Src.Core; // DBEngine y extensiones

namespace EAABAddIn.Src.Domain.Repositories;

public interface IPtPoisEaabRepository
{
	Task<List<PtPoisEaabEntity>> FindByWordAsync(string word, int limit = 25);
	List<PtPoisEaabEntity> FindByWord(string word, int limit = 25);
}


public class PtPoisEaabRepository : IPtPoisEaabRepository
{
	private const string TablePg = "public.sgo_pt_pois_eaab";      // PostgreSQL esquema sgo
	private const string TableOracle = "sgo.sgo_pt_pois_eaab";  // Ajustar si difiere

	// NOTA: no usamos constante fija para subfields porque algunas bases tienen OBJECTID en vez de id.
	private const string CommonFields = "idsig, technical_location, poi_type, name_poi, address, zone_code, zone_desc, city_code, city_desc, locality_code, locality_desc, neighborhood_code, neighborhood_desc, hydraulic_district_code, hydraulic_district_desc, sanitary_uga_code, sanitary_uga_desc, storm_uga_code, storm_uga_desc, grid_h3_index, zip_code, longitude, latitude, active";

	public List<PtPoisEaabEntity> FindByWord(string word, int limit = 25) =>
		FindByWordAsync(word, limit).GetAwaiter().GetResult();

	public async Task<List<PtPoisEaabEntity>> FindByWordAsync(string word, int limit = 25)
	{
		if (string.IsNullOrWhiteSpace(word) || Module1.DatabaseConnection?.Geodatabase == null)
			return new List<PtPoisEaabEntity>();

		var engine = Module1.Settings.motor.ToDBEngine();
		var sanitized = word.Trim();
		if (string.IsNullOrEmpty(sanitized)) return new List<PtPoisEaabEntity>();

		return await QueuedTask.Run(() =>
		{
			try
			{
				if (engine == DBEngine.PostgreSQL || engine == DBEngine.PostgreSQLSDE)
					return FindPostgresLike(sanitized, limit);
				return FindGenericSimilarity(sanitized, limit);
			}
			catch (Exception)
			{
				// Recolectar diagnóstico mínimo
				return new List<PtPoisEaabEntity>();
			}
		});
	}

	/// <summary>
	/// Devuelve los POIs encontrados convertidos a <see cref="PtAddressGralEntity"/> para facilitar su inserción en la capa
	/// de resultados (GeocodedAddresses). Sólo se incluyen aquellos con coordenadas válidas.
	/// </summary>
	public async Task<List<PtAddressGralEntity>> FindByWordAsAddressEntitiesAsync(string word, int limit = 25)
	{
		var pois = await FindByWordAsync(word, limit);
		var list = new List<PtAddressGralEntity>(pois.Count);
		foreach (var p in pois)
		{
			if (!p.Latitude.HasValue || !p.Longitude.HasValue) continue;
			list.Add(new PtAddressGralEntity
			{
				Latitud = (decimal?) (p.Latitude.HasValue ? Convert.ToDecimal(p.Latitude.Value) : null),
				Longitud = (decimal?) (p.Longitude.HasValue ? Convert.ToDecimal(p.Longitude.Value) : null),
				FullAddressEAAB = p.NamePoi,
				FullAddressCadastre = p.Address,
				CityCode = p.CityCode,
				CityDesc = p.CityDesc,
				ZoneCode = p.ZoneCode,
				ZoneDesc = p.ZoneDesc,
				HydraulicDistrictCode = p.HydraulicDistrictCode,
				HydraulicDistrictDescription = p.HydraulicDistrictDesc,
				SanitaryUgaCode = p.SanitaryUgaCode,
				SanitaryUgaDesc = p.SanitaryUgaDesc,
				StormUgaCode = p.StormUgaCode,
				StormUgaDesc = p.StormUgaDesc,
				GridH3Index = p.GridH3Index,
				ZipCode = p.ZipCode,
				Source = "POI",
				Score = p.TotalScore,
				ScoreText = p.TotalScore.ToString("0.000")
			});
		}
		return list;
	}

	#region PostgreSQL (LIKE + luego similitud en memoria)

	private static List<PtPoisEaabEntity> FindPostgresLike(string search, int limit)
	{
		var list = new List<PtPoisEaabEntity>();
		var gdb = Module1.DatabaseConnection.Geodatabase;
		using var table = gdb.OpenDataset<Table>(TablePg);
		var (idField, subFields) = ResolveIdFieldAndSubFields(table);
		var pattern = BuildLikeSanitized(search).ToUpperInvariant();
		var qf = new QueryFilter { SubFields = subFields, WhereClause = $"UPPER(name_poi) LIKE '%{EscapeLike(pattern)}%'" };
		using (var cursor = table.Search(qf, false))
		{
			while (cursor.MoveNext())
			{
				using var row = cursor.Current;
				list.Add(Map(row, idField));
			}
		}

		if (list.Count == 0)
		{
			// intentar token individual
			var token = pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
			if (!string.IsNullOrEmpty(token) && token.Length >= 3)
			{
				var qf2 = new QueryFilter { SubFields = subFields, WhereClause = $"UPPER(name_poi) LIKE '%{EscapeLike(token)}%'" };
				using var cursor2 = table.Search(qf2, false);
				while (cursor2.MoveNext())
				{
					using var row = cursor2.Current;
					list.Add(Map(row, idField));
				}
			}
		}

		ComputeScores(list, search);
		return list
			.OrderByDescending(e => e.TotalScore)
			.ThenBy(e => e.NamePoi)
			.Take(limit)
			.ToList();
	}

	#endregion

	#region Fallback genérico

	private static List<PtPoisEaabEntity> FindGenericSimilarity(string search, int limit)
	{
		var list = new List<PtPoisEaabEntity>();
		var gdb = Module1.DatabaseConnection.Geodatabase;
		Table table = null;
		try { table = gdb.OpenDataset<Table>(TableOracle); } catch { }
		if (table == null) return list;
		var (idField, subFields) = ResolveIdFieldAndSubFields(table);
		var like = BuildLikeSanitized(search).ToUpperInvariant();
		var qf = new QueryFilter { SubFields = subFields, WhereClause = $"UPPER(name_poi) LIKE '%{EscapeLike(like)}%'" };
		using (var cursor = table.Search(qf, false))
		{
			while (cursor.MoveNext())
			{
				using var row = cursor.Current;
				list.Add(Map(row, idField));
			}
		}

		if (list.Count == 0)
		{
			var token = like.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
			if (!string.IsNullOrEmpty(token) && token.Length >= 3)
			{
				var qf2 = new QueryFilter { SubFields = subFields, WhereClause = $"UPPER(name_poi) LIKE '%{EscapeLike(token)}%'" };
				using var cursor2 = table.Search(qf2, false);
				while (cursor2.MoveNext())
				{
					using var row = cursor2.Current;
					list.Add(Map(row, idField));
				}
			}
		}

		ComputeScores(list, search);
		return list
			.OrderByDescending(e => e.TotalScore)
			.ThenBy(e => e.NamePoi)
			.Take(limit)
			.ToList();
	}

	#endregion

	#region Helpers

	private static PtPoisEaabEntity Map(Row row, string idField) => new()
	{
		ID = !string.IsNullOrEmpty(idField) ? ToLong(row, idField) : 0,
		IdSig = row["idsig"]?.ToString(),
		TechnicalLocation = row["technical_location"]?.ToString(),
		PoiType = row["poi_type"]?.ToString(),
		NamePoi = row["name_poi"]?.ToString(),
		Address = row["address"]?.ToString(),
		ZoneCode = row["zone_code"]?.ToString(),
		ZoneDesc = row["zone_desc"]?.ToString(),
		CityCode = row["city_code"]?.ToString(),
		CityDesc = row["city_desc"]?.ToString(),
		LocalityCode = row["locality_code"]?.ToString(),
		LocalityDesc = row["locality_desc"]?.ToString(),
		NeighborhoodCode = row["neighborhood_code"]?.ToString(),
		NeighborhoodDesc = row["neighborhood_desc"]?.ToString(),
		HydraulicDistrictCode = row["hydraulic_district_code"]?.ToString(),
		HydraulicDistrictDesc = row["hydraulic_district_desc"]?.ToString(),
		SanitaryUgaCode = row["sanitary_uga_code"]?.ToString(),
		SanitaryUgaDesc = row["sanitary_uga_desc"]?.ToString(),
		StormUgaCode = row["storm_uga_code"]?.ToString(),
		StormUgaDesc = row["storm_uga_desc"]?.ToString(),
		GridH3Index = row["grid_h3_index"]?.ToString(),
		ZipCode = row["zip_code"]?.ToString(),
		Longitude = ToDouble(row, "longitude"),
		Latitude = ToDouble(row, "latitude"),
		Active = row["active"]?.ToString()
	};

	private static (string idField, string subFields) ResolveIdFieldAndSubFields(Table table)
	{
		try
		{
			var def = table.GetDefinition();
			var fields = def.GetFields();
			string idField = fields.FirstOrDefault(f => f.Name.Equals("id", StringComparison.OrdinalIgnoreCase))?.Name
							  ?? fields.FirstOrDefault(f => f.Name.Equals("objectid", StringComparison.OrdinalIgnoreCase))?.Name;
			string sub = string.IsNullOrEmpty(idField) ? CommonFields : idField + ", " + CommonFields;
			return (idField, sub);
		}
		catch { return (null, CommonFields); }
	}

	private static long ToLong(Row row, string field)
	{
		try { return row[field] is long l ? l : Convert.ToInt64(row[field]); } catch { return 0; }
	}
	private static double? ToDouble(Row row, string field)
	{
		try
		{
			var v = row[field];
			if (v == null || v is DBNull) return null;
			return Convert.ToDouble(v, CultureInfo.InvariantCulture);
		}
		catch { return null; }
	}

	private static string BuildLikeSanitized(string input)
	{
		var chars = " !\"#$%&()*+,-./<>?¿@^_´{}[]°¬¡~";
		var sb = new StringBuilder();
		foreach (var c in input)
		{
			if (chars.Contains(c)) sb.Append(' '); else sb.Append(c);
		}
		var interim = Regex.Replace(sb.ToString(), "  +", " ").Trim();
		return interim;
	}

	private static string EscapeLike(string s) => s.Replace("'", "''");

	private static void ComputeScores(List<PtPoisEaabEntity> list, string search)
	{
		var sLower = search.ToLowerInvariant();
		foreach (var e in list)
		{
			var n = e.NamePoi ?? string.Empty;
			e.ScoreJaroWinkler = JaroWinkler(n, search);
			e.ScoreLevenshtein = 1 - (LevenshteinDistance(n.ToLowerInvariant(), sLower) / (double)Math.Max(1, Math.Max(n.Length, search.Length)));
		}
	}

	private static int LevenshteinDistance(string s, string t)
	{
		if (s == t) return 0;
		if (s.Length == 0) return t.Length;
		if (t.Length == 0) return s.Length;
		var d = new int[s.Length + 1, t.Length + 1];
		for (int i = 0; i <= s.Length; i++) d[i, 0] = i;
		for (int j = 0; j <= t.Length; j++) d[0, j] = j;
		for (int i = 1; i <= s.Length; i++)
		{
			for (int j = 1; j <= t.Length; j++)
			{
				var cost = s[i - 1] == t[j - 1] ? 0 : 1;
				d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
			}
		}
		return d[s.Length, t.Length];
	}

	private static double JaroWinkler(string s1, string s2)
	{
		if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return 0d;
		int l1 = s1.Length, l2 = s2.Length;
		int matchDistance = Math.Max(l1, l2) / 2 - 1;
		var s1Matches = new bool[l1];
		var s2Matches = new bool[l2];
		int matches = 0;
		for (int i = 0; i < l1; i++)
		{
			int start = Math.Max(0, i - matchDistance);
			int end = Math.Min(i + matchDistance + 1, l2);
			for (int j = start; j < end; j++)
			{
				if (s2Matches[j]) continue;
				if (s1[i] != s2[j]) continue;
				s1Matches[i] = s2Matches[j] = true;
				matches++;
				break;
			}
		}
		if (matches == 0) return 0d;
		double t = 0;
		int k = 0;
		for (int i = 0; i < l1; i++)
		{
			if (!s1Matches[i]) continue;
			while (!s2Matches[k]) k++;
			if (s1[i] != s2[k]) t += 0.5;
			k++;
		}
		double jaro = (matches / (double)l1 + matches / (double)l2 + (matches - t) / matches) / 3.0;
		int prefix = 0;
		for (int i = 0; i < Math.Min(4, Math.Min(l1, l2)); i++)
		{
			if (s1[i] == s2[i]) prefix++; else break;
		}
		return jaro + 0.1 * prefix * (1 - jaro);
	}

	#endregion
}

