# Documentación Técnica - EAAB AddIn para ArcGIS Pro

## Descripción General

AddIn para ArcGIS Pro que proporciona capacidades de geocodificación individual y masiva mediante conexión a bases de datos corporativas PostgreSQL y Oracle, con soporte para conexiones directas por credenciales y mediante archivos SDE. Incluye también búsqueda de Puntos de Interés (POIs) y almacenamiento estructurado de resultados.

## Stack Tecnológico

- **.NET 8**
- **ArcGIS Pro SDK 3.4+**
- **WPF** con patrón MVVM
- **PostgreSQL 15+** con PostGIS
- **Oracle 18+**
- **EPPlus / ExcelDataReader** para lectura de Excel (según implementación)
- **Npgsql / Oracle Managed Data Access**

## Requisitos de Desarrollo

### Entorno de desarrollo

- Visual Studio 2022 o superior
- ArcGIS Pro SDK for .NET instalado (Extension Manager)
- ArcGIS Pro 3.4+ instalado (mismo equipo)
- .NET 8 SDK

### Dependencias del proyecto

```xml
<PackageReference Include="ArcGIS.Desktop.SDK" Version="3.4.*" />
<PackageReference Include="EPPlus" Version="7.0+" />
<PackageReference Include="ExcelDataReader" Version="3.6+" />
<PackageReference Include="Npgsql" Version="8.0+" />
<PackageReference Include="Oracle.ManagedDataAccess.Core" Version="3.21+" />
```

## Arquitectura del Sistema

### Estructura de Carpetas

```
EAABAddIn/
├── Src/
│   ├── Application/              # Capa de aplicación (casos de uso / servicios orquestadores)
│   │   ├── Services/
│   │   │   ├── AddressNormalizer.cs
│   │   │   ├── AddressSearchService.cs
│   │   │   ├── MassiveGeocodingService.cs
│   │   │   └── PoiSearchService.cs
│   │   └── DTOs/
│   │
│   ├── Core/                     # Transversal: conexión, capa de datos base, utilidades
│   │   ├── Config/
│   │   │   ├── ConfigurationManager.cs
│   │   │   ├── DatabaseConfiguration.cs
│   │   │   └── PersistentSettings.cs
│   │   │
│   │   ├── Data/
│   │   │   ├── DatabaseConnectionService.cs
│   │   │   ├── ConnectionPropertiesFactory.cs
│   │   │   ├── ResultsLayerService.cs
│   │   │   ├── AddressNotFoundTableService.cs
│   │   │   ├── PoiResultsLayerService.cs
│   │   │   └── Repositories/
│   │   │       ├── PtAddressGralEntityRepository.cs
│   │   │       ├── OraclePtAddressGralRepository.cs
│   │   │       ├── PostgresPtAddressGralRepository.cs
│   │   │       └── PoiRepository*.cs (si aplica)
│   │   │
│   │   └── Map/ (servicios de mapa, transformaciones)
│   │
│   └── Presentation/             # MVVM UI
│       ├── Converters/
│       ├── View/
│       └── ViewModel/
│
├── Images/                       # Recursos gráficos
├── Config.daml                   # Configuración AddIn / DAML (UI ArcGIS Pro)
└── Module1.cs                    # Punto de entrada / ciclo de vida
```

### Capas y Responsabilidades

| Capa | Responsabilidad | Ejemplos |
|------|-----------------|----------|
| Presentation | Interacción usuario, binding WPF | *ViewModels, XAML Views* |
| Application | Orquestación de casos de uso | *MassiveGeocodingService* |
| Core.Data | Conexión y repositorios | *DatabaseConnectionService* |
| Core.Map | Creación de capas, escritura espacial | *ResultsLayerService* |
| Domain (implícita) | Entidades lógicas | *PtAddressGralEntity* |

### Flujo de Geocodificación Individual

1. Usuario ingresa dirección y ciudad.  
2. ViewModel invoca `AddressSearchService`.  
3. El servicio consulta repositorio principal (EAAB).  
4. Si vacío, fallback a Catastro / ESRI según configuración o heurística.  
5. Resultados se normalizan (dirección preferida EAAB > Catastro > Original).  
6. Se envían a `ResultsLayerService` para persistir punto.

### Flujo de Geocodificación Masiva

1. Lectura de Excel → generación de lista de registros.  
2. Validaciones: estructura, campos obligatorios, códigos de ciudad válidos.  
3. Iteración paralela controlada (en esta versión: secuencial por seguridad ArcGIS MCT).  
4. Clasificación de resultados y enriquecimiento de atributos.  
5. Batch insert (acumulación + commit).  
6. Resumen (found / not found).  

### Flujo de Búsqueda de POIs

1. Usuario ingresa término (ej: "hospital").  
2. `PoiSearchService` genera un patrón (LIKE normalizado).  
3. Repositorio POI ejecuta consulta (index por nombre / categoría).  
4. Se limita número máximo (paginación futura).  
5. Se inserta en capa `POIResults` o memoria y luego commit.  
6. Selección de un resultado centra el mapa.

