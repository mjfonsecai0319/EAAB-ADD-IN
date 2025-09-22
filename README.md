# EAAB-AddIn para ArcGIS Pro

Un Add-In para ArcGIS Pro que facilita la **búsqueda de direcciones**, la **geocodificación masiva** y la **conexión a bases de datos corporativas** del Acueducto.  
Optimiza la gestión de datos espaciales, permitiendo integrar información directamente desde PostgreSQL, Oracle o SQL Server al entorno de ArcGIS Pro.

> [!note]
> Este Add-In no reemplaza la funcionalidad nativa de ArcGIS Pro, sino que la complementa con herramientas específicas para la EAAB.

---

## Requisitos del sistema

### Requisitos obligatorios

* ArcGIS Pro 3.0 o superior ejecutándose correctamente
* .NET 8 Runtime (normalmente incluido con ArcGIS Pro)
* Conexión a la base de datos corporativa (PostgreSQL, Oracle o SQL Server)

### Requisitos de base de datos

* PostgreSQL con PostGIS habilitado, Oracle o SQL Server
* Usuario con permisos de lectura y escritura
* Puerto habilitado para conexiones remotas (ej. PostgreSQL 5432, SQL Server 1433, Oracle 1521)

---

## Instalación

### Paso 1: Obtención del archivo (Add-In)

Recibirás el archivo de instalación con extensión `.esriAddInX` a través de uno de estos métodos:

* USB: Archivo físico en una unidad USB
* Correo electrónico: Archivo adjunto o enlace de descarga
* Red compartida: Carpeta de red compartida corporativa

Guarda el archivo **EAABAddIn.esriAddInX** en una ubicación accesible como:

* Escritorio
* Carpeta de Descargas
* Documentos

---

### Paso 2: Instalación del complemento

1. Cierra ArcGIS Pro si está ejecutándose
2. Navega hasta la ubicación del archivo `EAABAddIn.esriAddInX`
3. Haz doble clic en el archivo
4. Aparecerá el instalador de Add-In de ArcGIS Pro
5. Haz clic en "Instalar" y acepta los términos si se solicitan
6. Espera el mensaje de "Instalación completada"
7. Inicia ArcGIS Pro

---

## Configuración

### Configuración inicial obligatoria

Al utilizar el Add-In por primera vez, es necesario configurar la conexión a la base de datos:

1. Abre ArcGIS Pro
2. En la cinta de opciones, accede a la pestaña **EAAB-ADD-IN**
3. Haz clic en **Configurar Conexión**
4. Completa los parámetros:
   - Motor de base de datos (PostgreSQL, Oracle)
   - Servidor / Host
   - Puerto
   - Base de datos
   - Usuario
   - Contraseña
5. Haz clic en **Probar Conexión**

> [!warning]
> Si la conexión falla, revisa que los parámetros de red y las credenciales sean correctas antes de continuar.

---

## Cómo usar el Add-In EAAB

Una vez configurado correctamente, el Add-In ofrece tres herramientas principales accesibles desde la pestaña "EAAB" en la cinta de opciones de ArcGIS Pro.

---

### 1. Búsqueda de Direcciones

Propósito: Consultar y ubicar direcciones individuales en el mapa.

Pasos para usar:

1. En la pestaña **EAAB**, abre el panel **Búsqueda de Dirección**
2. Ingresa la dirección en el campo de texto
3. Presiona **Buscar**
4. Resultado:  
   - Dirección localizada en el mapa  
   - Información asociada desplegada en tabla

---

### 2. Geocodificación Masiva

Propósito: Procesar múltiples direcciones a partir de un archivo externo.

Pasos para usar:

1. En la pestaña **EAAB**, abre el panel **Geocodificación Masiva**
2. Carga un archivo en formato **CSV o Excel** con una columna de direcciones
3. Presiona **Procesar**
4. Resultado:  
   - Se genera una capa en el mapa con los puntos georreferenciados  
   - Los resultados se almacenan en una **feature class** dentro de la geodatabase de usuario

---

### 3. Validación de Conexión

Propósito: Confirmar la conexión activa con la base de datos.

Pasos para usar:

1. En la pestaña **EAAB**, haz clic en **Validar Conexión**
2. El sistema confirmará si la conexión es correcta o mostrará un error

---

## Solución de problemas

| Error | Causa probable | Solución |
|-------|----------------|----------|
| ❌ *“No se pudo conectar a la base de datos”* | Datos de conexión incorrectos o BD inactiva | Verificar credenciales y disponibilidad del servidor |
| ❌ *“El archivo CSV no contiene la columna Dirección”* | El archivo no tiene encabezados correctos | Revisar que el archivo tenga la columna `direccion` |
| ❌ *“El Add-In no aparece en ArcGIS Pro”* | El archivo no se instaló en la ruta correcta | Copiar el `.esriAddInX` en la carpeta de complementos de ArcGIS Pro |

---

## Desarrollo

Si deseas contribuir al desarrollo del Add-In, sigue estos pasos:

### Configuración del entorno de desarrollo

1. Clona el repositorio
2. Abre la solución en Visual Studio (2022 o superior recomendado)
3. Instala las dependencias NuGet requeridas
4. Configura la ruta de salida para apuntar a la carpeta de Add-Ins de ArcGIS Pro

---



