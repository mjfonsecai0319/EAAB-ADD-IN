# Script rápido para verificación desde consola de ArcGIS Pro
# Copiar y pegar en la consola de Python

import arcpy
import os

# CAMBIAR ESTA RUTA a tu GDB más reciente
gdb = r"C:\Users\57315\Desktop\Acueducto\ARCGIS\Proyectos\EAAB-ADD-IN\bin\Debug\GDB_Cargue_2025XXXXXXXX.gdb"

print("\n" + "="*60)
print("CONTEO RÁPIDO DE REGISTROS MIGRADOS")
print("="*60)

# Acueducto
clases_acu = ["acd_RedMatriz", "acd_Conduccion", "acd_RedMenor", "acd_LineaLateral",
              "acd_ValvulaSistema", "acd_ValvulaControl", "acd_Accesorio", "acd_Hidrante",
              "acd_MacroMedidor", "acd_PuntoAcometida", "acd_CamaraAcceso"]

print("\n🌊 ACUEDUCTO:")
total_acu = 0
for fc in clases_acu:
    try:
        count = int(arcpy.GetCount_management(os.path.join(gdb, fc))[0])
        if count > 0:
            print(f"  ✓ {fc}: {count}")
            total_acu += count
    except: pass

# Alcantarillado
clases_alc = ["als_RedLocal", "als_RedTroncal", "als_LineaLateral", 
              "als_Pozo", "als_Sumidero", "als_EstructuraRed",
              "alp_RedLocal", "alp_Pozo"]

print("\n🚰 ALCANTARILLADO:")
total_alc = 0
for fc in clases_alc:
    try:
        count = int(arcpy.GetCount_management(os.path.join(gdb, fc))[0])
        if count > 0:
            print(f"  ✓ {fc}: {count}")
            total_alc += count
    except: pass

print(f"\n{'='*60}")
print(f"TOTAL: {total_acu + total_alc} registros")
print("="*60)

# Verificar geometrías de una clase con datos
print("\n🔍 Verificando geometrías (primera clase con datos)...")
for fc in clases_acu + clases_alc:
    try:
        fc_path = os.path.join(gdb, fc)
        if int(arcpy.GetCount_management(fc_path)[0]) > 0:
            with arcpy.da.SearchCursor(fc_path, ["SHAPE@", "OBJECTID"]) as cursor:
                row = next(cursor)
                geom = row[0]
                print(f"\n✓ {fc} (OBJECTID {row[1]}):")
                print(f"  - Tipo: {geom.type}")
                print(f"  - HasZ: {geom.hasZ}, HasM: {geom.hasM}")
                if geom.type == "polyline":
                    print(f"  - Longitud: {geom.length:.2f} m")
                break
    except: pass

print("\n✅ Verificación completada\n")
