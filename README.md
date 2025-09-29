# EAAB-AddIn para ArcGIS Pro

El Add-In para ArcGIS Pro que facilita la **búsqueda de direcciones**, la **geocodificación masiva** y la **conexión a bases de datos corporativas**.  
Optimiza la gestión de datos espaciales, permitiendo integrar información directamente desde PostgreSQL u Oracle al entorno de ArcGIS Pro con persistencia de configuración automática.

## Características principales

1. **Conexión persistente** a bases de datos PostgreSQL y Oracle (modo credenciales) 
2. **Conexión mediante archivo .sde** para Oracle SDE y PostgreSQL SDE (sin exponer usuario/host)  
3. **Búsqueda individual** de direcciones con localización inmediata en el mapa  
4. **Geocodificación masiva** optimizada (batch insert en memoria) desde archivos Excel (.xlsx)  
5. **Registro automático de auditoría** con sello de fecha y hora local (campo `FechaHora`) tanto para encontrados como no encontrados  
6. **Clasificación unificada de origen** (EAAB / CATASTRO / ESRI) y calidad (Exacta / Aproximada / Score numérico)  
7. **Dirección ingresada vs dirección encontrada**: En geocodificación manual se preserva exactamente la dirección escrita por el usuario; en masivo se guarda la mejor dirección normalizada encontrada  
8. **Persistencia y recarga de configuración automática** entre sesiones  
9. **Interfaz adaptable** a tema claro/oscuro de ArcGIS Pro usando DynamicResource  
10. **Validación de conexión en tiempo real** y mensajes de estado descriptivos  
11. **Fallback inteligente de normalización**: Si el normalizador falla por léxico (CODE_145 / CODE_146) se usa la dirección original sin abortar  
12. **Soporte de búsqueda ampliada**: Coincidencia exacta + segundo intento LIKE si no hay resultados

## Requisitos del sistema

### Requisitos obligatorios

1. ArcGIS Pro 3.4 o superior ejecutándose correctamente
2. .NET 8 Runtime (normalmente incluido con ArcGIS Pro)
3. Conexión a la base de datos corporativa (PostgreSQL u Oracle)

