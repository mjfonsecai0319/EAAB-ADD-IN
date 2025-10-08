using System.Collections.Generic;
using System.Threading.Tasks;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using EAABAddIn.Src.Core.Map;

namespace EAABAddIn.Src.Application.UseCases;

public class GeneratePolygonsUseCase
{
    public Task<Dictionary<string,int>> InvokeAsync(string gdbPath = null)
    {
        return GeocodedPolygonsLayerService.GenerateAsync(gdbPath);
    }
}
