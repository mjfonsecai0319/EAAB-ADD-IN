"""
Script documentation

- Tool parameters are accessed using arcpy.GetParameter() or 
                                     arcpy.GetParameterAsText()
- Update derived parameter values using arcpy.SetParameter() or
                                        arcpy.SetParameterAsText()
"""
import arcpy, os, csv

arcpy.env.workspace = 'current'
arcpy.env.overwriteOutput = True

#-----------------Campos y Dominios para las Lineas de acueducto-----------------
# Atributos para las capas tipo linea acueducto
atrib_l_ecu_shp = ['Shape@','CLASE','SUBTIPO','N_INICIAL','N_FINAL','FECHAINST','ESTADOENRE','DIAMETRO','MATERIAL',
'CALIDADDED','ESTADOLEGA','OBSERV','TIPOINSTAL','CONTRATO_I','NDISENO','NOMBRE','COSTADO','T_SECCION',
'AREA_TR_M2','C_RASANTEI','C_RASANTEF','C_CLAVEI','C_CLAVEF','PROFUNDIDA','RUGOSIDAD','LONGITUD_m','CODACTIVO_','FID']

atrib_l_ecu_gdb = ['Shape@','CLASE','SUBTIPO','N_INICIAL','N_FINAL','FECHAINST','ESTADOENRED','DIAMETRO','MATERIAL',
'CALIDADDEDATO','ESTADOLEGAL','OBSERV','TIPOINSTALACION','CONTRATO_ID','NDISENO','NOMBRE','COSTADO','T_SECCION',
'AREA_TR_M2','C_RASANTEI','C_RASANTEF','C_CLAVEI','C_CLAVEF','PROFUNDIDAD','RUGOSIDAD','LONGITUD_m','CODACTIVO_FIJO','OBJECTID']

# dominios para las capas tipo linea acueducto
subtipo = {'redMatriz_1':[23,24,25,33], 'aduccion_2':[20,21,22,33], 'conduccion_3':[20,21,33],
 'redMenor_4':[26,27,33], 'lineaLat_5':[20,23,24,25,33]}
estadoEnRed = ['SE', 'SB', 'FU', 'FB', 'CN']
diametroNominal = ['0.5','0.75','1','1.50','2','2.50','3','4','6','8','10','12','14','16','18','20','24',
'27','30','34','36','39','42','48','51','54','60','72','78','86']
material = ['HC','HF','PDB','HA','AC','HG','CR','CCP','PCCP','ARB','PVC','ACE','AA','CU','G','HD','PFUAD','RCN','Con',
'RT','PAD','Otro']
calidadDato = ['0', '1', '2']
estadoLegal = ['PR', 'OF', 'NO', 'NA']
tipoInstalacion = ['1','2','3']
costado = ['N','S','E','W','NE','NW','SE','SW','SP','NA']
tipoSeccion = ['0','1','2','3','4','5','6','7','8','9']

dominios_l = [subtipo, estadoEnRed, diametroNominal, material, calidadDato, estadoLegal, tipoInstalacion, costado, tipoSeccion]

#-----------------Campos y Dominios para los puntos de acueducto-----------------
# Atributos para la capa tipo punto acueducto
atrib_p_acu_shp = ['Shape@','CLASE','SUBTIPO','IDENTIFIC','NORTE','ESTE','FECHAINST','ESTADOENRE','LOCALIZACI','CALIDADDAT','ROTACION',
'C_RASANTE','PROFUN','MATERIAL','VINCULO','OBSERVACIO','CONTRATO_I','NDISENO','TIPOESPPUB','MATESPPUBL','AUTOMATIZA','DIAMETRO1',
'DIAMETRO2','SENTIDOOPE','ESTADOOPER','TIPOOPERAC','ESTADOFIS_','TIPOVALVUL','VUELTASCIE','CLASEACCES','ESTADOFISI','MARCA',
'FUNCIONPIL','ESTADOMED','SECTORENTR','SECTORSALI','IDTUBERIAM','CAUDAL_PRO','TIPO_M','FECHA_TOMA','UBICACCAJI','CENTRO',
'L_ALM','AREARESP','TIPO_MUEST','FUENTEABAS','UBICAC_MUE','PTOANALISI','LOCPUNTO','ESTADO','FECHAESTAD','CLASEPUNTO',
'NROFILTROS','NROSEDIMEN','NROCOMPART','NROMEZCLAR','NROFLOCULA','CAPACINSTA','NROBOMBAS','CAPABOMBEO','COTABOMBEO','ALTURADINA',
'COTAFONDO','COTAREBOSE','CAPACIDAD','NIVELMAXIM','NIVELMINIM','AREATRANSV','TIENEVIGIL','OPERACTANQ','TIPOACCESO','DIAMETROAC',
'NOMBRE','DIRECCION','PRESION','CODACTIVO_','FID']

atrib_p_acu_gdb = ['Shape@','CLASE','SUBTIPO','IDENTIFIC','NORTE','ESTE','FECHAINST','ESTADOENRED','LOCALIZACIONRELATIVA',
'CALIDADDATO','ROTACION','C_RASANTE','PROFUN','MATERIAL','VINCULO','OBSERVACIONES','CONTRATO_ID','NDISENO','TIPOESPPUB',
'MATESPPUBL','AUTOMATIZA','DIAMETRO1','DIAMETRO2','SENTIDOOPERAC','ESTADOOPERAC','TIPOOPERAC','ESTADOFIS_VAL','TIPOVALVUL',
'VUELTASCIE','CLASEACCES','ESTADOFISICOH','MARCA','FUNCIONPIL','ESTADOMED','SECTORENTR','SECTORSALI','IDTUBERIAMEDIDA',
'CAUDAL_PROMEDIO','TIPO_M','FECHA_TOMA_C','UBICACCAJI','CENTRO','L_ALM','AREARESP','TIPO_MUESTR','FUENTEABAS','UBICAC_MUES',
'PTOANALISI','LOCPUNTO','ESTADO','FECHAESTADO','CLASEPUNTO','NROFILTROS','NROSEDIMEN','NROCOMPART','NROMEZCLAR','NROFLOCULA',
'CAPACINSTA','NROBOMBAS','CAPABOMBEO','COTABOMBEO','ALTURADINA','COTAFONDO','COTAREBOSE','CAPACIDAD','NIVELMAXIM','NIVELMINIM',
'AREATRANSV','TIENEVIGIL','OPERACTANQ','TIPOACCESO','DIAMETROAC','NOMBRE','DIRECCION','PRESION','CODACTIVO_FIJO','OBJECTID']

# lista con las clases de puntos de acueducto
lista_subtipo_p_acu = ['VALVULASISTEMA_1', 'VALVULACONTROL_2', 'ACCESORIO_CODO_3', 'ACCESORIO_REDUCCION_4', 'ACCESORIO_TAPON_5',
                     'ACCESORIO_TEE_6', 'ACCESORIO_UNION_7', 'ACCESORIO_OTROS_8','HIDRANTE_9', 'MACROMEDIDOR_10',
                     'PUNTO_ACOMETIDA_11', 'PILA_MUESTREO_12', 'CAPTACION_13', 'DESARENADOR_14', 'PLANTA_TRATAMIENTO_15',
                     'ESTACION_BOMBEO_16', 'TANQUE_17', 'PORTAL_18', 'CAMARA_ACCESO_19', 'ESTRUCTURA_CONTROL_20',
                     'INSTRUMENTOS_MEDICION_21']

# dominios para la capa tipo punto acueducto
subtipo_p_acu = {'VALVULASISTEMA_1':[20, 21, 22, 23, 24, 25, 26], 'VALVULACONTROL_2':[20, 21, 22, 23, 24, 25, 26, 27, 28],
                 'ACCESORIO_CODO_3':[20], 'ACCESORIO_REDUCCION_4':[25], 'ACCESORIO_TAPON_5':[21],'ACCESORIO_TEE_6':[24],
                 'ACCESORIO_UNION_7':[23], 'ACCESORIO_OTROS_8':[22,26,27], 'HIDRANTE_9':[20, 21],'MACROMEDIDOR_10':[20, 21, 22],
                 'PUNTO_ACOMETIDA_11':[21, 22], 'PILA_MUESTREO_12':[20, 21, 22],'CAPTACION_13':[1, 2, 3, 4, 5, 6],
                 'DESARENADOR_14':[1 ,2], 'PLANTA_TRATAMIENTO_15':[1, 2], 'ESTACION_BOMBEO_16':[1],'TANQUE_17':[1],
                 'PORTAL_18':[1], 'CAMARA_ACCESO_19':[1], 'ESTRUCTURA_CONTROL_20':[1],
                 'INSTRUMENTOS_MEDICION_21':[20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 32]}

estadoEnRed_p_acu = ['SE', 'SB', 'FU', 'FB', 'CN']
calidadDato_p_acu = ['0', '1', '2']
material_p_acu = ['ACE','Otro','Con','RT','HC','HF','PDB','HA','AC','HG','CR','ARB','AA','CU','G','HD','RCN','PVC','PFUAD',
                  'PCCP','CCP','PAD']
tipoEspPubli_p_acu = ['0', '1', '2', '3']
MatEspPubli_p_acu = ['0', '1', '2', '3', '4', '5', '6', '7']
automat_p_acu = [0, 1]
diametro_p_acu = ['0','0.5','0.75','1','1.50','2','2.50','3','4','6','8','10','12','16','18','20','24','30','36','42','48',
                  '60','72','78','14','27','51','34','54','39','86']
sentOpe_p_acu = ['1', '2']
estOpe_p_acu = ['A', 'C']
tipOpe_p_acu = ['0', '1', '2', '3']
estFisValv_p_acu = ['1','2','3','4','5','6','7','8','9','10','11','12','13','14','15','99']
tipoVal_p_acu = ['1','2','3','4','5','6','7','99']
clasAcces_p_acu = {'ACCESORIO_CODO_3':['1','2','3','4','5','7','8','9','10'], 'ACCESORIO_REDUCCION_4':['1', '2', '3'],
                   'ACCESORIO_TAPON_5':['1', '2', '3', '4'], 'ACCESORIO_TEE_6':['1', '2', '3'],
                   'ACCESORIO_UNION_7':['1','2','3','4','5','6','7','8','9','10','11','12']} 
estFisH_p_acu = ['0','1','2','3','4','5','6','7','8']
funPilaPubl_p_acu = [0, 1]
ubiCajilla_p_acu = ['0','1','2','3']
tipPuntMues_p_acu = ['R', 'NR']
fuentAbast_p_acu = ['1', '2', '3']
ubiPuntMuest_p_acu = ['1', '2', '3']
puntAnalBloq_p_acu = ['EB','RB','NA']
locaPto_p_acu = ['I','F','C','SI','IG','P']
estado_p_acu = ['C','D','I']
clasPto_p_acu = ['PM','TA','PL']
vigil_p_acu = [0, 1]
operTanq_p_acu = ['0', '1', '2']
tipoAcces_p_acu = ['C', 'E', 'D']

dominios_p_acu = [subtipo_p_acu, estadoEnRed_p_acu, calidadDato_p_acu, material_p_acu, tipoEspPubli_p_acu, MatEspPubli_p_acu,
                  automat_p_acu, diametro_p_acu, sentOpe_p_acu, estOpe_p_acu, tipOpe_p_acu, estFisValv_p_acu, tipoVal_p_acu,
                  clasAcces_p_acu, estFisH_p_acu, funPilaPubl_p_acu, ubiCajilla_p_acu, tipPuntMues_p_acu, fuentAbast_p_acu,
                  ubiPuntMuest_p_acu, puntAnalBloq_p_acu, locaPto_p_acu, estado_p_acu, clasPto_p_acu, vigil_p_acu,
                  operTanq_p_acu, tipoAcces_p_acu]

#-----------------Campos y Dominios para Lineas Alcantarillado-----------------
atrib_l_alc_shp = ['Shape@', 'CLASE', 'SUBTIPO', 'N_INICIAL', 'N_FINAL', 'SISTEMA', 'FECHAINST', 'MATERIAL', 'MATERIAL2',
                   'NDISENO', 'ESTADOENRE', 'DIAMETRO', 'T_SECCION', 'CALIDADDAT', 'ESTADOLEGA', 'OBSERVACIO', 'CONTRATO_I',
                   'CAM_CAIDA', 'C_RASATEI', 'C_RASANTEF', 'C_CLAVEI', 'C_CLAVEF', 'C_BATEAI', 'C_BATEAF', 'PENDIENTE',
                   'NOMBRE', 'BASE', 'PROFUNDIDA', 'ALTURA1', 'ALTURA2', 'NROCONDUCT', 'ANCHOBERMA', 'TALUD1', 'TALUD2',
                   'LONGITUD_M', 'INSTALACI', 'MATESPPUBL', 'CODACTIVOS', 'TIPOINSPEC', 'GRADOEST', 'GRADOOPER',
                   'RUGOSIDAD', 'FID']

atrib_l_alc_gdb = ['Shape@', 'CLASE', 'SUBTIPO', 'N_INICIAL', 'N_FINAL', 'SISTEMA', 'FECHAINST', 'MATERIAL', 'MATERIAL2',
                   'NDISENO', 'ESTADOENRED', 'DIAMETRO', 'T_SECCION', 'CALIDADDATO', 'ESTADOLEGAL', 'OBSERVACIONES',
                   'CONTRATO_ID', 'CAM_CAIDA', 'C_RASATEI', 'C_RASANTEF', 'C_CLAVEI', 'C_CLAVEF', 'C_BATEAI', 'C_BATEAF',
                   'PENDIENTE', 'NOMBRE', 'BASE', 'PROFUNDIDAD', 'ALTURA1', 'ALTURA2', 'NROCONDUCTOS', 'ANCHOBERMA',
                   'TALUD1', 'TALUD2', 'LONGITUD_M', 'INSTALACI', 'MATESPPUBL', 'CODACTIVOS_FIJOS', 'TIPOINSPEC',
                   'GRADOEST', 'GRADOOPER', 'RUGOSIDAD', 'OBJECTID']

# Dominios de la capa Lineas Alcantarillado
subtipo_l_alc = {'redLocal_1':[20, 21, 22, 26, 33, 34, 35], 'redTroncal_2':[20, 21, 22, 25, 26, 28, 33, 24, 27],
                 'linLat_3':[20, 21, 22, 24, 23]}
sistema_l_alc = ['0', '1', '2']
estadoRed_l_alc = ['0', '1', '2', '3']
material_l_alc = ['1', '2', '3', '4', '5', '6', '7', '8', '9', '10', '11', '12', '13', '14', '15', '16', '17', '18', '19', '20', '21', '22', '99']
calidadDato_l_alc = ['0', '1', '2', '3']
estadoLegal_l_alc = ['0', '1', '2']
diametro_l_alc = ['0', '0.10','0.15','0.20','0.225','0.25','0.30','0.35','0.40','0.45','0.50','0.55','0.60','0.65','0.70',
                  '0.75','0.80','0.85','0.90','0.95','1.00','1.05','1.10','1.15','1.20','1.25','1.30','1.35','1.40','1.45',
                  '1.50','1.55','1.60','1.65','1.70','1.75','1.80','1.85','1.90','1.95','2.00','2.05','2.10','2.15','2.20',
                  '2.25','2.30','2.35','2.40','2.45','2.50','2.55','2.60','2.65','2.70','2.75','2.80','2.85','2.90','2.95',
                  '3.00','3.05','3.10','3.15','3.20','3.25','3.30','3.40','3.50','3.70','4.00','5.30','99']
tipoSeccion_l_alc = ['0','1','2','3','4','5','6','7','8','9','10','11','12']
camaraCaida_l_alc = ['0','1','2', '99']
metodInstal_l_alc = ['0','1','2','3','4','5','6','7', '22', '99']
tipoMaterEspPbli_l_alc = ['0','1','2','3','4','5','6','99']
tipoInspec_l_alc = ['0','1','2', '99']
gradoEstruc_l_alc = ['1','2','3','4','5','6','99']
gradoOper_l_alc = ['1','2','3','4','5','6','99']

dominios_l_alc = [subtipo_l_alc, sistema_l_alc, estadoRed_l_alc, material_l_alc, calidadDato_l_alc, estadoLegal_l_alc,
                  diametro_l_alc, tipoSeccion_l_alc, camaraCaida_l_alc, metodInstal_l_alc, tipoMaterEspPbli_l_alc, tipoInspec_l_alc,
                  gradoEstruc_l_alc, gradoOper_l_alc]

#-----------------Campos y Dominios para Puntos Alcantarillado-----------------
atrib_p_alc_shp = ['Shape@','CLASE','SUBTIPO','IDENTIFIC','NORTE','ESTE','FECHADATO','TIPO_ALIVI','TIPO_VALV_','ESTADOENRE',
                   'LOCALIZACI','C_RASANTE','C_TERRENO','C_FONDO','MATERIAL','CALIDADDAT','SISTEMA','NOMBRE','OBSERV',
                   'CONTRATO_I','NDISENO','PROFUNDIDA','CONOREDUCC','MATERCONO','TIPO_CONO','EST_CONO','INICIAL_CU',
                   'ROTACION','CAMARASIF','EST_FISICO','CABEZAL','EST_TAPA','EST_POZO','MATESCALO','ESTESCALON','ESTCARGUE',
                   'ESTCILIND','ESTCANUE','ESTOPERA','CONTINSPE','FECHA_INSP','TIPOINSPEC','TIPOALMAC','COTACRESTA','C_TECHO_VE',
                   'LONGVERT','LARGO','ANCHO','ALTO','Q_BOMBEO','TIPOBOMB','UNIDBOMBEO','HBOMBEO','COTABOMBE','VOLBOMBEO',
                   'DIRECCION','ESTREJILLA','MATREJILLA','TAMREJILLA','ORIGENSEC','DISTORIGEN','ABSCISA','CODACTIVO_',
                   'OBJECTID']

