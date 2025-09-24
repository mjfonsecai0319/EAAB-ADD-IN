# EAAB-AddIn para ArcGIS Pro

El Add-In para ArcGIS Pro que facilita la **búsqueda de direcciones**, la **geocodificación masiva** y la **conexión a bases de datos corporativas**.  
Optimiza la gestión de datos espaciales, permitiendo integrar información directamente desde PostgreSQL u Oracle al entorno de ArcGIS Pro con persistencia de configuración automática.

## Características principales

1. **Conexión persistente** a bases de datos PostgreSQL y Oracle
2. **Búsqueda individual** de direcciones con localización en mapa
3. **Geocodificación masiva** desde archivos Excel (.xlsx)
4. **Configuración automática** que se mantiene entre sesiones
5. **Interfaz adaptable** al tema claro/oscuro de ArcGIS Pro
6. **Validación de conexión** en tiempo real

## Requisitos del sistema

### Requisitos obligatorios

1. ArcGIS Pro 3.4 o superior ejecutándose correctamente
2. .NET 8 Runtime (normalmente incluido con ArcGIS Pro)
3. Conexión a la base de datos corporativa (PostgreSQL u Oracle)

> [!tip]
> Consultar requisitos adicionales en [Requisitos del sistema de ArcGIS Pro 3.5](https://pro.arcgis.com/es/pro-app/latest/get-started/arcgis-pro-system-requirements.htm)

### Requisitos de base de datos

1. PostgreSQL 15+ con PostGIS habilitado, o Oracle 18+

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

##### Para PostgreSQL

* Motor: PostgreSQL
* Host: `localhost` o dirección IP del servidor
* Puerto: `5432` (se establece automáticamente)
* Base de datos: Nombre de la base de datos PostgreSQL
* Usuario: Tu nombre de usuario
* Contraseña: Tu contraseña

##### Para Oracle

* Motor: Oracle
* Host: `localhost` o dirección IP del servidor
* Puerto: `1521` (se establece automáticamente)
* Base de datos: SID o nombre del servicio Oracle
* Usuario: Tu nombre de usuario
* Contraseña: Tu contraseña
* Oracle Path: Ruta de instalación de Oracle (opcional)

### Cambio de motor de base de datos

Cuando cambies entre PostgreSQL y Oracle:

* **Todos los campos se limpiarán automáticamente**
* **Solo se conservará el puerto por defecto** del nuevo motor seleccionado
* **Deberás configurar nuevamente** todos los parámetros de conexión

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

#### Funcionalidades del panel de búsqueda

* **Estado de conexión**: Indica si la base de datos está conectada
* **Botón de refrescar**: Actualiza la lista de ciudades disponibles
* **Validación en tiempo real**: Los campos se validan antes de permitir la búsqueda
* **Barra de progreso**: Muestra el estado del proceso de búsqueda

#### Resultados esperados

* **Localización en mapa**: La dirección encontrada se centra y resalta en el mapa
* **Información detallada**: Se despliega información asociada como:
  * Coordenadas exactas
  * Código de dirección
  * Información catastral (si está disponible)
  * Barrio o localidad

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

- **Capa de puntos**: Se crea automáticamente una nueva capa en el mapa con todos los puntos geocodificados
- **Tabla de atributos**: Cada punto incluye:
  - Información original del Excel
  - Coordenadas calculadas por catastro/Esri
  - Estado de geocodificación (exitosa/fallida)
  - Precisión de la localización
- **Feature Class**: Los resultados se almacenan permanentemente en la geodatabase del proyecto

---

### 3. Configuración de Conexión

**Propósito**: Configurar, validar y gestionar la conexión a la base de datos corporativa.

**Acceso**: **Archivo → Opciones → Database**

#### Estado de la conexión:

El sistema muestra tres estados posibles:

- **✅ "Conexión activa"**: La base de datos está conectada y funcionando
- **⚠️ "Configuración válida pero no conectado"**: Los parámetros son correctos pero la conexión se perdió
- **❌ "Configure los parámetros de conexión"**: No hay configuración o es inválida

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

### Componentes principales:

1. **Module1.cs**: Gestor principal, inicialización y conexión automática
2. **DatabaseConnectionService**: Servicio de conexión persistente con pooling
3. **ConnectionPropertiesFactory**: Factory para crear propiedades de conexión específicas por motor
4. **Settings**: Sistema de configuración persistente en JSON
5. **ViewModels**: Lógica de presentación con binding bidireccional

### Patrones implementados:

- **MVVM (Model-View-ViewModel)**: Separación clara entre lógica y presentación
- **Factory Pattern**: Para crear conexiones específicas por tipo de BD
- **Singleton**: Para el servicio de conexión global
- **Command Pattern**: Para acciones de UI con validación

### Almacenamiento de configuración:

La configuración se guarda en dos ubicaciones para redundancia:

1. **JSON**: `%AppData%\EAABAddIn\settings.json`
2. **ApplicationSettings**: Configuración nativa de .NET

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

### Información de diagnóstico útil:

- Estado de conexión a base de datos
- Parámetros de conexión utilizados (sin contraseñas)
- Errores específicos de geocodificación
- Tiempo de respuesta de consultas

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

**Versión**: 1.0  
**Última actualización**: 23-09-2025  
**Compatible con**: ArcGIS Pro 3.4+