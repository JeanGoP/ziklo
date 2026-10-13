# Municipios de Colombia

`divipola-municipios.csv` contiene 1.103 municipios con código DIVIPOLA, nombre y departamento. Se tomó del conjunto público [DIVIPOLA - Códigos municipios](https://www.datos.gov.co/Mapas-Nacionales/DIVIPOLA-C-digos-municipios/gdxc-w37w), con corte informado al 30 de diciembre de 2024. Se excluyeron las 18 áreas no municipalizadas y la isla que el conjunto distingue de los municipios.

La migración `ColombianMunicipalities` crea y carga la tabla `Municipalities`. Para el servidor SQL Server ya inicializado, ejecutar `database/20261013_colombian_municipalities.sql` sobre la base `ziklo` antes de publicar el backend y el frontend de esta funcionalidad. El script es idempotente y registra la migración en `__EFMigrationsHistory`.
