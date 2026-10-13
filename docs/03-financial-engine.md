# Diseño matemático de FinancialEngine

Este documento especifica la fase 3: entradas, salidas, fórmulas y reglas de contabilización del motor financiero. Es una **propuesta de contrato técnico** para implementar y probar antes de operar con dinero real. Cada producto y préstamo debe declarar las convenciones que usa; las tasas permitidas, bases de mora y límites legales se validarán para la jurisdicción y contrato aplicables antes de producción.

El motor calcula resultados sin acceso a base de datos, reloj global ni servicios externos. La capa de aplicación decide si confirma esos resultados en una transacción. Así, simulación, desembolso, recaudo y cierre utilizan las mismas funciones.

## Variables y unidades

| Símbolo | Significado |
|---|---|
| `P` | Capital inicial del préstamo. |
| `B(t)` | Capital pendiente justo antes de la fecha `t`. |
| `r` | Tasa en fracción: `0,12` representa 12 %. |
| `i_k` | Tasa efectiva del intervalo de la cuota `k`. |
| `d(a,b)` | Número de días naturales en el intervalo semiabierto `[a,b)`. |
| `Y(a,b)` | Fracción de año según la convención contractual. |
| `I_k` | Interés corriente generado para el intervalo `k`. |
| `M_k` | Interés moratorio generado para el intervalo `k`. |
| `C_k` | Abono contractual a capital de la cuota `k`. |
| `A_k` | Valor de capital más interés de la cuota `k`, antes de seguros y cargos. |

Las fechas financieras se representan con `DateOnly`; los instantes de registro con UTC. Una operación con fecha `t` primero causa interés hasta `t` sobre el saldo anterior y después aplica el pago o abono de `t`. Así, el día del pago no produce interés sobre capital ya pagado. La fecha de desembolso es el inicio del primer intervalo; la fecha de vencimiento es el final del intervalo anterior.

## Política numérica y de calendario

La política financiera versionada y copiada al préstamo contiene: moneda, escala monetaria, modo de redondeo, escala interna de tasas, convención de días, tipo y periodo de tasa, calendario de vencimientos, regla de fin de mes, modo de interés, periodicidad de capitalización, regla de mora y orden de aplicación. Se rechaza una operación que no tenga definidos todos los parámetros que necesita.

- Los importes y tasas usan `decimal`; ninguna ruta financiera convierte a `float` o `double`. Para exponentes fraccionarios se requiere una implementación decimal determinista con error acotado y pruebas de referencia; no se usa `Math.Pow(double, double)`.
- El importe publicado en el libro se redondea a la escala de la moneda con el modo contractual. La propuesta inicial es **mitades alejándose de cero** para importes positivos; es configurable y debe fijarse en el contrato. .NET ofrece modos explícitos de redondeo para `decimal` ([Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/system.midpointrounding)).
- Los cálculos intermedios conservan al menos 12 decimales significativos de fracción cuando el rango lo permita. Cada causación guarda la precisión contractual publicada y su periodo. El residuo de redondeo del plan se asigna a la última cuota de capital, sin cambiar `P`.
- `ACT/365F`: `Y(a,b)=d(a,b)/365`, incluso en año bisiesto. `ACT/360`: `Y(a,b)=d(a,b)/360`. `ACT/ACT`: se divide el intervalo por años calendario y se suma `d_j/365` o `d_j/366` según el año de cada segmento. La convención pertenece al contrato.
- Las fechas de cuotas se generan desde una fecha ancla. Si la ancla es fin de mes, la política `FIN_DE_MES` mantiene los vencimientos al fin de cada mes; otra política usa el día nominal, ajustándolo al último día solo cuando haga falta. Un ajuste por día no hábil requiere un calendario y una regla explícitos; en la versión inicial, sin calendario configurado, no se modifica la fecha.

No se acumulan redondeos de cuotas para calcular el capital final: en cada paso se toma el saldo anterior publicado y `C_n=B_{n-1}`. Sin capitalizaciones, la suma de capital pagado debe ser exactamente `P`. Con capitalizaciones explícitas, el capital final a pagar incluye `P` más los intereses previamente capitalizados; estos intereses se muestran también por separado y no se suman dos veces al total a pagar.

