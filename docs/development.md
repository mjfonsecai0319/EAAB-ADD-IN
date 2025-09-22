+# Guía de Desarrollo

## Entorno
- Windows + ArcGIS Pro 3.4 (o versión compatible con el SDK configurado).
- Visual Studio 2022 + ArcGIS Pro SDK para .NET.
- .NET 8 SDK.

## Abrir y ejecutar
1. Abrir `EAABAddIn.sln` en Visual Studio.
2. Establecer configuración `Debug`.
3. Ejecutar (F5) inicia ArcGIS Pro con el Add-In cargado.
4. En ArcGIS Pro, abrir un proyecto con una geodatabase predeterminada válida y probar el Ribbon “EAAB Add-in”.

## Depuración
- Coloque breakpoints en ViewModels/UseCases/Servicios.
- Para código que corre en `QueuedTask.Run`, use `Debug.WriteLine` y el panel de Salida si se ejecuta en hilos del MCT.
- Capturar excepciones y registrarlas; evitar MessageBoxes en hilos MCT cuando sea posible.

## Patrones del SDK (crítico)
- Threading: todas las llamadas que tocan `ArcGIS.Desktop.*` (datos/cartografía) deben ejecutarse dentro de `QueuedTask.Run`.
- Edición: preferimos Core Data (`CreateRowBuffer`/`CreateRow`) en lugar de `EditOperation` para tolerar edición deshabilitada.
- Geoprocesamiento (GP): para crear datasets y campos (CreateFeatureclass, CreateTable, AddField) y usar `CancelableProgressor.None` + `GPExecuteToolFlags.AddToHistory`.
- Evitar schema locks: abrir/cerrar `Geodatabase` para inspeccionar/crear y no mantener handles abiertos al crear.

## Estándares de código
- C# 10/11, nullable awareness donde aplique.
- Nombres consistentes con el dominio: `FullAdressEAAB`, `FullAdressUACD`, etc. (mantener compatibilidad con esquema existente).
- Manejo de nulos defensivo al construir filas (usar `string.Empty` o `null` según tipo).
- No introducir cambios de formato masivos no relacionados.

## Estructura y puntos de extensión
- UI (WPF + MVVM): `Src/Presentation/View*` y `ViewModel/*`.
- Casos de uso: `Src/Application/UseCases/*`.
- Repositorios BD: `Src/Domain/Repositories/*` (implementar nuevos motores extendiendo el contrato).
- Servicios de mapa: `Src/Core/Map/*` (capas/tabla de resultados).
- Configuración: `Src/Core/Config/Settings.cs` (agregar propiedades si se amplía la configuración).

## Datos externos y APIs
- IDECA y ESRI: revisar límites, autenticación y Términos de Uso al incrementar consumo.
- `HttpService`: usar timeouts y cache local cuando aplique.

## Publicación
1. Compilar en `Release`.
2. Verificar `bin/Release/net8.0-windows/EAABAddIn.esriAddinX`.
3. Distribuir el `.esriAddinX` a usuarios (doble clic o Add-In Manager en ArcGIS Pro).

## Pruebas manuales sugeridas
- Búsqueda individual con dirección válida e inválida.
- Geocodificación masiva con Excel pequeño (10-20 filas) y grande (1000+ filas).
- Sin conexión a internet para verificar fallback/errores.
- Edición deshabilitada en ArcGIS Pro para validar inserciones por Core Data.

## Roadmap ideas
- Configurable: priorización de fuentes (EAAB/IDECA/ESRI) y umbrales de Score.
- Log de auditoría para búsquedas y resultados.
- Soporte para servicios geocodificadores internos.
