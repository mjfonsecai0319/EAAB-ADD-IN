# Uso

## Búsqueda individual
1. Pestaña “EAAB Add-in” → “Buscar”.
2. Seleccione ciudad y escriba dirección.
3. El sistema normaliza y busca en BD interna. Si no hay resultados, intenta IDECA (Bogotá) y luego ESRI.
4. Los resultados válidos se dibujan en el mapa en `GeocodedAddresses`.

## Geocodificación masiva
1. Pestaña “EAAB Add-in” → “Masivo”.
2. Seleccione archivo Excel `.xlsx`/`.xls` con columnas: Identificador, Dirección, Población.
3. Procesar: los puntos válidos se acumulan y se insertan en lote para mejor rendimiento.

## Salidas
- Capa `GeocodedAddresses` (puntos) en la GDB predeterminada del proyecto.
- Tabla `GeocodeNotFound` agregada al mapa con registros de entradas sin resultado.

## Consejos
- Mantenga una GDB predeterminada válida en el proyecto.
- Si la edición está deshabilitada, la inserción funciona con Core Data (no necesita `EditOperation`).
