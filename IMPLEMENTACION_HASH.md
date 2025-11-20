# Funcionalidad: Generador de Hash SHA256

## 📋 Resumen de Implementación

Se ha implementado exitosamente la funcionalidad completa de **Generador de Hash SHA256** para el Add-In de ArcGIS Pro de la EAAB.

---

## ✅ Archivos Creados

### Servicios (`Src/Application/Services/`)
1. **HashService.cs** - Servicio para calcular y gestionar hashes SHA256
   - `CalcularSHA256()` - Calcula hash de un archivo
   - `CalcularSHA256Carpeta()` - Calcula hash de todos los archivos en una carpeta
   - `ExtraerHashDeArchivo()` - Extrae hash de archivo .txt
   - `GenerarNombreArchivoHash()` - Genera nombres con timestamp
   - `GenerarContenidoHashTxt()` - Genera contenido formateado
   - `BuscarArchivoHashEnCarpeta()` - Búsqueda automática de archivos hash
   - `CompararHashes()` - Compara dos hashes

2. **CompressionService.cs** - Servicio para comprimir carpetas
   - `ComprimirEnZip()` - Comprime carpeta en ZIP
   - `ObtenerArchivosCarpeta()` - Lista archivos (no recursivo)
   - `GenerarNombreConTimestamp()` - Genera nombres con formato AAAAMMDDHHMMSS
   - `EsGDB()` - Valida si es una Geodatabase
   - `FormatearTamaño()` - Formatea tamaños de archivo

### Casos de Uso (`Src/Application/UseCases/`)
3. **GenerarHashUseCase.cs** - Lógica de negocio con 3 funcionalidades:
   - `ComprimirGdbYGenerarHash()` - Funcionalidad 1.1
   - `GenerarHashArchivosEnCarpeta()` - Funcionalidad 1.2
   - `VerificarIntegridadArchivo()` - Funcionalidad 2.1

### Presentación (`Src/Presentation/`)
4. **GeneradorHashViewModel.cs** (`ViewModel/`)
   - Gestión de estado y comandos
   - Navegación entre funcionalidades
   - Búsqueda automática de archivos hash

5. **GeneradorHashView.xaml** (`View/`)
   - Interfaz con TabControl (Generar Hash / Verificar Hash)
   - Diseño consistente con el estilo del Add-In
   - Feedback visual de operaciones

6. **GeneradorHashView.xaml.cs** (`View/`)
   - Code-behind de la vista

7. **GeneradorHashButton.cs** (`View/Buttons/`)
   - Botón que abre ventana modal

### Configuración
8. **Config.daml** - Actualizado con:
   - Nuevo grupo "Hash" en el ribbon
   - Botón "Generar Hash"
   - Condiciones y tooltips

---

## 🎯 Funcionalidades Implementadas

### ✨ GRUPO 1: Generar Hash

#### Funcionalidad 1.1: Comprimir GDB y Generar Hash
- Comprime carpeta/GDB en formato ZIP
- Genera hash SHA256 del ZIP
- Crea archivo de texto con información del hash
- Formato: `nombreGDB_AAAAMMDDHHMMSS.zip` y `nombreGDB_AAAAMMDDHHMMSS_HASH.txt`

**Archivo HASH generado incluye:**
```
Archivo: nombreGDB_20251119143045.zip
SHA256: a1b2c3d4e5f6g7h8...
Fecha: 2025-11-19 14:30:45
Tamaño: 15.5 MB
```

#### Funcionalidad 1.2: Generar Hash de Archivos en Carpeta
- Calcula SHA256 de todos los archivos en la carpeta (no recursivo)
- Genera archivo resumen con todos los hashes
- Formato: `carpeta_AAAAMMDDHHMMSS_HASH.txt`

**Archivo resumen incluye:**
```
Carpeta: C:\ruta\carpeta
Fecha: 2025-11-19 14:30:45
Total archivos: 5

archivo1.shp    | SHA256: a1b2c3d4e5f6...
archivo2.dbf    | SHA256: b2c3d4e5f6g7...
...
```

### ✅ GRUPO 2: Verificar Hash

#### Funcionalidad 2.1: Verificar Integridad de Archivo
- Busca automáticamente el archivo HASH asociado
- Calcula hash actual del archivo
- Compara con el hash esperado
- Muestra resultado detallado

**Resultado de verificación:**
```
✅ INTEGRIDAD VERIFICADA
   Archivo: archivo.zip
   HASH esperado: a1b2c3d4e5f6...
   HASH actual:   a1b2c3d4e5f6...
   
   ✅ Los hashes coinciden - Archivo íntegro
```

---

## 🎨 Interfaz de Usuario

### Pestaña "Generar Hash"
- Selector de funcionalidad (ComboBox)
- Campo para seleccionar carpeta/GDB
- Botón "Examinar" para navegación
- Área de resultados con scroll
- Indicador de progreso
- Botones: "Limpiar" y "GENERAR HASH"

### Pestaña "Verificar Hash"
- Campo para archivo a verificar
- Campo de solo lectura para archivo hash (búsqueda automática)
- Área de resultados con scroll
- Indicador de progreso
- Botones: "Limpiar" y "VERIFICAR INTEGRIDAD"