atrib_p_alc_gdb = ['Shape@','CLASE','SUBTIPO','IDENTIFIC','NORTE','ESTE','FECHADATO','TIPO_ALIVIO','TIPO_VALV_ANT','ESTADOENRED',
                   'LOCALIZACIONRELATIVA','C_RASANTE','C_TERRENO','C_FONDO','MATERIAL','CALIDADDATO','SISTEMA','NOMBRE','OBSERV',
                   'CONTRATO_ID','NDISENO','PROFUNDIDA','CONOREDUCC','MATERCONO','TIPO_CONO','EST_CONO','INICIAL_CUENCAS',
                   'ROTACION','CAMARASIF','EST_FISICO','CABEZAL','EST_TAPA','EST_POZO','MATESCALO','ESTESCALON','ESTCARGUE',
                   'ESTCILIND','ESTCANUE','ESTOPERA','CONTINSPE','FECHA_INSP','TIPOINSPEC','TIPOALMAC','COTACRESTA','C_TECHO_VE',
                   'LONGVERT','LARGO','ANCHO','ALTO','Q_BOMBEO','TIPOBOMB','UNIDBOMBEO','HBOMBEO','COTABOMBE','VOLBOMBEO',
                   'DIRECCION','ESTREJILLA','MATREJILLA','TAMREJILLA','ORIGENSEC','DISTORIGEN','ABSCISA','CODACTIVO_FIJO',
                   'OBJECTID']

# Dominios de la capa Puntos Alcantarillado
subtipo_p_alc = {'ESTRUCTURA_RED_1':[21,22,23,24,25,27,28,29,30,31,32], 'POZO_2':[21, 22, 23, 24],
                 'SUMIDERO_3':[21,22,23,24,25,26,27,28,29,30], 'CAJA_DOMICILIARIA_4':[20] }
tipoAlivio_p_alc = ['0','1','2','3','4','5','6','7','8','9','10','11','12','99']
tipoValvAnt_p_alc = ['0','1','2','3','4','5','6','7','99']
estadoRed_p_alc = ['0','1','2','3']
material_p_alc = ['1','2','3','4','5','6']
calidadDato_p_alc = ['0','1','2','3']
tipoSist_p_alc = ['0','1','2']
tieneConoReduc_p_alc = ['0','1','2','99']
materConoReduc_p_alc = ['0','1','2','3','4','22','99']
tipoConoReduc_p_alc = ['0','1','2', '99']
estadoConoReduc_p_alc = ['0','1','2','3','99']
inicialCuencas_p_alc = ['0','1', '2','99']
camaraSifon_p_alc = ['0','1','2','99']
estadoFisico_p_alc = ['0','1','2','3','4','5']
tieneCabezal_p_alc = ['0','1','2','99']
estadoTapa_p_alc = ['0','1','2','3','4','5','99']
estadoPozo_p_alc = ['0','1','2','3']
matEscalones_p_alc = ['0','1','2','3','4','99']
estadoEscalon_p_alc = ['0','1','2','3','4','5','6','99']
estadoCarge_p_alc = ['0','1','2','3','4','5','6','7','99']
estadoCilindro_p_alc = ['0','1','2','3','4','5','6','99']
estadoCanuela_p_alc = ['0','1','2','3','4','5','6','99']
estadoOperac_p_alc = ['0','1','2','3','4','5','99']
tipoInspec_p_alc = ['0','1','2', '99']
tipoAlmacen_p_alc = ['1','2','3','4']
tipoBomb_p_alc = ['0','1','2','3','99']
estadoRejilla_p_alc = ['0','1','2','3','4','5','99']
materialRejilla_p_alc = ['0','1','2','3','4','99']
origSeccion_p_alc = ['1','2','3','4','5']

dominios_p_alc = [subtipo_p_alc, tipoAlivio_p_alc, tipoValvAnt_p_alc, estadoRed_p_alc, material_p_alc, calidadDato_p_alc,
                  tipoSist_p_alc, tieneConoReduc_p_alc, materConoReduc_p_alc, tipoConoReduc_p_alc, estadoConoReduc_p_alc,
                  inicialCuencas_p_alc, camaraSifon_p_alc, estadoFisico_p_alc, tieneCabezal_p_alc, estadoTapa_p_alc,
                  estadoPozo_p_alc, matEscalones_p_alc, estadoEscalon_p_alc, estadoCarge_p_alc, estadoCilindro_p_alc,
                  estadoCanuela_p_alc, estadoOperac_p_alc, tipoInspec_p_alc, tipoAlmacen_p_alc, tipoBomb_p_alc,
                  estadoRejilla_p_alc, materialRejilla_p_alc, origSeccion_p_alc]

# ------------------------------------- VALIDACIONES LINEAS ACUEDUCTO -------------------------------------
# clasifica los tipos de linea de acueducto que puedo encontrarme
def clasif_l_ecu(l_acu, atrib_l_acu):
    clase_l = {'redMatriz_1':[], 'aduccion_2':[], 'conduccion_3':[], 'redMenor_4':[], 'lineaLat_5':[]}
    error_clase_l = []
    with arcpy.da.SearchCursor(l_acu,atrib_l_acu) as cursor:
        for linea in cursor:
            if linea[1] == 1:
                clase_l['redMatriz_1'].append(linea)
            elif linea[1] == 2:
                clase_l['aduccion_2'].append(linea)
            elif linea[1] == 3:
                clase_l['conduccion_3'].append(linea)
            elif linea[1] == 4:
                clase_l['redMenor_4'].append(linea)
            elif linea[1] == 5:
                clase_l['lineaLat_5'].append(linea)
            else:
                error_clase_l.append(linea[27])
    return clase_l, error_clase_l

# Valida los atributos que SI deberian estar en vacios pero que tienen algun valor (comisiones) - Linea Acueducto
def valida_no_blan_l_ecu(clase_l, orig):
    error_noBlan_l = {'ESTADOLEGAL':[], 'NOMBRE':[], 'COSTADO':[], 'T_SECCION':[], 'AREA_TR_M2':[], 'C_RASANTEI':[], 'C_RASANTEF':[],
    'C_CLAVEI':[], 'C_CLAVEF':[], 'PROFUNDIDAD':[], 'RUGOSIDAD':[], 'CODACTIVO_FIJO':[]}
    for red in clase_l:
        for line in clase_l[red]:
            if red in ('redMatriz_1', 'aduccion_2', 'conduccion_3'):
                if line[10] is not None and not (isinstance(line[10], str) and line[10].strip() == ''):
                    error_noBlan_l['ESTADOLEGAL'].append(line[27])
            if red in ('redMenor_4', 'lineaLat_5'):
                if line[15] is not None and not (isinstance(line[15], str) and line[15].strip() == ''):
                    error_noBlan_l['NOMBRE'].append(line[27])
            if red in ('aduccion_2', 'conduccion_3', 'lineaLat_5'):
                if line[16] is not None and not (isinstance(line[16], str) and line[16].strip() == ''):
                    error_noBlan_l['COSTADO'].append(line[27])
            if red in ('redMatriz_1', 'redMenor_4', 'lineaLat_5'):
                if line[17] is not None and not (isinstance(line[17], str) and line[17].strip() == ''):
                    error_noBlan_l['T_SECCION'].append(line[27])
                if (orig == 'shp' and (line[18] not in ('', None, 0))) or (orig == 'gdb' and (line[18] not in ('', None, 0))):
                    error_noBlan_l['AREA_TR_M2'].append(line[27])
                if (orig == 'shp' and (line[19] not in ('', None, 0))) or (orig == 'gdb' and (line[19] not in ('', None, 0))):
                    error_noBlan_l['C_RASANTEI'].append(line[27])
                if (orig == 'shp' and (line[20] not in ('', None, 0))) or (orig == 'gdb' and (line[20] not in ('', None, 0))):
                    error_noBlan_l['C_RASANTEF'].append(line[27])
                if (orig == 'shp' and (line[21] not in ('', None, 0))) or (orig == 'gdb' and (line[21] not in ('', None, 0))):
                    error_noBlan_l['C_CLAVEI'].append(line[27])
                if (orig == 'shp' and (line[22] not in ('', None, 0))) or (orig == 'gdb' and (line[22] not in ('', None, 0))):
                    error_noBlan_l['C_CLAVEF'].append(line[27])
            if red in ('aduccion_2', 'conduccion_3'):
                if (orig == 'shp' and (line[23] not in ('', None, 0))) or (orig == 'gdb' and (line[23] not in ('', None, 0))):
                    error_noBlan_l['PROFUNDIDAD'].append(line[27])
                if (orig == 'shp' and (line[24] not in ('', None, 0))) or (orig == 'gdb' and (line[24] not in ('', None, 0))):
                    error_noBlan_l['RUGOSIDAD'].append(line[27])
                if line[26] is not None and not (isinstance(line[26], str) and line[26].strip() == ''):
                    error_noBlan_l['CODACTIVO_FIJO'].append(line[27])
    return error_noBlan_l

# Valida los atributos que NO deberian estar en vacios pero estan si vacios (omisiones) - Linea Acueducto
def valida_blan_l_ecu(clase_l):
    error_blan_l = {'N_INICIAL':[],'N_FINAL':[],'FECHAINST':[], 'CONTRATO_ID':[],'NDISENO':[], 'AREA_TR_M2':[],'C_RASANTEI':[],
    'C_RASANTEF':[],'C_CLAVEI':[],'C_CLAVEF':[], 'PROFUNDIDAD':[],'RUGOSIDAD':[]}
    for red in clase_l:
        for line in clase_l[red]:
            if red in ('aduccion_2', 'conduccion_3'):
                if line[18] in ('', None):
                    error_blan_l['AREA_TR_M2'].append(line[27])
                if line[19] in ('', None):
                    error_blan_l['C_RASANTEI'].append(line[27])
                if line[20] in ('', None):
                    error_blan_l['C_RASANTEF'].append(line[27])
                if line[21] in ('', None):
                    error_blan_l['C_CLAVEI'].append(line[27])
                if line[22] in ('', None):
                    error_blan_l['C_CLAVEF'].append(line[27])
            if red in ('redMatriz_1', 'redMenor_4', 'lineaLat_5'):
                if line[23] in ('', None):
                    error_blan_l['PROFUNDIDAD'].append(line[27])
                if line[24] in ('', None):
                    error_blan_l['RUGOSIDAD'].append(line[27])
            if line[3] is None or (isinstance(line[3], str) and line[3].strip() == ''):
                error_blan_l['N_INICIAL'].append(line[27])
            if line[4] is None or (isinstance(line[4], str) and line[4].strip() == ''):
                error_blan_l['N_FINAL'].append(line[27])
            if line[5] in ('', None):
                error_blan_l['FECHAINST'].append(line[27])
            if line[13] is None or (isinstance(line[13], str) and line[13].strip() == ''):
                error_blan_l['CONTRATO_ID'].append(line[27])
            if line[14] is None or (isinstance(line[14], str) and line[14].strip() == ''):
                error_blan_l['NDISENO'].append(line[27])
    return error_blan_l

# Valida que los atributos de la capa de lineas acueducto cumplan con los dominios
def valida_dom_l_acu(clase_l):
    error_dom_l = {'SUBTIPO':[], 'ESTADOENRED':[], 'DIAMETRO':[], 'MATERIAL':[], 'CALIDADDEDATO':[], 'ESTADOLEGAL':[],
    'TIPOINSTALACION':[], 'COSTADO':[], 'T_SECCION':[]}
    for red in clase_l:
        for line in clase_l[red]:
            if red in ('redMatriz_1', 'redMenor_4'):
                if line[16] not in dominios_l[7]:
                    error_dom_l['COSTADO'].append(line[27])
            if red in ('redMenor_4', 'lineaLat_5'):
                if line[10] not in dominios_l[5]:
                    error_dom_l['ESTADOLEGAL'].append(line[27])
            if red in ('aduccion_2', 'conduccion_3'):
                if line[8] not in dominios_l[8]:
                    error_dom_l['T_SECCION'].append(line[27])
            if line[2] not in dominios_l[0][red]:
                error_dom_l['SUBTIPO'].append(line[27])
            if line[6] not in dominios_l[1]:
                error_dom_l['ESTADOENRED'].append(line[27])
            if line[7] not in dominios_l[2]:
                error_dom_l['DIAMETRO'].append(line[27])
            if line[8] not in dominios_l[3]:
                error_dom_l['MATERIAL'].append(line[27])
            if line[9] not in dominios_l[4]:
                error_dom_l['CALIDADDEDATO'].append(line[27])
            if line[12] not in dominios_l[6]:
                error_dom_l['TIPOINSTALACION'].append(line[27])
    return error_dom_l


# ------------------------------------- VALIDACIONES PUNTO ACUEDUCTO -------------------------------------
# clasifica los tipos de punto acueducto que puedo encontrarme
def clasif_p_acu(p_acu, atrib_p_acu):
    clase_p_acu = {'VALVULASISTEMA_1':[], 'VALVULACONTROL_2':[], 'ACCESORIO_CODO_3':[], 'ACCESORIO_REDUCCION_4':[], 'ACCESORIO_TAPON_5':[],
                     'ACCESORIO_TEE_6':[], 'ACCESORIO_UNION_7':[], 'ACCESORIO_OTROS_8':[],'HIDRANTE_9':[], 'MACROMEDIDOR_10':[],
                     'PUNTO_ACOMETIDA_11':[], 'PILA_MUESTREO_12':[], 'CAPTACION_13':[], 'DESARENADOR_14':[], 'PLANTA_TRATAMIENTO_15':[],
                     'ESTACION_BOMBEO_16':[], 'TANQUE_17':[], 'PORTAL_18':[], 'CAMARA_ACCESO_19':[], 'ESTRUCTURA_CONTROL_20':[],
                     'INSTRUMENTOS_MEDICION_21':[]}
    error_clase_p_acu = []
    with arcpy.da.SearchCursor(p_acu,atrib_p_acu) as cursor:
        for punto in cursor:
            if punto[1] == 1:
                clase_p_acu['VALVULASISTEMA_1'].append(punto)
            elif punto[1] == 2:
                clase_p_acu['VALVULACONTROL_2'].append(punto)
            elif punto[1] == 3:
                clase_p_acu['ACCESORIO_CODO_3'].append(punto)
            elif punto[1] == 4:
                clase_p_acu['ACCESORIO_REDUCCION_4'].append(punto)
            elif punto[1] == 5:
                clase_p_acu['ACCESORIO_TAPON_5'].append(punto)
            elif punto[1] == 6:
                clase_p_acu['ACCESORIO_TEE_6'].append(punto)
            elif punto[1] == 7:
                clase_p_acu['ACCESORIO_UNION_7'].append(punto)
            elif punto[1] == 8:
                clase_p_acu['ACCESORIO_OTROS_8'].append(punto)
            elif punto[1] == 9:
                clase_p_acu['HIDRANTE_9'].append(punto)
            elif punto[1] == 10:
                clase_p_acu['MACROMEDIDOR_10'].append(punto)
            elif punto[1] == 11:
                clase_p_acu['PUNTO_ACOMETIDA_11'].append(punto)
            elif punto[1] == 12:
                clase_p_acu['PILA_MUESTREO_12'].append(punto)
            elif punto[1] == 13:
                clase_p_acu['CAPTACION_13'].append(punto)
            elif punto[1] == 14:
                clase_p_acu['DESARENADOR_14'].append(punto)
            elif punto[1] == 15:
                clase_p_acu['PLANTA_TRATAMIENTO_15'].append(punto)
            elif punto[1] == 16:
                clase_p_acu['ESTACION_BOMBEO_16'].append(punto)
            elif punto[1] == 17:
                clase_p_acu['TANQUE_17'].append(punto)
            elif punto[1] == 18:
                clase_p_acu['PORTAL_18'].append(punto)
            elif punto[1] == 19:
                clase_p_acu['CAMARA_ACCESO_19'].append(punto)
            elif punto[1] == 20:
                clase_p_acu['ESTRUCTURA_CONTROL_20'].append(punto)
            elif punto[1] == 21:
                clase_p_acu['INSTRUMENTOS_MEDICION_21'].append(punto)
            else:
                error_clase_p_acu.append(punto[76])
    return clase_p_acu, error_clase_p_acu

