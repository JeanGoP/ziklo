# Ziklo

Ziklo es un sistema web en desarrollo para préstamos, recaudos y cartera. El trabajo sigue las fases descritas en `docs`. En desarrollo usa una base SQLite local persistente y acceso con usuarios, contraseñas y perfiles.

## Estado actual

| Fase | Estado |
|---|---|
| 1. Arquitectura | Definida en la conversación inicial. |
| 2. Modelo de datos | Especificado en `docs/02-modelo-de-datos.md`. |
| 3. FinancialEngine | Fórmulas y reglas propuestas en `docs/03-financial-engine.md`. |
| 4. Simulador | API y página responsive ejecutables; sin escrituras financieras. |
| 5. Clientes, productos y préstamos | Entidades, servicios, migraciones y endpoints con pertenencia a empresa y perfiles. La originación conecta radicación, estudio, decisión, préstamo vinculado, soporte contractual firmado y registro de desembolso. La operación con datos y contratos reales depende de las autorizaciones de la empresa. |
| 6. Tabla de amortización | El motor la calcula; el desembolso la guarda como primera versión y la API permite consultarla. |
| 7. Pagos | Recaudos exigibles persistidos con prioridad contractual, aplicaciones, libro inmutable e idempotencia; pantalla de recaudos y consulta de recibos. Faltan anticipos, excedentes, reversos y caja. |
| 8. Mora y cierre | Tasa, gracia y tope copiados al préstamo; cierre por fecha, mora por cuota y causaciones en libro con idempotencia; evaluación de promesas. |
| 9. Cartera y cobranza | Consulta de saldos y edades de cartera; bitácora de gestiones y promesas. Faltan alertas y vistas operativas. |
| 10. Dashboard y reportes | Panel de demostración y nueve informes con vista previa, CSV, Excel y PDF mediante impresión del navegador. Rentabilidad, condonaciones y refinanciaciones requieren sus módulos financieros. |
| 11. Seguridad, auditoría e integraciones | Permisos ajustables por empresa y rol, auditoría de cambios sensibles y outbox transaccional; falta publicador externo. |
| 12. Pruebas y consistencia | 32 comprobaciones de motor, persistencia y conciliación; falta prueba contra SQL Server real y carga concurrente. |

## Proyectos

- `src/Ziklo.FinancialEngine`: cálculos puros y estrategias de amortización.
- `src/Ziklo.Api`: API de simulación y frontend estático.
- `src/Ziklo.Domain`: entidades y estados del negocio.
- `src/Ziklo.Application`: contratos de casos de uso.
- `src/Ziklo.Infrastructure`: EF Core, SQL Server, servicios y migración.
- `tests/Ziklo.FinancialEngine.Checks`: comprobaciones numéricas y flujo de persistencia en SQLite temporal.

## Identidad visual

Los logos vectoriales y sus exportaciones PNG están en `src/Ziklo.Api/wwwroot/brand/`: `ziklo-logo-dark` para fondos oscuros, `ziklo-logo-light` para fondos claros y `ziklo-symbol` como icono cuadrado. El sistema usa la versión oscura en las cabeceras y el símbolo como favicon. Los SVG son escalables y los PNG tienen fondo transparente.

## Ejecutar la demostración

Requiere .NET SDK 8 o superior.

```powershell
dotnet restore Ziklo.sln
dotnet run --project src/Ziklo.Api --no-restore --urls http://127.0.0.1:5187
```

Abre `http://127.0.0.1:5187/login.html`. La primera vez, crea el correo y una contraseña de al menos 12 caracteres para el administrador. Después ingresa y crea las cuentas del equipo en `/usuarios.html`. La configuración inicial solo está disponible desde este equipo y se cierra tras crear la contraseña. Los datos locales se guardan en `src/Ziklo.Api/demo-data/ziklo-demo.db` y permanecen entre reinicios; la clave de autenticación local también se conserva allí. El simulador en `/` usa `POST /api/simulations/loans` y nunca crea movimientos reales. Swagger está en `/swagger`.