### Patrones de Diseño Implementados

(Se mantienen los ya descritos: MVVM, Factory, Repository, Singleton Controlado, Strategy (fallback), Lazy Initialization.)

Se adicionan:
- **Adapter** (normalización entre fuentes de resultados EAAB, Catastro, ESRI hacia `GeocodeResult`).
- **Value Objects ligeros** para direcciones normalizadas (en evolución).
- **Fail-Fast Validation** en masivo antes de consumir recursos.

## Componentes Principales (Resumen)

### 1. Module1.cs - Punto de Entrada

```csharp
internal class Module1 : Module
{
    protected override bool Initialize()
    {
        // Cargar configuración persistente
        LoadConfiguration();
        
        // Inicializar servicios
        InitializeServices();
        
        // Intentar reconexión diferida
        _ = Task.Run(async () => await AttemptReconnectionAsync());
        
        return base.Initialize();
    }
    
    private async Task AttemptReconnectionAsync()
    {
        var config = ConfigurationManager.LoadConfiguration();
        if (config.IsValid)
        {
            await ConnectionService.ConnectAsync(config);
        }
    }
}
```

### 2. DatabaseConnectionService

Gestiona las conexiones a bases de datos con soporte multi-motor.

```csharp
public class DatabaseConnectionService : IDisposable
{
    private Geodatabase _geodatabase;
    private DatabaseEngine _currentEngine;
    private string _sdeFilePath;
    
    public async Task<bool> ConnectAsync(DatabaseConfiguration config)
    {
        try
        {
            DatabaseConnectionProperties connectionProps;
            
            if (config.Engine is DatabaseEngine.PostgreSQLSDE or DatabaseEngine.OracleSDE)
            {
                // Conexión mediante archivo SDE
                connectionProps = new DatabaseConnectionFile(
                    new Uri(config.SdeFilePath, UriKind.Absolute));
            }
            else
            {
                // Conexión por credenciales
                connectionProps = ConnectionPropertiesFactory.Create(
                    config.Engine, config.Host, config.Port, 
                    config.Database, config.User, config.Password);
            }
            
            await QueuedTask.Run(() =>
            {
                _geodatabase = new Geodatabase(connectionProps);
            });
            
            _currentEngine = config.Engine;
            IsConnected = true;
            
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error de conexión: {ex.Message}");
            return false;
        }
    }
    
    public async Task<Geodatabase> GetConnectionAsync()
    {
        if (!IsConnected || _geodatabase == null)
            throw new InvalidOperationException("No hay conexión activa");
        
        return await Task.FromResult(_geodatabase);
    }
    
    public void Dispose()
    {
        _geodatabase?.Dispose();
        _geodatabase = null;
        IsConnected = false;
    }
}
```

### 3. ResultsLayerService

Gestiona la capa de resultados de geocodificación con batch insert optimizado.

```csharp
public class ResultsLayerService
{
    private const string FEATURE_CLASS_NAME = "GeocodedAddresses";
    
    public async Task InsertBatchAsync(List<GeocodeResult> results)
    {
        var featureClass = await GetOrCreateFeatureClassAsync();
        
        await QueuedTask.Run(() =>
        {
            using var operation = new EditOperation
            {
                Name = "Insertar resultados de geocodificación"
            };
            
            foreach (var result in results)
            {
                var attributes = new Dictionary<string, object>
                {
                    ["Identificador"] = result.Identifier,
                    ["Direccion"] = result.Address,
                    ["FullAdressEAAB"] = result.FullAddressEAAB,
                    ["FullAdressUACD"] = result.FullAddressUACD,
                    ["Geocoder"] = result.Geocoder.ToString(),
                    ["Score"] = result.Score,
                    ["ScoreText"] = result.ScoreText,
                    ["FechaHora"] = DateTime.Now
                };
                
                var geometry = MapPointBuilderEx.CreateMapPoint(
                    result.X, result.Y, SpatialReferences.WGS84);
                
                operation.Create(featureClass, attributes, geometry);
            }
            
            operation.Execute();
        });
    }
    
    private FeatureClass CreateOrRetrieveFeatureClass()
    {
        var gdb = GetDefaultGeodatabase();
        
        // Intentar abrir existente
        try
        {
            return gdb.OpenDataset<FeatureClass>(FEATURE_CLASS_NAME);
        }
        catch
        {
            // Crear nuevo
            return CreateNewFeatureClass(gdb);
        }
    }
    
    private FeatureClass CreateNewFeatureClass(Geodatabase gdb)
    {
        var fcDescription = new FeatureClassDescription(
            FEATURE_CLASS_NAME,
            new List<FieldDescription>
            {
                new FieldDescription("Identificador", FieldType.String),
                new FieldDescription("Direccion", FieldType.String),
                new FieldDescription("FullAdressEAAB", FieldType.String),
                new FieldDescription("FullAdressUACD", FieldType.String),
                new FieldDescription("Geocoder", FieldType.String),
                new FieldDescription("Score", FieldType.Double),
                new FieldDescription("ScoreText", FieldType.String),
                new FieldDescription("FechaHora", FieldType.Date)
            },
            new ShapeDescription(GeometryType.Point, SpatialReferences.WGS84)
        );
        
        return gdb.CreateFeatureClass(fcDescription);
    }
}
```