# Valida los atributos que SI deberian estar en vacios pero que tienen algun valor (comisiones) - PUNTOS ACUEDUCTO
def valida_no_blan_p_acu(clase_p_acu, orig):
    error_noBlan_p_acu = {'ROTACION':[], 'C_RASANTE':[], 'PROFUN':[], 'MATERIAL':[], 'TIPOESPPUB':[], 'MATESPPUBL':[], 
                          'AUTOMATIZA':[], 'DIAMETRO1':[], 'DIAMETRO2':[], 'SENTIDOOPERAC':[], 'ESTADOOPERAC':[],
                          'TIPOOPERAC':[], 'ESTADOFIS_VAL':[], 'TIPOVALVUL':[], 'VUELTASCIE':[], 'CLASEACCES':[],
                          'ESTADOFISICOH':[], 'MARCA':[] , 'FUNCIONPIL':[] , 'ESTADOMED':[], 'SECTORENTR':[],
                          'SECTORSALI':[],'IDTUBERIAMEDIDA':[],'CAUDAL_PROMEDIO':[],'TIPO_M':[],'FECHA_TOMA_C':[],
                          'UBICACCAJI':[],'CENTRO':[],'L_ALM':[],'AREARESP':[],'TIPO_MUESTR':[],'FUENTEABAS':[],
                          'UBICAC_MUES':[], 'PTOANALISI':[] ,'LOCPUNTO':[] ,'ESTADO':[] ,'FECHAESTADO':[], 'CLASEPUNTO':[],
                          'NROFILTROS':[],'NROSEDIMEN':[],'NROCOMPART':[],'NROMEZCLAR':[],'NROFLOCULA':[],'CAPACINSTA':[],
                          'NROBOMBAS':[], 'CAPABOMBEO':[] ,'COTABOMBEO':[] ,'ALTURADINA':[] ,'COTAFONDO':[] ,
                          'COTAREBOSE':[],'CAPACIDAD':[], 'NIVELMAXIM':[], 'NIVELMINIM':[],'AREATRANSV':[], 'TIENEVIGIL':[],
                          'OPERACTANQ':[] ,'TIPOACCESO':[] ,'DIAMETROAC':[] ,'NOMBRE':[],'DIRECCION':[],'PRESION':[]}

    for tip_p in clase_p_acu:
        for punto in clase_p_acu[tip_p]:
            if tip_p in ('INSTRUMENTOS_MEDICION_21'):
                if punto[10] not in ('', None, 0):
                    error_noBlan_p_acu['ROTACION'].append(punto[76])
            if tip_p in ('MACROMEDIDOR_10'):
                if punto[11] not in ('', None, 0):
                    error_noBlan_p_acu['C_RASANTE'].append(punto[76])
            if tip_p in ('HIDRANTE_9', 'MACROMEDIDOR_10', 'PUNTO_ACOMETIDA_11', 'PILA_MUESTREO_12', 'CAPTACION_13',
                         'DESARENADOR_14', 'PLANTA_TRATAMIENTO_15', 'ESTACION_BOMBEO_16', 'TANQUE_17', 'PORTAL_18',
                         'ESTRUCTURA_CONTROL_20', 'INSTRUMENTOS_MEDICION_21'):
                if punto[12] not in ('', None, 0):
                    error_noBlan_p_acu['PROFUN'].append(punto[76])
            if tip_p in ( 'MACROMEDIDOR_10', 'PUNTO_ACOMETIDA_11', 'CAPTACION_13', 'DESARENADOR_14', 'PLANTA_TRATAMIENTO_15',
                     'ESTACION_BOMBEO_16', 'TANQUE_17', 'PORTAL_18', 'CAMARA_ACCESO_19', 'ESTRUCTURA_CONTROL_20',
                     'INSTRUMENTOS_MEDICION_21'):
                if punto[13] is not None and not (isinstance(punto[13], str) and punto[13].strip() == ''):
                    error_noBlan_p_acu['MATERIAL'].append(punto[76])
            if tip_p not in('VALVULASISTEMA_1', 'VALVULACONTROL_2', 'HIDRANTE_9', 'PILA_MUESTREO_12'):
                if punto[18] is not None and not (isinstance(punto[18], str) and punto[18].strip() == ''):
                    error_noBlan_p_acu['TIPOESPPUB'].append(punto[76])
                if punto[19] is not None and not (isinstance(punto[19], str) and punto[19].strip() == ''):
                    error_noBlan_p_acu['MATESPPUBL'].append(punto[76])
            if tip_p not in ('VALVULASISTEMA_1', 'VALVULACONTROL_2'):
                if (orig == 'shp' and (punto[20] not in ('', None, 0))) or (orig == 'gdb' and (punto[20] not in ('', None, 0))):
                    error_noBlan_p_acu['AUTOMATIZA'].append(punto[76])
                if punto[23] is not None and not (isinstance(punto[23], str) and punto[23].strip() == ''):
                    error_noBlan_p_acu['SENTIDOOPERAC'].append(punto[76])
                if punto[24] is not None and not (isinstance(punto[24], str) and punto[24].strip() == ''):
                    error_noBlan_p_acu['ESTADOOPERAC'].append(punto[76])
                if punto[25] is not None and not (isinstance(punto[25], str) and punto[25].strip() == ''):
                    error_noBlan_p_acu['TIPOOPERAC'].append(punto[76])
                if punto[26] is not None and not (isinstance(punto[26], str) and punto[26].strip() == ''):
                    error_noBlan_p_acu['ESTADOFIS_VAL'].append(punto[76])
            
            if tip_p in ('MACROMEDIDOR_10', 'PUNTO_ACOMETIDA_11', 'PILA_MUESTREO_12', 'CAPTACION_13', 'DESARENADOR_14',
                         'PLANTA_TRATAMIENTO_15', 'ESTACION_BOMBEO_16', 'TANQUE_17', 'PORTAL_18', 'CAMARA_ACCESO_19',
                         'ESTRUCTURA_CONTROL_20'):
                if punto[21] is not None and not (isinstance(punto[21], str) and punto[21].strip() == ''):
                    error_noBlan_p_acu['DIAMETRO1'].append(punto[76])
            if tip_p in ('VALVULASISTEMA_1', 'VALVULACONTROL_2', 'HIDRANTE_9', 'MACROMEDIDOR_10', 'PUNTO_ACOMETIDA_11',
                         'PILA_MUESTREO_12', 'CAPTACION_13', 'DESARENADOR_14', 'PLANTA_TRATAMIENTO_15',
                         'ESTACION_BOMBEO_16', 'TANQUE_17', 'PORTAL_18', 'CAMARA_ACCESO_19', 'ESTRUCTURA_CONTROL_20',
                         'INSTRUMENTOS_MEDICION_21'):
                if punto[22] is not None and not (isinstance(punto[22], str) and punto[22].strip() == ''):
                    error_noBlan_p_acu['DIAMETRO2'].append(punto[76])
            if tip_p not in ('VALVULASISTEMA_1'):
                if punto[27] is not None and not (isinstance(punto[27], str) and punto[27].strip() == ''):
                    error_noBlan_p_acu['TIPOVALVUL'].append(punto[76])
                if (orig == 'shp' and (punto[28] not in ('', None, 0))) or (orig == 'gdb' and (punto[28] not in ('', None, 0))):
                    error_noBlan_p_acu['VUELTASCIE'].append(punto[76])
            if tip_p not in ('ACCESORIO_CODO_3', 'ACCESORIO_REDUCCION_4', 'ACCESORIO_TAPON_5', 'ACCESORIO_TEE_6',
                         'ACCESORIO_UNION_7'):
                if punto[29] is not None and not (isinstance(punto[29], str) and punto[29].strip() == ''):
                    error_noBlan_p_acu['CLASEACCES'].append(punto[76])
            if tip_p not in ('HIDRANTE_9'):
                if punto[30] is not None and not (isinstance(punto[30], str) and punto[30].strip() == ''):
                    error_noBlan_p_acu['ESTADOFISICOH'].append(punto[76])
                if punto[32] not in ('', None, 0):
                    error_noBlan_p_acu['FUNCIONPIL'].append(punto[76])
                if punto[74] not in ('', None, 0):
                    error_noBlan_p_acu['PRESION'].append(punto[76])
            if tip_p not in ('HIDRANTE_9', 'INSTRUMENTOS_MEDICION_21'):
                if punto[31] is not None and not (isinstance(punto[31], str) and punto[31].strip() == ''):
                    error_noBlan_p_acu['MARCA'].append(punto[76])
            if tip_p not in ('MACROMEDIDOR_10'):
                if punto[33] is not None and not (isinstance(punto[33], str) and punto[33].strip() == ''):
                    error_noBlan_p_acu['ESTADOMED'].append(punto[76])
                if punto[34] is not None and not (isinstance(punto[34], str) and punto[34].strip() == ''):
                    error_noBlan_p_acu['SECTORENTR'].append(punto[76])
                if punto[35] is not None and not (isinstance(punto[35], str) and punto[35].strip() == ''):
                    error_noBlan_p_acu['SECTORSALI'].append(punto[76])
                if punto[36] is not None and not (isinstance(punto[36], str) and punto[36].strip() == ''):
                    error_noBlan_p_acu['IDTUBERIAMEDIDA'].append(punto[76])
                if punto[38] is not None and not (isinstance(punto[38], str) and punto[38].strip() == ''):
                    error_noBlan_p_acu['TIPO_M'].append(punto[76])
                if punto[39] not in ('', None) and tip_p != 'HIDRANTE_9':
                    error_noBlan_p_acu['FECHA_TOMA_C'].append(punto[76])
            if tip_p not in ('MACROMEDIDOR_10', 'INSTRUMENTOS_MEDICION_21'):
                if punto[37] not in ('', None, 0):
                    error_noBlan_p_acu['CAUDAL_PROMEDIO'].append(punto[76])
            if tip_p not in ('PUNTO_ACOMETIDA_11'):
                if punto[40] is not None and not (isinstance(punto[40], str) and punto[40].strip() == ''):
                    error_noBlan_p_acu['UBICACCAJI'].append(punto[76])
            if tip_p not in ('PILA_MUESTREO_12'):
                if punto[41] is not None and not (isinstance(punto[41], str) and punto[41].strip() == ''):
                    error_noBlan_p_acu['CENTRO'].append(punto[76])
                if punto[42] is not None and not (isinstance(punto[42], str) and punto[42].strip() == ''):
                    error_noBlan_p_acu['L_ALM'].append(punto[76])
                if punto[43] is not None and not (isinstance(punto[43], str) and punto[43].strip() == ''):
                    error_noBlan_p_acu['AREARESP'].append(punto[76])
                if punto[44] is not None and not (isinstance(punto[44], str) and punto[44].strip() == ''):
                    error_noBlan_p_acu['TIPO_MUESTR'].append(punto[76])
                if punto[45] is not None and not (isinstance(punto[45], str) and punto[45].strip() == ''):
                    error_noBlan_p_acu['FUENTEABAS'].append(punto[76])
                if punto[46] is not None and not (isinstance(punto[46], str) and punto[46].strip() == ''):
                    error_noBlan_p_acu['UBICAC_MUES'].append(punto[76])
                if punto[47] is not None and not (isinstance(punto[47], str) and punto[47].strip() == ''):
                    error_noBlan_p_acu['PTOANALISI'].append(punto[76])
                if punto[48] is not None and not (isinstance(punto[48], str) and punto[48].strip() == ''):
                    error_noBlan_p_acu['LOCPUNTO'].append(punto[76])
                if punto[49] is not None and not (isinstance(punto[49], str) and punto[49].strip() == ''):
                    error_noBlan_p_acu['ESTADO'].append(punto[76])
                if punto[50] not in ('', None):
                    error_noBlan_p_acu['FECHAESTADO'].append(punto[76])
                if punto[51] is not None and not (isinstance(punto[51], str) and punto[51].strip() == ''):
                    error_noBlan_p_acu['CLASEPUNTO'].append(punto[76])
            if tip_p not in ('PLANTA_TRATAMIENTO_15'):
                if (orig == 'shp' and (punto[52] not in ('', None, 0))) or (orig == 'gdb' and (punto[52] not in ('', None, 0))):
                    error_noBlan_p_acu['NROFILTROS'].append(punto[76])
                if (orig == 'shp' and (punto[53] not in ('', None, 0))) or (orig == 'gdb' and (punto[53] not in ('', None, 0))):
                    error_noBlan_p_acu['NROSEDIMEN'].append(punto[76])
                if (orig == 'shp' and (punto[54] not in ('', None, 0))) or (orig == 'gdb' and (punto[54] not in ('', None, 0))):
                    error_noBlan_p_acu['NROCOMPART'].append(punto[76])
                if (orig == 'shp' and (punto[55] not in ('', None, 0))) or (orig == 'gdb' and (punto[55] not in ('', None, 0))):
                    error_noBlan_p_acu['NROMEZCLAR'].append(punto[76])
                if (orig == 'shp' and (punto[56] not in ('', None, 0))) or (orig == 'gdb' and (punto[56] not in ('', None, 0))):
                    error_noBlan_p_acu['NROFLOCULA'].append(punto[76])
                if punto[57] not in ('', None, 0):
                    error_noBlan_p_acu['CAPACINSTA'].append(punto[76])
            if tip_p not in ('ESTACION_BOMBEO_16'):
                if (orig == 'shp' and (punto[58] not in ('', None, 0))) or (orig == 'gdb' and (punto[58] not in ('', None, 0))):
                    error_noBlan_p_acu['NROBOMBAS'].append(punto[76])
                if punto[59] not in ('', None, 0):
                    error_noBlan_p_acu['CAPABOMBEO'].append(punto[76])
                if punto[60] not in ('', None, 0):
                    error_noBlan_p_acu['COTABOMBEO'].append(punto[76])
                if punto[61] not in ('', None, 0):
                    error_noBlan_p_acu['ALTURADINA'].append(punto[76])
            if tip_p not in ('TANQUE_17'):
                if punto[62] not in ('', None, 0):
                    error_noBlan_p_acu['COTAFONDO'].append(punto[76])
                if punto[63] not in ('', None, 0):
                    error_noBlan_p_acu['COTAREBOSE'].append(punto[76])
                if punto[64] not in ('', None, 0):
                    error_noBlan_p_acu['CAPACIDAD'].append(punto[76])
                if punto[65] not in ('', None, 0):
                    error_noBlan_p_acu['NIVELMAXIM'].append(punto[76])
                if punto[66] not in ('', None, 0):
                    error_noBlan_p_acu['NIVELMINIM'].append(punto[76])
                if punto[67] not in ('', None, 0):
                    error_noBlan_p_acu['AREATRANSV'].append(punto[76])
                if punto[68] not in ('', None, 0):
                    error_noBlan_p_acu['TIENEVIGIL'].append(punto[76])
                if punto[69] is not None and not (isinstance(punto[69], str) and punto[69].strip() == ''):

                    error_noBlan_p_acu['OPERACTANQ'].append(punto[76])
            if tip_p not in ('CAMARA_ACCESO_19'):
                if punto[70] is not None and not (isinstance(punto[70], str) and punto[70].strip() == ''):
                    error_noBlan_p_acu['TIPOACCESO'].append(punto[76])
                if punto[71] not in ('', None, 0):
                    error_noBlan_p_acu['DIAMETROAC'].append(punto[76])
            if tip_p not in ('PILA_MUESTREO_12', 'CAPTACION_13', 'DESARENADOR_14', 'PLANTA_TRATAMIENTO_15',
                     'ESTACION_BOMBEO_16', 'TANQUE_17', 'PORTAL_18', 'CAMARA_ACCESO_19', 'ESTRUCTURA_CONTROL_20'):
                if punto[72] is not None and not (isinstance(punto[72], str) and punto[72].strip() == ''):
                    error_noBlan_p_acu['NOMBRE'].append(punto[76])
            if tip_p not in ('VALVULASISTEMA_1', 'VALVULACONTROL_2', 'HIDRANTE_9', 'MACROMEDIDOR_10', 'PUNTO_ACOMETIDA_11',
                         'PILA_MUESTREO_12', 'CAPTACION_13', 'DESARENADOR_14', 'PLANTA_TRATAMIENTO_15',
                         'ESTACION_BOMBEO_16', 'TANQUE_17', 'PORTAL_18', 'CAMARA_ACCESO_19', 'ESTRUCTURA_CONTROL_20'):
                if punto[73] is not None and not (isinstance(punto[73], str) and punto[73].strip() == ''):
                    error_noBlan_p_acu['DIRECCION'].append(punto[76])
    return error_noBlan_p_acu

# Valida los atributos que NO deberian estar en vacios pero estan si vacios (omisiones) - PUNTOS ACUEDUCTO
def valida_blan_p_acu(clase_p_acu):
    error_blan_p_acu = {'IDENTIFIC':[], 'NORTE':[], 'ESTE':[], 'FECHAINST':[], 'LOCALIZACIONRELATIVA':[], 
                        'ROTACION':[], 'C_RASANTE':[], 'PROFUN':[], 'CONTRATO_ID':[], 'VUELTASCIE':[],
                        'MARCA':[], 'ESTADOMED':[], 'SECTORENTR':[], 'SECTORSALI':[], 'IDTUBERIAMEDIDA':[],
                        'CAUDAL_PRO':[], 'TIPO_M':[], 'FECHA_TOMA':[], 'CENTRO':[], 'L_ALM':[], 'AREARESP':[],
                        'FECHAESTADO':[], 'NROFILTROS':[], 'NROSEDIMEN':[], 'NROCOMPART':[],
                        'NROMEZCLAR':[], 'NROFLOCULA':[], 'CAPACINSTA':[], 'NROBOMBAS':[], 'CAPABOMBEO':[],
                        'COTABOMBEO':[], 'ALTURADINA':[], 'COTAFONDO':[], 'COTAREBOSE':[], 'CAPACIDAD':[],
                        'NIVELMAXIM':[], 'NIVELMINIM':[], 'AREATRANSV':[], 'DIAMETROAC':[],'DIRECCION':[],
                        'PRESION':[]}
    for tip_p in clase_p_acu:
        for punto in clase_p_acu[tip_p]:
            if tip_p not in ('INSTRUMENTOS_MEDICION_21'):
                if punto[10] in ('', None):
                    error_blan_p_acu['ROTACION'].append(punto[76])
            if tip_p not in ('MACROMEDIDOR_10'):
                if punto[11] in ('', None):
                    error_blan_p_acu['C_RASANTE'].append(punto[76])
            if tip_p in ('VALVULASISTEMA_1', 'VALVULACONTROL_2', 'ACCESORIO_CODO_3', 'ACCESORIO_REDUCCION_4', 'ACCESORIO_TAPON_5',
                     'ACCESORIO_TEE_6', 'ACCESORIO_UNION_7', 'ACCESORIO_OTROS_8'):
                if punto[12] in ('', None):
                    error_blan_p_acu['PROFUN'].append(punto[76])
            if tip_p in ('VALVULASISTEMA_1'):
                if punto[28] in ('', None):
                    error_blan_p_acu['VUELTASCIE'].append(punto[76])
            if tip_p in ('HIDRANTE_9', 'INSTRUMENTOS_MEDICION_21'):
                if punto[31] is None or (isinstance(punto[31], str) and punto[31].strip() == ''):
                    error_blan_p_acu['MARCA'].append(punto[76])
            if tip_p in ('MACROMEDIDOR_10'):
                if punto[33] is None or (isinstance(punto[33], str) and punto[33].strip() == ''):
                    error_blan_p_acu['ESTADOMED'].append(punto[76])
                if punto[34] is None or (isinstance(punto[34], str) and punto[34].strip() == ''):
                    error_blan_p_acu['SECTORENTR'].append(punto[76])
                if punto[35] is None or (isinstance(punto[35], str) and punto[35].strip() == ''):
                    error_blan_p_acu['SECTORSALI'].append(punto[76])
                if punto[36] is None or (isinstance(punto[36], str) and punto[36].strip() == ''):
                    error_blan_p_acu['IDTUBERIAMEDIDA'].append(punto[76])
                if punto[38] is None or (isinstance(punto[38], str) and punto[38].strip() == ''):
                    error_blan_p_acu['TIPO_M'].append(punto[76])
                if punto[39] in ('', None):
                    error_blan_p_acu['FECHA_TOMA'].append(punto[76])
            if tip_p in ('MACROMEDIDOR_10', 'INSTRUMENTOS_MEDICION_21'):
                if punto[37] in ('', None):
                    error_blan_p_acu['CAUDAL_PRO'].append(punto[76])
            if tip_p in ('PILA_MUESTREO_12'):
                if punto[41] is None or (isinstance(punto[41], str) and punto[41].strip() == ''):
                    error_blan_p_acu['CENTRO'].append(punto[76])
                if punto[42] is None or (isinstance(punto[42], str) and punto[42].strip() == ''):
                    error_blan_p_acu['L_ALM'].append(punto[76])
                if punto[43] is None or (isinstance(punto[43], str) and punto[43].strip() == ''):
                    error_blan_p_acu['AREARESP'].append(punto[76])
                if punto[50] in ('', None):
                    error_blan_p_acu['FECHAESTADO'].append(punto[76])
            if tip_p in ('PLANTA_TRATAMIENTO_15'):
                if punto[52] in ('', None):
                    error_blan_p_acu['NROFILTROS'].append(punto[76])
                if punto[53] in ('', None):
                    error_blan_p_acu['NROSEDIMEN'].append(punto[76])
                if punto[54] in ('', None):
                    error_blan_p_acu['NROCOMPART'].append(punto[76])
                if punto[55] in ('', None):
                    error_blan_p_acu['NROMEZCLAR'].append(punto[76])
                if punto[56] in ('', None):
                    error_blan_p_acu['NROFLOCULA'].append(punto[76])
                if punto[57] in ('', None):
                    error_blan_p_acu['CAPACINSTA'].append(punto[76])
            if tip_p in ('ESTACION_BOMBEO_16'):
                if punto[58] in ('', None):
                    error_blan_p_acu['NROBOMBAS'].append(punto[76])
                if punto[59] in ('', None):
                    error_blan_p_acu['CAPABOMBEO'].append(punto[76])
                if punto[60] in ('', None):
                    error_blan_p_acu['COTABOMBEO'].append(punto[76])
                if punto[61] in ('', None):
                    error_blan_p_acu['ALTURADINA'].append(punto[76])
            if tip_p in ('TANQUE_17'):
                if punto[62] in ('', None):
                    error_blan_p_acu['COTAFONDO'].append(punto[76])
                if punto[63] in ('', None):
                    error_blan_p_acu['COTAREBOSE'].append(punto[76])
                if punto[64] in ('', None):
                    error_blan_p_acu['CAPACIDAD'].append(punto[76])
                if punto[65] in ('', None):
                    error_blan_p_acu['NIVELMAXIM'].append(punto[76])
                if punto[66] in ('', None):
                    error_blan_p_acu['NIVELMINIM'].append(punto[76])
                if punto[67] in ('', None):
                    error_blan_p_acu['AREATRANSV'].append(punto[76])
            if tip_p in ('CAMARA_ACCESO_19'):
                if punto[71] in ('', None):
                    error_blan_p_acu['DIAMETROAC'].append(punto[76])
            if tip_p not in ('ACCESORIO_CODO_3', 'ACCESORIO_REDUCCION_4', 'ACCESORIO_TAPON_5', 'ACCESORIO_TEE_6',
                         'ACCESORIO_UNION_7', 'ACCESORIO_OTROS_8', 'INSTRUMENTOS_MEDICION_21'):
                if punto[73] is None or (isinstance(punto[73], str) and punto[73].strip() == ''):
                    error_blan_p_acu['DIRECCION'].append(punto[76])
            if tip_p in ('HIDRANTE_9'):
                if punto[74] in ('', None):
                    error_blan_p_acu['PRESION'].append(punto[76])
           
            if punto[13] is None or (isinstance(punto[13], str) and punto[13].strip() == ''):
                error_blan_p_acu['IDENTIFIC'].append(punto[76])
            if punto[4] in ('', None, 0):
                error_blan_p_acu['NORTE'].append(punto[76])
            if punto[5] in ('', None, 0):
                error_blan_p_acu['ESTE'].append(punto[76])
            if punto[6] in ('', None):
                error_blan_p_acu['FECHAINST'].append(punto[76])
            if punto[8] is None or (isinstance(punto[8], str) and punto[8].strip() == ''):
                error_blan_p_acu['LOCALIZACIONRELATIVA'].append(punto[76])
            if punto[16] is None or (isinstance(punto[16], str) and punto[16].strip() == ''):
                error_blan_p_acu['CONTRATO_ID'].append(punto[76])

    return error_blan_p_acu

