# Manual de Usuario - EAAB AddIn para ArcGIS Pro

## Descripción

El EAAB AddIn es una herramienta para ArcGIS Pro que facilita la búsqueda y localización de direcciones en Bogotá, tanto de forma individual como masiva. Permite conectarse a la base de datos corporativa para consultar direcciones y ubicarlas automáticamente en el mapa.

## Requisitos del Sistema

Antes de instalar, asegúrate de tener:

- **ArcGIS Pro 3.4 o superior** instalado y funcionando correctamente
- **Conexión a la base de datos corporativa** (PostgreSQL u Oracle)
- Permisos de acceso a la base de datos con usuario y contraseña

## Instalación

### Paso 1: Obtener el archivo de instalación

Recibirás el archivo **EAABAddIn.esriAddInX** por alguno de estos medios:

- USB
- Correo electrónico
- Carpeta compartida de red

Guarda el archivo en una ubicación fácil de encontrar (Escritorio, Descargas o Documentos).

### Paso 2: Instalar el complemento
![Video de Instalación](<docs/res/Grabación 2025-09-23 083552.gif>)

1. **Cierra ArcGIS Pro** completamente si está abierto
2. Ubica el archivo **EAABAddIn.esriAddInX** que guardaste
3. Haz **doble clic** sobre el archivo
4. Aparecerá una ventana del instalador de ArcGIS Pro
5. Haz clic en **"Instalar"** y acepta los términos si se solicitan
6. Espera el mensaje **"Instalación completada"**
7. Abre ArcGIS Pro

## Configuración Inicial

La primera vez que uses el AddIn, necesitas configurar la conexión a la base de datos.

### Configurar la conexión

1. Abre **ArcGIS Pro**
2. Ve al menú **Archivo → Opciones**
3. En el panel izquierdo, busca y haz clic en **"EAAB Add-In"**
4. Completa los datos de conexión según tu tipo de base de datos:

#### Si usas PostgreSQL:

- **Motor**: Selecciona "PostgreSQL"
- **Host**: Dirección del servidor (ej: `localhost` o `192.168.1.100`)
- **Puerto**: Normalmente es `5432` (se llena automáticamente)
- **Base de datos**: Nombre de la base de datos
- **Usuario**: Tu nombre de usuario
- **Contraseña**: Tu contraseña

#### Si usas Oracle:

- **Motor**: Selecciona "Oracle"
- **Host**: Dirección del servidor
- **Puerto**: Normalmente es `1521` (se llena automáticamente)
- **Base de datos**: Nombre del servicio Oracle (SID)
- **Usuario**: Tu nombre de usuario
- **Contraseña**: Tu contraseña

#### Si usas archivo .sde (Oracle SDE o PostgreSQL SDE):

- **Motor**: Selecciona "Oracle SDE" o "PostgreSQL SDE"
- Haz clic en **"Examinar"** y selecciona tu archivo `.sde`
- Los demás campos desaparecerán automáticamente

5. Haz clic en **"Probar Conexión"**
6. Si aparece **"Conexión exitosa"**, haz clic en **"Guardar y Conectar"**

> **Importante**: Si la conexión falla, verifica que tu usuario y contraseña sean correctos, y que puedas acceder a la red donde está el servidor.

### La configuración se guarda automáticamente

Una vez configurada la conexión, no necesitas volver a ingresarla. El AddIn recordará tus datos para la próxima vez que abras ArcGIS Pro.

## Cómo Usar el AddIn

Una vez configurado, verás una nueva pestaña llamada **"EAAB Add-in"** en la parte superior de ArcGIS Pro.

### 1. Buscar una Dirección Individual

Esta función te permite buscar y localizar direcciones una por una.

**Pasos:**

1. Haz clic en el botón **"Buscar"** en la pestaña EAAB Add-in
2. Se abrirá un panel a la derecha
3. **Selecciona la ciudad** de la lista desplegable
4. **Escribe la dirección** que quieres buscar (ej: "Calle 123 #45-67")
5. Haz clic en **"Buscar Dirección"**

**Resultados:**

- El mapa se moverá y hará zoom automáticamente a la ubicación encontrada
- Se creará un punto en el mapa con la ubicación
- Los datos se guardarán en una capa llamada `GeocodedAddresses` que incluye:
  - La dirección que escribiste
  - La dirección encontrada en la base de datos
  - La fuente de la información (EAAB, Catastro o ESRI)
  - Fecha y hora de la búsqueda
