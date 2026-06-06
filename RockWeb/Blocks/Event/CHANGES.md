# Cambios en Bloques Event — VidaReal fork (hotfix-18.1)

Base de comparacion: commit `ca2ca0ec94`
Rama actual: `hotfix-18.1`
Archivos modificados: 2 (71 inserciones, 13 eliminaciones)

---

## Contexto del modulo de eventos en VidaReal

VidaReal utiliza el modulo de Eventos de Rock para gestionar inscripciones a conferencias, retiros y eventos especiales. Las inscripciones pueden tener un costo que se paga en cuotas a traves de la pasarela de pago integrada (Cybersource/Epay). Cuando un pago se procesa en cuotas, la pasarela puede cobrar un **recargo por cuotas** (`FeeCoverageAmount`) que cubre el costo de procesamiento. Este recargo se almacena separado del monto principal de la transaccion en `FinancialTransaction`.

---

## 1. RegistrationDetail — Detalle de inscripcion individual

### Archivo
- `RockWeb/Blocks/Event/RegistrationDetail.ascx.cs` (+64 / -13 lineas)

### Problema original
El bloque de detalle de registro calculaba el costo total y el balance pendiente usando directamente `registration.DiscountedCost` y `registration.BalanceDue`. Estas propiedades del modelo ORM de Rock **no incluyen** el `FeeCoverageAmount` (recargo por cuotas) en su calculo. Como resultado:

- El badge `hlCost` mostraba un monto menor al cobrado realmente.
- El balance pendiente (`hlBalance`) podia mostrar un saldo incorrecto (aparecia deuda cuando en realidad estaba pagado con recargo, o viceversa).
- La tabla de costos/honorarios en el modo de edicion no listaba el recargo como una linea separada.

### Nuevo metodo privado: `GetRegistrationPaymentSummary`

```csharp
private void GetRegistrationPaymentSummary(
    Registration registration,
    out decimal totalPaid,
    out decimal surchargeTotal )
```

Este metodo centraliza la consulta de pagos reales de la inscripcion:

1. Llama a `registration.GetPayments()` para obtener las transacciones financieras asociadas.
2. Suma `p.Amount` → `totalPaid` (total cobrado incluyendo recargo).
3. Suma `p.FeeCoverageAmount ?? 0.0m` → `surchargeTotal` (solo el recargo).

**Tipo de cambio:** Integracion de pasarela de pago / logica de negocio.

### Cambio en el badge de costo (`hlCost`) y balance (`hlBalance`)

**Antes:**
```csharp
hlCost.Text = registration.DiscountedCost.FormatAsCurrency();
var balanceDue = registration.BalanceDue;
hlBalance.Text = balanceDue.FormatAsCurrency();
```

**Despues:**
```csharp
GetRegistrationPaymentSummary( registration, out var totalPaid, out var surchargeTotal );
var totalCost = registration.DiscountedCost + surchargeTotal;
var balanceDue = ( totalCost - totalPaid ).AsCurrency();

hlCost.Text = totalCost.FormatAsCurrency();
hlBalance.Text = balanceDue.FormatAsCurrency();
```

- `totalCost` = costo con descuento + recargo por cuotas.
- `balanceDue` se calcula localmente como `totalCost - totalPaid` en lugar de usar `registration.BalanceDue`.

### Cambio en la tabla de costos/honorarios (`BindCostsGrid`)

Se agrega una nueva linea de costo en la tabla cuando existe recargo:

```csharp
if ( surchargeTotal > 0.0m )
{
    costs.Add( new RegistrationCostSummaryInfo
    {
        Type = RegistrationCostSummaryType.Fee,
        Description = "Recargo por cuotas",  // texto en espanol — para VidaReal
        Cost = surchargeTotal,
        DiscountedCost = surchargeTotal,
        RegistrationRegistrantGuid = null
    } );
}
```

**Nota importante:** `"Recargo por cuotas"` es un string literal en espanol, no usa el sistema de traduccion de Rock. Esta es una personalizacion especifica de VidaReal.

### Cambio en el calculo del Total de la tabla

**Antes:** El total de la fila "Total" usaba `costs.Sum(c => c.Cost)` para el costo bruto pero `registration.DiscountedCost` (sin recargo) para el costo con descuento.

**Despues:** Ambos valores se calculan desde la lista de costos, que ahora incluye el recargo:
```csharp
var totalCost = costs.Sum( c => c.Cost );
var discountedTotalCost = costs.Sum( c => c.DiscountedCost );

costs.Add( new RegistrationCostSummaryInfo
{
    Type = RegistrationCostSummaryType.Total,
    Description = "Total",
    Cost = totalCost,
    DiscountedCost = discountedTotalCost,
    RegistrationRegistrantGuid = null
} );
```

### Cambio en los totales del pie de la tabla

**Antes:** Usaba `registration.TotalPaid` y `registration.BalanceDue` (propiedades ORM).

