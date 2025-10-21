# MANUAL DEL USUARIO PARA EL USO DEL ADD-IN EAAB

| Fecha   | 21/10/2025 |
| ------- | ---------- |
| Version | 1.0        |

## Tabla de Contenido

1. [Introducción](#introducción)
2. [Objetivo](#objetivo)
3. [Capacidades](#capacidades)
4. [Instalación](#instalación)
5. [Uso](#uso)
6. [Problemas](#problemas)

## Introducción

Este manual ha sido creado para guiar al usuario en la instalación, configuración y uso del Add-In EAAB para ArcGIS Pro, desarrollado por la Dirección de Información Técnica y Geográfica de la Empresa de Acueducto y Alcantarillado de Bogotá - E.S.P. (EAAB E.S.P.).

El objetivo de esta guía es describir de forma clara y práctica las funcionalidades incluidas hasta la versión 2.0 y los pasos necesarios para integrarlas en los flujos de trabajo GIS de la organización. El Add-In facilita principalmente dos procesos clave: la geocodificación de direcciones (transformar direcciones en coordenadas geográficas) y la creación de cierres (generación y validación de polígonos de cierre para parcelas, redes u otras geometrías). Estas funciones están orientadas a automatizar tareas repetitivas, mejorar la calidad de los datos espaciales y reducir el tiempo requerido por los analistas.

Este documento está organizado en secciones que cubren: objetivos y capacidades del Add-In; requisitos e instalación; instrucciones paso a paso para el uso de las herramientas; ejemplos prácticos; y solución de problemas y preguntas frecuentes. Antes de usar el Add-In, se recomienda revisar la sección de requisitos para confirmar la versión compatible de ArcGIS Pro y los permisos necesarios. Para mayor claridad, cada procedimiento incluye capturas de pantalla, ejemplos de entrada/salida y notas sobre limitaciones conocidas.

## Objetivo

Proporcionar una herramienta de apoyo dentro de ArcGIS Pro que facilite a los trabajadores de la Dirección de Información Técnica y Geográfica la realización de procesos de geocodificación y generación de cierres espaciales. Para ello, permite automatizar la localización de direcciones, la creación de polígonos de áreas afectadas y la gestión estructurada de los resultados en las bases de datos corporativas, con el fin de optimizar el tiempo de análisis, mejorar la precisión de la información geográfica y estandarizar los procedimientos operativos dentro de la entidad.

## Capacidades

## Instalación

## Uso

Una vez configurado, verás una nueva pestaña llamada **"EAAB Add-in"** en la parte superior de ArcGIS Pro.

### FUNCIONALIDADES DE GEOCODIFICACIÓN

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

## FUNCIONALIDADES DE GESTIÓN DE CIERRES

### 6. Crear Nuevo Cierre

Esta herramienta permite generar polígonos de cierre automáticamente a partir de puntos seleccionados, agrupándolos por un identificador común.

#### ¿Cuándo usar esta función?
- Cuando tienes puntos dispersos que representan eventos o incidencias
- Necesitas agrupar puntos por un criterio común (ej: número de orden, código de proyecto)
- Quieres generar automáticamente áreas de cobertura o influencia

**Pasos:**

1. Haz clic en el botón **"Nuevo Cierre"** en la pestaña EAAB Add-in
2. Se abrirá un panel a la derecha con las siguientes opciones:

**Configuración básica:**
- **Workspace**: Selecciona la geodatabase donde se guardarán los polígonos generados
- **Feature Class de Puntos**: Selecciona la capa de puntos desde donde generarás los cierres
- **Campo Identificador**: Selecciona el campo que agrupa los puntos (ej: "ID_ORDEN", "CODIGO_CIERRE")

**Configuración opcional (enriquecimiento):**
- **Feature Class de Barrios**: (Opcional) Capa de polígonos de barrios para identificar qué barrios intersecta cada cierre
- **Feature Class de Clientes**: (Opcional) Capa de puntos de clientes para contar cuántos quedan dentro de cada cierre

3. Haz clic en **"Generar Polígonos"**

**Resultados:**

- Se crearán polígonos envolventes (convex hull) para cada grupo de puntos con el mismo identificador
- Los polígonos se guardarán en una nueva Feature Class en el workspace seleccionado
- Si configuraste barrios y clientes, cada polígono incluirá:
  - **BARRIOS**: Lista de barrios intersectados (separados por comas)
  - **CLIENTES**: Cantidad de clientes afectados dentro del polígono
- Se abrirá un resumen indicando cuántos polígonos se generaron

**Requisitos importantes:**
- Se necesitan **mínimo 3-4 puntos** por identificador para generar un polígono válido
- Los puntos deben tener el mismo valor en el campo identificador para agruparse
- La configuración permite ajustar si se aceptan triángulos (3 puntos) desde las opciones del AddIn

**Ejemplo de uso:**
Si tienes una capa de válvulas con el campo "ORDEN_CIERRE" y quieres generar el área de impacto:
- Selecciona la capa de válvulas
- Elige "ORDEN_CIERRE" como campo identificador
- Opcionalmente agrega barrios y clientes
- El sistema generará un polígono por cada orden de cierre

---

### 7. Calcular Área Afectada

Esta función permite actualizar polígonos existentes con información de barrios y clientes afectados, sin necesidad de regenerar los polígonos.

#### ¿Cuándo usar esta función?
- Ya tienes polígonos de cierre creados manualmente o por otro método
- Necesitas actualizar la información de barrios y clientes en polígonos existentes
- Quieres enriquecer polígonos con datos de intersección espacial

**Pasos:**

1. **Selecciona los polígonos** en el mapa que deseas actualizar
2. Haz clic en el botón **"Área Afectada"** en la pestaña EAAB Add-in
3. En el panel que se abre, configura:

**Configuración requerida:**
- **Workspace**: Geodatabase de trabajo
- **Feature Class de Polígonos**: La capa que contiene los polígonos seleccionados
- **Campo Identificador**: Campo que identifica cada polígono (para actualización)

**Datos de enriquecimiento:**
- **Feature Class de Barrios**: Capa de barrios (obligatoria)
- **Feature Class de Clientes**: Capa de clientes (obligatoria)

4. Haz clic en **"Calcular"**

**Resultados:**

- Los polígonos seleccionados se actualizarán con:
  - **BARRIOS**: Nombre de los barrios que intersectan con el polígono
  - **CLIENTES**: Cantidad de puntos de clientes dentro del polígono
- El panel mostrará cuántos polígonos se procesaron exitosamente
- Los cambios se guardan directamente en la Feature Class

**Contador de selección:**
El panel muestra en tiempo real cuántos polígonos tienes seleccionados, ayudándote a verificar antes de ejecutar el cálculo.

---

### 8. Unir Polígonos

Esta herramienta permite fusionar múltiples polígonos seleccionados en uno solo, combinando sus atributos de forma inteligente.

#### ¿Cuándo usar esta función?
- Necesitas consolidar varios cierres en un área de impacto única
- Quieres fusionar zonas adyacentes o superpuestas
- Requieres combinar atributos de múltiples polígonos (sumar clientes, unir barrios)

**Pasos:**

1. **Selecciona 2 o más polígonos** en el mapa que deseas unir
2. Haz clic en el botón **"Unir Polígonos"** en la pestaña EAAB Add-in
3. En el panel que se abre, configura:

**Configuración básica:**
- **Workspace**: Geodatabase donde se guardará el polígono unido
- **Feature Class de Origen**: La capa de donde provienen los polígonos seleccionados
- **Campo Identificador**: Campo que identificará al nuevo polígono unido
- **Valor del Identificador**: Texto o número para identificar el polígono resultante

**Enriquecimiento (opcional):**
- **Feature Class de Barrios**: Para calcular barrios del polígono unido
- **Feature Class de Clientes**: Para contar clientes en el polígono unido

4. Verifica el contador de polígonos seleccionados (mínimo 2)
5. Haz clic en **"Ejecutar Unión"**

**Resultados:**

- Se genera un **nuevo polígono** que representa la unión geométrica de todos los seleccionados
- Los atributos se combinan inteligentemente:
  - **Campos numéricos**: Se suman (ej: total de clientes)
  - **Campo identificador**: Usa el valor que especificaste
  - **BARRIOS**: Combina y lista todos los barrios únicos
  - **CLIENTES**: Suma total de clientes o recalcula según geometría
- El nuevo polígono se agrega automáticamente al mapa
- Los polígonos originales **no se eliminan**, permanecen intactos
- El mapa hace zoom automático al polígono generado

**Vista de selección en tiempo real:**
El panel muestra una lista de los polígonos seleccionados con su nombre de capa y OID, actualizándose automáticamente cuando cambias la selección.

**Gestión de conflictos:**
Si la Feature Class de salida no tiene permisos de escritura o ya existe el registro, el sistema intentará automáticamente:
- Usar la geodatabase por defecto del proyecto
- Generar un nombre único agregando sufijo numérico
- Informarte de la ubicación alternativa donde se guardó

---

## Problemas
