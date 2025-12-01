# Manual de Usuario - EAAB AddIn para ArcGIS Pro

## Descripción

El EAAB AddIn es una herramienta integral para ArcGIS Pro que facilita:

- **Geocodificación de direcciones**: Búsqueda y localización de direcciones en Bogotá de forma individual o masiva
- **Búsqueda de Puntos de Interés (POIs)**: Localización de instituciones, equipamientos y servicios

- **Migración de datos**: transformación de redes de acueducto y alcantarillado a estructura corporativa

- **Corte de datos espaciales (Clip)**: Extracción de feature classes por áreas de interés usando polígonos

- **Generación y verificación de integridad (Hash SHA256)**: Aseguramiento de integridad de archivos y geodatabases

El AddIn permite conectarse a la base de datos corporativa para consultar información y ubicarla automáticamente en el mapa, además de realizar operaciones avanzadas de gestión y validación de datos.

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

**Herramientas disponibles:**
1. **Buscar** - Búsqueda individual de direcciones
2. **Masivo** - Geocodificación masiva desde Excel
3. **POI** - Búsqueda de Puntos de Interés
4. **Migración** - Migración de redes de acueducto/alcantarillado
5. **Clip** - Corte de Feature Datasets
6. **Hash** - Generador y verificador de integridad SHA256

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

### 4. Migración de Datos

Esta herramienta permite migrar datos de redes de acueducto y alcantarillado desde formatos antiguos o externos hacia la estructura corporativa de la EAAB. Es especialmente útil para:
- Integrar datos de proyectos nuevos al sistema corporativo
- Actualizar datos de versiones antiguas del modelo de datos
- Consolidar información de diferentes fuentes

#### ¿Qué hace la migración?

La migración **transforma y copia** las features (líneas y puntos) de capas de origen hacia una geodatabase de destino con la estructura estándar de la EAAB. Durante el proceso:

1. **Valida la estructura** de los datos de origen
2. **Clasifica automáticamente** cada feature según su tipo (CLASE, SUBTIPO, SISTEMA)
3. **Mapea los atributos** desde los campos antiguos a los nuevos
4. **Crea la geodatabase destino** usando un esquema XML predefinido
5. **Proyecta las geometrías** al sistema de coordenadas del mapa activo si es necesario
6. **Ajusta dimensiones Z/M** según los requisitos de las capas de destino
7. **Agrega las capas al mapa** automáticamente con simbología apropiada

#### Preparación antes de migrar

**1. Archivos necesarios:**
- **Capas de origen**: Tus shapefiles o feature classes con datos de acueducto/alcantarillado
- **Esquema XML**: El archivo XML que define la estructura de la geodatabase destino (proporcionado por el administrador)
- **Carpeta de salida**: Una carpeta donde se creará la geodatabase de migración

**2. Estructura esperada de datos de origen:**

Las capas de origen deben tener al menos estos campos clave:

| Campo | Tipo | Descripción | Requerido |
|-------|------|-------------|-----------|
| CLASE | Entero | Tipo de elemento (1=Red, 2=Troncal, 3=Lateral, etc.) | Sí |
| SUBTIPO | Entero | Subtipo específico del elemento | No |
| SISTEMA | Entero | Tipo de sistema (0/2=Sanitario, 1=Pluvial) | No* |

*Si SISTEMA está vacío, se asume sanitario por defecto.


#### Pasos para ejecutar la migración

**Paso 1: Abrir la herramienta**

1. Haz clic en el botón **"Migración"** en la pestaña EAAB Add-in
2. Se abrirá un panel a la derecha con el formulario de migración

**Paso 2: Seleccionar carpeta de salida**

1. Haz clic en **"Examinar..."** junto a "Carpeta de Salida"
2. Selecciona la carpeta donde se creará la geodatabase de migración
3. La geodatabase se llamará automáticamente `GBD_Cargue.gdb`

**Paso 3: Seleccionar esquema XML**

1. Haz clic en **"Examinar..."** junto a "Esquema XML"
2. Selecciona el archivo XML con la definición de la estructura destino
3. Este archivo debe ser proporcionado por el área de sistemas de la EAAB

**Paso 4: Seleccionar capas de origen**

Selecciona las capas que deseas migrar (puedes elegir una o varias):

**Para Acueducto:**
- **Líneas ACU**: Haz clic en "Examinar..." y selecciona la capa de líneas de acueducto
- **Puntos ACU**: Haz clic en "Examinar..." y selecciona la capa de puntos de acueducto

**Para Alcantarillado Sanitario:**
- **Líneas ALC**: Haz clic en "Examinar..." y selecciona la capa de líneas de alcantarillado sanitario
- **Puntos ALC**: Haz clic en "Examinar..." y selecciona la capa de puntos de alcantarillado sanitario