## Tasas y conversión

Cada tasa se declara con **valor, clase y periodo de cotización**. No se infiere que una tasa `12 %` sea mensual, anual nominal o efectiva.

1. **Anual simple `r_s`.** Para capital constante en `[a,b)`: `I=B·r_s·Y(a,b)`. Si el capital cambia, se divide el intervalo en tramos y se suman sus intereses.
2. **Anual efectiva `r_ea`.** Factor del intervalo: `F(a,b)=(1+r_ea)^Y(a,b)`; tasa del intervalo `i=F−1`. Debe cumplirse `r_ea > −1`. En un préstamo que paga intereses al final de cada intervalo, `B·i` se cobra como interés; `B` no aumenta automáticamente.
3. **Anual nominal convertible `m` veces `r_nom`.** Tasa de cada periodo nominal `j=r_nom/m`; tasa efectiva anual equivalente `(1+j)^m−1`. Para un intervalo de longitud no entera, el factor propuesto es `(1+j)^(m·Y(a,b))`, siempre que el contrato elija esta convención. `m` y la regla de fracción de periodo deben quedar congelados.
4. **Periódica efectiva `r_p`.** La cuota correspondiente a un periodo contractual completo usa `i=r_p`. Para liquidaciones parciales se convierte primero a una tasa anual equivalente `r_ea=(1+r_p)^q−1`, donde `q` es el número contractual de periodos por año, y se aplica la fracción de año pactada. Esta conversión se documenta en la cotización.
5. **Periódica simple `r_{sp}`.** La tasa anual simple equivalente es `q·r_{sp}` si el contrato declara `q`; para fechas parciales se usa `B·q·r_{sp}·Y(a,b)`. La suma anual puede diferir de `q` periodos calendario irregulares y debe mostrarse en la simulación.

Los valores iniciales de `q` serán 365, 52, 24, 12 y 1 para día, semana, quincena, mes y año, pero el producto los guarda explícitamente. Una quincena puede significar cada 15 días o dos fechas fijas por mes; debe elegir una regla de calendario y no se deduce solo de `q=24`.

La tasa efectiva anual y la periodicidad de capitalización describen cosas distintas. **Convertir una tasa no constituye una capitalización contable.** El capital solo aumenta cuando ocurre un evento de capitalización permitido por las condiciones del préstamo.

## Interés corriente y capitalización

Para interés simple sobre capital pendiente, si el intervalo se parte en tramos `j` con capital constante:

`I(a,b)=Σ_j B_j·r_s·Y(a_j,b_j)`.

Si el contrato calcula interés simple sobre capital original, se sustituye `B_j` por `P` y se marca esa base expresamente. La propuesta operativa por defecto es capital pendiente; ninguna base se selecciona por inferencia silenciosa.

Para una tasa efectiva de intervalo, el cargo proyectado es `I_k=B_{k−1}·i_k`. El cargo se publica en `INTERES_CORRIENTE`; no se suma a `CAPITAL`. Si vence impagado, permanece como interés pendiente. La política de mora define por separado si dicho importe puede formar parte de su base.

Una capitalización autorizada en `t` usa el interés corriente elegible `U(t)` y registra en el mismo evento:

- `INTERES_CORRIENTE = −U(t)`;
- `CAPITAL = +U(t)`.

El saldo total no cambia en ese evento. Los intereses posteriores se calculan sobre el nuevo capital. Se guarda fecha, base, regla y movimiento de origen. No se capitaliza mora automáticamente.

## Sistemas de amortización

Todos reciben `P`, condiciones, fecha de desembolso, fechas de cuota y cargos; devuelven una tabla sin persistencia. Se valida que `P>0`, `n>0`, fechas estrictamente crecientes, tasas admisibles y ninguna cuota tenga capital negativo. Sin capitalización, `ΣC_k=P`; con capitalización explícita, `ΣC_k=P+Σ intereses capitalizados`.