# Valida que los atributos de la capa de lineas acueducto cumplan con los dominios
def valida_dom_p_acu(clase_p_acu):
    error_dom_p_acu = {'SUBTIPO':[], 'ESTADOENRED':[], 'CALIDADDATO':[], 'MATERIAL':[], 'TIPOESPPUB':[], 
                       'MATESPPUBL':[], 'AUTOMATIZA':[], 'DIAMETRO1':[], 'DIAMETRO2':[], 'SENTIDOOPERAC':[],
                       'ESTADOOPERAC':[], 'TIPOOPERAC':[], 'ESTADOFIS_VAL':[], 'TIPOVALVUL':[], 'CLASEACCES':[],
                       'ESTADOFISICOH':[], 'FUNCIONPIL':[], 'UBICACCAJI':[], 'TIPO_MUESTR':[], 'FUENTEABAS':[],
                       'UBICAC_MUES':[], 'PTOANALISI':[], 'LOCPUNTO':[], 'ESTADO':[], 'CLASEPUNTO':[],
                       'TIENEVIGIL':[], 'OPERACTANQ':[], 'TIPOACCESO':[]}
    for tip_p in clase_p_acu:
        for punto in clase_p_acu[tip_p]:
            if tip_p in ('VALVULASISTEMA_1', 'VALVULACONTROL_2', 'ACCESORIO_CODO_3', 'ACCESORIO_REDUCCION_4',
                         'ACCESORIO_TAPON_5', 'ACCESORIO_TEE_6', 'ACCESORIO_UNION_7', 'ACCESORIO_OTROS_8',
                         'HIDRANTE_9', 'PILA_MUESTREO_12'):
                if punto[13] not in dominios_p_acu[3]:
                    error_dom_p_acu['MATERIAL'].append(punto[76])
            if tip_p in ('VALVULASISTEMA_1', 'VALVULACONTROL_2', 'HIDRANTE_9', 'PILA_MUESTREO_12'):
                if punto[18] not in dominios_p_acu[4]:
                    error_dom_p_acu['TIPOESPPUB'].append(punto[76])
                if punto[19] not in dominios_p_acu[5]:
                    error_dom_p_acu['MATESPPUBL'].append(punto[76])
            if tip_p in ('VALVULASISTEMA_1', 'VALVULACONTROL_2'):
                if punto[20] not in dominios_p_acu[6]:
                    error_dom_p_acu['AUTOMATIZA'].append(punto[76])
            if tip_p in ('VALVULASISTEMA_1', 'VALVULACONTROL_2', 'ACCESORIO_CODO_3', 'ACCESORIO_REDUCCION_4',
                         'ACCESORIO_TAPON_5', 'ACCESORIO_TEE_6', 'ACCESORIO_UNION_7', 'ACCESORIO_OTROS_8',
                         'HIDRANTE_9', 'PILA_MUESTREO_12', 'INSTRUMENTOS_MEDICION_21'):
                if punto[21] not in dominios_p_acu[7]:
                    error_dom_p_acu['DIAMETRO1'].append(punto[76])
            if tip_p in ('ACCESORIO_CODO_3', 'ACCESORIO_REDUCCION_4','ACCESORIO_TAPON_5', 'ACCESORIO_TEE_6',
                         'ACCESORIO_UNION_7', 'ACCESORIO_OTROS_8'):
                if punto[22] not in dominios_p_acu[7]:
                    error_dom_p_acu['DIAMETRO2'].append(punto[76])
            if tip_p in ('VALVULASISTEMA_1', 'VALVULACONTROL_2'):
                if str(punto[23]) not in dominios_p_acu[8]:
                    error_dom_p_acu['SENTIDOOPERAC'].append(punto[76])
                if punto[24] not in dominios_p_acu[9]:
                    error_dom_p_acu['ESTADOOPERAC'].append(punto[76])
                if punto[25] not in dominios_p_acu[10]:
                    error_dom_p_acu['TIPOOPERAC'].append(punto[76])
                if punto[26] not in dominios_p_acu[11]:
                    error_dom_p_acu['ESTADOFIS_VAL'].append(punto[76])
            if tip_p in ('VALVULASISTEMA_1'):
                if punto[27] not in dominios_p_acu[12]:
                    error_dom_p_acu['TIPOVALVUL'].append(punto[76])
            if tip_p in ('ACCESORIO_CODO_3', 'ACCESORIO_REDUCCION_4','ACCESORIO_TAPON_5', 'ACCESORIO_TEE_6',
                         'ACCESORIO_UNION_7'):
                if punto[29] not in dominios_p_acu[13][tip_p]:
                    error_dom_p_acu['CLASEACCES'].append(punto[76])
            if tip_p in ('HIDRANTE_9'):
                if punto[30] not in dominios_p_acu[14]:
                    error_dom_p_acu['ESTADOFISICOH'].append(punto[76])
                if punto[32] not in dominios_p_acu[15]:
                    error_dom_p_acu['FUNCIONPIL'].append(punto[76])
            if tip_p in ('PUNTO_ACOMETIDA_11'):
                if punto[40] not in dominios_p_acu[16]:
                    error_dom_p_acu['UBICACCAJI'].append(punto[76])
            if tip_p in ('PILA_MUESTREO_12'):
                if punto[44] not in dominios_p_acu[17]:
                    error_dom_p_acu['TIPO_MUESTR'].append(punto[76])
                if punto[45] not in dominios_p_acu[18]:
                    error_dom_p_acu['FUENTEABAS'].append(punto[76])
                if punto[46] not in dominios_p_acu[19]:
                    error_dom_p_acu['UBICAC_MUES'].append(punto[76])
                if punto[47] not in dominios_p_acu[20]:
                    error_dom_p_acu['PTOANALISI'].append(punto[76])
                if punto[48] not in dominios_p_acu[21]:
                    error_dom_p_acu['LOCPUNTO'].append(punto[76])
                if punto[49] not in dominios_p_acu[22]:
                    error_dom_p_acu['ESTADO'].append(punto[76])
                if punto[51] not in dominios_p_acu[23]:
                    error_dom_p_acu['CLASEPUNTO'].append(punto[76])
            if tip_p in ('TANQUE_17'):
                if punto[68] not in dominios_p_acu[24]:
                    error_dom_p_acu['TIENEVIGIL'].append(punto[76])
                if punto[69] not in dominios_p_acu[25]:
                    error_dom_p_acu['OPERACTANQ'].append(punto[76])
            if tip_p in ('CAMARA_ACCESO_19'):
                if punto[70] not in dominios_p_acu[26]:
                    error_dom_p_acu['TIPOACCESO'].append(punto[76])

            # validando los dominios generales
            if punto[2] not in dominios_p_acu[0][tip_p]:
                error_dom_p_acu['SUBTIPO'].append(punto[76])
            if punto[7] not in dominios_p_acu[1]:
                error_dom_p_acu['ESTADOENRED'].append(punto[76])
            if punto[9] not in dominios_p_acu[2]:
                error_dom_p_acu['CALIDADDATO'].append(punto[76])
                
    return error_dom_p_acu


# ------------------------------------- VALIDACIONES LINEAS ALCANTARILLADO -------------------------------------
# clasifica los tipos de linea alcantarillado que puedo encontrarme
def clasif_l_alc(l_alc, atrib_l_alc):
    clase_l_alc = {'redLocal_1':[], 'redTroncal_2':[], 'linLat_3':[]}
    error_clase_l_alc = []
    with arcpy.da.SearchCursor(l_alc,atrib_l_alc) as cursor:
        for line in cursor:
            if line[1] == 1:
                clase_l_alc['redLocal_1'].append(line)
            elif line[1] == 2:
                clase_l_alc['redTroncal_2'].append(line)
            elif line[1] == 3:
                clase_l_alc['linLat_3'].append(line)
            else:
                error_clase_l_alc.append(line[42])
    return clase_l_alc, error_clase_l_alc

# Valida los atributos que SI deberian estar en vacios pero que tienen algun valor (comisiones) - LINEAS ALCANTARILLADO
def valida_noBlan_l_alc(clase_l_alc, orig):
    error_noBlan_l_alc = {'MATERIAL2':[], 'T_SECCION':[], 'CAM_CAIDA':[], 'INSTALACI':[], 'MATESPPUBL':[], 'PENDIENTE':[],
                          'NOMBRE':[], 'BASE':[], 'PROFUNDIDAD':[], 'NROCONDUCTOS':[], 'ALTURA1':[], 'ALTURA2':[],
                          'ANCHOBERMA':[], 'TALUD1':[], 'TALUD2':[], 'TIPOINSPEC':[], 'GRADOEST':[], 'GRADOOPER':[]}
    for red in clase_l_alc:
        for line in clase_l_alc[red]:
            if red in ('linLat_3'):
                if line[8] is not None and not (isinstance(line[8], str) and line[8].strip() == ''):
                    error_noBlan_l_alc['MATERIAL2'].append(line[42])
                if line[12] is not None and not (isinstance(line[12], str) and line[12].strip() == ''):
                    error_noBlan_l_alc['T_SECCION'].append(line[42])
                if line[35] is not None and not (isinstance(line[35], str) and line[35].strip() == ''):
                    error_noBlan_l_alc['INSTALACI'].append(line[42])
                if line[36] is not None and not (isinstance(line[36], str) and line[36].strip() == ''):
                    error_noBlan_l_alc['MATESPPUBL'].append(line[42])
                if (orig == 'shp' and (line[27] not in ('', None, 0))) or (orig == 'gdb' and (line[27] not in ('', None, 0))):
                    error_noBlan_l_alc['PROFUNDIDAD'].append(line[42])
                if line[24] not in ('', None, 0):
                    error_noBlan_l_alc['PENDIENTE'].append(line[42])
                if (orig == 'shp' and (line[30] not in ('', None, 0))) or (orig == 'gdb' and (line[30] not in ('', None, 0))):
                    error_noBlan_l_alc['NROCONDUCTOS'].append(line[42])
                if (orig == 'shp' and (line[26] not in ('', None, 0))) or (orig == 'gdb' and (line[26] not in ('', None, 0))):
                    error_noBlan_l_alc['BASE'].append(line[42])
                if (orig == 'shp' and (line[28] not in ('', None, 0))) or (orig == 'gdb' and (line[28] not in ('', None, 0))):
                    error_noBlan_l_alc['ALTURA1'].append(line[42])

            if (red == 'redTroncal_2' and line[2] in ('24', '27')) or red == 'linLat_3':
                if line[17] is not None and not (isinstance(line[17], str) and line[17].strip() == ''):
                    error_noBlan_l_alc['CAM_CAIDA'].append(line[42])
                if line[38] is not None and not (isinstance(line[38], str) and line[38].strip() == ''):
                    error_noBlan_l_alc['TIPOINSPEC'].append(line[42])
                if line[39] is not None and not (isinstance(line[39], str) and line[39].strip() == ''):
                    error_noBlan_l_alc['GRADOEST'].append(line[42])
                if line[40] is not None and not (isinstance(line[40], str) and line[40].strip() == ''):
                    error_noBlan_l_alc['GRADOOPER'].append(line[42])

            if (red == 'redTroncal_2' and line[2] not in ('24', '27')) or red == 'linLat_3':
                if (orig == 'shp' and (line[29] not in ('', None, 0))) or (orig == 'gdb' and (line[29] not in ('', None, 0))):
                    error_noBlan_l_alc['ALTURA2'].append(line[42])
                if (orig == 'shp' and (line[31] not in ('', None, 0))) or (orig == 'gdb' and (line[31] not in ('', None, 0))):
                    error_noBlan_l_alc['ANCHOBERMA'].append(line[42])
                if (orig == 'shp' and (line[32] not in ('', None, 0))) or (orig == 'gdb' and (line[32] not in ('', None, 0))):
                    error_noBlan_l_alc['TALUD1'].append(line[42])
                if (orig == 'shp' and (line[33] not in ('', None, 0))) or (orig == 'gdb' and (line[33] not in ('', None, 0))):
                    error_noBlan_l_alc['TALUD2'].append(line[42])

            if red not in ('redTroncal_2'):
                if line[25] is not None and not (isinstance(line[25], str) and line[25].strip() == ''):
                    error_noBlan_l_alc['NOMBRE'].append(line[42])
    return error_noBlan_l_alc

# Valida los atributos que NO deberian estar en vacios pero estan si vacios (omisiones) - LINEAS ALCANTARILLADO
def valida_blan_l_alc(clase_l_alc):
    error_blan_l_alc = {'N_INICIAL':[], 'N_FINAL':[], 'FECHAINST':[], 'CONTRATO_ID':[], 'C_RASATEI':[],
                        'C_RASANTEF':[], 'C_CLAVEI':[], 'C_CLAVEF':[], 'C_BATEAI':[], 'C_BATEAF':[], 'PENDIENTE':[],
                        'PROFUNDIDAD':[],'NROCONDUCTOS':[], 'BASE':[], 'ALTURA1':[], 'ALTURA2':[],
                        'ANCHOBERMA':[],'TALUD1':[], 'TALUD2':[]}
    for red in clase_l_alc:
        for line in clase_l_alc[red]:
            if red not in ('linLat_3'):
                if line[24] in ('', None):
                    error_blan_l_alc['PENDIENTE'].append(line[42])
                if line[27] in ('', None):
                    error_blan_l_alc['PROFUNDIDAD'].append(line[42])
                if line[30] in ('', None):
                    error_blan_l_alc['NROCONDUCTOS'].append(line[42])
                if line[26] in ('', None):
                    error_blan_l_alc['BASE'].append(line[42])
                if line[28] in ('', None):
                    error_blan_l_alc['ALTURA1'].append(line[42])
                if line[2] in (24, 27) and line[29] in ('', None):
                    error_blan_l_alc['ALTURA2'].append(line[42])
                if line[2] in (24, 27) and line[31] in ('', None):
                    error_blan_l_alc['ANCHOBERMA'].append(line[42])
                if line[2] in (24, 27) and line[32] in ('', None):
                    error_blan_l_alc['TALUD1'].append(line[42])
                if line[2] in (24, 27) and line[33] in ('', None):
                    error_blan_l_alc['TALUD2'].append(line[42])
            if line[3] is None or (isinstance(line[3], str) and line[3].strip() == ''):
                error_blan_l_alc['N_INICIAL'].append(line[42])
            if line[4] is None or (isinstance(line[4], str) and line[4].strip() == ''):
                error_blan_l_alc['N_FINAL'].append(line[42])
            if line[6] in ('', None):
                error_blan_l_alc['FECHAINST'].append(line[42])
            if line[16] is None or (isinstance(line[16], str) and line[16].strip() == ''):
                error_blan_l_alc['CONTRATO_ID'].append(line[42])
            if line[18] in ('', None):
                error_blan_l_alc['C_RASATEI'].append(line[42])
            if line[19] in ('', None):
                error_blan_l_alc['C_RASANTEF'].append(line[42])
            if line[20] in ('', None):
                error_blan_l_alc['C_CLAVEI'].append(line[42])
            if line[21] in ('', None):
                error_blan_l_alc['C_CLAVEF'].append(line[42])
            if line[22] in ('', None):
                error_blan_l_alc['C_BATEAI'].append(line[42])
            if line[23] in ('', None):
                error_blan_l_alc['C_BATEAF'].append(line[42])

    return error_blan_l_alc

