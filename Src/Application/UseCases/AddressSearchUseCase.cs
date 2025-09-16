using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

// Para parsing ligero opcional
using ArcGIS.Core.Data;

using EAABAddIn.Src.Application.Mappers.EsriMappingExtensions;
using EAABAddIn.Src.Application.Mappers.IdecaMappingExtensions;
using EAABAddIn.Src.Application.Models;
using EAABAddIn.Src.Core;
using EAABAddIn.Src.Core.Entities;
using EAABAddIn.Src.Core.Http;
using EAABAddIn.Src.Domain.Repositories;

// HttpService

namespace EAABAddIn.Src.Application.UseCases;

public class AddressSearchUseCase
{
    private readonly IPtAddressGralEntityRepository _ptAddressGralRepository;

    private readonly DatabaseConnectionProperties _connectionProperties;

    public AddressSearchUseCase(DBEngine engine, DatabaseConnectionProperties connectionProperties)
    {
        this._ptAddressGralRepository = this.GetRepository(engine);
        this._connectionProperties = connectionProperties;
    }

    public List<PtAddressGralEntity> Invoke(string address, string cityCode = "11001", string cityDesc = "BOGOTA D.C.")
    {
        var searchResults = _ptAddressGralRepository.FindByCityCodeAndAddresses(
            _connectionProperties,
            cityCode,
            address
        );

        if (searchResults.Count == 0 && cityCode == "11001")
        {
            // Fallback a IDECA cuando BD local no retorna resultados
            var externo = GetFromIDECA(address);
            if (externo.Count > 0) return externo.Take(5).ToList();
        }

        if (searchResults.Count == 0)
        {
            // Fallback a ESRI cuando BD local e IDECA no retornan resultados
            var externo = GetFromESRI(address, cityDesc);
            if (externo.Count > 0) return externo.OrderByDescending(it => it.Score).Take(1).ToList();
        }

        return searchResults.Take(5).ToList();
    }

    private IPtAddressGralEntityRepository GetRepository(DBEngine engine) => engine switch
    {
        DBEngine.Oracle => new PtAddressGralOracleRepository(),
        DBEngine.PostgreSQL => new PtAddressGralPostgresRepository(),
        _ => throw new NotSupportedException($"El motor de base de datos '{engine}' no es compatible.")
    };

    private List<PtAddressGralEntity> GetFromIDECA(string address)
    {
        var list = new List<PtAddressGralEntity>();
        try
        {
            if (string.IsNullOrWhiteSpace(address)) return list;

            // 1. Construir URL
            const string baseUrl = "https://catalogopmb.catastrobogota.gov.co/PMBWeb/web/geocodificador2";
            var url = $"{baseUrl}?cmd=geocodificar&query={Uri.EscapeDataString(address)}";

            // 2. Opciones HTTP (cache corto + timeout)
            var options = new HttpService.HttpRequestOptions
            {
                Timeout = TimeSpan.FromSeconds(15),
                UseCache = true,
                CacheDuration = TimeSpan.FromMinutes(2),
                ThrowOn400 = false
            };

            // 3. Consumir servicio (sin async aquí para mantener firma actual)
            var httpResult = HttpService.Instance.GetAsync(url, options).GetAwaiter().GetResult();
            if (!httpResult.IsSuccess || string.IsNullOrWhiteSpace(httpResult.Content))
            {
                return list; // retornar vacío si falla
            }

            var body = httpResult.Content.Trim();
            var envelope = JsonSerializer.Deserialize<IdecaApiEnvelope>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (envelope?.Response?.Data == null) return list;

            if (envelope.Response.Success == false) return list;

            if (envelope.Response.Data.Tipo_Direccion == "Asignada por Catastro")
            {
                var entidad = envelope.Response.Data.ToPtAddressGral();
                list.Add(entidad);
            }
            return list;
        }
        catch (Exception)
        {
            return list;
        }
    }

    private List<PtAddressGralEntity> GetFromESRI(string address, string cityDesc)
    {
        var list = new List<PtAddressGralEntity>();
        var full = $"{address}, {cityDesc}";

        try
        {
            if (string.IsNullOrWhiteSpace(address)) return list;

            // 1. Construir URL
            const string baseUrl = "https://geocode.arcgis.com/arcgis/rest/services/World/GeocodeServer/findAddressCandidates";
            var url = $"{baseUrl}?f=pjson&SingleLine={Uri.EscapeDataString(full)}&outFields=*&maxLocations=5";

            // 2. Opciones HTTP (cache corto + timeout)
            var options = new HttpService.HttpRequestOptions
            {
                Timeout = TimeSpan.FromSeconds(15),
                UseCache = true,
                CacheDuration = TimeSpan.FromMinutes(2),
                ThrowOn400 = false
            };

            // 3. Consumir servicio (sin async aquí para mantener firma actual)
            var httpResult = HttpService.Instance.GetAsync(url, options).GetAwaiter().GetResult();
            if (!httpResult.IsSuccess || string.IsNullOrWhiteSpace(httpResult.Content))
            {
                return list; // retornar vacío si falla
            }

            var body = httpResult.Content.Trim();
            var envelope = JsonSerializer.Deserialize<EsriGeocodeApiEnvelope>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (envelope?.Candidates == null) return list;

            foreach (var candidate in envelope.Candidates)
            {
                var entidad = candidate.ToPtAddressGral();
                if (entidad != null)
                    list.Add(entidad);
            }

            return list;
        }
        catch (Exception)
        {
            return list;
        }
    }
}