La demostración trae cinco clientes, tres préstamos con saldo, uno cancelado, una solicitud rechazada, cuatro recaudos y una promesa de pago. Puedes crear más clientes y recaudos para la reunión. Usa `/informes.html` para presentar y exportar informes.

`/solicitar.html` permite radicar una nueva solicitud ficticia sin crear todavía un préstamo. La captura es un flujo web de seis etapas: crédito, datos personales, actividad e ingresos, referencias, codeudor y revisión. Los datos complementarios se despliegan cuando se necesitan y el codeudor aparece solo si se indica que aplica. El formulario conserva los apartados del formato físico de dos páginas. Tras radicar, **Ver e imprimir solicitud** abre `/solicitud-impresa.html?radicado=...`, que presenta los datos en dos páginas tamaño carta y permite imprimir o guardar como PDF desde el navegador. El encabezado toma el nombre y NIT de la única empresa de la instalación. Los apartados de autorización mantienen su lugar en el formato, con textos provisionales claramente marcados para demostración; la empresa deberá reemplazarlos por los suyos antes de operar con clientes reales. Se guarda el detalle en `CreditApplications.FormDataJson` y hasta tres soportes PDF, PNG o JPEG de 10 MB cada uno en `CreditApplicationDocuments`. Los endpoints de demostración solo funcionan desde el equipo local. No introduzcas datos reales en esta etapa.

Cada instalación admite **una sola empresa**. El aprovisionamiento inicial crea esa empresa, el servidor no inicia si la base contiene más de una y las pantallas muestran su nombre sin selector. `CompanyId` se conserva como clave interna para las relaciones y rutas existentes; no representa una opción de multiempresa para el usuario.

El equipo puede consultar las solicitudes radicadas mediante `GET /api/companies/{companyId}/credit-applications`, abrir una ficha en `GET /credit-applications/{applicationId}` y descargar sus soportes desde la ruta de documentos de esa ficha; estas lecturas requieren el permiso `ViewLoans`. Cuando un usuario autenticado radica desde `/solicitar.html`, se guarda su identificador como responsable de la radicación. Las solicitudes anteriores a este cambio no tienen responsable asignado automáticamente.

`/solicitudes.html` muestra la bandeja de estudio y, junto al estado, el siguiente paso y las acciones disponibles para el perfil. Una solicitud pasa de `Submitted` a `InReview`, puede solicitar información (`NeedsInformation`) y volver al estudio, y termina en `Approved` o `Rejected`. `POST /api/companies/{companyId}/credit-applications/{applicationId}/reviews` recibe `status`, `notes`, `verifiedMonthlyIncome` y `verifiedMonthlyObligations`. Iniciar o retomar el estudio y pedir información requieren `StudyApplications`; aprobar o rechazar requiere `ApproveLoan`. El radicador no puede estudiar ni decidir. Cada cambio deja un registro inmutable con usuario, fecha, cifras verificadas y observaciones. La aprobación exige ingresos y obligaciones verificados; pedir información o rechazar exige un motivo. Cuando se pide información, el usuario que radicó ve un aviso con el motivo en la bandeja y en el menú del sistema. Puede responder con una nota y hasta diez PDF adicionales desde su aviso; solo él puede usar `/response` y `/supplements` para esa solicitud. La respuesta devuelve el radicado a `InReview` y queda en el historial. Los avisos se consultan cada 30 segundos mientras Ziklo está abierto y desaparecen al retomar el estudio. Todavía no se envía correo ni mensaje al solicitante. Una decisión final no se cambia desde esta ruta.