**Para Alcantarillado Pluvial:**
- **Líneas ALC Pluvial**: Haz clic en "Examinar..." y selecciona la capa de líneas de alcantarillado pluvial
- **Puntos ALC Pluvial**: Haz clic en "Examinar..." y selecciona la capa de puntos de alcantarillado pluvial

**Paso 5: Validación automática**

Antes de la migración, el sistema valida automáticamente:
- ✓ Estructura de campos requeridos
- ✓ Tipos de datos correctos
- ✓ Valores en campos clave (CLASE, SUBTIPO, SISTEMA)
- ⚠ Features sin clasificación
- ⚠ Valores inesperados o nulos

**Paso 6: Gestión de advertencias**

Si la validación detecta advertencias:

1. **Aparecerá un cuadro de diálogo** mostrando:
   - Número total de advertencias
   - Datasets que presentan problemas
   - Ubicación de los reportes de validación (archivos CSV)

2. **Opciones:**
   - **Revisar reportes**: Abre los archivos CSV generados en la carpeta de salida
   - **Corregir datos**: Edita las capas de origen y vuelve a intentar
   - **Continuar con advertencias**: Marca el checkbox ☑ **"Migrar con Advertencias"** y ejecuta nuevamente

**Nota importante:** Por seguridad, la migración **NO se ejecutará** si hay advertencias a menos que marques explícitamente el checkbox "Migrar con Advertencias".

**Paso 7: Ejecutar migración**

1. Haz clic en el botón **"Ejecutar"**
2. Observa la barra de progreso y mensajes de estado
3. El proceso puede tomar varios minutos dependiendo del volumen de datos

#### Resultados de la migración

**Geodatabase creada:**
- Se crea automáticamente en la carpeta de salida
- Nombre: `GDB.Cargue.gdb`
- Contiene feature classes organizadas por tipo de red

**Feature Classes de Acueducto:**
- `acu_RedLocal`: Líneas de red local
- `acu_RedMatriz`: Líneas de red matriz
- `acu_Tanque`: Puntos de tanques
- `acu_Valvula`: Puntos de válvulas
- *(y otros según esquema XML)*

**Feature Classes de Alcantarillado Sanitario:**
- `als_RedLocal`: Líneas de red local sanitaria
- `als_RedTroncal`: Líneas de red troncal sanitaria
- `als_LineaLateral`: Líneas laterales sanitarias
- `als_Pozo`: Puntos de pozos sanitarios
- `als_Sumidero`: Puntos de sumideros sanitarios
- `als_EstructuraRed`: Estructuras de red sanitaria
- `als_CajaDomiciliaria`: Cajas domiciliarias sanitarias
- `als_SeccionTransversal`: Secciones transversales sanitarias

**Feature Classes de Alcantarillado Pluvial:**
- `alp_RedLocal`: Líneas de red local pluvial
- `alp_RedTroncal`: Líneas de red troncal pluvial
- `alp_LineaLateral`: Líneas laterales pluviales
- `alp_Pozo`: Puntos de pozos pluviales
- `alp_Sumidero`: Puntos de sumideros pluviales
- `alp_EstructuraRed`: Estructuras de red pluvial
- `alp_CajaDomiciliaria`: Cajas domiciliarias pluviales
- `alp_SeccionTransversal`: Secciones transversales pluviales

**Capas agregadas al mapa:**
- Todas las capas con datos se agregan automáticamente al mapa activo
- Líneas aparecen en color verde
- Puntos aparecen en color naranja
- El mapa hace zoom automático al extent de los datos migrados

**Reportes de migración:**
En la carpeta de salida se generan reportes CSV con:
- Resumen por clase de feature migrada
- Número de features procesadas exitosamente
- Features que no pudieron migrarse y razón
- Features sin campo CLASE o sin clase destino

#### Mapeo de clasificaciones

El sistema mapea automáticamente según el campo CLASE:

**Líneas (según CLASE y SISTEMA):**
| CLASE | SISTEMA | Capa Destino |
|-------|---------|--------------|
| 1 | 0 o 2 | als_RedLocal (sanitario) |
| 1 | 1 | alp_RedLocal (pluvial) |
| 2 | 0 o 2 | als_RedTroncal (sanitario) |
| 2 | 1 | alp_RedTroncal (pluvial) |
| 3 | 0 o 2 | als_LineaLateral (sanitario) |
| 3 | 1 | alp_LineaLateral (pluvial) |
| 4 | 0 o 2 | als_RedLocal (sanitario) |
| 4 | 1 | alp_RedLocal (pluvial) |