### 4. AddressSearchService

Orquesta la búsqueda de direcciones con fallback inteligente.

```csharp
public class AddressSearchService
{
    private readonly IPtAddressGralEntityRepository _repository;
    private readonly AddressNormalizer _normalizer;
    
    public async Task<SearchResult> SearchAsync(
        string address, string city, CancellationToken cancellationToken)
    {
        // Primer intento: búsqueda exacta
        var results = await _repository.SearchAddressesAsync(
            address, city, cancellationToken);
        
        if (results.Any())
            return new SearchResult(results, SearchStrategy.Exact);
        
        // Segundo intento: búsqueda LIKE ampliada
        Debug.WriteLine("Búsqueda exacta sin resultados, intentando LIKE...");
        results = await _repository.SearchAddressesLikeAsync(
            address, city, cancellationToken);
        
        return new SearchResult(results, SearchStrategy.Like);
    }
    
    public async Task<string> NormalizeAddressAsync(string address)
    {
        try
        {
            return await _normalizer.NormalizeAsync(address);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error de normalización: {ex.Message}");
            return address; // Fallback a dirección original
        }
    }
}
```

### 5. MassiveGeocodingService

Procesamiento masivo con optimizaciones de rendimiento.

```csharp
public class MassiveGeocodingService
{
    private readonly AddressSearchService _searchService;
    private readonly ResultsLayerService _resultsService;
    
    public async Task<MassiveResult> ProcessFileAsync(
        string filePath, IProgress<ProgressInfo> progress)
    {
        var records = await ReadExcelFileAsync(filePath);
        var results = new List<GeocodeResult>();
        var notFound = new List<NotFoundRecord>();
        
        int processed = 0;
        int total = records.Count;
        
        foreach (var record in records)
        {
            var searchResult = await _searchService.SearchAsync(
                record.Address, record.City, CancellationToken.None);
            
            if (searchResult.HasResults)
            {
                // Determinar mejor dirección: EAAB > Catastro > Original
                var bestAddress = DetermineBestAddress(searchResult);
                results.Add(CreateGeocodeResult(record, bestAddress));
            }
            else
            {
                notFound.Add(new NotFoundRecord
                {
                    Identifier = record.Identifier,
                    Address = record.Address,
                    City = record.City,
                    Timestamp = DateTime.Now
                });
            }
            
            processed++;
            progress?.Report(new ProgressInfo(processed, total));
        }
        
        // Inserción en lote optimizada
        await _resultsService.InsertBatchAsync(results);
        await _notFoundService.InsertBatchAsync(notFound);
        
        return new MassiveResult
        {
            Found = results.Count,
            NotFound = notFound.Count,
            Total = total
        };
    }
    
    private string DetermineBestAddress(SearchResult result)
    {
        // Prioridad: FullAddressEAAB > FullAddressUACD > Original > Calle básica
        var first = result.Addresses.First();
        
        if (!string.IsNullOrEmpty(first.FullAddressEAAB))
            return first.FullAddressEAAB;
        
        if (!string.IsNullOrEmpty(first.FullAddressUACD))
            return first.FullAddressUACD;
        
        return first.Direccion;
    }
}
```

## Sistema de Configuración

- Persistencia JSON + respaldo en settings App.
- Validación contextual según tipo de motor.
- Admite cambio caliente (runtime) disparando reconexión.

### Seguridad de Configuración

- Contraseñas no se registran en logs.
- Posible mejora: cifrado AES local (pendiente) usando DPAPI Windows.
- Recomendado: restringir permisos de carpeta `%AppData%/EAABAddIn`.

## Modelo de Datos (Lógico Simplificado)

| Entidad | Campos clave | Fuente | Notas |
|---------|--------------|--------|-------|
| PtAddressGralEntity | ID, Direccion, FullAddressEAAB, FullAddressCadastre, Poblacion | BD corporativa | Base principal de direcciones |
| GeocodeResult | Identifier, Address, Source, Score, Lat/Long | Derivado | Unión de varias fuentes |
| NotFoundRecord | Identifier, Address, City, Timestamp | Generado | Auditoría de intentos fallidos |
| PoiEntity | PoiId, Name, Category, City, X, Y | BD corporativa / vista | Indexable para búsqueda |

## Capa de Resultados Espaciales

### `ResultsLayerService`
- Lazy create de Feature Class `GeocodedAddresses` (WGS84).  
- Inserciones agrupadas dentro de `EditOperation`.  
- Campos calculados en el ViewModel (score interpretado).

### `PoiResultsLayerService`
- Similar estrategia: `POIResults` con campos `PoiId`, `Nombre`, `Categoria`, `Ciudad`, `FechaHora`.
- Reutiliza builder de geometrías estándar.

## Logging y Observabilidad