Una solicitud aprobada puede convertirse una sola vez con `POST /api/companies/{companyId}/credit-applications/{applicationId}/loan?approvalDate=AAAA-MM-DD`. La operación reutiliza al cliente si coinciden documento y nombre, o lo crea, y vincula un préstamo en estado `Approved`. Requiere `CreateLoan`; la decisión previa exige `ApproveLoan`. El usuario con `DisburseLoan` adjunta un PDF de contrato firmado en `POST /credit-applications/{applicationId}/signed-contract?fileName=...` con cuerpo `application/pdf`. El soporte es inmutable, conserva SHA-256 y su incorporación queda en auditoría con usuario y fecha. Después de verificar el soporte y la entrega de fondos por fuera de Ziklo, registra el desembolso en `POST /loans/{loanId}/disburse`. Esto activa el préstamo, crea su plan de cuotas y cambia la solicitud a `Disbursed` en la misma operación. La clave de idempotencia impide duplicar el desembolso. Ziklo no transfiere fondos ni valida la autenticidad legal de la firma; la empresa debe proporcionar contratos y autorizaciones aprobados antes de usar datos reales.

En el entorno de desarrollo sin acceso a NuGet se puede restaurar desde la caché local con `dotnet restore Ziklo.sln --configfile NuGet.Offline.Config`. Ese archivo elimina fuentes remotas solo para esa invocación; no sustituye la restauración normal en otras máquinas.

## Habilitar la operación con SQL Server

El modo local es exclusivo del entorno `Development`; no admite combinarlo con SQL Server ni una clave JWT externa. Para usar SQL Server, establece `Ziklo__DemoMode=false`, configura `ConnectionStrings__Ziklo` con una conexión a SQL Server y `ZIKLO_JWT_KEY` con una clave aleatoria de al menos 32 bytes UTF-8. El mismo formulario de ingreso funciona con SQL Server. No se incluye ninguna credencial predeterminada para SQL Server.

Para aprovisionar la primera empresa y usuario administrador, configura temporalmente `ZIKLO_SEED_EMAIL`, `ZIKLO_SEED_PASSWORD` (mínimo 12 caracteres), `ZIKLO_SEED_COMPANY_NAME` y `ZIKLO_SEED_TAX_ID`, y ejecuta:

```powershell
dotnet run --project src/Ziklo.Api --no-restore -- --seed-admin
```

El comando aplica migraciones y solo funciona si todavía no hay usuarios. Después elimina las variables de aprovisionamiento. Inicia la API normalmente y usa `POST /api/auth/login` para obtener un JWT. Los endpoints empresariales están bajo `/api/companies/{companyId}` y comprueban la pertenencia activa del usuario en la base de datos en cada solicitud. Los roles son iniciales; la configuración fina de permisos corresponde a la fase 11.

## Comprobaciones

```powershell
dotnet run --project tests/Ziklo.FinancialEngine.Checks --no-restore
```

Las comprobaciones cubren cuota fija, capital fijo, interés simple, capitalización explícita, pago único, cuotas personalizadas, tasa cero, año bisiesto, fin de mes, aplicación parcial de pagos, excedentes, liquidación, mora, reconstrucción de saldos, modelo EF, aislamiento empresarial, desembolso idempotente y recaudo parcial persistido.

Las referencias del cliente se crean con `POST /api/companies/{companyId}/clients/{clientId}/references` y se consultan con `GET` en la misma ruta. El tipo es `Personal` o `Commercial`; la API exige permisos de clientes y comprueba la empresa del cliente.

Los documentos se adjuntan con `POST /api/companies/{companyId}/clients/{clientId}/documents?fileName=...`, cuerpo binario y `Content-Type` correspondiente. Se admiten PDF, PNG y JPEG de hasta 10 MB; se valida la firma inicial y se conserva SHA-256, usuario y metadatos. `GET` en la misma ruta lista metadatos; `GET /documents/{documentId}` entrega el archivo como descarga. El contenido no se incluye en la auditoría JSON. La pantalla de clientes permite adjuntar y descargar. Antes de operar en producción debe añadirse análisis antimalware y definir política de retención documental.