**Puntos (según CLASE y SISTEMA):**
| CLASE | SISTEMA | Capa Destino |
|-------|---------|--------------|
| 1 | 0 o 2 | als_EstructuraRed (sanitario) |
| 1 | 1 | alp_EstructuraRed (pluvial) |
| 2 | 0 o 2 | als_Pozo (sanitario) |
| 2 | 1 | alp_Pozo (pluvial) |
| 3 | 0 o 2 | als_Sumidero (sanitario) |
| 3 | 1 | alp_Sumidero (pluvial) |
| 4 | 0 o 2 | als_CajaDomiciliaria (sanitario) |
| 4 | 1 | alp_CajaDomiciliaria (pluvial) |
| 5 | 0 o 2 | als_SeccionTransversal (sanitario) |
| 5 | 1 | alp_SeccionTransversal (pluvial) |
| 6 | 0 o 2 | als_EstructuraRed (sanitario) |
| 6 | 1 | alp_EstructuraRed (pluvial) |
| 7 | 0 o 2 | als_Sumidero (sanitario) |
| 7 | 1 | alp_Sumidero (pluvial) |

#### Atributos migrados

**Para líneas se migran:**
- SUBTIPO, DOMTIPOSISTEMA, FECHAINSTALACION
- LONGITUD_M, PENDIENTE, PROFUNDIDADMEDIA
- DOMMATERIAL, DOMMATERIAL2, DOMDIAMETRONOMINAL
- DOMTIPOSECCION, NUMEROCONDUCTOS
- BASE, ALTURA1, ALTURA2, TALUD1, TALUD2, ANCHOBERMA
- DOMESTADOENRED, DOMCALIDADDATO, DOMESTADOLEGAL
- COTARASANTEINICIAL, COTARASANTEFINAL
- COTACLAVEINICIAL, COTACLAVEFINAL
- COTABATEAINICIAL, COTABATEAFINAL
- N_INICIAL, N_FINAL, NOMBRE
- OBSERVACIONES, CODACTIVOS_FIJOS
- *(y otros según esquema)*

**Para puntos se migran:**
- SUBTIPO, DOMTIPOSISTEMA, FECHAINSTALACION
- COTARASANTE, COTATERRENO, COTAFONDO, PROFUNDIDAD
- DOMMATERIAL, LOCALIZACIONRELATIVA
- DOMESTADOENRED, DOMCALIDADDATO
- LARGOESTRUCTURA, ANCHOESTRUCTURA, ALTOESTRUCTURA
- ROTACIONSIMBOLO, DIRECCION, NOMBRE
- OBSERVACIONES, CODACTIVO_FIJO
- NORTE, ESTE, ABSCISA, IDENTIFIC
- *(y otros según esquema)*

### 5. Cortar Feature Dataset (Clip)

La funcionalidad de "Cortar" permite extraer (clip) las Feature Classes de un Feature Dataset usando un polígono seleccionado en el mapa como máscara de recorte. Es útil para obtener subconjuntos de datos por área de interés o trabajar con áreas específicas.

**Casos de uso:**
- Extraer datos de una zona específica para análisis
- Crear subconjuntos más pequeños para procesar o compartir
- Recortar redes de acueducto/alcantarillado por localidad o sector
- Generar entregas de proyecto con datos limitados geográficamente

**Pasos detallados:**

**Paso 1: Abrir la herramienta**
1. En la pestaña **EAAB Add-in**, haz clic en el botón **"Clip"** (ícono de scissors/corte)
2. Se abrirá un panel a la derecha con el título "Clip Feature Dataset"

**Paso 2: Seleccionar Feature Dataset de origen**
1. Haz clic en **"Examinar..."** junto a "Feature Dataset"
2. Navega hasta la carpeta que contiene tu geodatabase
3. Selecciona la geodatabase (.gdb) que contiene los Feature Dataset que deseas recortar
4. El sistema cargará automáticamente la lista de Feature Datasets disponibles

**Paso 3: Seleccionar Feature Classes a recortar**
1. Expande el Feature Dataset en la lista
2. Selecciona las Feature Classes que deseas recortar (puedes seleccionar múltiples)
3. Usa los botones de conveniencia:
   - **"Seleccionar todo"**: Marca todas las Feature Classes
   - **"Deseleccionar todo"**: Desmarca todas las selecciones
4. (Opcional) Usa el campo de **filtro** para buscar Feature Classes por nombre

**Paso 4: Seleccionar carpeta de salida**
1. Haz clic en **"Examinar..."** junto a "Carpeta de Salida"
2. Selecciona la carpeta donde se creará la nueva geodatabase recortada
3. **Nota**: La herramienta creará automáticamente una GDB con nombre `Clip_YYYYMMDD_HHmmss.gdb`

