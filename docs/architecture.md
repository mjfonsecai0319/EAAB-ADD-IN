# Arquitectura

## Capas
- Presentación (WPF + MVVM): Views, ViewModels, comandos y DAML para Ribbon/DockPane.
- Aplicación: Casos de uso y mapeos entre APIs externas y entidades internas.
- Dominio: Interfaces de repositorio y contratos.
- Core: Conexiones a BD, entidades, servicios HTTP y servicios de mapa.

## Componentes principales
- DockPane y Ribbon: `Config.daml`, `Src/Presentation/View*`, `ViewModel/*`.
- Búsqueda individual: `AddressSearchViewModel`, `AddressSearchUseCase`, `ResultsLayerService`.
- Geocodificación masiva: `MassiveGeocodeViewModel` → Excel → `ResultsLayerService.CommitPointsAsync`.
- No encontrados: `AddressNotFoundTableService`.
- Conexión a BD: `DatabaseConnectionService`, `Settings`.

## Esquemas de salida
- Feature Class `GeocodedAddresses` (WGS84):
  - Campos: `Identificador`, `Direccion`, `Poblacion`, `FullAdressEAAB`, `FullAdressUACD`, `Geocoder`, `Score` (DOUBLE), `ScoreText`.
- Tabla `GeocodeNotFound`:
  - Campos: `Identificador`, `Direccion`, `Poblacion`, `full_address_eaab`, `full_address_uacd`, `Geocoder`, `Score` (DOUBLE).

## Consideraciones ArcGIS Pro SDK
- Todas las operaciones de Core Data/Mapa se ejecutan con `QueuedTask.Run`.
- Inserción sin `EditOperation` para tolerar edición deshabilitada.
- Uso de `Geoprocessing` para crear tabla/feature class y agregar campos si faltan.