# Valida que los atributos de la capa de lineas Alcantarillado cumplan con los dominios
def valida_dom_l_alc(clase_l_alc):
    error_dom_l_alc = {'SUBTIPO':[], 'SISTEMA':[], 'ESTADOENRED':[], 'MATERIAL':[], 'MATERIAL2':[], 'CALIDADDATO':[],
                       'ESTADOLEGAL':[], 'DIAMETRO':[], 'T_SECCION':[], 'CAM_CAIDA':[], 'INSTALACI':[], 'MATESPPUBL':[],
                       'TIPOINSPEC':[], 'GRADOEST':[], 'GRADOOPER':[]}
    for red in clase_l_alc:
        for line in clase_l_alc[red]:
            if red not in ('linLat_3'):
                if line[8] not in dominios_l_alc[3]:
                    error_dom_l_alc['MATERIAL2'].append(line[42])
                if line [12] not in dominios_l_alc[7]:
                    error_dom_l_alc['T_SECCION'].append(line[42])
                if line[2] not in ('24', '27') and line[17] not in dominios_l_alc[8]:
                    error_dom_l_alc['CAM_CAIDA'].append(line[42])
                if line[35] not in dominios_l_alc[9]:
                    error_dom_l_alc['INSTALACI'].append(line[42])
                if line[36] not in dominios_l_alc[10]:
                    error_dom_l_alc['MATESPPUBL'].append(line[42])
                if line[38] not in ('24', '27') and line[17] not in dominios_l_alc[11]:
                    error_dom_l_alc['TIPOINSPEC'].append(line[42])
                if line[39] not in ('24', '27') and line[17] not in dominios_l_alc[12]:
                    error_dom_l_alc['GRADOEST'].append(line[42])
                if line[40] not in ('24', '27') and line[17] not in dominios_l_alc[13]:
                    error_dom_l_alc['GRADOOPER'].append(line[42])
            if line[2] not in dominios_l_alc[0][red]:
                error_dom_l_alc['SUBTIPO'].append(line[42])
            if line[5] not in dominios_l_alc[1]:
                error_dom_l_alc['SISTEMA'].append(line[42])
            if line[10] not in dominios_l_alc[2]:
                error_dom_l_alc['ESTADOENRED'].append(line[42])
            if line[7] not in dominios_l_alc[3]:
                error_dom_l_alc['MATERIAL'].append(line[42])
            if line[13] not in dominios_l_alc[4]:
                error_dom_l_alc['CALIDADDATO'].append(line[42])
            if line[14] not in dominios_l_alc[5]:
                error_dom_l_alc['ESTADOLEGAL'].append(line[42])
            if line [11] not in dominios_l_alc[6]:
                error_dom_l_alc['DIAMETRO'].append(line[42])
    return error_dom_l_alc


# ------------------------------------- VALIDACIONES PUNTOS ALCANTARILLADO -------------------------------------
# clasifica los tipos de punto alcantarillado que puedo encontrarme
def clasif_p_alc(l_alc, atrib_p_alc):
    clase_p_alc = {'ESTRUCTURA_RED_1':[], 'POZO_2':[], 'SUMIDERO_3':[], 'CAJA_DOMICILIARIA_4':[], 'SECCION_TRANSVERSAL_5':[]}
    error_clase_p_alc = []
    with arcpy.da.SearchCursor(l_alc,atrib_p_alc) as cursor:
        for punto in cursor:
            if punto[1] == 1:
                clase_p_alc['ESTRUCTURA_RED_1'].append(punto)
            elif punto[1] == 2:
                clase_p_alc['POZO_2'].append(punto)
            elif punto[1] == 3:
                clase_p_alc['SUMIDERO_3'].append(punto)
            elif punto[1] == 4:
                clase_p_alc['CAJA_DOMICILIARIA_4'].append(punto)
            elif punto[1] == 5:
                clase_p_alc['SECCION_TRANSVERSAL_5'].append(punto)
            else:
                error_clase_p_alc.append(punto[63])
    return clase_p_alc, error_clase_p_alc

# Valida los atributos que SI deberian estar en vacios pero que tienen algun valor (comisiones) - PUNTOS ALCANTARILLADO
def valida_noBlan_p_alc(clase_p_alc, orig):
    error_noBlan_p_alc = {'SUBTIPO':[], 'FECHADATO':[], 'TIPO_ALIVIO':[], 'TIPO_VALV_ANT':[], 'ESTADOENRED':[], 'LOCALIZACIONRELATIVA':[],
                          'C_RASANTE':[], 'C_TERRENO':[], 'C_FONDO':[], 'MATERIAL':[], 'SISTEMA':[], 'NOMBRE':[], 'OBSERV':[],'CONTRATO_ID':[],
                          'PROFUNDIDA':[],'CONOREDUCC':[],'MATERCONO':[],'TIPO_CONO':[],'EST_CONO':[],'INICIAL_CUENCAS':[],'ROTACION':[],
                          'CAMARASIF':[],'EST_FISICO':[],'CABEZAL':[],'EST_TAPA':[],'EST_POZO':[],'MATESCALO':[],'ESTESCALON':[],'ESTCARGUE':[],
                          'ESTCILIND':[],'ESTCANUE':[],'ESTOPERA':[],'CONTINSPE':[],'FECHA_INSP':[],'TIPOINSPEC':[],'TIPOALMAC':[],'COTACRESTA':[],
                          'C_TECHO_VE':[],'LONGVERT':[],'LARGO':[],'ANCHO':[],'ALTO':[],'Q_BOMBEO':[],'TIPOBOMB':[],'UNIDBOMBEO':[],'HBOMBEO':[],
                          'COTABOMBE':[],'VOLBOMBEO':[],'DIRECCION':[],'ESTREJILLA':[],'MATREJILLA':[],'TAMREJILLA':[],'ORIGENSEC':[],
                          'DISTORIGEN':[],'ABSCISA':[]}

    for tip_p in clase_p_alc:
        for punto in clase_p_alc[tip_p]:
            
            if tip_p in ('SECCION_TRANSVERSAL_5'):
                if punto[2] not in ('', None, 0):
                    error_noBlan_p_alc['SUBTIPO'].append(punto[63])
                if punto[6] not in ('', None):
                    error_noBlan_p_alc['FECHADATO'].append(punto[63])
                if punto[9] not in ('', None, 0):
                    error_noBlan_p_alc['ESTADOENRED'].append(punto[63])
                if punto[11] not in ('', None, 0):
                    error_noBlan_p_alc['C_RASANTE'].append(punto[63])
                if punto[14] not in ('', None):
                    error_noBlan_p_alc['MATERIAL'].append(punto[63])
                if punto[16] not in ('', None):
                    error_noBlan_p_alc['SISTEMA'].append(punto[63])
                if punto[18] is not None and not (isinstance(punto[18], str) and punto[18].strip() == ''):
                    error_noBlan_p_alc['OBSERV'].append(punto[63])
                if punto[19] is not None and not (isinstance(punto[19], str) and punto[19].strip() == ''):
                    error_noBlan_p_alc['CONTRATO_ID'].append(punto[63])
                if punto[40] not in ('', None):
                    error_noBlan_p_alc['FECHA_INSP'].append(punto[63])
                if punto[55] not in ('', None):
                    error_noBlan_p_alc['DIRECCION'].append(punto[63])
                
            
            if tip_p not in ('ESTRUCTURA_RED_1'):
                if punto[7] is not None and not (isinstance(punto[7], str) and punto[7].strip() == ''):
                    error_noBlan_p_alc['TIPO_ALIVIO'].append(punto[63])
                if punto[8] is not None and not (isinstance(punto[8], str) and punto[8].strip() == ''):
                    error_noBlan_p_alc['TIPO_VALV_ANT'].append(punto[63])
                if punto[30] is not None and not (isinstance(punto[30], str) and punto[30].strip() == ''):
                    error_noBlan_p_alc['CABEZAL'].append(punto[63])
                if punto[43] not in ('', None, 0):
                    error_noBlan_p_alc['COTACRESTA'].append(punto[63])
                if punto[44] not in ('', None, 0):
                    error_noBlan_p_alc['C_TECHO_VE'].append(punto[63])
                if punto[45] not in ('', None, 0):
                    error_noBlan_p_alc['LONGVERT'].append(punto[63])
                if punto[46] not in ('', None, 0):
                    error_noBlan_p_alc['LARGO'].append(punto[63])
                if punto[47] not in ('', None, 0):
                    error_noBlan_p_alc['ANCHO'].append(punto[63])
                if punto[48] not in ('', None, 0):
                    error_noBlan_p_alc['ALTO'].append(punto[63])
                if punto[49] not in ('', None, 0):
                    error_noBlan_p_alc['Q_BOMBEO'].append(punto[63])
                if punto[50] is not None and not (isinstance(punto[50], str) and punto[50].strip() == ''):
                    error_noBlan_p_alc['TIPOBOMB'].append(punto[63])
                if punto[51] is not None and not (isinstance(punto[51], str) and punto[51].strip() == ''):
                    error_noBlan_p_alc['UNIDBOMBEO'].append(punto[63])
                if punto[52] not in ('', None, 0):
                    error_noBlan_p_alc['HBOMBEO'].append(punto[63])
                if punto[53] not in ('', None, 0):
                    error_noBlan_p_alc['COTABOMBE'].append(punto[63])
                if punto[54] not in ('', None, 0):
                    error_noBlan_p_alc['VOLBOMBEO'].append(punto[63])

                    
            if tip_p in ('POZO_2', 'SECCION_TRANSVERSAL_5'):
                if punto[10] is not None and not (isinstance(punto[10], str) and punto[10].strip() == ''):
                    error_noBlan_p_alc['LOCALIZACIONRELATIVA'].append(punto[63])
                if punto[27] not in ('', None, 0):
                    error_noBlan_p_alc['ROTACION'].append(punto[63])
            
            if tip_p not in ('POZO_2', 'CAJA_DOMICILIARIA_4'):
                if punto[12] not in ('', None, 0):
                    error_noBlan_p_alc['C_TERRENO'].append(punto[63])
                    
            if tip_p in ('SUMIDERO_3', 'SECCION_TRANSVERSAL_5'):
                if punto[13] not in ('', None, 0):
                    error_noBlan_p_alc['C_FONDO'].append(punto[63])
            
            if tip_p not in ('ESTRUCTURA_RED_1', 'SECCION_TRANSVERSAL_5'):
                if punto[17] is not None and not (isinstance(punto[17], str) and punto[17].strip() == ''):
                    error_noBlan_p_alc['NOMBRE'].append(punto[63])
            
            if tip_p not in ('POZO_2'):
                if punto[21] not in ('', None, 0):
                    error_noBlan_p_alc['PROFUNDIDA'].append(punto[63])
                if punto[22] is not None and not (isinstance(punto[22], str) and punto[22].strip() == ''):
                    error_noBlan_p_alc['CONOREDUCC'].append(punto[63])
                if punto[23] is not None and not (isinstance(punto[23], str) and punto[23].strip() == ''):
                    error_noBlan_p_alc['MATERCONO'].append(punto[63])
                if punto[24] is not None and not (isinstance(punto[24], str) and punto[24].strip() == ''):
                    error_noBlan_p_alc['TIPO_CONO'].append(punto[63])
                if punto[25] is not None and not (isinstance(punto[25], str) and punto[25].strip() == ''):
                    error_noBlan_p_alc['EST_CONO'].append(punto[63])
                if punto[26] is not None and not (isinstance(punto[26], str) and punto[26].strip() == ''):
                    error_noBlan_p_alc['INICIAL_CUENCAS'].append(punto[63])
                if punto[28] is not None and not (isinstance(punto[28], str) and punto[28].strip() == ''):
                    error_noBlan_p_alc['CAMARASIF'].append(punto[63])
                if punto[31] is not None and not (isinstance(punto[31], str) and punto[31].strip() == ''):
                    error_noBlan_p_alc['EST_TAPA'].append(punto[63])
                if punto[32] is not None and not (isinstance(punto[32], str) and punto[32].strip() == ''):
                    error_noBlan_p_alc['EST_POZO'].append(punto[63])
                if punto[33] is not None and not (isinstance(punto[33], str) and punto[33].strip() == ''):
                    error_noBlan_p_alc['MATESCALO'].append(punto[63])
                if punto[34] is not None and not (isinstance(punto[34], str) and punto[34].strip() == ''):
                    error_noBlan_p_alc['ESTESCALON'].append(punto[63])
                if punto[35] is not None and not (isinstance(punto[35], str) and punto[35].strip() == ''):
                    error_noBlan_p_alc['ESTCARGUE'].append(punto[63])
                if punto[36] is not None and not (isinstance(punto[36], str) and punto[36].strip() == ''):
                    error_noBlan_p_alc['ESTCILIND'].append(punto[63])
                if punto[37] is not None and not (isinstance(punto[37], str) and punto[37].strip() == ''):
                    error_noBlan_p_alc['ESTCANUE'].append(punto[63])
                if punto[42] is not None and not (isinstance(punto[42], str) and punto[42].strip() == ''):
                    error_noBlan_p_alc['TIPOALMAC'].append(punto[63])
            
            if tip_p not in ('ESTRUCTURA_RED_1', 'POZO_2'):
                if punto[29] is not None and not (isinstance(punto[29], str) and punto[29].strip() == ''):
                    error_noBlan_p_alc['EST_FISICO'].append(punto[63])

            if tip_p not in ('POZO_2', 'SUMIDERO_3'):
                if punto[38] is not None and not (isinstance(punto[38], str) and punto[38].strip() == ''):
                    error_noBlan_p_alc['ESTOPERA'].append(punto[63])
                if punto[39] is not None and not (isinstance(punto[39], str) and punto[39].strip() == ''):
                    error_noBlan_p_alc['CONTINSPE'].append(punto[63])
                if punto[41] is not None and not (isinstance(punto[41], str) and punto[41].strip() == ''):
                    error_noBlan_p_alc['TIPOINSPEC'].append(punto[63])

            if tip_p not in ('SUMIDERO_3'):
                if punto[56] is not None and not (isinstance(punto[56], str) and punto[56].strip() == ''):
                    error_noBlan_p_alc['ESTREJILLA'].append(punto[63])
                if punto[57] is not None and not (isinstance(punto[57], str) and punto[57].strip() == ''):
                    error_noBlan_p_alc['MATREJILLA'].append(punto[63])
                if punto[58] is not None and not (isinstance(punto[58], str) and punto[58].strip() == ''):
                    error_noBlan_p_alc['TAMREJILLA'].append(punto[63])

            if tip_p not in ('SECCION_TRANSVERSAL_5'):
                if punto[59] is not None and not (isinstance(punto[59], str) and punto[59].strip() == ''):
                    error_noBlan_p_alc['ORIGENSEC'].append(punto[63])
                if punto[61] is not None and not (isinstance(punto[61], str) and punto[61].strip() == ''):
                    error_noBlan_p_alc['ABSCISA'].append(punto[63])
                if punto[60] not in ('', None, 0):
                    error_noBlan_p_alc['DISTORIGEN'].append(punto[63])
    return error_noBlan_p_alc

# Valida los atributos que NO deberian estar en vacios pero estan si vacios (omisiones) - PUNTOS ALCANTARILLADO
def valida_blan_p_alc(clase_p_alc):
    error_blan_p_alc = {'IDENTIFIC':[],'NORTE':[],'ESTE':[], 'FECHADATO':[], 'LOCALIZACIONRELATIVA':[],'C_RASANTE':[],
                        'C_TERRENO':[],'C_FONDO':[], 'NOMBRE':[], 'CONTRATO_ID':[], 'PROFUNDIDA':[], 'ROTACION':[],
                        'CONTINSPE':[],'FECHA_INSP':[], 'COTACRESTA':[], 'C_TECHO_VE':[],'LONGVERT':[],'LARGO':[],
                        'ANCHO':[],'ALTO':[],'Q_BOMBEO':[],'UNIDBOMBEO':[],'HBOMBEO':[],'COTABOMBE':[],'VOLBOMBEO':[],
                        'DIRECCION':[],'TAMREJILLA':[],'DISTORIGEN':[],'ABSCISA':[]}
    for tip_p in clase_p_alc:
        for punto in clase_p_alc[tip_p]:
            if tip_p not in ('SECCION_TRANSVERSAL_5'):
                if punto[6] in ('', None):
                    error_blan_p_alc['FECHADATO'].append(punto[63])
                if punto[11] in ('', None):
                    error_blan_p_alc['C_RASANTE'].append(punto[63])
                if punto[19] is None or (isinstance(punto[19], str) and punto[19].strip() == ''):
                    error_blan_p_alc['CONTRATO_ID'].append(punto[63])
                if punto[40] is None or (isinstance(punto[40], str) and punto[40].strip() == ''):
                    error_blan_p_alc['FECHA_INSP'].append(punto[63])
                if punto[55] is None or (isinstance(punto[55], str) and punto[55].strip() == ''):
                    error_blan_p_alc['DIRECCION'].append(punto[63])
                    
            if tip_p not in ('POZO_2','SECCION_TRANSVERSAL_5'):
                if punto[10] is None or (isinstance(punto[10], str) and punto[10].strip() == ''):
                    error_blan_p_alc['LOCALIZACIONRELATIVA'].append(punto[63])
                
            if tip_p in ('POZO_2','CAJA_DOMICILIARIA_4'):
                if punto[12] in ('', None):
                    error_blan_p_alc['C_TERRENO'].append(punto[63])
                    
            if tip_p not in ('SUMIDERO_3', 'SECCION_TRANSVERSAL_5'):
                if punto[13] in ('', None):
                    error_blan_p_alc['C_FONDO'].append(punto[63])
                    
            if tip_p in ('SECCION_TRANSVERSAL_5'):
                if punto[17] is None or (isinstance(punto[17], str) and punto[17].strip() == ''):
                    error_blan_p_alc['NOMBRE'].append(punto[63])
                if punto[60] in ('', None):
                    error_blan_p_alc['DISTORIGEN'].append(punto[63])
                if punto[61] is None or (isinstance(punto[61], str) and punto[61].strip() == ''):
                    error_blan_p_alc['ABSCISA'].append(punto[63])
                    
            if tip_p in ('POZO_2'):
                if punto[21] in ('', None):
                    error_blan_p_alc['PROFUNDIDA'].append(punto[63])
                    
            if tip_p in ('ESTRUCTURA_RED_1','SUMIDERO_3'):
                if punto[27] in ('', None):
                    error_blan_p_alc['ROTACION'].append(punto[63])
                    
            if tip_p in ('POZO_2','SUMIDERO_3'):
                if punto[39] in ('', None):
                    error_blan_p_alc['CONTINSPE'].append(punto[63])
                    
            if tip_p in ('ESTRUCTURA_RED_1'):
                if punto[43] in ('', None):
                    error_blan_p_alc['COTACRESTA'].append(punto[63])
                if punto[44] in ('', None):
                    error_blan_p_alc['C_TECHO_VE'].append(punto[63])
                if punto[45] in ('', None):
                    error_blan_p_alc['LONGVERT'].append(punto[63])
                if punto[46] in ('', None):
                    error_blan_p_alc['LARGO'].append(punto[63])
                if punto[47] in ('', None):
                    error_blan_p_alc['ANCHO'].append(punto[63])
                if punto[48] in ('', None):
                    error_blan_p_alc['ALTO'].append(punto[63])
                if punto[49] in ('', None):
                    error_blan_p_alc['Q_BOMBEO'].append(punto[63])
                if punto[51] is None or (isinstance(punto[51], str) and punto[51].strip() == ''):
                    error_blan_p_alc['UNIDBOMBEO'].append(punto[63])
                if punto[52] in ('', None):
                    error_blan_p_alc['HBOMBEO'].append(punto[63])
                if punto[53] in ('', None):
                    error_blan_p_alc['COTABOMBE'].append(punto[63])
                if punto[54] in ('', None):
                    error_blan_p_alc['VOLBOMBEO'].append(punto[63])
            
            if tip_p in ('SUMIDERO_3'):
                if punto[58] in ('', None):
                    error_blan_p_alc['TAMREJILLA'].append(punto[63])
            
            if punto[3] in ('', None):
                error_blan_p_alc['IDENTIFIC'].append(punto[63])
            if punto[4] in ('', None, 0):
                error_blan_p_alc['NORTE'].append(punto[63])
            if punto[5] in ('', None, 0):
                error_blan_p_alc['ESTE'].append(punto[63])
                
    return error_blan_p_alc