**Paso 5: Seleccionar polígono de recorte en el mapa**
1. En ArcGIS Pro, en el mapa activo, **selecciona exactamente un polígono** que será la máscara de recorte
2. La herramienta mostrará:
   - ✓ Estado de selección: "Selección válida" o "Sin selección"
   - Área del polígono en m² 
3. **Importante**: Debe haber exactamente 1 polígono seleccionado. Si hay 0 o más de 1, no se permitirá ejecutar

**Paso 6: (Opcional) Configurar Buffer**
1. Marca el checkbox **"Aplicar Buffer"** si deseas expandir el área de recorte
2. Ingresa la distancia en **metros** (ej: 100, 500, 1000)
3. Selecciona el tipo de buffer:
   - **Redondeado**: Puntas redondeadas en las esquinas
   - **Plano**: Esquinas afiladas (Aproximado)
4. El buffer se sumará al polígono original para crear un área más grande

**Paso 7: Ejecutar el corte**
1. Haz clic en el botón **"Cortar / Ejecutar"**
2. Observa la barra de progreso
3. La herramienta procesará cada Feature Class seleccionada

**Resultados:**

-  Se crea una nueva geodatabase con nombre `Clip_YYYYMMDD_HHmmss.gdb`
- Todas las Feature Classes recortadas se incluyen con la estructura original
- Solo se copian las features dentro del polígono (o buffer)
-  Los atributos se mantienen sin cambios
-  **Ubicación de salida**: Aparece como hipervínculo en el panel; haz clic para abrir la carpeta en el Explorador de Windows
-  Mensajes de progreso en el área de estado


**Consejos y mejores prácticas:**

-  **Selección de polígono**: Asegúrate de que el polígono esté visible y seleccionado en el mapa

- **Permisos**: Verifica que tengas permisos de escritura en la carpeta de salida

- **Espacio en disco**: Asegúrate de tener suficiente espacio para la geodatabase de salida (puede ser grande)

- **Rendimiento**: Con muchas features, el proceso puede tomar tiempo. No cierres ArcGIS Pro durante la ejecución

- **Buffer**: Úsalo cuando necesites datos con un margen alrededor de tu área de interés

- **Múltiples cortes**: Puedes ejecutar varios cortes seguidos en la misma carpeta (se crearán GDB diferentes)

**Manejo de errores:**

| Problema | Solución |
|----------|----------|
| "Sin selección / Selección múltiple" | Selecciona exactamente 1 polígono en el mapa |
| "Carpeta no existe" | Verifica la ruta de salida |
| "Permiso denegado" | Comprueba permisos de escritura en la carpeta |
| "GDB no encontrada" | Verifica que la ruta al Feature Dataset sea correcta |
| Proceso muy lento | Es normal con features grandes; espera o divide el trabajo |
| Resultado vacío | Es posible que no haya features dentro del polígono |

**Después del corte:**

1. Abre la carpeta de salida (clic en el hipervínculo)
2. Localiza la GDB creada: `Clip_YYYYMMDD_HHmmss.gdb`
3. Puedes agregar la GDB al mapa de ArcGIS Pro
4. Los datos están listos para compartir, analizar o procesar

### 6. Generador de Hash SHA256 (Verificación de Integridad)

Esta herramienta permite generar y verificar hashes SHA256 de archivos y geodatabases para garantizar su integridad durante el almacenamiento y transferencia.

**¿Por qué es importante?**
- Verifica que los archivos no hayan sido alterados o corrompidos
- Garantiza integridad de datos en trasferencias entre equipos
- Audita los cambios en archivos críticos
- Valida descargas o copias de seguridad

#### Generar Hash

**Funcionalidad 1: Comprimir GDB y Generar Hash**

Permite comprimir una Geodatabase en ZIP y generar su hash SHA256.

**Pasos:**
1. En la pestaña **EAAB Add-in**, haz clic en **" Generar Hash"** (ícono de candado/verificación)
2. Se abrirá un panel con la pestaña **"Generar Hash"**
3. Selecciona la opción **"Comprimir GDB y Generar Hash"** en el combo
4. Haz clic en **"Examinar..."** y selecciona la carpeta o GDB que deseas comprimir
5. Haz clic en **"Generar Hash"**

**Resultado:**
- Se crea un archivo ZIP: `nombreGDB_YYYYMMDDHHMMSS.zip`
- Se crea un archivo de texto: `nombreGDB_YYYYMMDDHHMMSS_HASH.txt` con:
  ```
  Archivo: nombreGDB_20251201143045.zip
  SHA256: a1b2c3d4e5f6g7h8i9j0...
  Fecha: 2025-12-01 14:30:45
  Tamaño: 125.5 MB
  ```