- Si no se encuentra la dirección, se registrará en una tabla de **direcciones no encontradas** para auditoría y análisis posteriores.

**Consejos:**

- Si la lista de ciudades aparece vacía, haz clic en el botón de **"Recargar"** (ícono de actualizar)
- El panel te indicará si estás conectado a la base de datos
- La barra de progreso muestra el estado de la búsqueda

### 2. Geocodificar Direcciones Masivamente

Esta función permite procesar muchas direcciones al mismo tiempo desde un archivo Excel.

#### Preparar el archivo Excel

Tu archivo Excel debe tener exactamente estas tres columnas:

| Identificador | Direccion | Poblacion |
|--------------|-----------|-----------|
| 001 | Calle 123 #45-67 | Bogotá |
| 002 | Carrera 50 #20-30 | Bogotá |

- **Identificador**: Un código único para cada dirección (puede ser número o texto)
- **Direccion**: La dirección completa
- **Poblacion**: El nombre de la ciudad

**Pasos:**

1. Haz clic en el botón **"Masivo"** en la pestaña EAAB Add-in
2. Se abrirá un panel a la derecha
3. Haz clic en **"Examinar..."** y selecciona tu archivo Excel (.xlsx)
4. El sistema revisará que tu archivo tenga el formato correcto
5. Si todo está bien, haz clic en **"Procesar Archivo"**
6. Espera mientras se procesan las direcciones (verás una barra de progreso)

**Resultados:**

- Al finalizar, verás un resumen:
  - Número de direcciones encontradas
  - Número de direcciones no encontradas
  - Total procesado
- Todas las direcciones se agregarán a la capa `GeocodedAddresses`
- Todas las no encontradas quedarán registradas en la tabla de **direcciones no encontradas** con fecha y hora
- El sistema intenta encontrar cada dirección dos veces si no hay coincidencia exacta

### 3. Búsqueda de Puntos de Interés (POIs)

La herramienta también permite localizar Puntos de Interés (instituciones, equipamientos, servicios, etc.).

**Pasos:**
1. Haz clic en el botón **"POI"** en la pestaña EAAB Add-in (ícono de lupa sobre edificio).
2. Se abrirá un panel lateral similar al de direcciones.
3. Ingresa un término de búsqueda (ej: "fontibon", "colegio", "calera", "acueducto").
4. (Opcional) Selecciona una ciudad o limita por área activa del mapa.
5. Dependiendo del término y lo que necesites:
   - Selecciona un resultado específico de la lista y haz clic en **"Ubicar seleccionado"** para centrar solo ese.
   - Haz clic en **"Ubicar todos"** para agregar y centrar todos los resultados devueltos.
6. (Alternativamente) Usa **"Buscar POI"** para refrescar/filtrar la lista si cambias el texto.

**Resultados:**
- Se listarán coincidencias con nombre, tipo y código.
- Al seleccionar un POI y ubicarlo el mapa hace zoom y se agrega un punto a la capa `POIResults`.
- Si eliges "Ubicar todos" se insertan todos los puntos visibles.



### 4. Cambiar la Configuración de Conexión

Si necesitas cambiar de base de datos o actualizar tus credenciales:

1. Ve a **Archivo → Opciones → EAAB Add-In**
2. Modifica los datos que necesites cambiar
3. Haz clic en **"Probar Conexión"** para verificar
4. Haz clic en **"Guardar y Conectar"**

Los cambios se guardan automáticamente mientras editas los campos.

### 5. Exportar Resultados

Puedes exportar los puntos generados a otros formatos para compartir o procesar:

**Opciones comunes:**
- Clic derecho sobre la capa `GeocodedAddresses` → Export → **Feature Class To Feature Class** (para otra GDB)
- Clic derecho → Data → **Export Features** → Guardar como Shapefile o GeoPackage
- Uso de **Table To Excel** para extraer atributos en tabular

**Campos Clave en `GeocodedAddresses`:**
- `Identificador`: El código original del archivo o de tu búsqueda individual
- `Direccion`: Dirección consultada
- `FullAddressEAAB` / `FullAddressCadastre`: Variantes enriquecidas
- `Source` / `ScoreText`: Origen y calidad
- `FechaHora`: Marca de tiempo de la operación

### 6. Buenas Prácticas de Uso

- Revisa que tu Excel no tenga filas totalmente vacías al final.
- Evita caracteres especiales innecesarios (ej: múltiples espacios, tabs).
- Prefiere códigos de ciudad oficiales si el sistema lo requiere (ver previsualización en panel masivo).
- No lances procesos masivos mientras ArcGIS Pro ejecuta otras ediciones complejas.
- Guarda el proyecto antes de una geocodificación masiva grande.