# Valida que los atributos de la capa de puntos Alcantarillado cumplan con los dominios
def valida_dom_p_alc(clase_p_alc):
    error_dom_p_alc = {'SUBTIPO':[], 'TIPO_ALIVIO':[], 'TIPO_VALV_ANT':[], 'ESTADOENRED':[], 'MATERIAL':[], 'CALIDADDATO':[],
                       'SISTEMA':[], 'CONOREDUCC':[], 'MATERCONO':[], 'TIPO_CONO':[], 'EST_CONO':[], 'INICIAL_CUENCAS':[],
                       'CAMARASIF':[], 'EST_FISICO':[], 'CABEZAL':[], 'EST_TAPA':[], 'EST_POZO':[], 'MATESCALO':[],
                       'ESTESCALON':[], 'ESTCARGUE':[], 'ESTCILIND':[], 'ESTCANUE':[], 'ESTOPERA':[], 'TIPOINSPEC':[],
                       'TIPOALMAC':[], 'TIPOBOMB':[], 'ESTREJILLA':[], 'MATREJILLA':[], 'ORIGENSEC':[]}
    for tip_p in clase_p_alc:
        for punto in clase_p_alc[tip_p]:
            if tip_p in ('ESTRUCTURA_RED_1'):
                if punto[7] not in dominios_p_alc[1]:
                    error_dom_p_alc['TIPO_ALIVIO'].append(punto[63])
                if punto[8] not in dominios_p_alc[2]:
                    error_dom_p_alc['TIPO_VALV_ANT'].append(punto[63])
                if punto[30] not in dominios_p_alc[14]:
                    error_dom_p_alc['CABEZAL'].append(punto[63])
                if punto[50] not in dominios_p_alc[25]:
                    error_dom_p_alc['TIPOBOMB'].append(punto[63])
            
            if tip_p !='SECCION_TRANSVERSAL_5':
                if punto[2] not in dominios_p_alc[0][tip_p]:
                    error_dom_p_alc['SUBTIPO'].append(punto[63])
                if punto[9] not in dominios_p_alc[3]:
                    error_dom_p_alc['ESTADOENRED'].append(punto[63])
                if punto[14] not in dominios_p_alc[4]:
                    error_dom_p_alc['MATERIAL'].append(punto[63])
                if punto[16] not in dominios_p_alc[6]:
                    error_dom_p_alc['SISTEMA'].append(punto[63])
                    
            if tip_p in ('POZO_2'):
                if punto[22] not in dominios_p_alc[7]:
                    error_dom_p_alc['CONOREDUCC'].append(punto[63])
                if punto[23] not in dominios_p_alc[8]:
                    error_dom_p_alc['MATERCONO'].append(punto[63])
                if punto[24] not in dominios_p_alc[9]:
                    error_dom_p_alc['TIPO_CONO'].append(punto[63])
                if punto[25] not in dominios_p_alc[10]:
                    error_dom_p_alc['EST_CONO'].append(punto[63])
                if punto[26] not in dominios_p_alc[11]:
                    error_dom_p_alc['INICIAL_CUENCAS'].append(punto[63])
                if punto[28] not in dominios_p_alc[12]:
                    error_dom_p_alc['CAMARASIF'].append(punto[63])
                if punto[31] not in dominios_p_alc[15]:
                    error_dom_p_alc['EST_TAPA'].append(punto[63])
                if punto[32] not in dominios_p_alc[16]:
                    error_dom_p_alc['EST_POZO'].append(punto[63])
                if punto[33] not in dominios_p_alc[17]:
                    error_dom_p_alc['MATESCALO'].append(punto[63])
                if punto[34] not in dominios_p_alc[18]:
                    error_dom_p_alc['ESTESCALON'].append(punto[63])
                if punto[35] not in dominios_p_alc[19]:
                    error_dom_p_alc['ESTCARGUE'].append(punto[63])
                if punto[36] not in dominios_p_alc[20]:
                    error_dom_p_alc['ESTCILIND'].append(punto[63])
                if punto[37] not in dominios_p_alc[21]:
                    error_dom_p_alc['ESTCANUE'].append(punto[63])
                if punto[42] not in dominios_p_alc[24]:
                    error_dom_p_alc['TIPOALMAC'].append(punto[63])
                
            if tip_p in ('ESTRUCTURA_RED_1', 'POZO_2'):
                if punto[29] not in dominios_p_alc[13]:
                    error_dom_p_alc['EST_FISICO'].append(punto[63])
                    
            if tip_p in ('POZO_2', 'SUMIDERO_3'):
                if punto[38] not in dominios_p_alc[22]:
                    error_dom_p_alc['ESTOPERA'].append(punto[63])
                if punto[41] not in dominios_p_alc[23]:
                    error_dom_p_alc['TIPOINSPEC'].append(punto[63])

            if tip_p in ('SUMIDERO_3'):
                if punto[56] not in dominios_p_alc[26]:
                    error_dom_p_alc['ESTREJILLA'].append(punto[63])
                if punto[57] not in dominios_p_alc[27]:
                    error_dom_p_alc['MATREJILLA'].append(punto[63])
            
            if tip_p in ('SECCION_TRANSVERSAL_5'):
                if punto[59] not in dominios_p_alc[28]:
                    error_dom_p_alc['ORIGENSEC'].append(punto[63])
                    
            if punto[15] not in dominios_p_alc[5]:
                error_dom_p_alc['CALIDADDATO'].append(punto[63])
                
    return error_dom_p_alc
    

# ------------------------------------- VALIDACIONES GENERALES -------------------------------------

# crea el reporte con las inconsistencias encontradas
def reporte(error_clase, error_noBlan, error_blan, error_dom, capa, workspace):

    salida = os.path.join(workspace, f'Inconsistencias_{capa}.csv')
    
    with open(salida, 'w', newline='', encoding='utf-8') as archivo:
        escritor = csv.writer(archivo)
        
        # Escribimos la cabecera
        escritor.writerow(['Tipo de Error', 'Nombre del atributo', 'ID del Registro'])
        
        if not error_clase:
            pass
        else:
            for id_registro in error_clase:
                escritor.writerow(['Inconsistencia en el Dominio Clase', 'CLASE', id_registro])
        for atributo, ids in error_noBlan.items():
            if not ids:
                pass
            else:
                for id_registro in ids:
                    escritor.writerow(['Comision informacion', atributo, id_registro])
        for atributo, ids in error_blan.items():
            if not ids:
                pass
            else:
                for id_registro in ids:
                    escritor.writerow(['Omision de informacion', atributo, id_registro])
        for atributo, ids in error_dom.items():
            if not ids:
                pass
            else:
                for id_registro in ids:
                    escritor.writerow(['Inconsistencia de Dominio', atributo, id_registro])
        
# Muestra los mensajes de advertencia cuando se encuentran errores en la estructura de los datos
def msg_error_estrc(error_clase, error_noBlan, error_blan, error_dom, nombre):
    er = 0

    if len(error_clase) > 0 or any(len(error_dom[key])>0 for key in error_dom) or any(len(error_blan[key])>0 for key in error_blan) or any(len(error_noBlan[key])>0 for key in error_noBlan):
        arcpy.AddWarning(f'---- Se identificaron Errores en la estructura de la capa {nombre} ----')
        if len(error_clase) > 0:
            arcpy.AddWarning(f'Se identificaron {len(error_clase)} registros con errores de Dominio en el atributo "CLASE"')
        if any(len(error_dom[key])>0 for key in error_dom):
            c = 0
            for e in error_dom:
                c = c + len(error_dom[e])
            arcpy.AddWarning(f'Se identificaron {c} registros con errores de Dominio')
        if any(len(error_blan[key])>0 for key in error_blan):
            c = 0
            for e in error_blan:
                c = c + len(error_blan[e])
            arcpy.AddWarning(f'Se identificaron {c} (Omisiones) registros con valores que No deben estar vacios')
        if any(len(error_noBlan[key])>0 for key in error_noBlan):
            c = 0
            for e in error_noBlan:
                c = c + len(error_noBlan[e])
        arcpy.AddWarning(f'Se identificaron {c} (Comisiones) registros con valores que Si deben estar vacios')
        er = 1
    else:
        arcpy.AddMessage(f'La Estructura de la capa {nombre} esta Correcta..')
    return er

# Funcion que recoje las validaciones de estructura de los datos
def validacion_estruct(l_acu_orig, p_acu_orig, l_alc_orig, p_alc_orig, l_alc_pluv_orig, p_alc_pluv_orig, workspace):
    arcpy.AddMessage("Validando la estructura de los datos..")
    clase_l = []
    er_l_acu = 0
    if l_acu_orig != '':
        desc_l_acu = arcpy.Describe(l_acu_orig)
        if desc_l_acu.name.split('.')[-1] != 'shp':
            arcpy.AddMessage(f'Tipo Origen de datos: GDB')
            clase_l,error_clase_l = clasif_l_ecu(l_acu_orig, atrib_l_ecu_gdb)
            orig = 'gdb'
        else:
            arcpy.AddMessage(f'Tipo Origen de datos: .SHP')
            clase_l,error_clase_l = clasif_l_ecu(l_acu_orig, atrib_l_ecu_shp)
            orig = 'shp'
        error_noBlan_l = valida_no_blan_l_ecu(clase_l, orig)
        error_blan_l = valida_blan_l_ecu(clase_l)
        error_dom_l = valida_dom_l_acu(clase_l)
        
        er_l_acu = msg_error_estrc(error_clase_l, error_noBlan_l, error_blan_l, error_dom_l, 'Lineas Acueducto')
        if er_l_acu == 1:
            reporte(error_clase_l, error_noBlan_l, error_blan_l, error_dom_l, 'lineasAcueducto', workspace)
        
    clase_p_acu = []
    er_p_acu = 0
    if p_acu_orig != '':
        desc_p_acu = arcpy.Describe(p_acu_orig)
        if desc_p_acu.name.split('.')[-1] != 'shp':
            arcpy.AddMessage(f'Tipo Origen de datos: GDB')
            clase_p_acu, error_clase_p_acu = clasif_p_acu(p_acu_orig, atrib_p_acu_gdb)
            orig = 'gdb'
        else:
            arcpy.AddMessage(f'Tipo Origen de datos: .SHP')
            clase_p_acu, error_clase_p_acu = clasif_p_acu(p_acu_orig, atrib_p_acu_shp)
            orig = 'shp'
        error_noBlan_p_acu = valida_no_blan_p_acu(clase_p_acu, orig)
        error_blan_p_acu = valida_blan_p_acu(clase_p_acu)
        error_dom_p_acu = valida_dom_p_acu(clase_p_acu)

        er_p_acu = msg_error_estrc(error_clase_p_acu, error_noBlan_p_acu, error_blan_p_acu, error_dom_p_acu, 'Nodos Acueducto')
        if er_p_acu == 1:
            reporte(error_clase_p_acu, error_noBlan_p_acu, error_blan_p_acu, error_dom_p_acu, 'nodosAcueducto', workspace)
        
        
    clase_l_alc = []
    er_l_alc = 0
    if l_alc_orig != '':
        desc_l_alc = arcpy.Describe(l_alc_orig)
        if desc_l_alc.name.split('.')[-1] != 'shp':
            arcpy.AddMessage(f'Tipo Origen de datos: GDB')
            clase_l_alc, error_clase_l_alc = clasif_l_alc(l_alc_orig, atrib_l_alc_gdb)
            orig = 'gdb'
        else:
            arcpy.AddMessage(f'Tipo Origen de datos: .SHP')
            clase_l_alc, error_clase_l_alc = clasif_l_alc(l_alc_orig, atrib_l_alc_shp)
            orig = 'shp'
        error_noBlan_l_alc = valida_noBlan_l_alc(clase_l_alc, orig)
        error_blan_l_alc = valida_blan_l_alc(clase_l_alc)
        error_dom_l_alc = valida_dom_l_alc(clase_l_alc)
        
        er_l_alc = msg_error_estrc(error_clase_l_alc, error_noBlan_l_alc, error_blan_l_alc, error_dom_l_alc, 'Lineas Alcantarillado')
        if er_l_alc == 1:
            reporte(error_clase_l_alc, error_noBlan_l_alc, error_blan_l_alc, error_dom_l_alc, 'lineasAlcantarillado', workspace)
    
    clase_p_alc = []
    er_p_alc = 0
    if p_alc_orig != '':
        desc_p_acu = arcpy.Describe(p_alc_orig)
        if desc_p_acu.name.split('.')[-1] != 'shp':
            arcpy.AddMessage(f'Tipo Origen de datos: GDB')
            clase_p_alc, error_clase_p_alc = clasif_p_alc(p_alc_orig, atrib_p_alc_gdb)
            orig = 'gdb'
        else:
            arcpy.AddMessage(f'Tipo Origen de datos: .SHP')
            clase_p_alc, error_clase_p_alc = clasif_p_alc(p_alc_orig, atrib_p_alc_shp)
            orig = 'shp'
        error_noBlan_p_alc = valida_noBlan_p_alc(clase_p_alc, orig)
        error_blan_p_alc = valida_blan_p_alc(clase_p_alc)
        error_dom_p_alc = valida_dom_p_alc(clase_p_alc)

        er_p_alc = msg_error_estrc(error_clase_p_alc, error_noBlan_p_alc, error_blan_p_alc, error_dom_p_alc, 'Nodos Alcantarillado')
        if er_p_alc == 1:
            reporte(error_clase_p_alc, error_noBlan_p_alc, error_blan_p_alc, error_dom_p_alc, 'nodosAlcantarillado', workspace)
            
    clase_l_alc_pluv = []
    er_l_alc_pluv = 0
    if l_alc_pluv_orig != '':
        desc_l_alc_pluv = arcpy.Describe(l_alc_pluv_orig)
        if desc_l_alc_pluv.name.split('.')[-1] != 'shp':
            arcpy.AddMessage(f'Tipo Origen de datos: GDB')
            clase_l_alc_pluv, error_clase_l_alc_pluv = clasif_l_alc(l_alc_pluv_orig, atrib_l_alc_gdb)  
            orig = 'gdb'
        else:
            arcpy.AddMessage(f'Tipo Origen de datos: .SHP')
            clase_l_alc_pluv, error_clase_l_alc_pluv = clasif_l_alc(l_alc_pluv_orig, atrib_l_alc_shp)
            orig = 'shp'
        error_noBlan_l_alc_pluv = valida_noBlan_l_alc(clase_l_alc_pluv, orig)
        error_blan_l_alc_pluv = valida_blan_l_alc(clase_l_alc_pluv)
        error_dom_l_alc_pluv = valida_dom_l_alc(clase_l_alc_pluv)
        
        er_l_alc_pluv = msg_error_estrc(error_clase_l_alc_pluv, error_noBlan_l_alc_pluv, error_blan_l_alc_pluv, error_dom_l_alc_pluv, 'Lineas Alcantarillado Pluvial')
        if er_l_alc_pluv == 1:
            reporte(error_clase_l_alc_pluv, error_noBlan_l_alc_pluv, error_blan_l_alc_pluv, error_dom_l_alc_pluv, 'lineasAlcantarilladoPluvial', workspace)
    
    clase_p_alc_pluv = []
    er_p_alc_pluv = 0
    if p_alc_pluv_orig != '':
        desc_p_alc_pluv = arcpy.Describe(p_alc_pluv_orig)
        if desc_p_alc_pluv.name.split('.')[-1] != 'shp':
            arcpy.AddMessage(f'Tipo Origen de datos: GDB')
            clase_p_alc_pluv, error_clase_p_alc_pluv = clasif_p_alc(p_alc_pluv_orig, atrib_p_alc_gdb)
            orig = 'gdb'
        else:
            arcpy.AddMessage(f'Tipo Origen de datos: .SHP')
            clase_p_alc_pluv, error_clase_p_alc_pluv = clasif_p_alc(p_alc_pluv_orig, atrib_p_alc_shp)
            orig = 'shp'
        error_noBlan_p_alc_pluv = valida_noBlan_p_alc(clase_p_alc_pluv, orig)
        error_blan_p_alc_pluv = valida_blan_p_alc(clase_p_alc_pluv)
        error_dom_p_alc_pluv = valida_dom_p_alc(clase_p_alc_pluv)
        
        er_p_alc_pluv = msg_error_estrc(error_clase_p_alc_pluv, error_noBlan_p_alc_pluv, error_blan_p_alc_pluv, error_dom_p_alc_pluv, 'Nodos Alcantarillado Pluvial')
        if er_p_alc_pluv == 1:
            reporte(error_clase_p_alc_pluv, error_noBlan_p_alc_pluv, error_blan_p_alc_pluv, error_dom_p_alc_pluv, 'nodosAlcantarilladoPluvial', workspace)
        
        
    # OJO AGREGAR CLASE y ERROR
    return clase_l, er_l_acu, clase_p_acu, er_p_acu, clase_l_alc, er_l_alc, clase_p_alc, er_p_alc, clase_l_alc_pluv, er_l_alc_pluv, clase_p_alc_pluv, er_p_alc_pluv