- Ambos archivos se guardan en la misma carpeta del GDB original
- El hash se muestra en el panel para verificación rápida

**Casos de uso:**
- Realizar respaldo de GDB con verificación de integridad
- Preparar GDB para compartir de forma segura
- Documentar estado de una GDB en una fecha específica

**Funcionalidad 2: Generar Hash de Archivos en Carpeta**

Calcula SHA256 de todos los archivos en una carpeta (sin incluir subcarpetas).

**Pasos:**
1. En la pestaña **"Generar Hash"**, selecciona **"Generar Hash de Archivos en Carpeta"**
2. Haz clic en **"Examinar..."** y selecciona la carpeta
3. Haz clic en **"Generar Hash"**

**Resultado:**
- Se crea un archivo resumen: `carpeta_YYYYMMDDHHMMSS_HASH.txt` con:
  ```
  Carpeta: C:\ruta\a\carpeta
  Fecha: 2025-12-01 14:30:45
  Total archivos: 12
  
  archivo1.shp      | SHA256: a1b2c3d4e5f6...
  archivo2.dbf      | SHA256: b2c3d4e5f6g7...
  archivo3.xlsx     | SHA256: c3d4e5f6g7h8...
  (12 archivos en total)
  ```
- Cada archivo se lista con su hash individual
- Facilita auditoría de cambios en múltiples archivos

**Casos de uso:**
- Verificar integridad de un shapefile completo (todos sus componentes)
- Documentar estado de una carpeta de proyecto
- Validar que un delivery de archivos está completo

#### Verificar Integridad

**Funcionalidad 1: Verificar Integridad de Archivo**

Comprueba que un archivo no haya sido modificado comparando su hash actual con el esperado.

**Pasos:**
1. En la pestaña **"Verificar Hash"**
2. Haz clic en **"Examinar..."** junto a "Archivo a Verificar"
3. Selecciona el archivo (ZIP, SHP, etc.)
4. El sistema buscará automáticamente el archivo HASH asociado
   - Busca patrones: `nombrearchivo_HASH.txt` o `nombrearchivo_[timestamp]_HASH.txt`
   - Si lo encuentra, lo carga automáticamente
5. Si no lo encuentra, puedes:
   - Haz clic en **"Examinar..."** junto a "Archivo Hash" y selecciónalo manualmente
   - O proporciona el hash manualmente en el campo de texto
6. Haz clic en **"VERIFICAR INTEGRIDAD"**

**Resultado - Si es válido:**
```
✅ INTEGRIDAD VERIFICADA
   Archivo: archivo.zip
   HASH esperado: a1b2c3d4e5f6g7h8...
   HASH actual:   a1b2c3d4e5f6g7h8...
   
   ✅ Los hashes coinciden - Archivo íntegro
   Fecha verificación: 2025-12-01 14:35:10
```

**Resultado - Si está corrupto:**
```
❌ INTEGRIDAD COMPROMETIDA
   Archivo: archivo.zip
   HASH esperado: a1b2c3d4e5f6g7h8...
   HASH actual:   x9y8z7w6v5u4t3s2...
   
   ❌ Los hashes NO coinciden - Archivo modificado o corrupto
   Diferencia: detectada
```

**Casos de uso:**
- Verificar que una descarga no fue corrompida
- Validar que un respaldo está íntegro
- Confirmar que un archivo no ha sido modificado
- Auditoría de cambios

**Interfaz del Generador de Hash:**

El panel se divide en dos pestañas:

| Sección | Funciones |
|---------|----------|
| **Generar Hash** | Crear nuevos hashes |
|  - Combo de función | Seleccionar entre comprimir GDB o generar de carpeta |
|  - Examinar | Seleccionar archivo/carpeta de origen |
|  - Generar Hash | Ejecutar generación |
| **Verificar Hash** | Validar integridad |
|  - Archivo a verificar | Seleccionar archivo |
|  - Archivo HASH | Carga automática o manual |
|  - Verificar integridad | Ejecutar verificación |
| **Resultados** | Área de scroll con:
|  | - Detalles de operación |
|  | - Hashes generados/verificados |
|  | - Rutas de archivos |
|  | - Mensajes de éxito/error |

**Validaciones implementadas:**