Estado actual: logging mínimo mediante `Debug.WriteLine` y mensajes UI.  
Sugerido:
- Introducir `ILogger` (MS.Extensions.Logging) con proveedor simple.
- Niveles: Info (operaciones), Warning (faltantes), Error (excepciones).
- Métricas futuras: tiempo promedio por geocodificación, % éxito.

## Rendimiento y Escalabilidad

| Área | Riesgo | Mitigación Actual | Mejora Potencial |
|------|-------|-------------------|------------------|
| Masivo secuencial | Lento con >50k filas | Procesamiento controlado | Paralelizar lectura + cola MCT |
| Acceso BD | Latencia variable | Repositorio único | Cache ciudades en memoria |
| Insert espacial | Bloqueos si muchas ediciones | Batch + single commit | Chunk configurable |
| Normalización externa | Timeout/errores | Fallback inmediato | Circuit breaker + retry |

## Tratamiento de Errores

- Validaciones previas detienen proceso temprano (fail-fast).
- Excepciones en loop masivo contabilizan como "no encontrados" sin detener el resto.
- Mostrar mensajes al usuario solo cuando agregan valor (no spam por cada fila).

## Pruebas (Testing)

### Estrategia Propuesta

| Tipo | Objetivo | Ejemplos |
|------|----------|----------|
| Unit | Lógica pura (normalizador, filtros) | `AddressNormalizerTests` |
| Repository (mock) | Queries adaptadas por motor | `PtAddressGralRepositoryTests` |
| Integration (opcional) | Conexión real a GDB de prueba | Escenarios mínimos |
| UI (manual) | Flujo MVVM básico | Buscar, Masivo, POI |

### Recomendaciones
- Introducir interfaces para capa ResultsLayer para facilitar mocks.
- Usar `xUnit` + `Moq`.
- Datos de prueba ligeros (JSON) para direcciones.

## Build y Empaquetado

### Compilación Local

1. Abrir solución en Visual Studio con ArcGIS Pro instalado.
2. Restaurar paquetes NuGet.
3. Asegurar target: `net8.0-windows` con `UseWPF` habilitado.
4. Compilar en modo Release.

### Generación del AddIn (.esriAddInX)

1. Verificar `Config.daml` actualizado (botones/paneles).  
2. Build Release genera carpeta `bin/Release`.  
3. Utilizar herramienta de empaquetado del SDK (si configurada) o copiar output.  
4. Validar firma (si política corporativa lo exige).  

### Versionado

- Mantener versión en AssemblyInfo o proyecto (PropertyGroup `<Version>`).  
- Sincronizar con sección "Información de Versión" de manual usuario.  

## Despliegue

| Entorno | Acción | Notas |
|---------|-------|-------|
| Usuario final | Distribuir `.esriAddInX` | Instrucciones en READMEUSER |
| Piloto | Revisión funcional | Capturar métricas básicas |
| Producción | Publicación controlada | Registrar hash archivo |

## Seguridad y Acceso a Datos

- Principio de mínimo privilegio para usuarios de BD.
- No almacenar contraseñas en texto plano fuera de `%AppData%` (cifrar futuro).
- Validar origen de archivos Excel (no macros, no binarios maliciosos).

## Internacionalización (i18n)

- Textos actualmente en español embebidos.
- Mejora futura: recursos (.resx) para soportar EN/ES.

## Roadmap Propuesto

| Prioridad | Feature | Descripción |
|-----------|---------|-------------|
| Alta | Cancelación masiva | Token cancelar proceso en curso |
| Media | Cache ciudades | Reducir llamadas repetidas |
| Media | Exportar no encontrados | CSV automático |
| Media | Paginación POIs | Controlar grandes resultados |
| Baja | Cifrado credenciales | DPAPI / AES |
| Baja | Telemetría | Eventos anónimos de uso |

## Ejemplo Simplificado de POI Repository

```csharp
public interface IPoiRepository {
    IEnumerable<PoiEntity> Search(string term, string city = null, int max = 500);
}

public class PostgresPoiRepository : IPoiRepository {
    private readonly DatabaseConnectionService _connection;
    public IEnumerable<PoiEntity> Search(string term, string city = null, int max = 500) {
        // Implementación con ILIKE y limit
    }
}
```

## Notas de Mantenimiento

- Revisar compatibilidad ArcGIS Pro antes de subir versión SDK.
- Ejecutar pruebas de regresión después de cambios en repositorios.
- Documentar nuevas columnas añadidas a Feature Class.

---

(Sección original de patrones y ejemplos mantenida abajo para referencia)

# Documentación Técnica - EAAB AddIn para ArcGIS Pro

## Descripción General

AddIn para ArcGIS Pro que proporciona capacidades de geocodificación individual y masiva mediante conexión a bases de datos corporativas PostgreSQL y Oracle, con soporte para conexiones directas por credenciales y mediante archivos SDE.

## Stack Tecnológico

- **.NET 8**
- **ArcGIS Pro SDK 3.4+**
- **WPF** con patrón MVVM
- **PostgreSQL 15+** con PostGIS
- **Oracle 18+**
- **EPPlus** para procesamiento de archivos Excel