Una solicitud de préstamo puede rechazarse con `POST /api/companies/{companyId}/loans/{loanId}/reject` y cuerpo `{ "decisionDate": "AAAA-MM-DD", "reason": "..." }`. Se exige permiso de aprobación; el motivo, fecha y responsable quedan en un registro inmutable consultable en `GET /loans/{loanId}/rejection` dentro de la misma empresa.

## Recaudos exigibles

`POST /api/companies/{companyId}/loans/{loanId}/payments` acepta `effectiveDate`, `amount`, `method`, `externalReference`, `idempotencyKey` y `installmentNumbers` para dirigir el pago a una o varias cuotas exigibles. El usuario autenticado queda registrado como responsable; los perfiles autorizados pueden registrar el recaudo. La respuesta incluye el recibo y su aplicación por cuota y componente; `GET /api/companies/{companyId}/loans/{loanId}/payments/{receiptId}` permite consultarlo dentro de la misma empresa.

El flujo actual acepta pagos de cuotas cuya fecha de vencimiento ya llegó. Causa en el libro el interés, seguro y otros cargos de esas cuotas si aún no estaban registrados, registra las capitalizaciones pactadas en movimientos separados, calcula la mora sobre capital vencido pendiente hasta la fecha del recaudo, aplica el pago por la prioridad `paymentPriority` definida en la versión del producto y copiada al contrato, y confirma todo en una transacción. Debe contener cada categoría (`Charges`, `LateInterest`, `OverdueCurrentInterest`, `OverduePrincipal`, `CurrentInterest`, `Principal`) exactamente una vez; si se omite, rige ese orden predeterminado. Repetir exactamente la misma clave devuelve el mismo recibo; reutilizarla con otros datos se rechaza. Al pagar todas las cuotas marca el préstamo como cancelado. Los importes superiores a la deuda exigible se rechazan. Por ahora no se registran anticipos, saldo a favor, reversos ni caja.

## Cierre diario y mora

La versión del producto puede definir `lateAnnualRate` (fracción anual; cero desactiva mora), `lateGraceDays`, `lateGraceMode` (`Prospective` o `Retroactive`) y `lateMaximumPerInstallment`. La aprobación copia estos valores a las condiciones del préstamo. La base implementada para mora es el capital vencido impago de cada cuota; los pagos parciales reducen esa base desde su fecha efectiva. No se capitaliza mora.

`POST /api/companies/{companyId}/daily-closes` recibe `businessDate` y exige rol administrador o gerente. Causa cuotas exigibles, calcula el incremento de mora y registra evaluaciones, eventos y movimientos en una transacción. Una segunda ejecución de la misma fecha devuelve el cierre original. El cierre marca en mora los préstamos con obligaciones anteriores a esa fecha aún pendientes y evalúa promesas vencidas. Después de cerrar una fecha, no se aceptan recaudos con fecha efectiva igual o anterior. El proceso se invoca por API; todavía no existe un programador automático.

## Cartera y cobranza

`GET /api/companies/{companyId}/portfolio?asOf=AAAA-MM-DD` devuelve saldos publicados en el libro por préstamo, días de atraso y clasificación por edades. Las cuotas permiten detectar obligaciones vencidas; el total financiero contiene solo importes ya causados. Una consulta histórica utiliza las fechas efectivas de los eventos y recibos.

`POST /api/companies/{companyId}/loans/{loanId}/collections` registra una gestión con `contactDate`, `channel`, `result`, `notes` y `nextContactDate` opcional. Puede incluir `promiseDueDate` y `promisedAmount`. `GET` en la misma ruta lista las gestiones y el estado de cada promesa. El cierre evalúa las promesas pendientes cuyo vencimiento llegó, según los recaudos recibidos desde su creación hasta su fecha prometida, y conserva una evaluación auditable. Solo se admite una promesa pendiente por préstamo.

## Panel y reportes