| Validación | Comportamiento |
|-----------|----------------|
| Carpeta no existe | ❌ Error con sugerencia |
| Carpeta vacía | ⚠️ Advertencia (sin archivos) |
| Archivo no existe | ❌ Error |
| No hay archivo HASH | ⚠️ Busca automáticamente o permite entrada manual |
| HASH con formato incorrecto | ❌ Error: no se puede parsear |
| Hashes no coinciden | ❌ Alerta: integridad comprometida |

**Consejos y mejores prácticas:**

- **Guardar hashes**: Mantén los archivos `*_HASH.txt` en lugar seguro

-  **Documentación**: Anota la fecha y propósito de cada verificación

-  **Transferencias**: Siempre verifica después de copiar archivos entre equipos o redes

-  **Respaldos**: Incluye un hash con cada respaldo para validar restauración

-  **Automatización**: Puedes programar generación de hashes regularmente

- **Comparación manual**: Puedes comparar hashes directamente sin usar la herramienta

**Interpretación del formato SHA256:**

- **Longitud**: Siempre 64 caracteres hexadecimales (0-9, a-f)
- **Sensibilidad**: Cualquier cambio en el archivo produce un hash completamente diferente
- **Unicidad**: Es prácticamente imposible encontrar dos archivos diferentes con el mismo SHA256
- **Ejemplo**: `a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6q7r8s9t0u1v2w3x4y5z6a7b8c9d0e1f2`

 

### 7. Cambiar la Configuración de Conexión

Si necesitas cambiar de base de datos o actualizar tus credenciales:

1. Ve a **Archivo → Opciones → EAAB Add-In**
2. Modifica los datos que necesites cambiar
3. Haz clic en **"Probar Conexión"** para verificar
4. Haz clic en **"Guardar y Conectar"**

Los cambios se guardan automáticamente mientras editas los campos.

### 8. Exportar Resultados

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

### 9. Buenas Prácticas de Uso

- Revisa que tu Excel no tenga filas totalmente vacías al final.
- Evita caracteres especiales innecesarios (ej: múltiples espacios, tabs).
- Prefiere códigos de ciudad oficiales si el sistema lo requiere (ver previsualización en panel masivo).
- No lances procesos masivos mientras ArcGIS Pro ejecuta otras ediciones complejas.
- Guarda el proyecto antes de una geocodificación masiva grande.

### 10. Interpretación de la Calidad (Score / Etiquetas)

La columna `ScoreText` sintetiza la procedencia/calidad:
- `Exacta`: Coincidencia directa registrada en EAAB.
- `Aproximada por Catastro`: Ajustada usando datos catastrales.
- `ESRI <valor>`: Resultado proveniente del servicio ESRI con score numérico.
- Otros valores pueden representar transformaciones adicionales.

## Solución de Problemas Comunes de Migración

### La migración se detiene con mensaje de advertencias

**Causa:**
- El sistema detectó features sin campo CLASE o con valores inesperados
- Features que no tienen una clase destino asignada

**Solución:**
1. Revisa los archivos CSV generados en la carpeta de reportes
2. Corrige los datos de origen si es posible
3. O marca el checkbox ☑ "Migrar con Advertencias" para continuar ignorando estas features

### Error: "Editing in the application is not enabled"

**Causa:**
- La edición no está habilitada en ArcGIS Pro

**Solución:**
1. Ve a **Proyecto → Opciones → Edición**
2. Marca **"Habilitar edición"**
3. Reinicia la migración

### Features no se migran correctamente

**Causas posibles:**
- Geometría nula o vacía en origen
- Incompatibilidad de sistemas de coordenadas
- Diferencias en dimensiones Z/M

**Solución:**
- Verifica que las features tengan geometría válida
- El sistema intentará proyectar automáticamente al SR del mapa
- Revisa el reporte CSV para ver qué features fallaron y por qué

### Las capas migradas no aparecen en el mapa

**Causa:**
- Las feature classes están vacías (todas las features fueron rechazadas)

**Solución:**
- Revisa los reportes CSV de migración
- Verifica que los datos de origen tengan el campo CLASE con valores válidos

### No se puede abrir la capa de origen

**Causas posibles:**
- Ruta incorrecta al archivo
- Formato no soportado
- Feature class dentro de un feature dataset

**Solución:**
- Verifica la ruta completa del archivo
- Asegúrate de usar Shapefile (.shp) o Feature Class de GDB (.gdb)
- El sistema buscará automáticamente en feature datasets si es necesario

### La geodatabase de destino ya existe

**Comportamiento:**
- El sistema reutiliza la GDB existente si ya existe con el mismo nombre
- Esto permite ejecutar migraciones incrementales

**Nota:**
- Si deseas empezar desde cero, renombra o elimina la GDB existente antes de ejecutar

### Errores de truncamiento de texto