> [!tip]
> Consultar requisitos adicionales en [Requisitos del sistema de ArcGIS Pro 3.5](https://pro.arcgis.com/es/pro-app/latest/get-started/arcgis-pro-system-requirements.htm)

### Requisitos de base de datos

1. PostgreSQL 15+ con PostGIS habilitado, o Oracle 18+  
2. Opcional: Archivos `.sde` válidos generados desde ArcGIS Pro / ArcCatalog para conexión SDE (Oracle o PostgreSQL)

## Instalación

![Video de Instalación](<docs/res/Grabación 2025-09-23 083552.gif>)

### Paso 1: Obtención del archivo (Add-In)

Recibirás el archivo de instalación con extensión `.esriAddInX` a través de uno de estos métodos:

* USB: Archivo físico en una unidad USB
* Correo electrónico: Archivo adjunto o enlace de descarga
* Red compartida: Carpeta de red compartida corporativa

Guarda el archivo **EAABAddIn.esriAddInX** en una ubicación accesible como:

* Escritorio
* Carpeta de Descargas
* Documentos

### Paso 2: Instalación del complemento

1. Cierra ArcGIS Pro si está ejecutándose
2. Navega hasta la ubicación del archivo `EAABAddIn.esriAddInX`
3. Haz doble clic en el archivo
4. Aparecerá el instalador de Add-In de ArcGIS Pro
5. Haz clic en "Instalar" y acepta los términos si se solicitan
6. Espera el mensaje de "Instalación completada"
7. Inicia ArcGIS Pro

## Configuración

### Configuración inicial automática

Al utilizar el Add-In por primera vez:

1. **Si no hay configuración previa**: Los campos aparecerán vacíos para configuración manual
2. **Si hay configuración guardada**: Los campos se llenarán automáticamente con los valores previamente guardados

#### Configuración manual paso a paso

1. Abre ArcGIS Pro
2. Ve a **Archivo → Opciones → EAAB Add-In** (en el panel lateral izquierdo)
3. Haz clic en **Probar Conexión** - debe mostrar *"Conexión exitosa"*
4. Haz clic en **Guardar y Conectar** para establecer la conexión permanente

#### Completa los parámetros según tu motor de base de datos

##### Para PostgreSQL (credenciales)

* Motor: PostgreSQL
* Host: `localhost` o dirección IP del servidor
* Puerto: `5432` (se establece automáticamente)
* Base de datos: Nombre de la base de datos PostgreSQL
* Usuario: Tu nombre de usuario
* Contraseña: Tu contraseña

##### Para Oracle (credenciales)

* Motor: Oracle
* Host: `localhost` o dirección IP del servidor
* Puerto: `1521` (se establece automáticamente)
* Base de datos: SID o nombre del servicio Oracle
* Usuario: Tu nombre de usuario
* Contraseña: Tu contraseña

##### Para PostgreSQL SDE / Oracle SDE (archivo .sde)

* Selecciona el motor: "PostgreSQL SDE" u "Oracle SDE"  
* El formulario ocultará campos de Host/Usuario/Contraseña/Base y mostrará el selector de archivo  
* Navega y selecciona el archivo `.sde`  
* Haz clic en "Probar Conexión" y luego "Guardar y Conectar"  

> [!note]
> El archivo .sde encapsula los parámetros de conexión, por lo que no se almacenan credenciales explícitas en la configuración del Add-In.

### Cambio de motor de base de datos

Al cambiar de motor (credenciales ↔ SDE o entre motores):

* Se limpian campos no aplicables automáticamente  
* Se asigna el puerto por defecto cuando aplica (5432 / 1521)  
* Para modos SDE solo se persiste la ruta del archivo  

> [!warning]
> Si la conexión falla, revisa que los parámetros de red y las credenciales sean correctas antes de continuar.

## Cómo usar el Add-In EAAB

Una vez configurado correctamente, el Add-In ofrece herramientas principales accesibles desde la pestaña **"EAAB Add-in"** en la cinta de opciones de ArcGIS Pro.

### 1. Búsqueda Individual de Direcciones

**Propósito**: Consultar y ubicar direcciones específicas en el mapa con información detallada.

**Acceso**: Botón **"Buscar"** en la pestaña EAAB Add-in

#### Pasos para usar

1. Haz clic en **"Buscar"** para abrir el panel de búsqueda
2. **Selecciona la ciudad** desde el dropdown (se cargan automáticamente desde la base de datos)
3. **Ingresa la dirección** en el campo de texto (ej: "Calle 123 #45-67")
4. Haz clic en **"Buscar Dirección"**

> [!tip]
> Si la lista de ciudades aparece vacía o incompleta (por ejemplo tras cambiar de motor o reconectar), usa el botón **"Recargar" / ícono de refresco** del panel para volver a consultar el catálogo directamente desde la base de datos.

#### Funcionalidades del panel de búsqueda

* **Estado de conexión**: Indica si la base de datos está conectada
* **Botón de refrescar**: Actualiza la lista de ciudades disponibles
* **Validación en tiempo real**: Los campos se validan antes de permitir la búsqueda
* **Barra de progreso**: Muestra el estado del proceso de búsqueda

#### Resultados esperados

* **Localización en mapa** y zoom automático a los resultados
* **Auditoría en capa** `GeocodedAddresses`: Campos principales:
  * Identificador
  * Direccion (exactamente lo que escribió el usuario en búsqueda individual)
  * FullAdressEAAB / FullAdressUACD (cuando existan)
  * Geocoder (EAAB / CATASTRO / ESRI)
  * Score (numérico, solo ESRI)
  * ScoreText (Exacta / Aproximada por Catastro / ESRI n.n)
  * FechaHora (hora local)
* **Tabla de no encontrados** (cuando se implementa): registro de direcciones sin resultado con timestamp

### 2. Geocodificación Masiva

**Propósito**: Procesar múltiples direcciones simultáneamente desde archivos Excel.

**Acceso**: Botón **"Masivo"** en la pestaña EAAB Add-in

#### Formato requerido del archivo Excel

El archivo debe contener **obligatoriamente** estas columnas:

| Identificador | Direccion | Poblacion |
|--------------|-----------|-----------|
| 001 | Calle 123 #45-67 | Bogotá |

* **Identificador**: ID único de cada registro (texto o número)
* **Direccion**: Dirección completa a geocodificar
* **Poblacion**: Ciudad o población donde se encuentra la dirección

#### Pasos para usar

1. Haz clic en **"Masivo"** para abrir el panel de geocodificación masiva
2. Haz clic en **"Examinar..."** para seleccionar tu archivo Excel (.xlsx)
3. **El sistema validará automáticamente**:
   - Formato del archivo
   - Presencia de columnas requeridas
   - Integridad de los datos
4. Si la validación es exitosa, haz clic en **"Procesar Archivo"**
5. **Monitorea el progreso**:
   - Barra de progreso indeterminada durante el procesamiento
   - Mensaje de estado con información del proceso

#### Resultados del procesamiento:

* Inserción optimizada en lote (menor tiempo y menos locks)
* La columna `Direccion` almacena la mejor dirección encontrada (prioridad: EAAB > Catastro > Original > Calle básica)
* Campos de clasificación y timestamp idénticos a la búsqueda individual
* **Contador final:** Encontradas / No encontradas / Total
* Posibilidad de reintentos ampliando coincidencias por LIKE en la segunda pasada

---

### 3. Configuración de Conexión

**Propósito**: Configurar, validar y gestionar la conexión a la base de datos corporativa.

**Acceso**: **Archivo → Opciones → Database**

#### Estado de la conexión:

Estados posibles (credenciales y SDE):

* ✅ Conexión activa
* ⚠️ Configuración válida pero no conectado
* ❌ Configure los parámetros de conexión

#### Funciones disponibles:

1. **Probar Conexión**: Valida los parámetros sin guardar cambios
2. **Guardar y Conectar**: Guarda la configuración y establece conexión permanente
3. **Cambio automático de puerto**: Al cambiar el motor de BD, se actualiza el puerto predeterminado

#### Persistencia de configuración:

- **Guardado automático**: Los cambios se guardan al modificar cualquier campo
- **Carga automática**: Al abrir la configuración, se cargan los valores previamente guardados
- **Configuración por motor**: Cada motor de BD mantiene su configuración independiente

---

## Arquitectura técnica

### Componentes principales

1. `Module1.cs`: Inicialización, reconexión diferida y soporte multi-motor (incluye SDE)
2. `DatabaseConnectionService`: Abstracción de conexión (credenciales o archivo .sde) con liberación controlada
3. `ConnectionPropertiesFactory`: Factory para propiedades Oracle / PostgreSQL
4. `ResultsLayerService`: Gestión de Feature Class `GeocodedAddresses` (creación, campos, batch insert)
5. `AddressNotFoundTableService`: Registro de intentos fallidos con timestamp
6. `PtAddressGralEntityRepository` (Oracle / Postgres): Búsqueda de direcciones y catálogo de ciudades
7. `AddressNormalizer` y fallback de errores de léxico
8. ViewModels (AddressSearch / Massive / PropertyPage): Orquestación MVVM

### Patrones implementados

* MVVM (WPF + ArcGIS Pro SDK)
* Factory (conexiones)
* Singleton controlado (`Module1` + settings cache)
* Command (RelayCommand / AsyncRelayCommand)
* Fallback Strategy (normalización / búsqueda LIKE)
* Lazy initialization (carga diferida de capa y campos)

### Almacenamiento de configuración

Ubicaciones:

1. JSON: `%AppData%\EAABAddIn\settings.json`
2. ApplicationSettings: respaldo

Campos guardados por motor (independientes): host, puerto, base, usuario, archivo .sde, etc.

---

## Solución de problemas

### Problemas de conexión

| Error | Causa probable | Solución |
|-------|----------------|----------|
| ❌ "Conflicting connection parameters" | Formato incorrecto de parámetros de conexión | Verificar formato: PostgreSQL usa `host:puerto`, Oracle usa `host:puerto/database` |
| ❌ "Error de conexión: authentication failed" | Credenciales incorrectas | Verificar usuario y contraseña en la base de datos |
| ❌ "No se pudo conectar a la base de datos" | Servidor inaccesible o puerto cerrado | Verificar conectividad de red y configuración del firewall |
| ❌ "Database does not exist" | Nombre de base de datos incorrecto | Confirmar el nombre exacto de la base de datos |

### Problemas de geocodificación

| Error | Causa probable | Solución |
|-------|----------------|----------|
| ❌ "El archivo no contiene la columna requerida" | Encabezados incorrectos en Excel | Verificar que las columnas sean: `Identificador`, `Direccion`, `Poblacion` |
| ❌ "No se pudo leer el archivo" | Archivo corrupto o formato incorrecto | Usar Excel (.xlsx) y verificar que no esté protegido |
| ⚠️ "Sin coincidencias" | Primera pasada exacta falló | Se ejecuta fallback LIKE automáticamente |
| ❌ "No hay ciudades disponibles" | Sin conexión a base de datos | Verificar conexión antes de usar herramientas |

### Problemas de instalación

| Error | Causa probable | Solución |
|-------|----------------|----------|
| ❌ "El Add-In no aparece en ArcGIS Pro" | Instalación incompleta | Reinstalar el `.esriAddInX` con ArcGIS Pro cerrado |
| ❌ "Error al cargar el complemento" | Versión incompatible de ArcGIS Pro | Verificar que ArcGIS Pro sea 3.0 o superior |
| ❌ "Faltan dependencias .NET" | Runtime .NET no instalado | Instalar .NET 8 Runtime desde Microsoft |

---

## Logs y diagnóstico

### Ubicación de logs:

Los logs de diagnóstico se escriben en la ventana de **Output** de Visual Studio cuando se ejecuta en modo desarrollo, o en el **Event Viewer** de Windows en producción.

### Información de diagnóstico útil

Se registran (Output VS / Debug):

* Intento de conexión y motor seleccionado
* Ruta de archivo .sde (cuando aplica)
* Código de error de normalización (cuando se produce fallback)
* Número de resultados por búsqueda
* Errores de inserción en Feature Class / tabla auxiliar

---

## Desarrollo y contribución

Si deseas contribuir al desarrollo del Add-In:

### Configuración del entorno de desarrollo:

1. **Clonar repositorio**: `git clone [repository-url]`
2. **Visual Studio 2022** o superior con ArcGIS Pro SDK
3. **ArcGIS Pro 3.0+** instalado para pruebas
4. **Configurar rutas de salida** hacia la carpeta de Add-Ins de ArcGIS Pro

### Estructura del proyecto:

```
EAABAddIn/
├── Src/
│   ├── Application/          # Lógica de aplicación
│   ├── Core/                 # Servicios core y configuración
│   │   ├── Config/          # Sistema de configuración
│   │   └── Data/            # Servicios de datos y conexión
│   └── Presentation/        # UI y ViewModels
│       ├── Converters/      # Converters XAML
│       ├── View/            # Vistas XAML
│       └── ViewModel/       # ViewModels MVVM
├── Images/                  # Recursos gráficos
└── Config.daml             # Configuración del Add-In
```

### Compilación y despliegue:

1. **Build Release**: Genera el archivo `.esriAddInX`
2. **Testing**: Instalar en ArcGIS Pro de desarrollo
3. **Distribución**: Compartir el `.esriAddInX` para instalación

---

## Licencia y soporte

Este Add-In es de uso interno de la EAAB. Para soporte técnico, contactar al equipo de desarrollo de sistemas de información geográfica.

**Versión**: 1.1  
**Última actualización**: 29-09-2025  
**Compatible con**: ArcGIS Pro 3.4+  
**Novedades 1.1**: Soporte PostgreSQL SDE, timestamps locales, clasificación origen/score, fallback LIKE, auditoría no encontrados.