### Cuota fija o sistema francés

Con periodos iguales y tasa `i` constante, la cuota de capital más interés es:

`A = P·i / (1−(1+i)^(−n))`, si `i≠0`; `A=P/n` si `i=0`.

En cada cuota: `I_k=B_{k−1}·i`, `C_k=A−I_k`, `B_k=B_{k−1}−C_k`. Seguros y cargos se añaden a `A` y no reducen capital. La última cuota ajusta `C_n=B_{n−1}` y `A_n=C_n+I_n`, para cerrar exactamente el capital.

Con fechas irregulares o tasas de intervalo distintas, una cuota constante se obtiene por descuento:

`A = P / Σ_{j=1}^n [1 / Π_{k=1}^j (1+i_k)]`.

Si algún `i_k` o el importe de los cargos hace inviable amortizar sin capital negativo, la simulación lo rechaza o exige una modalidad que permita saldo final. La fórmula de cuota fija no autoriza capitalizar un interés vencido: supone que cada cuota contractual se paga según el plan.

### Abono fijo a capital

`C_k=P/n` para `k<n`; `C_n=P−Σ_{k<n}C_k`. En cada periodo `I_k` se calcula sobre `B_{k−1}` con la política de tasas; `A_k=C_k+I_k`. La cuota total normalmente decrece si la tasa es constante.

### Interés simple con pagos periódicos

La estrategia inicial paga interés corriente en cada vencimiento y todo el capital al final: `C_k=0` para `k<n`, `C_n=P`, `I_k` se calcula por los días del intervalo sobre capital pendiente. Una distribución de capital distinta se configura como plan personalizado o abono fijo. Esta definición evita que dos estrategias produzcan planes indistinguibles.

### Pago único al vencimiento

Una cuota final contiene `P` y el interés causado desde el desembolso hasta el vencimiento. Si el producto prevé capitalizaciones intermedias, estas son eventos explícitos del calendario contractual y se muestran en la simulación; no se esconden dentro de una única fórmula de monto.

### Cuotas personalizadas

El usuario define fechas y capital contractual por cuota; `ΣC_k=P`. El motor calcula el interés correspondiente en cada intervalo y añade cargos de acuerdo con sus reglas. Si también se pacta una cuota total fija personalizada, debe validarse que cubre interés y capital pactado; una diferencia se rechaza o se modela como saldo final permitido por contrato, nunca como capital negativo implícito.

## Mora

La mora se calcula por cuota vencida y solo sobre la **base contractual autorizada**. Opciones técnicas de base: capital vencido impago; capital más interés vencidos; u otra composición explícita permitida. La elegibilidad legal de cada opción se revisa antes de activar el producto. Nunca se presume que el interés corriente o la mora se capitalizan.

Para una cuota con vencimiento `v`, gracia de `g` días y fecha de corte `t`, la propuesta inicial de gracia **prospectiva** empieza en `s=v+g` y causa por el intervalo `[s,t)`. Por ello, pagar exactamente en `v` genera cero días de mora; con `g=0`, un pago al día siguiente genera uno. Si el contrato establece gracia retroactiva, se configura una regla distinta que, al superar la gracia, liquida desde `v`; queda reflejada en la simulación y en el cálculo guardado.

Con tasa moratoria anual simple `r_m`, base constante `D` y convención `Y`:

`M(a,b)=D·r_m·Y(a,b)`.

Si un pago parcial cambia la base, se parte el periodo en tramos: `M=Σ_j D_j·r_m·Y(a_j,b_j)`. Una tasa o límite que cambia durante el periodo exige particionar también por vigencia de regla. El límite se aplica al total acumulado según el alcance configurado: por cuota, préstamo, periodo o contrato. El incremento publicable es `max(0, min(M_teorica_acumulada, Tope)−M_ya_causada)`; una reducción o condonación usa un evento separado.

`CalculateLateInterest` recibe una fecha de corte y movimientos previos, y devuelve desglose por cuota, tramo, días, base, tasa, importe y regla. `CalculateLateInterest` en modo simulación no crea registros. El cierre diario persiste una causación solo si su clave préstamo/cuota/componente/periodo todavía no existe.