**Causa:**
- Valores de texto en origen más largos que el límite del campo destino

**Solución:**
- El sistema trunca automáticamente y registra una advertencia
- Revisa el output de debug para ver qué campos fueron truncados
- Considera ajustar el esquema XML si es necesario

## Solución de Problemas Comunes Generales

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

### La migración es muy lenta

**Causas:**
- Gran volumen de datos (más de 50,000 features)
- Proyecciones complejas entre sistemas de coordenadas

**Solución:**
- Divide los datos de origen en lotes más pequeños
- Ejecuta migraciones por separado (primero líneas, luego puntos)
- Cierra otras aplicaciones que puedan consumir recursos

## Preguntas Frecuentes (FAQ)

### Generales

**¿Necesito conexión a Internet?**  
Solo para servicios ESRI complementarios; la base principal usa red corporativa.

**¿Se sobrescriben los puntos anteriores?**  
No, la capa acumula resultados hasta que la limpies manualmente.

**¿Puedo cancelar una ejecución masiva?**  
Versión actual: no. Recomendado dividir archivos grandes (>10 mil filas).

**¿Qué formato de coordenadas usa?**  
WGS84 (EPSG:4326) para puntos internos; el mapa reproyecta según tu vista.

**¿Puedo usar CSV en vez de Excel para geocodificación masiva?**  
No en esta versión (solo `.xlsx` / `.xls`).

### Sobre Corte (Clip)

**¿Qué pasa si no selecciono ningún polígono?**
La herramienta mostrará un error indicando "Sin selección". Debes seleccionar exactamente 1 polígono en el mapa.

**¿Puedo recortar múltiples Feature Datasets a la vez?**
No directamente. Si tienes múltiples Feature Datasets, puedes:
- Ejecutar el clip varias veces (una por cada FDS)
- Usar la misma carpeta de salida (se crearán GDB diferentes con timestamps)

**¿El buffer crea anillos o expande la geometría?**
Expande la geometría en todas direcciones. Un buffer de 100m creará un polígono 100m más grande alrededor del original.

**¿Se preservan los atributos en las features recortadas?**
Sí, todos los atributos se copian exactamente igual. Solo cambia la geometría (se recorta).

**¿Cuánto espacio en disco necesito?**
Mínimo: tamaño de las features a recortar + 20% de margen. La nueva GDB puede ser bastante grande dependiendo del volumen.

**¿Puedo cancelar un proceso de clip en curso?**
No se recomienda. Espera a que termine. Si necesitas cancelar, cierra ArcGIS Pro (no recomendado).

### Sobre Hash

**¿Qué es un hash SHA256?**
Es una función criptográfica que genera un código de 64 caracteres único para un archivo. Si el archivo cambia aunque sea 1 bit, el hash cambia completamente.

**¿Puedo reutilizar un hash antiguo para verificar?**
Sí, siempre que el archivo original no haya sido modificado. El hash es válido indefinidamente para el mismo archivo.

**¿Dónde guardo los archivos _HASH.txt?**
En la misma carpeta que el archivo original, o en un lugar seguro separado. La herramienta busca automáticamente en la carpeta del archivo.

**¿Dos geodatabases con los mismos datos tienen el mismo hash?**
No necesariamente. Aunque contengan los mismos datos, si fueron creadas en momentos diferentes o con herramientas diferentes, su hash será diferente.

**¿Es seguro compartir el archivo HASH?**
Sí, perfectamente. El hash es público y no contiene información sensible. Solo asegúrate de que el archivo _HASH.txt no se modifique.

**¿Puedo generar hash de archivos muy grandes?**
Sí, pero tomará más tiempo. Archivos de varios GB pueden tomar minutos. La herramienta mostrará progreso.

**¿Qué pasa si pierdo el archivo _HASH.txt?**
Puedes regenerarlo en cualquier momento con la herramienta. Simplemente vuelve a ejecutar "Generar Hash" sobre el archivo o carpeta.

**¿Por qué cambió el hash de mi GDB después de hacer backup?**
Al comprimir un GDB en ZIP, ciertos metadatos internos pueden variar. El contenido de datos es el mismo, pero la estructura de compresión es diferente.

### Sobre Migración

**¿La migración modifica mis datos originales?**  
No, la migración solo **lee** de las capas de origen. Crea una copia transformada en la nueva geodatabase sin tocar los archivos originales.

**¿Puedo migrar datos parcialmente?**  
Sí, puedes seleccionar solo las capas que necesites (por ejemplo, solo líneas de alcantarillado o solo puntos de acueducto).

**¿Qué pasa con las features que no tienen CLASE?**  
Se registran en el reporte CSV como "sin CLASE" y **no se migran** a la geodatabase destino.