`/dashboard.html` abre los indicadores, edades de cartera y préstamos con saldo después de iniciar sesión. `/clientes.html` permite crear y buscar clientes, consultar su cartera, registrar referencias y adjuntar documentos. `/recaudos.html` permite buscar préstamos por número o nombre del cliente, escoger una o varias cuotas exigibles, registrar pagos totales o parciales, ver recibos y abrir un comprobante imprimible. `GET /api/auth/me` devuelve el usuario autenticado y la empresa asignada. `GET /api/companies/{companyId}/dashboard?asOf=AAAA-MM-DD` obtiene el resumen. El dashboard usa movimientos financieros confirmados y recaudos registrados; no incluye intereses futuros del plan como saldo causado.

`/informes.html` reúne cartera general, cartera vencida, cartera por edades, clientes morosos, recaudos, intereses y mora, préstamos desembolsados, préstamos cancelados y promesas de pago. Cada informe tiene vista previa, descarga CSV y Excel, e impresión a PDF desde el navegador. La API del catálogo usa `/api/companies/{companyId}/reports/catalog/{kind}` y las variantes `.csv` y `.xlsx`, con filtros `from` y `to`. Los reportes específicos anteriores de cartera (`/reports/portfolio.csv` y `.xlsx`) también continúan disponibles.

`GET /api/companies/{companyId}/reports/payments?from=AAAA-MM-DD&to=AAAA-MM-DD` agrupa los recaudos por medio y devuelve el detalle con préstamo, cliente y usuario. La variante `/reports/payments.csv` exporta ese detalle; la pantalla de recaudos incluye el botón de descarga. El rango se limita a 367 días y 50.000 recaudos por solicitud.

`GET /api/companies/{companyId}/loans/{loanId}/statement?asOf=AAAA-MM-DD` muestra saldos, pagos, movimientos y cuotas vencidas del préstamo. `GET /api/companies/{companyId}/clients/{clientId}/summary?asOf=AAAA-MM-DD` agrega la cartera y los recaudos del cliente.

## Permisos, auditoría e integración

La API aplica permisos a cada empresa y rol. Un administrador puede consultar `GET /api/companies/{companyId}/permissions` y cambiar un permiso con `PUT /api/companies/{companyId}/permissions/{role}/{permission}` y cuerpo `{ "allowed": true }`. Los cambios conservan actor, IP y valor anterior/nuevo. El permiso principal del administrador para gestionar permisos no se puede revocar.

`GET /api/companies/{companyId}/audit?take=50` devuelve los cambios sensibles registrados en la misma operación de base de datos; el acceso requiere `ViewAudit`. Desembolsos, recaudos y cierres generan mensajes de outbox en la misma transacción para una futura integración con ERP o mensajería. Todavía no hay proceso que publique esos mensajes. `GET /api/companies/{companyId}/consistency` revisa secuencias del libro, saldos, planes, recibos y aplicaciones, y devuelve incidencias sin modificar datos.

## Base de datos

Las migraciones están en `src/Ziklo.Infrastructure/Migrations`. El script idempotente completo para una base SQL Server existente llamada `ziklo` está en `docs/ziklo-sqlserver.sql`; incluye todas las migraciones actuales. `docs/initial-schema.sql` es una versión anterior e incompleta. No se ha ejecutado el script contra una instancia de SQL Server en este entorno. El aprovisionamiento inicial es un comando local; no hay endpoint público para crear un administrador.

## Límites actuales del simulador

- Moneda de la interfaz: COP, con dos decimales. El motor admite de cero a cuatro decimales.
- Capitalización explícita disponible en la modalidad de pago único, por cada periodo configurado.
- Las cuotas personalizadas permiten definir el capital de cada periodo; sus fechas siguen la periodicidad seleccionada.
- La página del simulador aún no muestra mora, pagos parciales, refinanciación ni liquidación anticipada. La API empresarial de recaudos funciona en la demostración local; no está conectada a la página pública del simulador.
- Las políticas contractuales y la legalidad de tasas y mora deben validarse antes de utilizar el sistema con préstamos reales.