## Requisitos de Desarrollo

### Entorno de desarrollo

- Visual Studio 2022 o superior
- ArcGIS Pro SDK for .NET
- ArcGIS Pro 3.4+ instalado
- .NET 8 SDK

### Dependencias del proyecto

```xml
<PackageReference Include="ArcGIS.Desktop.SDK" Version="3.4.*" />
<PackageReference Include="EPPlus" Version="7.0+" />
<PackageReference Include="Npgsql" Version="8.0+" />
<PackageReference Include="Oracle.ManagedDataAccess.Core" Version="3.21+" />
```

## Arquitectura del Sistema

### Estructura de Carpetas

```
EAABAddIn/
├── Src/
│   ├── Application/              # Capa de aplicación
│   │   ├── Services/
│   │   │   ├── AddressNormalizer.cs
│   │   │   ├── AddressSearchService.cs
│   │   │   └── MassiveGeocodingService.cs
│   │   └── DTOs/
│   │
│   ├── Core/                     # Capa core
│   │   ├── Config/
│   │   │   ├── ConfigurationManager.cs
│   │   │   ├── DatabaseConfiguration.cs
│   │   │   └── PersistentSettings.cs
│   │   │
│   │   └── Data/
│   │       ├── DatabaseConnectionService.cs
│   │       ├── ConnectionPropertiesFactory.cs
│   │       ├── ResultsLayerService.cs
│   │       ├── AddressNotFoundTableService.cs
│   │       └── Repositories/
│   │           ├── PtAddressGralEntityRepository.cs
│   │           ├── OraclePtAddressGralRepository.cs
│   │           └── PostgresPtAddressGralRepository.cs
│   │
│   └── Presentation/             # Capa de presentación
│       ├── Converters/           # Convertidores XAML
│       │   ├── BoolToVisibilityConverter.cs
│       │   └── ConnectionStatusConverter.cs
│       │
│       ├── View/                 # Vistas XAML
│       │   ├── AddressSearchView.xaml
│       │   ├── MassiveGeocodingView.xaml
│       │   └── PropertyPageView.xaml
│       │
│       └── ViewModel/            # ViewModels
│           ├── AddressSearchViewModel.cs
│           ├── MassiveGeocodingViewModel.cs
│           └── PropertyPageViewModel.cs
│
├── Images/                       # Recursos gráficos
├── Config.daml                   # Configuración del AddIn
└── Module1.cs                    # Punto de entrada
```

### Patrones de Diseño Implementados

#### 1. MVVM (Model-View-ViewModel)

```csharp
// ViewModel base con INotifyPropertyChanged
public class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

// Ejemplo de comando asíncrono
private AsyncRelayCommand _searchCommand;
public ICommand SearchCommand => _searchCommand ??= 
    new AsyncRelayCommand(ExecuteSearchAsync, CanExecuteSearch);
```

#### 2. Factory Pattern

```csharp
public static class ConnectionPropertiesFactory
{
    public static DatabaseConnectionProperties Create(DatabaseEngine engine, 
        string host, int port, string database, string user, string password)
    {
        return engine switch
        {
            DatabaseEngine.PostgreSQL => CreatePostgreSQL(host, port, database, user, password),
            DatabaseEngine.Oracle => CreateOracle(host, port, database, user, password),
            DatabaseEngine.PostgreSQLSDE => CreateFromSdeFile(sdeFilePath),
            DatabaseEngine.OracleSDE => CreateFromSdeFile(sdeFilePath),
            _ => throw new NotSupportedException($"Motor no soportado: {engine}")
        };
    }
}
```

#### 3. Repository Pattern

```csharp
public interface IPtAddressGralEntityRepository
{
    Task<IEnumerable<PtAddressGralEntity>> SearchAddressesAsync(
        string address, string city, CancellationToken cancellationToken);
    
    Task<IEnumerable<string>> GetAvailableCitiesAsync(
        CancellationToken cancellationToken);
}

public class OraclePtAddressGralRepository : IPtAddressGralEntityRepository
{
    private readonly DatabaseConnectionService _connectionService;
    
    public async Task<IEnumerable<PtAddressGralEntity>> SearchAddressesAsync(
        string address, string city, CancellationToken cancellationToken)
    {
        var connection = await _connectionService.GetConnectionAsync();
        using var command = connection.CreateCommand();
        
        command.CommandText = @"
            SELECT ID, DIRECCION, FULLADDRESSEAAB, FULLADDRESSUACD, POBLACION
            FROM PT_ADDRESS_GRAL_ENTITY
            WHERE UPPER(POBLACION) = UPPER(:city) 
            AND UPPER(DIRECCION) = UPPER(:address)";
        
        // Implementación...
    }
}
```

#### 4. Singleton Controlado

```csharp
public class Module1 : Module
{
    private static Module1 _this = null;
    private static DatabaseConnectionService _connectionService;
    
    public static Module1 Current => _this ?? (_this = 
        (Module1)FrameworkApplication.FindModule("EAABAddIn_Module"));
    
    public static DatabaseConnectionService ConnectionService => 
        _connectionService ??= new DatabaseConnectionService();
}
```