**¿Puedo ejecutar la migración varias veces?**  
Sí, si la geodatabase destino ya existe, el sistema la reutiliza y agrega las nuevas features. Sin embargo, puede haber duplicados si migras los mismos datos varias veces.

**¿Se mantienen los ObjectID originales?**  
No, se generan nuevos ObjectID en la geodatabase destino según las reglas de ArcGIS.

**¿Qué sistemas de coordenadas soporta?**  
La migración soporta cualquier sistema de coordenadas. Si el SR de origen es diferente al del mapa activo, el sistema proyecta automáticamente las geometrías.

**¿Qué pasa con campos que no existen en el esquema destino?**  
Solo se migran campos que existen en el esquema XML destino. Los campos adicionales del origen se ignoran.

**¿Se pueden migrar datos de múltiples fuentes a la misma GDB?**  
Sí, pero asegúrate de usar la misma carpeta de salida y el mismo esquema XML para todas las ejecuciones.

**¿El proceso de migración genera respaldos?**  
No automáticamente. Se recomienda hacer respaldo manual de los datos de origen antes de cualquier proceso importante.

**¿Qué tan grandes pueden ser los archivos de origen?**  
No hay límite estricto, pero archivos con más de 100,000 features pueden tomar tiempo considerable. Considera dividirlos en lotes.

## Glosario Rápido

| Término | Definición |
|---------|------------|
| Geocodificar | Transformar una dirección en coordenadas espaciales |
| POI | Punto de Interés (edificio, institución, servicio) |
| SDE | Archivo de conexión a geodatabase corporativa |
| Score | Valor numérico de confianza del servicio (cuando aplica) |
| Exacta | Coincidencia directa en la base interna |
| Migración | Proceso de transformar y copiar datos de una estructura a otra |
| Geodatabase (GDB) | Base de datos geográfica de Esri para almacenar datos espaciales |
| Feature Class | Tabla de datos espaciales (puntos, líneas o polígonos) en una geodatabase |
| Feature Dataset | Contenedor para agrupar feature classes relacionadas |
| Sistema de Referencia (SR) | Sistema de coordenadas geográfico o proyectado |
| CLASE | Campo que identifica el tipo principal de elemento de red |
| SUBTIPO | Campo que identifica la variante específica dentro de una clase |
| SISTEMA | Campo que indica el tipo de red (0/2=Sanitario, 1=Pluvial) |
| Esquema XML | Archivo que define la estructura de una geodatabase |
| Proyección | Transformación de geometrías entre diferentes sistemas de coordenadas |
| Z/M | Dimensiones adicionales de geometría (Z=elevación, M=medida lineal) |
| Clip/Corte | Extracción de features dentro de un área de interés usando un polígono |
| Buffer | Área expandida alrededor de una geometría (en metros o unidades) |
| Hash SHA256 | Código criptográfico de 64 caracteres que identifica un archivo |
| Integridad | Verificación de que un archivo no ha sido modificado o corrupto |
| ZIP | Formato de compresión de archivos |
| Timestamp | Marca de fecha y hora en formato YYYYMMDDHHMMSS |

## Información de Versión

**Versión**: 1.3  
**Última actualización**: 1 de diciembre de 2025  
**Compatible con**: ArcGIS Pro 3.4 o superior

**Novedades de la versión 1.3:**
- ✨ **Nueva herramienta de Generador de Hash SHA256** para verificación de integridad
  - Compresión de GDB y generación de hash automática
  - Generación de hashes para múltiples archivos en carpeta
  - Verificación de integridad con búsqueda automática de archivos HASH
- ✨ **Mejora de herramienta Clip/Corte**
  - Documentación detallada de workflow
  - Manejo mejorado de errores y validaciones
  - Soporte para buffer configurable (redondeado/plano)
  - Interfaz más clara con hipervínculo a carpeta de salida
- 📚 Documentación de usuario completamente actualizada
- 🐛 Correcciones menores en validaciones

**Novedades de la versión 1.2:**
- ✨ **Nueva herramienta de migración** de datos de acueducto y alcantarillado
- Validación automática de estructura de datos antes de migración
- Sistema de advertencias con opción de continuar bajo responsabilidad del usuario
- Proyección automática de geometrías entre sistemas de coordenadas
- Ajuste automático de dimensiones Z/M en geometrías
- Mapeo inteligente de atributos desde campos antiguos a nuevos
- Generación de reportes CSV detallados por clase migrada
- Agregado automático de capas al mapa con simbología predefinida
- Zoom automático al extent de datos migrados
- Soporte para reutilización de geodatabases existentes

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