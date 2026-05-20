# Planning: Split Migration Use Cases

Objective: refactor ACU/ALC migrations into separate, focused use cases, remove repeated code via shared helpers, and apply modern C#/.NET practices at a medium scope.

## Decisions
- Remove legacy use cases and update call sites to new classes.
- Shared helpers for FeatureClass operations and map-layer plumbing.
- Domain-specific symbology and class lists stay in each domain.
- Alcantarillado split by sanitary/pluvial plus line/point use cases.
- Best-practices scope is medium (XML docs, modern syntax, helper extraction).

## Steps
1. Inventory and contracts
   - Confirm all call sites for MigrateAcueductoUseCase and MigrateAlcantarilladoUseCase.
   - List required public surface (lines/points migrate + add-to-map flow).

2. Shared helpers
   - Extend FeatureClassUtils with reusable FC open helpers.
   - Add shared utilities for field access, type coercion, row insertion (SR/Z/M), and CSV summary wiring.

3. Acueducto split
   - Implement lines in MigrateAcuLinesUseCase.
   - Create MigrateAcuPointsUseCase.
   - Move line/point mappings and loops, use shared helpers, add XML docs.

4. Alcantarillado split (sanitary/pluvial)
   - Create four use cases: sanitary lines/points and pluvial lines/points.
   - Use fixed als_/alp_ prefixes; keep SISTEMA only for attributes.
   - Use shared helpers.

5. Map-layer helper
   - Centralize common AddFeatureLayer/EnsureLayer logic in a helper.
   - Keep domain-specific symbology and class lists in ACU/ALC.

6. Update orchestration and cleanup
   - Update MigrationViewModel to call new use cases.
   - Remove old MigrateAcueductoUseCase and MigrateAlcantarilladoUseCase.

7. Quality pass
   - XML docs for new public classes/methods.
   - Align logging strings and check for remaining duplication.

## Verification
1. Build the solution and confirm no compile errors.
2. Run ACU line/point and ALC sanitary/pluvial line/point migrations and verify logs + CSV summaries.
3. Confirm layers are added with correct symbology and extents.

## Open questions
- Should ALC use cases filter by SISTEMA (skip mismatches) or assume input datasets are already separated?

## Notes: MigrateLines flow (Acueducto)
- Runs inside `QueuedTask.Run` to execute ArcGIS Pro SDK operations on the MCT.
- Validates inputs: non-empty paths and target GDB exists.
- Opens the source FeatureClass (shp or gdb FC), then opens the target GDB.
- Iterates source features; for each feature:
   - Reads `CLASE` and `SUBTIPO`.
   - Resolves target class name from `CLASE` (mapping table).
   - Verifies target FC exists in the GDB.
   - Ensures the target layer is added to the active map once per class.
   - Builds attribute dictionary for the target schema, coerces types, and inserts row (ApplyEdits).
   - Tracks per-class stats and error messages.
- Writes a CSV summary report (per-class attempts/migrated/failed plus `_SinCLASE`/`_SinDestino`).
- Returns `(ok, message)` with a log summary or error.
