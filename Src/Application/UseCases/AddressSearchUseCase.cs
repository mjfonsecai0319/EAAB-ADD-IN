using System;
using System.Collections.Generic;
using System.Linq;

using ArcGIS.Core.Data;

using EAABAddIn.Src.Core;
using EAABAddIn.Src.Core.Entities;
using EAABAddIn.Src.Domain.Repositories;

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

    public List<PtAddressGralEntity> Invoke(string address, string cityCode = "11001")
    {
        var searchResults = _ptAddressGralRepository.FindByCityCodeAndAddresses(
            _connectionProperties,
            cityCode,
            address
        );
        return searchResults.Take(5).ToList();
    }

    private IPtAddressGralEntityRepository GetRepository(DBEngine engine) => engine switch
    {
        DBEngine.Oracle => new PtAddressGralOracleRepository(),
        DBEngine.PostgreSQL => new PtAddressGralPostgresRepository(),
        _ => throw new NotSupportedException($"El motor de base de datos '{engine}' no es compatible.")
    };
}
