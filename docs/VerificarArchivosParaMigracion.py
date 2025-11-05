# Script de Migración Completa - Ejecutar en ArcGIS Pro Python Window
# Este script migra datos de acueducto y alcantarillado a GDB_Cargue

import arcpy
import sys
import os

# ============================================================================
# CONFIGURACIÓN
# ============================================================================

# Rutas de archivos de origen
lineas_acu = r"C:\Users\57315\Desktop\Acueducto\ARCGIS\Proyectos\Insumos\Obra_01_3101\shp_Acueducto_PuenteExitoSuba\Lin_RecAcue_PuenteExitoSuba.shp"
puntos_acu = r"C:\Users\57315\Desktop\Acueducto\ARCGIS\Proyectos\Insumos\Obra_01_3101\shp_Acueducto_PuenteExitoSuba\Nod_RecAcue_PuenteExitoSuba.shp"

lineas_alc = r"C:\Users\57315\Desktop\Acueducto\ARCGIS\Proyectos\Insumos\Obra_01_3110\8.3 Shape, GDB-Xml\Lin_RecAlcComb_USAQUEN.shp"
puntos_alc = r"C:\Users\57315\Desktop\Acueducto\ARCGIS\Proyectos\Insumos\Obra_01_3110\8.3 Shape, GDB-Xml\Nod_RecAlcComb_USAQUEN.shp"

# GDB destino
gdb_destino = r"C:\Users\57315\Desktop\Acueducto\ARCGIS\Proyectos\Insumos\GDB_Cargue.gdb"

print("="*80)
print("MIGRACIÓN DE DATOS - ACUEDUCTO Y ALCANTARILLADO")
print("="*80)
print(f"\nGDB Destino: {gdb_destino}")
print(f"\nArchivos de origen:")
print(f"  ACU Líneas: {lineas_acu}")
print(f"  ACU Puntos: {puntos_acu}")
print(f"  ALC Líneas: {lineas_alc}")
print(f"  ALC Puntos: {puntos_alc}")

# Verificar que los archivos existan
print("\n" + "="*80)
print("VERIFICACIÓN DE ARCHIVOS")
print("="*80)

archivos = {
    "ACU Líneas": lineas_acu,
    "ACU Puntos": puntos_acu,
    "ALC Líneas": lineas_alc,
    "ALC Puntos": puntos_alc
}

archivos_ok = True
for nombre, ruta in archivos.items():
    if arcpy.Exists(ruta):
        count = int(arcpy.GetCount_management(ruta)[0])
        print(f"✅ {nombre}: {count} registros")
    else:
        print(f"❌ {nombre}: NO EXISTE")
        archivos_ok = False

if not archivos_ok:
    print("\n⚠ ERROR: No se encontraron todos los archivos de origen")
    print("Por favor verifica las rutas y vuelve a ejecutar")
    sys.exit()

if not arcpy.Exists(gdb_destino):
    print(f"\n❌ ERROR: La GDB destino no existe: {gdb_destino}")
    sys.exit()

print("\n✅ Todos los archivos de origen existen")
print("✅ La GDB destino existe")

# ============================================================================
# MIGRACIÓN - ESTE CÓDIGO DEBE EJECUTARSE DESDE EL ADD-IN
# ============================================================================

print("\n" + "="*80)
print("⚠ IMPORTANTE ⚠")
print("="*80)
print("""
Este script solo VERIFICA que los archivos existen.

Para MIGRAR los datos, debes usar el Add-In de ArcGIS Pro:

1. Abre ArcGIS Pro
2. Busca el botón "Migrar" en la cinta del Add-In
3. En el panel de migración, ingresa estas rutas:

   ACUEDUCTO:
   - Líneas: C:\\Users\\57315\\Desktop\\Acueducto\\ARCGIS\\Proyectos\\Insumos\\Obra_01_3101\\shp_Acueducto_PuenteExitoSuba\\Lin_RecAcue_PuenteExitoSuba.shp
   - Puntos: C:\\Users\\57315\\Desktop\\Acueducto\\ARCGIS\\Proyectos\\Insumos\\Obra_01_3101\\shp_Acueducto_PuenteExitoSuba\\Nod_RecAcue_PuenteExitoSuba.shp
   
   ALCANTARILLADO:
   - Líneas: C:\\Users\\57315\\Desktop\\Acueducto\\ARCGIS\\Proyectos\\Insumos\\Obra_01_3110\\8.3 Shape, GDB-Xml\\Lin_RecAlcComb_USAQUEN.shp
   - Puntos: C:\\Users\\57315\\Desktop\\Acueducto\\ARCGIS\\Proyectos\\Insumos\\Obra_01_3110\\8.3 Shape, GDB-Xml\\Nod_RecAlcComb_USAQUEN.shp
   
   GDB DESTINO:
   - C:\\Users\\57315\\Desktop\\Acueducto\\ARCGIS\\Proyectos\\Insumos\\GDB_Cargue.gdb

4. Click en "Migrar"

ALTERNATIVA: Si quieres usar el XML para crear una GDB nueva:
- XML: Busca el archivo NS-046 en la carpeta de insumos
- Esto creará una GDB nueva con timestamp
""")

print("\n" + "="*80)
print("✅ Verificación completada")
print("="*80)