---

## ⚙️ Validaciones Implementadas

| Validación | Acción |
|-----------|--------|
| Carpeta no existe | ❌ Error con mensaje claro |
| No es GDB | ⚠️ Advertencia (permite continuar) |
| Carpeta vacía | ❌ Error: sin archivos |
| Archivo no existe | ❌ Error con mensaje |
| No hay archivo HASH | ❌ Error indicando patrón esperado |
| HASH corrupto | ❌ Error: no se puede parsear |
| Hashes no coinciden | ❌ Alerta de integridad comprometida |

---

## 📦 Dependencias Utilizadas

- `System.IO` - Manejo de archivos y directorios
- `System.IO.Compression` - Compresión ZIP
- `System.Security.Cryptography` - SHA256
- `ArcGIS.Desktop.Catalog` - Diálogos de navegación
- `ArcGIS.Desktop.Framework` - Framework de ArcGIS Pro

---

## 🚀 Cómo Usar

### Para Generar Hash:
1. Abrir ArcGIS Pro
2. En el ribbon "EAAB", hacer clic en el grupo "Hash"
3. Hacer clic en el botón "Generar Hash"
4. Seleccionar la funcionalidad deseada:
   - **Comprimir GDB y Generar Hash**: Para GDBs/carpetas grandes
   - **Generar Hash de Archivos en Carpeta**: Para múltiples archivos individuales
5. Hacer clic en "Examinar" y seleccionar la carpeta
6. Hacer clic en "GENERAR HASH"
7. Los archivos se crearán en la ubicación apropiada

### Para Verificar Integridad:
1. Ir a la pestaña "Verificar Hash"
2. Hacer clic en "Examinar" y seleccionar el archivo a verificar
3. El sistema buscará automáticamente el archivo HASH
4. Hacer clic en "VERIFICAR INTEGRIDAD"
5. Revisar el resultado de la verificación

---

## 📝 Notas Importantes

### Formato de Timestamp
- Se usa formato: `AAAAMMDDHHMMSS` (20251119143045)
- 24 horas, sin separadores
- Garantiza unicidad y orden cronológico

### Ubicación de Archivos
- **ZIP y HASH**: Se crean en la carpeta **padre** de la carpeta comprimida
- **Resumen de carpeta**: Se crea **dentro** de la carpeta analizada
- Esto evita conflictos y facilita la organización

### Performance
- SHA256 se calcula en bloques para archivos grandes
- Compresión se ejecuta en tarea asíncrona
- UI no se bloquea durante operaciones largas

---

## 🔧 Pendiente

### Imágenes del Botón
Se requiere agregar dos imágenes en la carpeta `Images/`:
- **Hash16.png** (16x16 píxeles) - Ícono pequeño para el grupo
- **Hash32.png** (32x32 píxeles) - Ícono grande para el botón

**Sugerencia de diseño:**
- Ícono representando seguridad/candado
- Ícono de checksuma o verificación (✓)
- Símbolo # (hash)
- Colores: azul/verde para consistencia con EAAB

**Alternativa temporal:**
Puedes copiar temporalmente una imagen existente mientras creas las definitivas:
```powershell
# En la carpeta del proyecto
Copy-Item "Images\Settings16.png" "Images\Hash16.png"
Copy-Item "Images\Settings32.png" "Images\Hash32.png"
```

---

## 🧪 Testing Recomendado

1. **Comprimir GDB pequeña** (<100 MB)
2. **Comprimir GDB grande** (>500 MB) - verificar progreso
3. **Generar hash de carpeta** con varios tipos de archivo
4. **Verificar archivo íntegro** - debe mostrar ✅
5. **Modificar archivo y verificar** - debe mostrar ❌
6. **Verificar sin archivo HASH** - debe mostrar error claro

---

## 📚 Estructura de Código

```
Src/
├── Application/
│   ├── Services/
│   │   ├── HashService.cs           ✨ Nuevo
│   │   └── CompressionService.cs    ✨ Nuevo
│   └── UseCases/
│       └── GenerarHashUseCase.cs    ✨ Nuevo
│
└── Presentation/
    ├── ViewModel/
    │   └── GeneradorHashViewModel.cs ✨ Nuevo
    └── View/
        ├── GeneradorHashView.xaml     ✨ Nuevo
        ├── GeneradorHashView.xaml.cs  ✨ Nuevo
        └── Buttons/
            └── GeneradorHashButton.cs ✨ Nuevo
```

---

## 🎉 Conclusión

La funcionalidad de **Generador de Hash** está completamente implementada y lista para usar. Solo falta agregar las imágenes para el botón en el ribbon de ArcGIS Pro.

Todas las validaciones, manejo de errores y feedback al usuario están implementados según las especificaciones de la guía proporcionada.

---

**Fecha de implementación:** 19 de noviembre de 2025  
**Desarrollado por:** GitHub Copilot (Claude Sonnet 4.5)  
**Para:** EAAB - Empresa de Acueducto y Alcantarillado de Bogotá
