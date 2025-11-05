# Verificación de GDB - Ejecutar en ArcGIS Pro Python Window
import arcpy

# GDB más reciente encontrada
gdb = r"C:\Users\57315\Desktop\Acueducto\ARCGIS\Proyectos\Insumos\GDB_Cargue.gdb"

print("\n" + "="*80)
print("VERIFICACIÓN DE GDB_Cargue")
print("="*80)
print(f"GDB: {gdb}\n")

# Listar todas las feature classes de acueducto y alcantarillado
print("📋 Listando clases de acueducto (acd_*):")
acd_classes = arcpy.ListFeatureClasses("acd_*", feature_dataset="")
if acd_classes:
    for fc in sorted(acd_classes):
        try:
            count = int(arcpy.GetCount_management(gdb + "\\" + fc)[0])
            print(f"  {fc}: {count} registros")
        except:
            print(f"  {fc}: Error al contar")
else:
    print("  ❌ No se encontraron clases acd_*")

print("\n📋 Listando clases de alcantarillado (als_* y alp_*):")
alc_classes = arcpy.ListFeatureClasses("al*_*", feature_dataset="")
if alc_classes:
    for fc in sorted(alc_classes):
        try:
            count = int(arcpy.GetCount_management(gdb + "\\" + fc)[0])
            if count > 0:
                print(f"  {fc}: {count} registros")
        except:
            pass
else:
    print("  ❌ No se encontraron clases als_* o alp_*")

print("\n" + "="*80)
print("VERIFICACIÓN DE GEOMETRÍAS")
print("="*80)

# Buscar clases con datos para verificar geometrías
arcpy.env.workspace = gdb
all_fcs = arcpy.ListFeatureClasses()

print(f"\nTotal de Feature Classes en la GDB: {len(all_fcs) if all_fcs else 0}")

# Verificar algunas con datos
classes_to_check = []
if all_fcs:
    for fc in all_fcs:
        try:
            count = int(arcpy.GetCount_management(fc)[0])
            if count > 0 and (fc.startswith("acd_") or fc.startswith("als_") or fc.startswith("alp_")):
                classes_to_check.append(fc)
                if len(classes_to_check) >= 4:  # Solo verificar las primeras 4
                    break
        except:
            pass

if classes_to_check:
    print(f"\n🔍 Verificando geometrías de clases con datos:")
    for fc in classes_to_check:
        try:
            desc = arcpy.Describe(fc)
            count = int(arcpy.GetCount_management(fc)[0])
            
            print(f"\n  ✅ {fc} ({count} registros)")
            print(f"     Tipo: {desc.shapeType}")
            print(f"     SR: {desc.spatialReference.name}")
            
            # Verificar primera geometría
            with arcpy.da.SearchCursor(fc, ["SHAPE@", "OBJECTID"]) as cursor:
                row = next(cursor)
                geom = row[0]
                if geom is None:
                    print(f"     ❌ GEOMETRÍA NULA - PROBLEMA CRÍTICO")
                elif geom.isEmpty:
                    print(f"     ⚠ GEOMETRÍA VACÍA")
                else:
                    print(f"     ✅ Geometría válida")
                    if desc.shapeType == "Polyline":
                        print(f"     Longitud: {geom.length:.2f} m")
                    elif desc.shapeType == "Point":
                        print(f"     Coords: X={geom.firstPoint.X:.2f}, Y={geom.firstPoint.Y:.2f}")
        except Exception as e:
            print(f"  ❌ {fc}: Error - {str(e)}")
else:
    print("\n⚠ No se encontraron clases de acueducto/alcantarillado con datos")

print("\n" + "="*80)
print("✅ Verificación completada")
print("="*80)