# ------------------------------- CREANDO LA ESTRUCTURA DE LA BASE DE DATOS -------------------------------
def estruc_vacia_bd(workspace):
    salida_estr = os.path.join(workspace, 'GDB_Cargue.gdb')
    script_dir = os.path.dirname(os.path.abspath(__file__))
    xml_path = os.path.join(script_dir, 'Obra_Vacias_Planas.xml')
    arcpy.AddMessage(f"La ruta del xml es:{xml_path}")
    arcpy.management.CreateFileGDB(workspace, 'GDB_Cargue.gdb', '10.0')
    arcpy.management.ImportXMLWorkspaceDocument(salida_estr, xml_path, 'SCHEMA_ONLY')

    return salida_estr


# ------------------------------------- MIGRACIONES DE INFORMACION -------------------------------------
# Migra la informacion de las LINEAS ACUEDUCTO
def migra_l_acu(clase_l, workspace):
    for red in clase_l:
        for line in clase_l[red]:
            if red == 'redMatriz_1':
                redmatriz = os.path.join(workspace, 'acd_RedMatriz')
                campos = ['Shape@', 'SUBTIPO', 'DOMDIAMETRONOMINAL', 'DOMMATERIAL', 'DOMESTADOENRED', 'FECHAINSTALACION',
                          'DOMCALIDADDATO', 'OBSERVACIONES','DOMSUITIPOINSTALACION', 'CONTRATO_ID', 'LONGITUD_M',
                          'DOMCOSTADO', 'PROFUNDIDAD']
                with arcpy.da.InsertCursor(redmatriz, campos) as Incursor:
                    reg = [line[0], line[2], line[7], line[8], line[6], line[5], line[9], line[11], line[12], line[13],
                           line[25], line[16], line[23]]
                    Incursor.insertRow(reg)
            elif red == 'aduccion_2':
                conduccion = os.path.join(workspace, 'acd_Conduccion')
                campos = ['Shape@', 'SUBTIPO', 'DOMDIAMETRONOMINAL', 'DOMMATERIAL', 'DOMESTADOENRED', 'FECHAINSTALACION',
                          'DOMCALIDADDATO', 'OBSERVACIONES','DOMSUITIPOINSTALACION', 'CONTRATO_ID', 'LONGITUD_M',
                          'T_SECCION', 'AREA_TR_M2', 'C_RASANTEI', 'C_RASANTEF', 'C_CLAVEI', 'C_CLAVEF']
                with arcpy.da.InsertCursor(conduccion, campos) as Incursor:
                    reg = [line[0], line[2], line[7], line[8], line[6], line[5], line[9], line[11], line[12], line[13],
                           line[25], line[17], line[18], line[19], line[20], line[21], line[22]]
                    Incursor.insertRow(reg)
            elif red == 'conduccion_3':
                conduccion = os.path.join(workspace, 'acd_Conduccion')
                campos = ['Shape@', 'SUBTIPO', 'DOMDIAMETRONOMINAL', 'DOMMATERIAL', 'DOMESTADOENRED', 'FECHAINSTALACION',
                          'DOMCALIDADDATO', 'OBSERVACIONES','DOMSUITIPOINSTALACION', 'CONTRATO_ID', 'LONGITUD_M',
                          'T_SECCION', 'AREA_TR_M2', 'C_RASANTEI', 'C_RASANTEF', 'C_CLAVEI', 'C_CLAVEF']
                with arcpy.da.InsertCursor(conduccion, campos) as Incursor:
                    reg = [line[0], line[2], line[7], line[8], line[6], line[5], line[9], line[11], line[12], line[13],
                           line[25], line[17], line[18], line[19], line[20], line[21], line[22]]
                    Incursor.insertRow(reg)
            elif red == 'redMenor_4':
                redMenor = os.path.join(workspace, 'acd_RedMenor')
                campos = ['Shape@', 'SUBTIPO', 'DOMDIAMETRONOMINAL', 'DOMMATERIAL', 'DOMESTADOENRED', 'FECHAINSTALACION',
                            'DOMCALIDADDATO', 'OBSERVACIONES','DOMSUITIPOINSTALACION', 'CONTRATO_ID', 'DOMESTADOLEGAL',
                            'DOMCOSTADO', 'LONGITUD_M', 'PROFUNDIDAD']
                with arcpy.da.InsertCursor(redMenor, campos) as Incursor:
                    reg = (line[0], line[2], line[7], line[8], line[6], line[5], line[9], line[11], line[12], line[13],
                            line[10], line[16], line[25], line[23])
                    Incursor.insertRow(reg)
            elif red == 'lineaLat_5':
                linLat = os.path.join(workspace, 'acd_LineaLateral')
                campos = ['Shape@', 'SUBTIPO', 'DOMDIAMETRONOMINAL', 'DOMMATERIAL', 'DOMESTADOENRED', 'FECHAINSTALACION',
                            'DOMCALIDADDATO', 'OBSERVACIONES','DOMSUITIPOINSTALACION', 'CONTRATO_ID', 'DOMESTADOLEGAL',
                            'PROFUNDIDAD', 'RUGOSIDAD', 'LONGITUD_M']
                with arcpy.da.InsertCursor(linLat, campos) as Incursor:
                    reg = (line[0], line[2], line[7], line[8], line[6], line[5], line[9], line[11], line[12],line[13],
                            line[10], line[23], line[24], line[25])
                    Incursor.insertRow(reg)

# Migra la informacion de los PUNTOS ACUEDUCTO
def migra_p_acu(clase_p_acu, workspace):
    for tipo_nod in clase_p_acu:
        for punto in clase_p_acu[tipo_nod]:
            if tipo_nod == 'VALVULASISTEMA_1':
                valv_sis = os.path.join(workspace, 'acd_ValvulaSistema')
                campos = ['Shape@', 'SUBTIPO','DOMESTADOENRED', 'LOCALIZACIONRELATIVA','DOMCALIDADDATO', 'FECHAINSTALACION',
                          'ROTACIONSIMBOLO', 'OBSERVACIONES','CONTRATO_ID','DOMTIPOESPPUBLICO','DOMMATESPPUBLICO',
                          'DOMMATERIAL', 'DOMDIAMETRONOMINAL', 'DOMAUTOMATIZADA', 'DOMSENTIDOOPERACION', 'COTARASANTE',
                          'PROFUNDIDAD', 'DOMESTADOOPERACION', 'DOMTIPOOPERACION', 'DOMESTADOFISICO', 'DIRECCION',
                          'DOMTIPO', 'VUELTASCIERRE']
                with arcpy.da.InsertCursor(valv_sis, campos) as Incursor:
                    reg = (punto[0], punto[2], punto[7], punto[8], punto[9], punto[6], punto[10], punto[15], punto[16],
                        punto[18], punto[19], punto[13], punto[21], punto[20], punto[23], punto[11], punto[12], punto[24],
                        punto[25], punto[26], punto[73], punto[27], punto[28])
                    Incursor.insertRow(reg)
            elif tipo_nod == 'VALVULACONTROL_2':
                valv_con = os.path.join(workspace, 'acd_ValvulaControl')
                campos = ['Shape@', 'SUBTIPO','DOMESTADOENRED', 'LOCALIZACIONRELATIVA','DOMCALIDADDATO', 'FECHAINSTALACION',
                          'ROTACIONSIMBOLO', 'OBSERVACIONES','CONTRATO_ID','DOMTIPOESPPUBLICO','DOMMATESPPUBLICO',
                          'DOMMATERIAL', 'DOMDIAMETRONOMINAL', 'DOMAUTOMATIZADA', 'DOMSENTIDOOPERACION', 'COTARASANTE',
                          'PROFUNDIDAD', 'DOMESTADOOPERACION', 'DOMTIPOOPERACION', 'DOMESTADOFISICO', 'DIRECCION',
                          'DOMTIPO', 'VUELTASCIERRE']
                with arcpy.da.InsertCursor(valv_con, campos) as Incursor:
                    reg = (punto[0], punto[2], punto[7], punto[8], punto[9], punto[6], punto[10], punto[15], punto[16],
                        punto[18], punto[19], punto[13], punto[21], punto[20], punto[23], punto[11], punto[12], punto[24],
                        punto[25], punto[26], punto[73], punto[27], punto[28]) 
                    Incursor.insertRow(reg)
            elif tipo_nod in ('ACCESORIO_CODO_3'):
                if punto[29] in ('1'):
                    codo = os.path.join(workspace, 'acd_Accesorio')
                    campos = ['Shape@', 'SUBTIPO','DOMESTADOENRED', 'LOCALIZACIONRELATIVA','DOMCALIDADDATO', 'FECHAINSTALACION',
                          'ROTACIONSIMBOLO', 'OBSERVACIONES','CONTRATO_ID', 'DOMMATERIAL', 'COTARASANTE', 'PROFUNDIDAD',
                          'DOMDIAMETRONOMINAL', 'DOMDIAMETRONOMINAL2', 'DOMCLASEACCESORIO']
                    reg = [punto[0], punto[2], punto[7], punto[8], punto[9], punto[6], punto[10], punto[15], punto[16],
                           punto[13], punto[11], punto[12], punto[21], punto[22], punto[29]]
                else:
                    codo = os.path.join(workspace, 'acd_CodosPasivos')
                    campos = ['Shape@','DOMCLASECODO', 'DOMDIAMETRONOMINAL', 'DOMMATERIAL', 'COTARASANTE', 'PROFUNDIDAD',
                              'DOMESTADOENRED','LOCALIZACIONRELATIVA', 'ROTACIONSIMBOLO', 'FECHAINSTALACION', 'CONTRATO_ID',
                              'DOMCALIDADDATO', 'OBSERVACIONES']
                    reg = [punto[0], punto[29], punto[21], punto[13], punto[11], punto[12], punto[7], punto[8], punto[10],
                           punto[6], punto[16], punto[9], punto[15]]
                with arcpy.da.InsertCursor(codo, campos) as Incursor:
                    Incursor.insertRow(reg)
            elif tipo_nod in ('ACCESORIO_REDUCCION_4', 'ACCESORIO_TAPON_5', 'ACCESORIO_TEE_6', 'ACCESORIO_UNION_7',
                     'ACCESORIO_OTROS_8'):
                accesorio = os.path.join(workspace, 'acd_Accesorio')
                campos = ['Shape@', 'SUBTIPO','DOMESTADOENRED', 'LOCALIZACIONRELATIVA','DOMCALIDADDATO', 'FECHAINSTALACION',
                          'ROTACIONSIMBOLO', 'OBSERVACIONES','CONTRATO_ID', 'DOMMATERIAL', 'COTARASANTE', 'PROFUNDIDAD',
                          'DOMDIAMETRONOMINAL', 'DOMDIAMETRONOMINAL2', 'DOMCLASEACCESORIO']
                reg = [punto[0], punto[2], punto[7], punto[8], punto[9], punto[6], punto[10], punto[15], punto[16],
                        punto[13], punto[11], punto[12], punto[21], punto[22], punto[29]]
                with arcpy.da.InsertCursor(accesorio, campos) as Incursor:
                    Incursor.insertRow(reg)
            elif tipo_nod == 'HIDRANTE_9':
                hidrante = os.path.join(workspace, 'acd_Hidrante')
                campos = ['Shape@', 'SUBTIPO','DOMESTADOENRED', 'LOCALIZACIONRELATIVA','DOMCALIDADDATO', 'FECHAINSTALACION',
                          'ROTACIONSIMBOLO', 'OBSERVACIONES','CONTRATO_ID', 'DOMTIPOESPPUBLICO', 'DOMMATESPPUBLICO',
                          'DOMMATERIAL', 'DOMDIAMETRONOMINAL', 'MARCA', 'DOMFUNCIONPILAPUBLICA', 'DOMESTADOFISICO',
                          'COTARASANTE', 'DIRECCION', 'PRESION', 'FECHA_TOMA_P']
                reg = [punto[0], punto[2], punto[7], punto[8], punto[9], punto[6], punto[10], punto[15], punto[16],
                       punto[18], punto[19], punto[13], punto[21], punto[31], punto[32], punto[30], punto[11],
                       punto[73], punto[74], punto[39]]
                with arcpy.da.InsertCursor(hidrante, campos) as Incursor:
                    Incursor.insertRow(reg)
            elif tipo_nod == 'MACROMEDIDOR_10':
                macromedidor = os.path.join(workspace, 'acd_MacroMedidor')
                campos = ['Shape@', 'SUBTIPO', 'DOMESTADOENRED', 'LOCALIZACIONRELATIVA', 'DOMCALIDADDATO', 'FECHAINSTALACION',
                          'ROTACIONSIMBOLO', 'OBSERVACIONES', 'CONTRATO_ID', 'DOMTIPOESPPUBLICO', 'DOMMATESPPUBLICO',
                          'SECTORHIDENTRADA', 'SECTORHIDSALIDA', 'DIRECCION', 'CAUDAL_PROMEDIO', 'TIPO_M', 'FECHA_TOMA_C',
                          'NOMBRE']
                reg = [punto[0], punto[2], punto[7], punto[8], punto[9], punto[6], punto[10], punto[15], punto[16], punto[18],
                       punto[18], punto[34], punto[35], punto[73], punto[37], punto[38], punto[39], punto[72]]
                with arcpy.da.InsertCursor(macromedidor, campos) as Incursor:
                    Incursor.insertRow(reg)
            elif tipo_nod == 'PUNTO_ACOMETIDA_11':
                punto_aco = os.path.join(workspace, 'acd_PuntoAcometida')
                campos = ['Shape@', 'SUBTIPO', 'DOMESTADOENRED', 'LOCALIZACIONRELATIVA', 'DOMCALIDADDATO', 'FECHAINSTALACION',
                          'ROTACIONSIMBOLO', 'OBSERVACIONES', 'CONTRATO_ID', 'DIRECCION']
                reg = [punto[0], punto[2], punto[7], punto[8], punto[9], punto[6], punto[10], punto[15], punto[16],
                       punto[73]]
                with arcpy.da.InsertCursor(punto_aco, campos) as Incursor:
                    Incursor.insertRow(reg)
            elif tipo_nod == 'PILA_MUESTREO_12':
                pila_muest = os.path.join(workspace, 'acd_PilaMuestreo')
                campos = ['Shape@', 'SUBTIPO', 'DOMESTADOENRED', 'LOCALIZACIONRELATIVA', 'DOMCALIDADDATO', 'FECHAINSTALACION',
                          'ROTACIONSIMBOLO', 'OBSERVACIONES', 'CONTRATO_ID', 'DOMTIPOESPPUBLICO', 'DOMMATESPPUBLICO',
                          'DOMMATERIAL', 'DOMDIAMETRONOMINAL', 'COTARASANTE', 'DIRECCION', 'CENTRO', 'L_ALM', 'AREARESP',
                          'TIPO', 'FUENTEABAST', 'UBICACION', 'PTOANALISISBLQ', 'LOCPUNTO', 'ESTADO', 'FECHAESTADO',
                          'CLASEPUNTO', 'NOMBRE', 'LATITUD', 'LONGITUD']
                reg = [punto[0], punto[2], punto[7], punto[8], punto[9], punto[6], punto[10], punto[15], punto[16],
                       punto[18], punto[19], punto[13], punto[21], punto[11], punto[73], punto[41], punto[42],
                       punto[43], punto[44], punto[45], punto[46], punto[47], punto[48], punto[49], punto[50],
                       punto[51], punto[72], punto[4], punto[5]]
                with arcpy.da.InsertCursor(pila_muest, campos) as Incursor:
                    Incursor.insertRow(reg)
            elif tipo_nod == 'CAPTACION_13':
                captacion = os.path.join(workspace, 'acd_Captacion')
                campos = ['Shape@', 'SUBTIPO', 'DOMESTADOENRED', 'LOCALIZACIONRELATIVA', 'DOMCALIDADDATO', 'FECHAINSTALACION',
                          'ROTACIONSIMBOLO', 'OBSERVACIONES', 'CONTRATO_ID', 'NOMBRE', 'DIRECCION', 'COTARASANTE']
                reg = [punto[0], punto[2], punto[7], punto[8], punto[9], punto[6], punto[10], punto[15], punto[16],
                       punto[72], punto[73], punto[11]]
                with arcpy.da.InsertCursor(captacion, campos) as Incursor:
                    Incursor.insertRow(reg)
            elif tipo_nod == 'DESARENADOR_14':
                desarenador = os.path.join(workspace, 'acd_Desarenador')
                campos = ['Shape@', 'SUBTIPO', 'DOMESTADOENRED', 'LOCALIZACIONRELATIVA', 'DOMCALIDADDATO', 'FECHAINSTALACION',
                          'ROTACIONSIMBOLO', 'OBSERVACIONES', 'CONTRATO_ID', 'NOMBRE', 'DIRECCION', 'COTARASANTE']
                reg = [punto[0], punto[2], punto[7], punto[8], punto[9], punto[6], punto[10], punto[15], punto[16],
                       punto[72], punto[73], punto[11]]
                with arcpy.da.InsertCursor(desarenador, campos) as Incursor:
                    Incursor.insertRow(reg)
            elif tipo_nod == 'PLANTA_TRATAMIENTO_15':
                plant_trat = os.path.join(workspace, 'acd_PlantaTratamiento')
                campos = ['Shape@', 'SUBTIPO', 'DOMESTADOENRED', 'LOCALIZACIONRELATIVA', 'DOMCALIDADDATO', 'FECHAINSTALACION',
                          'ROTACIONSIMBOLO', 'OBSERVACIONES', 'CONTRATO_ID', 'NOMBRE', 'DIRECCION', 'COTARASANTE','NROFILTROS',
                          'NROSEDIMENTADORES','NROCOMPARTIMIENTOS','NROMEZCLADORES', 'NROFLOCULADORES', 'CAPACIDADINSTALADA']
                reg = [punto[0], punto[2], punto[7], punto[8], punto[9], punto[6], punto[10], punto[15], punto[16],
                       punto[72], punto[73], punto[11], punto[52], punto[53], punto[54], punto[55], punto[56], punto[57]]
                with arcpy.da.InsertCursor(plant_trat, campos) as Incursor:
                    Incursor.insertRow(reg)
            elif tipo_nod == 'ESTACION_BOMBEO_16':
                estacion_bom = os.path.join(workspace, 'acd_EstacionBombeo')
                campos = ['Shape@', 'SUBTIPO', 'DOMESTADOENRED', 'LOCALIZACIONRELATIVA', 'DOMCALIDADDATO', 'FECHAINSTALACION',
                          'ROTACIONSIMBOLO', 'OBSERVACIONES', 'CONTRATO_ID', 'NOMBRE', 'DIRECCION', 'COTARASANTE',
                          'CAPACIDADBOMBEO_M3_S', 'COTABOMBEOSUCCION', 'ALTURADINAMICATOTAL']
                reg = [punto[0], punto[2], punto[7], punto[8], punto[9], punto[6], punto[10], punto[15], punto[16],
                       punto[72], punto[73], punto[11], punto[59], punto[60], punto[61]]
                with arcpy.da.InsertCursor(estacion_bom, campos) as Incursor:
                    Incursor.insertRow(reg)
            elif tipo_nod == 'TANQUE_17':
                tanque = os.path.join(workspace, 'acd_Tanque')
                campos = ['Shape@', 'SUBTIPO', 'DOMESTADOENRED', 'LOCALIZACIONRELATIVA', 'DOMCALIDADDATO', 'FECHAINSTALACION',
                          'ROTACIONSIMBOLO', 'OBSERVACIONES', 'CONTRATO_ID', 'NOMBRE', 'DIRECCION', 'COTARASANTE',
                          'CAPACIDAD_M3', 'COTAFONDO', 'COTAREBOSE', 'NIVELMAXIMO', 'NIVELMINIMO', 'AREATRANSVERSAL_M2',
                          'DOMTIENETELEVIGILANCIA']
                reg = [punto[0], punto[2], punto[7], punto[8], punto[9], punto[6], punto[10], punto[15], punto[16], 
                       punto[72], punto[73], punto[11], punto[64], punto[62], punto[63], punto[65], punto[66], 
                       punto[67], punto[68]]
                with arcpy.da.InsertCursor(tanque, campos) as Incursor:
                    Incursor.insertRow(reg)
            elif tipo_nod == 'PORTAL_18':
                portal = os.path.join(workspace, 'acd_Portal')
                campos = ['Shape@', 'SUBTIPO', 'DOMESTADOENRED', 'LOCALIZACIONRELATIVA', 'DOMCALIDADDATO', 'FECHAINSTALACION',
                          'ROTACIONSIMBOLO', 'OBSERVACIONES', 'CONTRATO_ID', 'NOMBRE', 'DIRECCION', 'COTARASANTE']
                reg = [punto[0], punto[2], punto[7], punto[8], punto[9], punto[6], punto[10], punto[15], punto[16], 
                       punto[72], punto[73], punto[11]]
                with arcpy.da.InsertCursor(portal, campos) as Incursor:
                    Incursor.insertRow(reg)
            elif tipo_nod == 'CAMARA_ACCESO_19':
                cam_acce = os.path.join(workspace, 'acd_CamaraAcceso')
                campos = ['Shape@', 'SUBTIPO', 'DOMESTADOENRED', 'LOCALIZACIONRELATIVA', 'DOMCALIDADDATO', 'FECHAINSTALACION',
                          'ROTACIONSIMBOLO', 'OBSERVACIONES', 'CONTRATO_ID', 'NOMBRE', 'DIRECCION', 'COTARASANTE', 
                          'DOMTIPOACCESO', 'PROFUNDIDAD', 'DOMDIAMETROACCESO']
                reg = [punto[0], punto[2], punto[7], punto[8], punto[9], punto[6], punto[10], punto[15], punto[16], 
                       punto[72], punto[73], punto[11], punto[70], punto[12], punto[71]]
                with arcpy.da.InsertCursor(cam_acce, campos) as Incursor:
                    Incursor.insertRow(reg)
            elif tipo_nod == 'ESTRUCTURA_CONTROL_20':
                estr_contr =  os.path.join(workspace, 'acd_ValvulaControl')
                # OJO VERIFICAR OS ATRIBUTOS QUE SE INGRESARIANDEBEN INGRESAR
                pass
            elif tipo_nod == 'INSTRUMENTOS_MEDICION_21':
                inst_med = os.path.join(workspace, 'acd_CamaraAcceso')
                campos = ['Shape@', 'SUBTIPO', 'DESCRIPCIONLOCALIZACION', 'OBSERVACIONES', 'FECHAINSTALACION', 'COTARASANTE',
                          'DOMCALIDADDATO', 'MARCA', 'DIAMETRO', 'ESTADOFISICO', 'CAUDAL']
                reg = [punto[0], punto[2], punto[8], punto[15], punto[6], punto[11], punto[9], punto[31], punto[21],
                       punto[49], punto[37]]

