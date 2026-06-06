# RockWeb/Blocks/Finance — Cambios en los bloques de transacciones financieras

## Resumen del scope

Commit base de comparación: `ca2ca0ec94` (rama `hotfix-18.1`)

El diff comprende **3 archivos, 106 inserciones y 44 eliminaciones**. Los cambios son exclusivamente de lógica de negocio y están enfocados en un tema central: **soporte correcto de multi-moneda en la visualización y totalización de transacciones financieras**.

---

## Archivos modificados

| Archivo | Cambios | Naturaleza |
|---|---|---|
| `TransactionList.ascx` | 16 | Markup: se agrega un nuevo Repeater |
| `TransactionList.ascx.cs` | 115 | Lógica: refactorización completa de totales multi-moneda |
| `TransactionDetail.ascx.cs` | 19 | Corrección de bug en visualización de moneda extranjera |

---

## TransactionList.ascx.cs — Totales por moneda

### El problema que resuelve
Rock permite registrar transacciones en moneda extranjera (`ForeignCurrencyCodeValueId`). El código original agrupaba todos los totales ignorando la moneda, produciendo sumas incorrectas cuando en el mismo filtro coexistían transacciones en GTQ (quetzales) y USD u otras monedas.

### Los cambios

**Agrupación por moneda en los totales de cuenta:**
La consulta de resumen (`AccountSummaryRow`) ahora incluye `ForeignCurrencyCodeValueId` en el `GroupBy`, de modo que una misma cuenta financiera puede aparecer varias veces en el resumen si tiene transacciones en distintas monedas. El modelo `AccountSummaryRow` recibe el campo `ForeignCurrencyCodeValueId` como nueva propiedad.

**Eliminación del "Grand Total" único:**
Se eliminó el control `lGrandTotal` que mostraba una suma global en una sola moneda. En su lugar se agrega un nuevo repeater `rptCurrencyTotals` que muestra un total independiente por cada moneda presente en el resultado filtrado.

**Formato de montos con símbolo correcto:**
Todos los `FormatAsCurrency()` en la lista de resumen ahora reciben `ForeignCurrencyCodeValueId` como parámetro, usando la sobrecarga de Rock que formatea el monto con el símbolo correspondiente a esa moneda (en lugar de asumir siempre la moneda por defecto del sistema).

**Columna de monto en la grilla:**
Se agrega un `Literal` de nombre `lTotalAmount` en el evento `RowDataBound` que formatea el monto de cada fila de transacción con su propia moneda (`txn.ForeignCurrencyCodeValueId`).

**Orden de resultados:**
El resumen de cuentas ahora ordena primero por `ForeignCurrencyCodeValueId` (agrupando visualmente por moneda) y luego por `Order` de la cuenta financiera.

---

## TransactionDetail.ascx.cs — Corrección de visualización de moneda extranjera

### El problema que resuelve
El método `GetForeignCurrencyFields()` tenía una condición que impedía cargar el ID de la moneda si el atributo de bloque `EnableForeignCurrency` estaba desactivado. Esto causaba que los montos en el detalle de transacción siempre se formatearan con el símbolo de la moneda por defecto, aunque la transacción tuviera `ForeignCurrencyCodeValueId` asignado.

### Los cambios
Se separó la lógica en dos partes:
1. `_foreignCurrencyCodeDefinedValueId` siempre se carga si la transacción tiene `ForeignCurrencyCodeValueId`, independientemente del atributo `EnableForeignCurrency`.
2. El símbolo visual (`_foreignCurrencySymbol`) y el código de moneda (`_foreignCurrencyCode`) solo se cargan si `EnableForeignCurrency` está activado en el bloque (comportamiento previo preservado para la UI).

El resultado es que `lAccountsViewAmountMinusFeeCoverageAmount` y `lAccountsEditAmountMinusFeeCoverageAmount` ahora formatean correctamente usando `FormatAsCurrency(viewCurrencyId)`, mostrando el símbolo de moneda correcto en todos los casos.

---

## Contexto: por qué estos cambios son necesarios para VidaReal

VidaReal opera en Guatemala (moneda GTQ) pero puede recibir donaciones en dólares USD u otras monedas a través de pasarelas de pago internacionales. Sin estos cambios, los reportes financieros sumaban GTQ y USD en una sola cifra, produciendo totales incoherentes. La corrección permite que los administradores financieros vean totales separados por moneda en la lista de transacciones y que el detalle de cada transacción muestre el símbolo de moneda correcto sin importar la configuración del bloque.