#### 5. Strategy Pattern (Fallback)

```csharp
public class AddressNormalizer
{
    public async Task<string> NormalizeAsync(string address)
    {
        try
        {
            // Intento de normalización con servicio ESRI
            return await ESRINormalizationService.NormalizeAsync(address);
        }
        catch (LexiconException ex) when (ex.Code is "CODE_145" or "CODE_146")
        {
            // Fallback: usar dirección original
            Debug.WriteLine($"Fallback de normalización: {ex.Code}");
            return address;
        }
    }
}
```

#### 6. Lazy Initialization

```csharp
public class ResultsLayerService
{
    private FeatureClass _geocodedAddressesFC;
    
    public async Task<FeatureClass> GetOrCreateFeatureClassAsync()
    {
        if (_geocodedAddressesFC != null)
            return _geocodedAddressesFC;
        
        await QueuedTask.Run(() =>
        {
            // Crear o recuperar Feature Class
            _geocodedAddressesFC = CreateOrRetrieveFeatureClass();
        });
        
        return _geocodedAddressesFC;
    }
}
```

## Componentes Principales

### 1. Module1.cs - Punto de Entrada

```csharp
internal class Module1 : Module
{
    protected override bool Initialize()
    {
        // Cargar configuración persistente
        LoadConfiguration();
        
        // Inicializar servicios
        InitializeServices();
        
        // Intentar reconexión diferida
        _ = Task.Run(async () => await AttemptReconnectionAsync());
        
        return base.Initialize();
    }
    
    private async Task AttemptReconnectionAsync()
    {
        var config = ConfigurationManager.LoadConfiguration();
        if (config.IsValid)
        {
            await ConnectionService.ConnectAsync(config);
        }
    }
}
```

### 2. DatabaseConnectionService

Gestiona las conexiones a bases de datos con soporte multi-motor.

```csharp
public class DatabaseConnectionService : IDisposable
{
    private Geodatabase _geodatabase;
    private DatabaseEngine _currentEngine;
    private string _sdeFilePath;
    
    public async Task<bool> ConnectAsync(DatabaseConfiguration config)
    {
        try
        {
            DatabaseConnectionProperties connectionProps;
            
            if (config.Engine is DatabaseEngine.PostgreSQLSDE or DatabaseEngine.OracleSDE)
            {
                // Conexión mediante archivo SDE
                connectionProps = new DatabaseConnectionFile(
                    new Uri(config.SdeFilePath, UriKind.Absolute));
            }
            else
            {
                // Conexión por credenciales
                connectionProps = ConnectionPropertiesFactory.Create(
                    config.Engine, config.Host, config.Port, 
                    config.Database, config.User, config.Password);
            }
            
            await QueuedTask.Run(() =>
            {
                _geodatabase = new Geodatabase(connectionProps);
            });
            
            _currentEngine = config.Engine;
            IsConnected = true;
            
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error de conexión: {ex.Message}");
            return false;
        }
    }
    
    public async Task<Geodatabase> GetConnectionAsync()
    {
        if (!IsConnected || _geodatabase == null)
            throw new InvalidOperationException("No hay conexión activa");
        
        return await Task.FromResult(_geodatabase);
    }
    
    public void Dispose()
    {
        _geodatabase?.Dispose();
        _geodatabase = null;
        IsConnected = false;
    }
}
```

### 3. ResultsLayerService

Gestiona la capa de resultados de geocodificación con batch insert optimizado.

```csharp
public class ResultsLayerService
{
    private const string FEATURE_CLASS_NAME = "GeocodedAddresses";
    
    public async Task InsertBatchAsync(List<GeocodeResult> results)
    {
        var featureClass = await GetOrCreateFeatureClassAsync();
        
        await QueuedTask.Run(() =>
        {
            using var operation = new EditOperation
            {
                Name = "Insertar resultados de geocodificación"
            };
            
            foreach (var result in results)
            {
                var attributes = new Dictionary<string, object>
                {
                    ["Identificador"] = result.Identifier,
                    ["Direccion"] = result.Address,
                    ["FullAdressEAAB"] = result.FullAddressEAAB,
                    ["FullAdressUACD"] = result.FullAddressUACD,
                    ["Geocoder"] = result.Geocoder.ToString(),
                    ["Score"] = result.Score,
                    ["ScoreText"] = result.ScoreText,
                    ["FechaHora"] = DateTime.Now
                };
                
                var geometry = MapPointBuilderEx.CreateMapPoint(
                    result.X, result.Y, SpatialReferences.WGS84);
                
                operation.Create(featureClass, attributes, geometry);
            }
            
            operation.Execute();
        });
    }
    
    private FeatureClass CreateOrRetrieveFeatureClass()
    {
        var gdb = GetDefaultGeodatabase();
        
        // Intentar abrir existente
        try
        {
            return gdb.OpenDataset<FeatureClass>(FEATURE_CLASS_NAME);
        }
        catch
        {
            // Crear nuevo
            return CreateNewFeatureClass(gdb);
        }
    }
    
    private FeatureClass CreateNewFeatureClass(Geodatabase gdb)
    {
        var fcDescription = new FeatureClassDescription(
            FEATURE_CLASS_NAME,
            new List<FieldDescription>
            {
                new FieldDescription("Identificador", FieldType.String),
                new FieldDescription("Direccion", FieldType.String),
                new FieldDescription("FullAdressEAAB", FieldType.String),
                new FieldDescription("FullAdressUACD", FieldType.String),
                new FieldDescription("Geocoder", FieldType.String),
                new FieldDescription("Score", FieldType.Double),
                new FieldDescription("ScoreText", FieldType.String),
                new FieldDescription("FechaHora", FieldType.Date)
            },
            new ShapeDescription(GeometryType.Point, SpatialReferences.WGS84)
        );
        
        return gdb.CreateFeatureClass(fcDescription);
    }
}
```

