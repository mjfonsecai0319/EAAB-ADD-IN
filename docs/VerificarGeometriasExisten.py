# Script para verificar si las geometrías existen aunque no se vean
# Ejecutar desde ArcGIS Pro Python Window

import arcpy

# CAMBIAR esta ruta
gdb = r"C:\Users\57315\Desktop\Acueducto\ARCGIS\Proyectos\EAAB-ADD-IN\bin\Debug\GDB_Cargue_20251104000000.gdb"

print("\n" + "="*80)
print("VERIFICACIÓN RÁPIDA DE GEOMETRÍAS")
print("="*80)

# Verificar acd_RedMenor (líneas)
print("\n🔍 Verificando acd_RedMenor (28 líneas esperadas)...")
fc = gdb + r"\acd_RedMenor"
try:
    count = int(arcpy.GetCount_management(fc)[0])
    print(f"✅ Total registros: {count}")
    
    if count > 0:
        # Ver primera geometría
        with arcpy.da.SearchCursor(fc, ["SHAPE@", "OBJECTID", "SHAPE@LENGTH"]) as cursor:
            row = next(cursor)
            if row[0] is not None:
                print(f"✅ Primera línea (OID {row[1]}):")
                print(f"   - Longitud: {row[2]:.2f} m")
                print(f"   - Puntos en geometría: {row[0].pointCount}")
                print(f"   - Coordenadas inicio: X={row[0].firstPoint.X:.2f}, Y={row[0].firstPoint.Y:.2f}")
                print(f"   - Sistema coords: {arcpy.Describe(fc).spatialReference.name}")
            else:
                print(f"❌ Geometría NULA")
except Exception as e:
    print(f"❌ Error: {e}")

# Verificar acd_Accesorio (puntos)
print("\n🔍 Verificando acd_Accesorio (33 puntos esperados)...")
fc = gdb + r"\acd_Accesorio"
try:
    count = int(arcpy.GetCount_management(fc)[0])
    print(f"✅ Total registros: {count}")
    
    if count > 0:
        # Ver primer punto
        with arcpy.da.SearchCursor(fc, ["SHAPE@", "OBJECTID", "SHAPE@X", "SHAPE@Y"]) as cursor:
            row = next(cursor)
            if row[0] is not None:
                print(f"✅ Primer punto (OID {row[1]}):")
                print(f"   - Coordenadas: X={row[2]:.2f}, Y={row[3]:.2f}")
                print(f"   - Sistema coords: {arcpy.Describe(fc).spatialReference.name}")
            else:
                print(f"❌ Geometría NULA")
except Exception as e:
    print(f"❌ Error: {e}")

print("\n" + "="*80)
print("SOLUCIÓN SI HAY GEOMETRÍAS VÁLIDAS PERO NO SE VEN:")
print("="*80)
print("""
1. En ArcGIS Pro:
   - Click derecho en la capa → Zoom to Layer
   
2. Si aún no se ve:
   - Click derecho en la capa → Properties → Source
   - Verificar que el Spatial Reference sea correcto
   
3. Verificar que el Data Frame tenga el mismo sistema de coordenadas:
   - Map Properties → Coordinate Systems
   - Debería ser el mismo que las capas
   
4. Verificar simbología:
   - Appearance → Symbology
   - Aumentar tamaño de símbolos
""")

print("\n✅ Verificación completada\n")