## Aplicación de pagos

`ApplyPayment` es una función pura que recibe: saldo y cuotas a una fecha de corte, recaudo positivo, política de prioridad versionada y opciones autorizadas. Devuelve aplicaciones, excedente, nuevos saldos y eventos financieros propuestos. Antes de aplicar, el caso de uso causa hasta la fecha efectiva los importes que correspondan, sin duplicar los ya publicados.

Para cada nivel de prioridad `j`: `x_j=min(R_j,D_j)`, `R_{j+1}=R_j−x_j`, donde `R_1` es el recibido y `D_j` la deuda elegible. Dentro del nivel se aplica primero a la cuota vencida más antigua y después a la siguiente, salvo regla contractual distinta. La secuencia propuesta es cargos, mora, interés corriente vencido, capital vencido, interés corriente exigible y capital no vencido. Una regla puede omitir o reordenar niveles; la versión aplicada se guarda con el pago.

`Σ aplicaciones + excedente = importe recibido` a la escala monetaria. Ningún componente de deuda queda negativo. El excedente se rechaza, devuelve o registra como `SALDO_A_FAVOR` según la política. La operación confirmada guarda recaudo, aplicaciones, evento y movimientos del libro, proyección de saldo, auditoría y outbox en una transacción. Reintentar con la misma clave devuelve el resultado original.

Una anulación registra el recaudo original como anulado y crea movimientos exactamente opuestos a sus aplicaciones, con vínculo al evento anterior. Si existen operaciones posteriores dependientes, se exige una secuencia de reversos y recálculo autorizada; no se modifica el historial en silencio.

## Abonos extraordinarios y recálculo del plan

Un abono adicional se aplica a capital **después** de liquidar los conceptos exigibles que el contrato obligue a pagar primero. La simulación parte del saldo posterior al pago en la fecha efectiva y conserva el plan anterior. Opciones:

- **Reducir plazo:** conservar la cuota objetivo, recalcular hacia adelante y finalizar cuando el capital llegue a cero; la última cuota se ajusta.
- **Reducir cuota:** conservar el número de vencimientos restantes y recalcular la cuota con el capital nuevo.
- **Mantener plan:** conservar fechas e importes futuros pactados, mostrando el efecto como menor interés o pago final ajustado según la regla del producto. Si contradice las condiciones, se rechaza.
- **Cancelar:** obtener la liquidación total a la fecha y aplicar el pago completo.

`RecalculateLoan` produce una nueva versión del plan y la comparación con el anterior. Solo `ConfirmRecalculation` la persiste tras autorización, enlazando recaudo, plan previo y plan nuevo. No recalcula retroactivamente cuotas pagadas.

## Liquidación anticipada

Para fecha `t`:

`TotalCancelacion(t) = CapitalPendiente + InteresCorrientePublicadoNoPagado + InteresDevengadoNoPublicadoHasta(t) + MoraPublicadaNoPagada + MoraDevengadaNoPublicadaHasta(t) + CargosExigibles − SaldoAFavor`.

Los dos términos no publicados se calculan desde el último corte causado hasta `t` y se muestran por separado; no se suman otra vez si ya existe un movimiento del mismo periodo. Una condonación solo reduce el saldo cuando queda confirmada y registrada en el libro. Los intereses futuros del plan se excluyen salvo cargo contractual explícito y permitido. `CalculatePayoff` devuelve desglose, fecha de validez y supuestos. La consulta no modifica el préstamo; al confirmar se recalcula dentro de la transacción para evitar usar un saldo cambiado por otro pago.

## Refinanciación y condonación

La refinanciación obtiene una liquidación a la fecha, separa capital, interés, mora y cargos, registra condonaciones autorizadas y crea un préstamo nuevo con condiciones nuevas. El préstamo original conserva libro y plan; queda relacionado con el nuevo. Cada componente transferido debe tener tratamiento contractual explícito. Si se convierte interés en capital del nuevo préstamo, la operación debe estar permitida y mostrar ese importe por separado.