### 4. AddressSearchService

Orquesta la búsqueda de direcciones con fallback inteligente.

```csharp
public class AddressSearchService
{
    private readonly IPtAddressGralEntityRepository _repository;
    private readonly AddressNormalizer _normalizer;
    
    public async Task<SearchResult> SearchAsync(
        string address, string city, CancellationToken cancellationToken)
    {
        // Primer intento: búsqueda exacta
        var results = await _repository.SearchAddressesAsync(
            address, city, cancellationToken);
        
        if (results.Any())
            return new SearchResult(results, SearchStrategy.Exact);
        
        // Segundo intento: búsqueda LIKE ampliada
        Debug.WriteLine("Búsqueda exacta sin resultados, intentando LIKE...");
        results = await _repository.SearchAddressesLikeAsync(
            address, city, cancellationToken);
        
        return new SearchResult(results, SearchStrategy.Like);
    }
    
    public async Task<string> NormalizeAddressAsync(string address)
    {
        try
        {
            return await _normalizer.NormalizeAsync(address);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error de normalización: {ex.Message}");
            return address; // Fallback a dirección original
        }
    }
}
```

### 5. MassiveGeocodingService

Procesamiento masivo con optimizaciones de rendimiento.

```csharp
public class MassiveGeocodingService
{
    private readonly AddressSearchService _searchService;
    private readonly ResultsLayerService _resultsService;
    
    public async Task<MassiveResult> ProcessFileAsync(
        string filePath, IProgress<ProgressInfo> progress)
    {
        var records = await ReadExcelFileAsync(filePath);
        var results = new List<GeocodeResult>();
        var notFound = new List<NotFoundRecord>();
        
        int processed = 0;
        int total = records.Count;
        
        foreach (var record in records)
        {
            var searchResult = await _searchService.SearchAsync(
                record.Address, record.City, CancellationToken.None);
            
            if (searchResult.HasResults)
            {
                // Determinar mejor dirección: EAAB > Catastro > Original
                var bestAddress = DetermineBestAddress(searchResult);
                results.Add(CreateGeocodeResult(record, bestAddress));
            }
            else
            {
                notFound.Add(new NotFoundRecord
                {
                    Identifier = record.Identifier,
                    Address = record.Address,
                    City = record.City,
                    Timestamp = DateTime.Now
                });
            }
            
            processed++;
            progress?.Report(new ProgressInfo(processed, total));
        }
        
        // Inserción en lote optimizada
        await _resultsService.InsertBatchAsync(results);
        await _notFoundService.InsertBatchAsync(notFound);
        
        return new MassiveResult
        {
            Found = results.Count,
            NotFound = notFound.Count,
            Total = total
        };
    }
    
    private string DetermineBestAddress(SearchResult result)
    {
        // Prioridad: FullAddressEAAB > FullAddressUACD > Original > Calle básica
        var first = result.Addresses.First();
        
        if (!string.IsNullOrEmpty(first.FullAddressEAAB))
            return first.FullAddressEAAB;
        
        if (!string.IsNullOrEmpty(first.FullAddressUACD))
            return first.FullAddressUACD;
        
        return first.Direccion;
    }
}
```

## Sistema de Configuración

- Persistencia JSON + respaldo en settings App.
- Validación contextual según tipo de motor.
- Admite cambio caliente (runtime) disparando reconexión.

### Seguridad de Configuración

- Contraseñas no se registran en logs.
- Posible mejora: cifrado AES local (pendiente) usando DPAPI Windows.
- Recomendado: restringir permisos de carpeta `%AppData%/EAABAddIn`.

## Modelo de Datos (Lógico Simplificado)

| Entidad | Campos clave | Fuente | Notas |
|---------|--------------|--------|-------|
| PtAddressGralEntity | ID, Direccion, FullAddressEAAB, FullAddressCadastre, Poblacion | BD corporativa | Base principal de direcciones |
| GeocodeResult | Identifier, Address, Source, Score, Lat/Long | Derivado | Unión de varias fuentes |
| NotFoundRecord | Identifier, Address, City, Timestamp | Generado | Auditoría de intentos fallidos |
| PoiEntity | PoiId, Name, Category, City, X, Y | BD corporativa / vista | Indexable para búsqueda |