**Despues:**
```csharp
lTotalCost.Text = discountedTotalCost.FormatAsCurrency();
lPreviouslyPaid.Text = totalPaid.FormatAsCurrency();  // de GetRegistrationPaymentSummary
decimal balanceDue = ( discountedTotalCost - totalPaid ).AsCurrency();
lRemainingDue.Text = balanceDue.FormatAsCurrency();
```

### Cambio en la visibilidad del boton "Add Payment"

Simplificacion menor:
```csharp
// Antes:
nbNoAssociatedPerson.Visible = registration.BalanceDue > 0.0m ? true : false;
// Despues:
nbNoAssociatedPerson.Visible = balanceDue > 0.0m;
```

---

## 2. RegistrationInstanceRegistrationList — Lista de inscripciones de una instancia

### Archivo
- `RockWeb/Blocks/Event/RegistrationInstanceRegistrationList.ascx.cs` (+20 / -7 lineas)

### Problema original
La lista de inscripciones de una instancia de evento mostraba el pago total sin descontar el `FeeCoverageAmount`. Esto causaba que el balance pendiente apareciera como menor al real (porque el recargo infla el `Amount` de la transaccion pero no reduce la deuda del costo del evento).

### Cambio en el calculo de pagos por fila de grid

**Antes:**
```csharp
decimal totalPaid = hasPayments
    ? payments.Select( p => p.Amount ).DefaultIfEmpty().Sum()
    : 0.0m;
```

**Despues:**
```csharp
decimal totalFeeCoverage = hasPayments
    ? payments.Select( p => p.FeeCoverageAmount ?? 0.0m ).DefaultIfEmpty().Sum()
    : 0.0m;

decimal totalPaid = hasPayments
    ? payments.Select( p => p.Amount - ( p.FeeCoverageAmount ?? 0.0m ) ).DefaultIfEmpty().Sum()
    : 0.0m;
```

El `totalPaid` ahora excluye el recargo, por lo que el balance pendiente se calcula correctamente.

### Nueva columna "Fee" en el grid

Se agrega una columna dinamica al grid de inscripciones:

**En la construccion de columnas:**
```csharp
var lFee = new RockLiteralField { ID = "lFee", HeaderText = "Fee" };
lFee.HeaderStyle.HorizontalAlign = HorizontalAlign.Right;
lFee.ItemStyle.HorizontalAlign = HorizontalAlign.Right;
gRegistrations.Columns.Add( lFee );
```

**En el data binding de cada fila:**
```csharp
var lFee = e.Row.FindControl( "lFee" ) as Literal;
if ( lFee != null )
{
    lFee.Visible = _instanceHasCost || discountedCost > 0.0M || totalFeeCoverage > 0.0m;
    var feeCssClass = totalFeeCoverage > 0.0m ? "label-warning" : "label-default";
    lFee.Text = $"<span class='label {feeCssClass}'>{totalFeeCoverage.FormatAsCurrency()}</span>";
}
```

- Si hay recargo, el badge se muestra en amarillo (`label-warning`).
- Si no hay recargo, se muestra el badge en gris (`label-default`) con valor $0.

### Cambio en la consulta de totales del resumen del grid

La consulta que suma pagos para el resumen inferior del grid tambien fue corregida:

**Antes:**
```csharp
Payment = d.Amount
```

**Despues:**
```csharp
Payment = d.Amount - ( d.FeeCoverageAmount ?? 0.0m )
```

---

## Resumen del patron de cambio

Ambos archivos del modulo Event implementan el mismo patron de correccion:

| Aspecto | Antes | Despues |
|---|---|---|
| `totalPaid` | `p.Amount` (incluye recargo) | `p.Amount - p.FeeCoverageAmount` |
| Costo total mostrado | `registration.DiscountedCost` | `DiscountedCost + surchargeTotal` |
| Balance pendiente | `registration.BalanceDue` (ORM) | `totalCost - totalPaid` (calculado) |
| Recargo visible | No (oculto en pagos) | Si (linea "Recargo por cuotas" y columna "Fee") |

## Impacto en el sistema VidaReal

- **Cybersource/Epay cuotas:** Cuando la pasarela cobra un recargo por dividir el pago en cuotas, ese monto ahora aparece explicitamente en el detalle de la inscripcion y en la lista de inscripciones del evento.
- **Reconciliacion contable:** El staff financiero puede verificar que el recargo fue cobrado sin tener que revisar las transacciones financieras individuales.
- **Balance correcto:** El sistema ya no muestra "debe Q50" cuando en realidad esos Q50 son el recargo ya pagado a la pasarela, no deuda del evento.
- **Relacion con QREVENT:** El modulo QREVENT personalizado de VidaReal se integra con estos bloques para el check-in de eventos. Si bien QREVENT no lee directamente `FeeCoverageAmount`, la correccion del balance pendiente evita que aparezcan inscripciones como "con deuda" en los reportes de check-in cuando en realidad estan al corriente.