# Migra la informacion de LINEAS ALCANTARILLADO
def migra_l_alc(clase_l_alc, workspace):
    for red in clase_l_alc:
        for line in clase_l_alc[red]:
            if red == 'redLocal_1':
                campos = ['Shape@','DOMDIAMETRONOMINAL','DOMMATERIAL','DOMMATERIALESPPUBLICO','DOMTIPOSISTEMA','COTARASANTEINICIAL',
                          'COTACLAVEINICIAL','COTABATEAINICIAL','COTARASANTEFINAL','COTACLAVEFINAL','COTABATEAFINAL','FECHAINSTALACION',
                          'DOMESTADOENRED','DOMCALIDADDATO','DOMESTADOLEGAL','OBSERVACIONES','CONTRATO_ID','LONGITUD_M','DOMMATERIAL2',
                          'NUMEROCONDUCTOS','DOMTIPOSECCION','DOMCAMARACAIDA','BASE','ALTURA1','DOMMETODOINSTALACION','PROFUNDIDADMEDIA',
                          'PENDIENTE','SUBTIPO', 'DISENO_ID']
                reg = [line[0], line[11],line[7],line[36],line[5],line[18],line[20],line[22],line[19],line[21],line[23],line[6],line[10],
                       line[13],line[14],line[15],line[16],line[34],line[8],line[30],line[12],line[17],line[26],line[28],line[35],line[27],
                       line[24],line[2],line[9]]
                
                capa = 'als_RedLocal' if line[5] in ('0', '2') else 'alp_RedLocal'
                redLocal = os.path.join(workspace, capa)
                
                with arcpy.da.InsertCursor(redLocal, campos) as Incursor:
                    Incursor.insertRow(reg)
                    
            elif red == 'redTroncal_2':
                campos = ['Shape@', 'DOMDIAMETRONOMINAL','DOMMATERIAL','DOMMATERIALESPPUBLICO','DOMTIPOSISTEMA','COTARASANTEINICIAL',
                          'COTACLAVEINICIAL','COTABATEAINICIAL','COTARASANTEFINAL','COTACLAVEFINAL','COTABATEAFINAL','FECHAINSTALACION',
                          'DOMESTADOENRED','DOMCALIDADDATO','DOMESTADOLEGAL','OBSERVACIONES','CONTRATO_ID','LONGITUD_M','DOMMATERIAL2',
                          'NUMEROCONDUCTOS','DOMTIPOSECCION','DOMCAMARACAIDA','BASE','ALTURA1','DOMMETODOINSTALACION','PROFUNDIDADMEDIA',
                          'PENDIENTE','ALTURA2','TALUD1','TALUD2','ANCHOBERMA','NOMBRE','SUBTIPO','DISENO_ID']
                reg = [line[0],line[11],line[7],line[36],line[5],line[18],line[20],line[22],line[19],line[21],line[23],line[6],line[10],line[13],
                       line[14],line[15],line[16],line[34],line[8],line[30],line[12],line[17],line[26],line[28],line[35],line[27],line[24],
                       line[29],line[32],line[33],line[31],line[25],line[2], line[9]]
                
                capa = 'als_RedTroncal' if line[5] in ('0', '2') else 'alp_RedTroncal'
                redTroncal = os.path.join(workspace, capa)
                
                with arcpy.da.InsertCursor(redTroncal, campos) as Incursor:
                    Incursor.insertRow(reg)
            
            elif red == 'linLat_3':
                campos = ['Shape@', 'DOMDIAMETRONOMINAL','DOMMATERIAL','DOMMATERIALESPPUBLICO','DOMTIPOSISTEMA','COTARASANTEINICIAL',
                          'COTACLAVEINICIAL','COTABATEAINICIAL','COTARASANTEFINAL','COTACLAVEFINAL','COTABATEAFINAL','FECHAINSTALACION',
                          'DOMESTADOENRED','DOMCALIDADDATO','DOMESTADOLEGAL','OBSERVACIONES','CONTRATO_ID','LONGITUD_M','SUBTIPO','DISENO_ID']
                reg = [line[0],line[11],line[7],line[36],line[5],line[18],line[20],line[22],line[19],line[21],line[23],line[6],line[10],line[13],
                       line[14],line[15],line[16],line[34],line[2],line[9]]

                capa = 'als_LineaLateral' if line[5] in ('0', '2') else 'alp_LineaLateral'
                lineaLateral = os.path.join(workspace, capa)
                
                with arcpy.da.InsertCursor(lineaLateral, campos) as Incursor:
                    Incursor.insertRow(reg)

# Migra la informacion de PUNTOS ALCANTARILLADO
def migra_p_alc(clase_p_alc, workspace):
    for tipo_nod in clase_p_alc:
        for punto in clase_p_alc[tipo_nod]:
            if tipo_nod == 'ESTRUCTURA_RED_1':
                campos = ['Shape@','DOMTIPOSISTEMA','COTARASANTE','DOMMATERIAL','FECHAINSTALACION','DOMESTADOENRED','DOMCALIDADDATO',
                          'OBSERVACIONES','CONTRATO_ID','DIRECCION','LOCALIZACIONRELATIVA','ROTACIONSIMBOLO','DOMTIENECABEZAL',
                          'DOMESTADOFISICO','DOMTIPOVALVULAANTIRREFLUJO','COTAFONDO','COTACRESTA',
                          'COTATECHOVERTEDERO','LONGVERTEDERO','LARGOESTRUCTURA','ANCHOESTRUCTURA','ALTOESTRUCTURA','CAUDALBOMBEO',
                          'DOMTIPOBOMBEO','UNIDADESBOMBEO','ALTURABOMBEO','COTABOMBEO','VOLUMENBOMBEO','NOMBRE','SUBTIPO', 'DISENO_ID']
                reg = [punto[0],punto[16],punto[11],punto[14],punto[6],punto[9],punto[15],punto[18],punto[19],punto[55],punto[10],punto[27],
                       punto[30],punto[29],punto[8],punto[13],punto[43],punto[44],punto[45],punto[46],punto[47],punto[48],
                       punto[49],punto[50],punto[51],punto[52],punto[53],punto[54],punto[17],punto[2], punto[20]]
            
                capa = 'als_EstructuraRed' if punto[16] in ('0', '2') else 'alp_EstructuraRed'
                estructRed = os.path.join(workspace, capa)
                
                with arcpy.da.InsertCursor(estructRed, campos) as Incursor:
                    Incursor.insertRow(reg)
                    
            if tipo_nod == 'POZO_2':
                campos = ['Shape@','DOMTIPOSISTEMA','COTARASANTE','FECHAINSTALACION','DOMESTADOENRED','DOMCALIDADDATO','OBSERVACIONES',
                          'CONTRATO_ID','DIRECCION','DOMESTADOFISICO','COTATERRENO','COTAFONDO','PROFUNDIDAD','DOMINICIALVARIASCUENCAS',
                          'DOMCAMARASIFON','DOMESTADOPOZO','DOMTIPOALMACENAMIENTO','SUBTIPO', 'DISENO_ID']
                reg = [punto[0],punto[16],punto[11],punto[6],punto[9],punto[15],punto[18],punto[19],punto[55],punto[29],punto[12],punto[13],
                       punto[21],punto[26],punto[28],punto[32],punto[42],punto[2], punto[20]]
                
                capa = 'als_Pozo' if punto[16] in ('0', '2') else 'alp_Pozo'
                pozo = os.path.join(workspace, capa)
                
                with arcpy.da.InsertCursor(pozo, campos) as Incursor:
                    Incursor.insertRow(reg)

            if tipo_nod == 'SUMIDERO_3':
                campos = ['Shape@','DOMTIPOSISTEMA','COTARASANTE','DOMMATERIAL','FECHAINSTALACION','DOMESTADOENRED','DOMCALIDADDATO','OBSERVACIONES',
                          'CONTRATO_ID','DIRECCION','LOCALIZACIONRELATIVA','ROTACIONSIMBOLO','SUBTIPO', 'DISENO_ID']
                reg = [punto[0],punto[16],punto[11],punto[14],punto[6],punto[9],punto[15],punto[18],punto[19],punto[55],punto[10],punto[27],punto[2], punto[20]]
                
                capa = 'als_Sumidero' if punto[16] in ('0', '2') else 'alp_Sumidero'
                sumidero = os.path.join(workspace, capa)

                with arcpy.da.InsertCursor(sumidero, campos) as Incursor:
                    Incursor.insertRow(reg)
            
            if tipo_nod == 'CAJA_DOMICILIARIA_4':
                campos = ['Shape@','DOMTIPOSISTEMA','COTARASANTE','DOMMATERIAL','FECHAINSTALACION','DOMESTADOENRED','DOMCALIDADDATO','OBSERVACIONES',
                          'CONTRATO_ID','DIRECCION','LOCALIZACIONRELATIVA','ROTACIONSIMBOLO','SUBTIPO', 'DISENO_ID']
                reg = [punto[0],punto[16],punto[11],punto[14],punto[6],punto[9],punto[15],punto[18],punto[19],punto[55],punto[10],punto[27],punto[2], punto[20]]
                
                capa = 'als_CajaDomiciliaria' if punto[16] in ('0', '2') else 'alp_CajaDomiciliaria'
                cajaDom = os.path.join(workspace, capa)
                
                with arcpy.da.InsertCursor(cajaDom, campos) as Incursor:
                    Incursor.insertRow(reg)
            
            if tipo_nod == 'SECCION_TRANSVERSAL_5':
                campos = ['Shape@','NOMBRE','ABSCISA','DISTANCIADESDEORIGEN','DOMORIGENSECCION']
                reg = [punto[0],punto[17],punto[61],punto[60],punto[59]]
                
                capa = 'als_SeccionTransversal' if punto[16] in ('0', '2') else 'alp_SeccionTransversal'
                secTrans = os.path.join(workspace, capa)

                with arcpy.da.InsertCursor(secTrans, campos) as Incursor:
                    Incursor.insertRow(reg)


# valida que existan datos a mirar de lo contrario False
def datos(clase):
    n = False
    for clas in clase:
        if len(clase[clas]) > 0:
            n = True
    return n

# Migra la informacion de todas las capas
def migracion_datos(clase_l, clase_p_acu, clase_l_alc, clase_p_alc, l_alc_pluv_orig, p_alc_pluv_orig, workspace):
    editor = arcpy.da.Editor(workspace)
    editor.startEditing(with_undo=False, multiuser_mode=False)
    editor.startOperation()

    if datos(clase_l):
        migra_l_acu(clase_l, workspace)
    if datos(clase_p_acu):
        migra_p_acu(clase_p_acu, workspace)
    if datos(clase_l_alc):
        migra_l_alc(clase_l_alc, workspace)
    if datos(clase_p_alc):
        migra_p_alc(clase_p_alc, workspace)
    if datos(l_alc_pluv_orig):
        migra_l_alc(l_alc_pluv_orig, workspace)
    if datos(p_alc_pluv_orig):
        migra_p_alc(p_alc_pluv_orig, workspace)

    editor.stopOperation()
    editor.stopEditing(save_changes=True)


# ------------------------------------- EJECUCION PRINCIPAL -------------------------------------
# funcion que recoje la informacion de validacion y migracion de informacion
def script_tool(l_acu_orig, p_acu_orig, l_alc_orig, p_alc_orig, l_alc_pluv_orig, p_alc_pluv_orig, workspace, migr_adver):
    # Validacion de la estructura de la informacion
    clase_l, er_l_acu, clase_p_acu, er_p_acu, clase_l_alc, er_l_alc, clase_p_alc, er_p_alc,clase_l_alc_pluv, error_clase_l_alc_pluv, clase_p_alc_pluv, error_clase_p_alc_pluv  = validacion_estruct(l_acu_orig, p_acu_orig, l_alc_orig, p_alc_orig, l_alc_pluv_orig, p_alc_pluv_orig, workspace)

    if migr_adver == 'true':
        # Creando la gdb con la estructura vacia correspondiente
        workspace = estruc_vacia_bd(workspace)
        # OJO NO OLVIDAR VALIDAR QUE SI HAY ERRORES NO SE REALICE LA MIRACION DE INFO..
        migracion_datos(clase_l, clase_p_acu, clase_l_alc, clase_p_alc, clase_l_alc_pluv, clase_p_alc_pluv, workspace)
    else:
        if er_l_acu == 0 and er_p_acu == 0 and er_l_alc == 0 and er_p_alc == 0 and error_clase_l_alc_pluv == 0 and error_clase_p_alc_pluv == 0:
            # Creando la gdb con la estructura vacia correspondiente
            workspace = estruc_vacia_bd(workspace)
            # OJO NO OLVIDAR VALIDAR QUE SI HAY ERRORES NO SE REALICE LA MIRACION DE INFO..
            migracion_datos(clase_l, clase_p_acu, clase_l_alc, clase_p_alc, clase_l_alc_pluv, clase_p_alc_pluv, workspace)
        else:
            arcpy.AddWarning("Revise la ruta de salida para conocer los detalles de las inconsistencias..")

if __name__ == "__main__":
    workspace = arcpy.GetParameterAsText(0)
    l_acu_orig = arcpy.GetParameterAsText(1)
    p_acu_orig = arcpy.GetParameterAsText(2)
    l_alc_orig = arcpy.GetParameterAsText(3)
    p_alc_orig = arcpy.GetParameterAsText(4)
    l_alc_pluv_orig = arcpy.GetParameterAsText(5)
    p_alc_pluv_orig = arcpy.GetParameterAsText(6)
    migr_adver = arcpy.GetParameterAsText(7)

    arcpy.AddMessage(f"Ruta de la GDB de salida:\n{workspace}")

    script_tool(l_acu_orig, p_acu_orig, l_alc_orig, p_alc_orig, l_alc_pluv_orig, p_alc_pluv_orig, workspace, migr_adver)
    #arcpy.SetParameterAsText(2, "Result")