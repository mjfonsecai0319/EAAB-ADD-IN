# Diagnóstico de Geometrías - Ejecutar desde ArcGIS Pro Python Window
# Este script verifica por qué no se visualizan las geometrías

import arcpy
import os

# CAMBIAR ESTA RUTA a tu GDB
gdb = r"C:\Users\57315\Desktop\Acueducto\ARCGIS\Proyectos\EAAB-ADD-IN\bin\Debug\GDB_Cargue_20251104000000.gdb"

print("\n" + "="*80)
print("DIAGNÓSTICO DE GEOMETRÍAS")
print("="*80)

# Clases a verificar
clases = [
    "acd_RedMenor",      # 28 líneas
    "acd_Accesorio",     # 33 puntos
    "als_RedLocal",      # 15 líneas
    "als_Pozo"           # 17 puntos
]

for clase in clases:
    fc_path = os.path.join(gdb, clase)
    print(f"\n{'='*80}")
    print(f"📍 Clase: {clase}")
    print("="*80)
    
    try:
        # Contar registros
        count = int(arcpy.GetCount_management(fc_path)[0])
        print(f"✅ Registros totales: {count}")
        
        if count == 0:
            print("⚠ No hay registros en esta clase")
            continue
        
        # Describir la clase
        desc = arcpy.Describe(fc_path)
        print(f"\n📊 Información de la clase:")
        print(f"   - Tipo: {desc.shapeType}")
        print(f"   - Sistema Coordenadas: {desc.spatialReference.name}")
        print(f"   - WKID: {desc.spatialReference.factoryCode}")
        print(f"   - Extensión:")
        print(f"     XMin: {desc.extent.XMin:.2f}")
        print(f"     YMin: {desc.extent.YMin:.2f}")
        print(f"     XMax: {desc.extent.XMax:.2f}")
        print(f"     YMax: {desc.extent.YMax:.2f}")
        
        # Verificar geometrías
        print(f"\n🔍 Verificando geometrías (primeras 5):")
        
        geom_validas = 0
        geom_nulas = 0
        geom_vacias = 0
        
        with arcpy.da.SearchCursor(fc_path, ["SHAPE@", "OBJECTID", "SUBTIPO"]) as cursor:
            for i, row in enumerate(cursor):
                if i < 5:  # Mostrar detalle de las primeras 5
                    geom = row[0]
                    oid = row[1]
                    subtipo = row[2]
                    
                    if geom is None:
                        print(f"   ❌ OBJECTID {oid}: Geometría NULA")
                        geom_nulas += 1
                    elif geom.isEmpty:
                        print(f"   ⚠ OBJECTID {oid}: Geometría VACÍA")
                        geom_vacias += 1
                    else:
                        geom_validas += 1
                        print(f"   ✅ OBJECTID {oid} (SUBTIPO={subtipo}):")
                        print(f"      - Tipo: {geom.type}")
                        print(f"      - HasZ: {geom.hasZ}, HasM: {geom.hasM}")
                        
                        if geom.type == "point":
                            print(f"      - Coordenadas: X={geom.firstPoint.X:.2f}, Y={geom.firstPoint.Y:.2f}")
                            if geom.hasZ:
                                print(f"      - Z: {geom.firstPoint.Z:.2f}")
                        elif geom.type == "polyline":
                            print(f"      - Longitud: {geom.length:.2f} m")
                            print(f"      - Puntos: {geom.pointCount}")
                            print(f"      - Primer punto: X={geom.firstPoint.X:.2f}, Y={geom.firstPoint.Y:.2f}")
                            print(f"      - Último punto: X={geom.lastPoint.X:.2f}, Y={geom.lastPoint.Y:.2f}")
                else:
                    # Contar el resto
                    geom = row[0]
                    if geom is None:
                        geom_nulas += 1
                    elif geom.isEmpty:
                        geom_vacias += 1
                    else:
                        geom_validas += 1
        
        print(f"\n📈 Resumen de geometrías:")
        print(f"   ✅ Válidas: {geom_validas}")
        print(f"   ❌ Nulas: {geom_nulas}")
        print(f"   ⚠ Vacías: {geom_vacias}")
        
        if geom_nulas > 0 or geom_vacias > 0:
            print(f"\n⚠ PROBLEMA DETECTADO: Hay geometrías nulas o vacías")
            print(f"   Las features existen pero no tienen geometría válida")
        
        if geom_validas > 0:
            print(f"\n✅ Hay {geom_validas} geometrías válidas")
            print(f"   Si no se visualizan, puede ser problema de:")
            print(f"   1. Sistema de coordenadas del mapa")
            print(f"   2. Extensión del mapa (hacer Zoom to Layer)")
            print(f"   3. Simbología de la capa")
        
    except Exception as e:
        print(f"❌ Error: {str(e)}")
        import traceback
        traceback.print_exc()

print("\n" + "="*80)
print("RECOMENDACIONES:")
print("="*80)
print("""
Si las geometrías son VÁLIDAS pero NO SE VEN:

1. 🗺️ Verificar Sistema de Coordenadas del Mapa:
   - Map Properties → Coordinate Systems
   - Debe ser el mismo que las capas (ej: MAGNA_Colombia_Bogota)

2. 🔍 Hacer Zoom a la Capa:
   - Click derecho en la capa → Zoom to Layer
   - Esto ajusta la vista a la extensión de los datos

3. 🎨 Verificar Simbología:
   - Symbology pane → Asegurar que tiene símbolos visibles
   - Para puntos: tamaño > 5
   - Para líneas: grosor > 1

4. 📐 Verificar Escala:
   - Algunas capas tienen rango de escala
   - Layer Properties → General → Scale Range

Si las geometrías son NULAS o VACÍAS:
   ❌ HAY UN PROBLEMA EN LA MIGRACIÓN
   Las geometrías no se están copiando correctamente
""")

print("\n✅ Diagnóstico completado\n")