### 7. Interpretación de la Calidad (Score / Etiquetas)

La columna `ScoreText` sintetiza la procedencia/calidad:
- `Exacta`: Coincidencia directa registrada en EAAB.
- `Aproximada por Catastro`: Ajustada usando datos catastrales.
- `ESRI <valor>`: Resultado proveniente del servicio ESRI con score numérico.
- Otros valores pueden representar transformaciones adicionales.

## Solución de Problemas Comunes

### El AddIn no aparece en ArcGIS Pro

**Solución:**
1. Cierra completamente ArcGIS Pro
2. Desinstala el AddIn desde el Administrador de Add-Ins de ArcGIS Pro
3. Vuelve a instalar el archivo `.esriAddInX`

### No se puede conectar a la base de datos

**Causas posibles:**

- Usuario o contraseña incorrectos
- No tienes acceso a la red donde está el servidor
- El servidor está apagado o no disponible

**Solución:**
1. Verifica tus credenciales con el administrador
2. Confirma que estás conectado a la red corporativa
3. Prueba hacer ping al servidor desde la línea de comandos

### No aparecen ciudades en la lista

**Solución:**
1. Verifica que estés conectado a la base de datos (revisa el estado de conexión)
2. Haz clic en el botón **"Recargar"** del panel de búsqueda
3. Si persiste, verifica tu conexión

### El archivo Excel no se procesa

**Errores comunes:**

- "El archivo no contiene la columna requerida"
  - **Solución**: Verifica que tu archivo tenga las columnas `Identificador`, `Direccion` y `Poblacion` exactamente con esos nombres

- "No se pudo leer el archivo"
  - **Solución**: Asegúrate de que el archivo sea `.xlsx` y no esté protegido con contraseña

### No se encuentran direcciones

Si una dirección no se encuentra:

- El sistema intentará buscarla de forma más amplia automáticamente
- La dirección se registrará en la tabla de no encontrados con la fecha y hora
- Verifica que la dirección esté bien escrita
- Confirma que la ciudad sea correcta

### La búsqueda de POIs devuelve demasiados resultados
- Añade más palabras clave específicas.
- Usa filtros de tipo si están disponibles.
- Limita el área haciendo un zoom mayor antes de buscar.

### La búsqueda de POIs no devuelve resultados
- Revisa tu conexión a la base de datos.
- Prueba con un término más general.
- Evita abreviaturas poco comunes.

## Preguntas Frecuentes (FAQ)

**¿Necesito conexión a Internet?**  Solo para servicios ESRI complementarios; la base principal usa red corporativa.

**¿Se sobrescriben los puntos anteriores?**  No, la capa acumula resultados hasta que la limpies manualmente.

**¿Puedo cancelar una ejecución masiva?**  Versión actual: no. Recomendado dividir archivos grandes (>10 mil filas).

**¿Qué formato de coordenadas usa?**  WGS84 (EPSG:4326) para puntos internos; el mapa reproyecta según tu vista.

**¿Puedo usar CSV en vez de Excel?**  No en esta versión (solo `.xlsx` / `.xls`).

## Glosario Rápido

| Término | Definición |
|---------|------------|
| Geocodificar | Transformar una dirección en coordenadas espaciales |
| POI | Punto de Interés (edificio, institución, servicio) |
| SDE | Archivo de conexión a geodatabase corporativa |
| Score | Valor numérico de confianza del servicio (cuando aplica) |
| Exacta | Coincidencia directa en la base interna |

## Información de Versión

**Versión**: 1.1  
**Última actualización**: 29 de septiembre de 2025  
**Compatible con**: ArcGIS Pro 3.4 o superior

**Novedades de la versión 1.1:**
- Soporte para conexiones PostgreSQL SDE y Oracle SDE
- Registro de fecha y hora local en todas las búsquedas
- Mejor clasificación de resultados por origen y calidad
- Búsqueda ampliada automática cuando no hay coincidencias exactas
- Registro de direcciones no encontradas con fecha y hora

## Contacto y Soporte

Para soporte técnico o reportar problemas, contacta al equipo de desarrollo de sistemas de información geográfica de la EAAB.

---

**Nota**: Este manual está diseñado para usuarios finales. Si eres desarrollador y necesitas información técnica, solicita el Manual Técnico.