## Capa de Resultados Espaciales

### `ResultsLayerService`
- Lazy create de Feature Class `GeocodedAddresses` (WGS84).  
- Inserciones agrupadas dentro de `EditOperation`.  
- Campos calculados en el ViewModel (score interpretado).

### `PoiResultsLayerService`
- Similar estrategia: `POIResults` con campos `PoiId`, `Nombre`, `Categoria`, `Ciudad`, `FechaHora`.
- Reutiliza builder de geometrías estándar.

## Logging y Observabilidad

Estado actual: logging mínimo mediante `Debug.WriteLine` y mensajes UI.  
Sugerido:
- Introducir `ILogger` (MS.Extensions.Logging) con proveedor simple.
- Niveles: Info (operaciones), Warning (faltantes), Error (excepciones).
- Métricas futuras: tiempo promedio por geocodificación, % éxito.

## Rendimiento y Escalabilidad

| Área | Riesgo | Mitigación Actual | Mejora Potencial |
|------|-------|-------------------|------------------|
| Masivo secuencial | Lento con >50k filas | Procesamiento controlado | Paralelizar lectura + cola MCT |
| Acceso BD | Latencia variable | Repositorio único | Cache ciudades en memoria |
| Insert espacial | Bloqueos si muchas ediciones | Batch + single commit | Chunk configurable |
| Normalización externa | Timeout/errores | Fallback inmediato | Circuit breaker + retry |

## Tratamiento de Errores

- Validaciones previas detienen proceso temprano (fail-fast).
- Excepciones en loop masivo contabilizan como "no encontrados" sin detener el resto.
- Mostrar mensajes al usuario solo cuando agregan valor (no spam por cada fila).

## Pruebas (Testing)

### Estrategia Propuesta

| Tipo | Objetivo | Ejemplos |
|------|----------|----------|
| Unit | Lógica pura (normalizador, filtros) | `AddressNormalizerTests` |
| Repository (mock) | Queries adaptadas por motor | `PtAddressGralRepositoryTests` |
| Integration (opcional) | Conexión real a GDB de prueba | Escenarios mínimos |
| UI (manual) | Flujo MVVM básico | Buscar, Masivo, POI |

### Recomendaciones
- Introducir interfaces para capa ResultsLayer para facilitar mocks.
- Usar `xUnit` + `Moq`.
- Datos de prueba ligeros (JSON) para direcciones.

## Build y Empaquetado

### Compilación Local

1. Abrir solución en Visual Studio con ArcGIS Pro instalado.
2. Restaurar paquetes NuGet.
3. Asegurar target: `net8.0-windows` con `UseWPF` habilitado.
4. Compilar en modo Release.

### Generación del AddIn (.esriAddInX)

1. Verificar `Config.daml` actualizado (botones/paneles).  
2. Build Release genera carpeta `bin/Release`.  
3. Utilizar herramienta de empaquetado del SDK (si configurada) o copiar output.  
4. Validar firma (si política corporativa lo exige).  

### Versionado

- Mantener versión en AssemblyInfo o proyecto (PropertyGroup `<Version>`).  
- Sincronizar con sección "Información de Versión" de manual usuario.  

## Despliegue

| Entorno | Acción | Notas |
|---------|-------|-------|
| Usuario final | Distribuir `.esriAddInX` | Instrucciones en READMEUSER |
| Piloto | Revisión funcional | Capturar métricas básicas |
| Producción | Publicación controlada | Registrar hash archivo |

## Seguridad y Acceso a Datos

- Principio de mínimo privilegio para usuarios de BD.
- No almacenar contraseñas en texto plano fuera de `%AppData%` (cifrar futuro).
- Validar origen de archivos Excel (no macros, no binarios maliciosos).

## Internacionalización (i18n)

- Textos actualmente en español embebidos.
- Mejora futura: recursos (.resx) para soportar EN/ES.

## Roadmap Propuesto

| Prioridad | Feature | Descripción |
|-----------|---------|-------------|
| Alta | Cancelación masiva | Token cancelar proceso en curso |
| Media | Cache ciudades | Reducir llamadas repetidas |
| Media | Exportar no encontrados | CSV automático |
| Media | Paginación POIs | Controlar grandes resultados |
| Baja | Cifrado credenciales | DPAPI / AES |
| Baja | Telemetría | Eventos anónimos de uso |

## Ejemplo Simplificado de POI Repository

```csharp
public interface IPoiRepository {
    IEnumerable<PoiEntity> Search(string term, string city = null, int max = 500);
}

public class PostgresPoiRepository : IPoiRepository {
    private readonly DatabaseConnectionService _connection;
    public IEnumerable<PoiEntity> Search(string term, string city = null, int max = 500) {
        // Implementación con ILIKE y limit
    }
}
```

## Notas de Mantenimiento

- Revisar compatibilidad ArcGIS Pro antes de subir versión SDK.
- Ejecutar pruebas de regresión después de cambios en repositorios.
- Documentar nuevas columnas añadidas a Feature Class.