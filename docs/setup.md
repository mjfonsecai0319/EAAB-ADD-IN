# Configuración y compilación

## Requisitos previos
- Windows con ArcGIS Pro 3.4 (o compatible según el proyecto).
- Visual Studio con ArcGIS Pro SDK.
- .NET 8 SDK.

## Compilación
1. Abrir `EAABAddIn.sln` en Visual Studio.
2. Compilar en `Debug` o `Release`.
3. Localizar el archivo `EAABAddIn.esriAddinX` en `bin/<config>/net8.0-windows/`.

## Instalación del Add-In
- Doble clic al `.esriAddinX` o instálelo desde ArcGIS Pro (Add-In Manager).

## Configuración de conexión
- Abra Opciones → Página “EAAB-ADD-IN”.
- Configure:
  - Motor: Oracle o PostgreSQL
  - Host y Puerto
  - Usuario y Contraseña
  - Base de datos/Servicio
- Los valores se guardan en `%APPDATA%/EAABAddIn/settings.json`.
