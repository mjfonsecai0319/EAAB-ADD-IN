"""
Script para verificar la migración de datos en la GDB
Ejecutar desde la consola de Python de ArcGIS Pro
"""

import arcpy
import os
from datetime import datetime

def verificar_migracion(gdb_path):
    """
    Verifica los conteos y muestra ejemplos de datos migrados
    """
    print("=" * 80)
    print("VERIFICACIÓN DE MIGRACIÓN DE DATOS")
    print("=" * 80)
    print(f"\nGDB: {gdb_path}")
    print(f"Fecha verificación: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    
    # Lista de clases a verificar
    clases_acueducto = [
        "acd_RedMatriz",
        "acd_Conduccion", 
        "acd_RedMenor",
        "acd_LineaLateral",
        "acd_ValvulaSistema",
        "acd_ValvulaControl",
        "acd_Accesorio",
        "acd_Hidrante",
        "acd_MacroMedidor",
        "acd_PuntoAcometida",
        "acd_PilaMuestreo",
        "acd_Captacion",
        "acd_Desarenador",
        "acd_PlantaTratamiento",
        "acd_EstacionBombeo",
        "acd_Tanque",
        "acd_Portal",
        "acd_CamaraAcceso"
    ]
    
    clases_alcantarillado = [
        "als_RedLocal",
        "als_RedTroncal",
        "als_LineaLateral",
        "als_Pozo",
        "als_Sumidero",
        "als_CajaDomiciliaria",
        "als_EstructuraRed",
        "alp_RedLocal",
        "alp_RedTroncal",
        "alp_LineaLateral",
        "alp_Pozo",
        "alp_Sumidero"
    ]
    
    print("\n" + "=" * 80)
    print("ACUEDUCTO")
    print("=" * 80)
    
    total_acu = 0
    for clase in clases_acueducto:
        try:
            fc_path = os.path.join(gdb_path, clase)
            count = int(arcpy.GetCount_management(fc_path)[0])
            total_acu += count
            
            if count > 0:
                print(f"\n✅ {clase}: {count} registros")
                
                # Mostrar primeros 3 registros
                with arcpy.da.SearchCursor(fc_path, ["OBJECTID", "SUBTIPO", "FECHAINSTALACION"]) as cursor:
                    for i, row in enumerate(cursor):
                        if i >= 3:
                            break
                        print(f"   └─ OBJECTID={row[0]}, SUBTIPO={row[1]}, FECHA={row[2]}")
            else:
                print(f"⚪ {clase}: 0 registros")
                
        except Exception as e:
            print(f"❌ {clase}: Error - {str(e)}")
    
    print(f"\n{'─' * 80}")
    print(f"TOTAL ACUEDUCTO: {total_acu} registros")
    
    print("\n" + "=" * 80)
    print("ALCANTARILLADO")
    print("=" * 80)
    
    total_alc = 0
    for clase in clases_alcantarillado:
        try:
            fc_path = os.path.join(gdb_path, clase)
            count = int(arcpy.GetCount_management(fc_path)[0])
            total_alc += count
            
            if count > 0:
                print(f"\n✅ {clase}: {count} registros")
                
                # Mostrar primeros 3 registros
                with arcpy.da.SearchCursor(fc_path, ["OBJECTID", "SUBTIPO", "FECHAINSTALACION"]) as cursor:
                    for i, row in enumerate(cursor):
                        if i >= 3:
                            break
                        print(f"   └─ OBJECTID={row[0]}, SUBTIPO={row[1]}, FECHA={row[2]}")
            else:
                print(f"⚪ {clase}: 0 registros")
                
        except Exception as e:
            print(f"❌ {clase}: Error - {str(e)}")
    
    print(f"\n{'─' * 80}")
    print(f"TOTAL ALCANTARILLADO: {total_alc} registros")
    
    print("\n" + "=" * 80)
    print(f"TOTAL GENERAL: {total_acu + total_alc} registros migrados")
    print("=" * 80)
    
    # Verificación de geometrías
    print("\n" + "=" * 80)
    print("VERIFICACIÓN DE GEOMETRÍAS")
    print("=" * 80)
    
    clases_con_datos = []
    for clase in clases_acueducto + clases_alcantarillado:
        try:
            fc_path = os.path.join(gdb_path, clase)
            count = int(arcpy.GetCount_management(fc_path)[0])
            if count > 0:
                clases_con_datos.append(fc_path)
        except:
            pass
    
    for fc_path in clases_con_datos[:5]:  # Solo primeras 5 con datos
        nombre = os.path.basename(fc_path)
        try:
            with arcpy.da.SearchCursor(fc_path, ["SHAPE@", "OBJECTID"]) as cursor:
                row = next(cursor)
                geom = row[0]
                if geom:
                    geom_type = geom.type
                    has_z = geom.hasZ
                    has_m = geom.hasM
                    print(f"\n✅ {nombre} (OBJECTID={row[1]})")
                    print(f"   └─ Tipo: {geom_type}, HasZ: {has_z}, HasM: {has_m}")
                    if geom_type == "polyline":
                        print(f"   └─ Longitud: {geom.length:.2f} m")
                    elif geom_type == "point":
                        print(f"   └─ Coordenadas: X={geom.firstPoint.X:.2f}, Y={geom.firstPoint.Y:.2f}")
        except Exception as e:
            print(f"❌ {nombre}: Error leyendo geometría - {str(e)}")
    
    print("\n" + "=" * 80)
    print("VERIFICACIÓN COMPLETADA")
    print("=" * 80)


if __name__ == "__main__":
    # Buscar la GDB más reciente en bin\Debug
    import glob
    
    debug_path = r"C:\Users\57315\Desktop\Acueducto\ARCGIS\Proyectos\EAAB-ADD-IN\bin\Debug"
    gdbs = glob.glob(os.path.join(debug_path, "GDB_Cargue_*.gdb"))
    
    if not gdbs:
        print("❌ No se encontraron GDBs en bin\\Debug")
    else:
        # Ordenar por fecha de modificación y tomar la más reciente
        gdb_mas_reciente = max(gdbs, key=os.path.getmtime)
        print(f"\n🔍 GDB más reciente encontrada:")
        print(f"   {gdb_mas_reciente}")
        print(f"   Modificada: {datetime.fromtimestamp(os.path.getmtime(gdb_mas_reciente)).strftime('%Y-%m-%d %H:%M:%S')}")
        
        respuesta = input("\n¿Verificar esta GDB? (s/n): ")
        if respuesta.lower() == 's':
            verificar_migracion(gdb_mas_reciente)
        else:
            # Permitir ingresar ruta manualmente
            ruta_manual = input("Ingrese la ruta completa de la GDB: ")
            if os.path.exists(ruta_manual):
                verificar_migracion(ruta_manual)
            else:
                print("❌ La ruta no existe")