Una condonación de componente `c` por importe `W` exige `0<W≤SaldoElegible_c`. Se registra el importe anterior, `W`, el saldo resultante, solicitante, autorizador y motivo. El libro recibe `Δ_c=−W`. Una condonación no equivale a un pago recibido ni altera el recaudo de caja.

## Contrato de servicios

| Función | Entrada principal | Salida pura |
|---|---|
| `CalculateInterest` | Capital por tramos, tasa, fechas y convención. | Interés y desglose por tramos. |
| `CalculateLateInterest` | Cuotas vencidas, pagos, regla de mora y corte. | Mora incremental y desglose auditable. |
| `GenerateAmortizationSchedule` | Condiciones, principal y calendario. | Plan y totales; sin IDs persistidos. |
| `ApplyPayment` | Estado a fecha, importe y política versionada. | Aplicaciones, excedente y deltas. |
| `CalculatePayoff` | Libro, cuotas, reglas y fecha. | Liquidación desglosada sin efectos. |
| `RecalculateLoan` | Plan vigente, saldo nuevo y opción. | Plan propuesto y diferencias. |
| `SimulateLoan` | Datos hipotéticos completos. | Cuota, totales, fechas y plan. |
| `CalculateOutstandingBalance` | Secuencia de movimientos hasta corte. | Saldo por componente y total. |

Los métodos públicos usan DTOs inmutables, errores de dominio tipados y un `CalculationContext` con política versionada. La capa de aplicación añade IDs, controla permisos y concurrencia, confirma transacciones y publica eventos. Una misma entrada debe producir exactamente la misma salida.

## Invariantes y ejemplos de referencia

1. Con capital `1.000`, tasa anual simple `12 %`, `ACT/360` y 30 días sin pagos, el interés es `1.000 × 0,12 × 30/360 = 10,00`.
2. Con capital vencido `100`, mora anual simple `24 %`, `ACT/360` y cinco días elegibles, la mora antes de redondear es `0,333333…`; a dos decimales con redondeo de mitades alejándose de cero es `0,33`.
3. En cuota fija con `P=1.000`, tasa efectiva mensual `1 %` y dos cuotas, la cuota calculada es aproximadamente `507,5124`. Publicadas a dos decimales, la primera cuota es `507,51`: interés `10,00`, capital `497,51`, saldo `502,49`. La segunda: interés `5,02`, capital `502,49`, total `507,51`. La suma de capital es `1.000,00`.
4. En `ACT/ACT`, un día de 2024 usa `1/366`; en `ACT/365F` usa `1/365`. Un intervalo que cruza de año se parte antes de sumar.
5. Para todo plan sin capitalización: `Σ capital de cuotas = P`; con capitalización: `Σ capital de cuotas = P + Σ intereses capitalizados`. En ambos: `SaldoCapitalFinal=0`, importes no negativos y fechas crecientes. Para todo pago: `recibido=aplicado+excedente`. Para todo préstamo: saldo materializado por componente igual a la suma de sus movimientos.
6. Pagar exactamente en vencimiento no causa mora. Ejecutar dos veces el cierre de la misma fecha, o reintentar un pago con la misma clave, no cambia el saldo la segunda vez.

Estos ejemplos serán pruebas automatizadas del motor junto con pagos parciales, múltiples cuotas vencidas, capitalización explícita, bisiestos, cambios de mes, abonos, refinanciación, condonación, reversos y pagos superiores al saldo.

## Decisiones pendientes de validación de negocio

Antes de habilitar productos reales se deben aprobar por cada producto: convención de días; representación de tasa; base de interés simple; fechas de causación y vencimiento; gracia prospectiva o retroactiva; base y tope de mora; orden de aplicación; tratamiento de excedentes; regla de calendario; escala y redondeo; posibilidad de capitalizar; y tratamiento de intereses al refinanciar. Estas decisiones se almacenan con versión y se copian al préstamo. La arquitectura no fija por su cuenta una tasa legal ni valida la licitud de una cláusula para una jurisdicción concreta.
