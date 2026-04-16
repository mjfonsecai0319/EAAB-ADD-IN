# Diagrama de flujo — ClipFeatureDatasetUseCase

Diagrama Mermaid que representa el flujo principal del método `ExecuteAsync` en `ClipFeatureDatasetUseCase.cs`.

```mermaid
flowchart TD
  Start([Inicio]) --> Validate[Validar parámetros:\noutputGdbPath, sourceGdbPath, featureDatasets, clipPolygon]
  Validate -- "Inválidos" --> Error[Retornar error con mensaje y terminar]
  Validate -- "Válidos" --> EnsureOutGDB[Ensure: GDB de salida (CreateFileGDB si no existe)]
  EnsureOutGDB --> CreateTempGDB[Crear GDB temporal]
  CreateTempGDB --> CheckBuffer{bufferMeters > 0?}
  CheckBuffer -- "Sí" --> ApplyBuffer[Aplicar buffer (GeometryEngine.Buffer)]
  ApplyBuffer --> BufferType{useRoundedBuffer?}
  BufferType -- "Sí" --> Rounded[Buffer redondeado (exacto)]
  BufferType -- "No" --> Approx[Buffer aproximado: Buffer + Generalize (puntas rectas)]
  Rounded --> AfterBuffer[Obtener polígono buffered]
  Approx --> AfterBuffer
  CheckBuffer -- "No" --> AfterBuffer[Usar polígono original]
  AfterBuffer --> ValidBuffered{¿Polígono (buffered) válido y no vacío?}
  ValidBuffered -- "No" --> Error
  ValidBuffered -- "Sí" --> CreateMask[Crear máscara 'ClipMask' en GDB temporal\n(CreateFeatureclass + insertar polígono)]
  CreateMask --> DatasetLoop[Por cada Feature Dataset]
  DatasetLoop --> CreateDataset[Crear Feature Dataset en GDB de salida\n(usar SR del dataset fuente)]
  CreateDataset --> FCLoop[Por cada Feature Class en el dataset]
  FCLoop --> DeleteExisting[Borrar salida existente (si existe)]
  DeleteExisting --> RunClip[Ejecutar analysis.Clip(sourceFc, maskFc, outputFc)]
  RunClip --> ClipOK{¿Clip exitoso?}
  ClipOK -- "Sí" --> RecordSuccess[Registrar éxito (incrementar contador)]
  ClipOK -- "No" --> RecordFailure[Registrar fallo (agregar a lista de fallidos)]
  RecordSuccess --> FCLoop
  RecordFailure --> FCLoop
  FCLoop --> DatasetLoop
  DatasetLoop --> Cleanup[Eliminar GDB temporal (si existe)]
  Cleanup --> Summary[Construir mensaje resumen:\nFeature Datasets creados, éxitos, fallos, buffer info]
  Summary --> End([Fin])
```

**Notas rápidas**:
- `bufferMeters` debe estar en las unidades del mapa; considerar proyectar si se usa metros.
- El flujo registra éxitos y fallos por Feature Class y retorna `(bool success, string message)`.
- Se usa `QueuedTask.Run` para operaciones con `Geodatabase` y `Geoprocessing.ExecuteToolAsync` para herramientas GP.
