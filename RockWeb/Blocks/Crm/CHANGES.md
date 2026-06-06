# Cambios en Bloques CRM — VidaReal fork (hotfix-18.1)

Base de comparacion: commit `ca2ca0ec94`
Rama actual: `hotfix-18.1`
Archivos modificados: 4 (208 inserciones, 49 eliminaciones)

---

## 1. PersonMerge — Merge de personas duplicadas

### Archivos
- `RockWeb/Blocks/Crm/PersonMerge.ascx` (+10 / -2 lineas)
- `RockWeb/Blocks/Crm/PersonMerge.ascx.cs` (+117 / -19 lineas)

### Contexto del bloque
Este bloque permite a administradores de datos combinar registros de personas duplicadas en Rock. Es critico en el flujo de deteccion de duplicados del sistema de login passwordless: cuando un usuario se autentica via OTP y Rock detecta que podria existir mas de un registro para ese individuo, se genera una solicitud de merge que eventualmente llega a este bloque.

### Cambios en la UI (`PersonMerge.ascx`)

**Problema original:** El formulario solo tenia un `PersonPicker` (buscador por nombre) para agregar personas al merge. Esto imposibilitaba agregar registros de personas que no tienen nombre visible o cuyo nombre es ambiguo en la busqueda.

**Cambio aplicado:** Se reestructuro el layout del area de entrada usando Bootstrap grid. Se agrego un segundo metodo de entrada en columna `col-md-4`:

```aspx
<div class="col-md-4">
    <Rock:NumberBox ID="nbAddPersonId" runat="server"
        NumberType="Integer" MinimumValue="1"
        Label="Add by Person ID" />
    <asp:LinkButton ID="lbAddPersonById" runat="server"
        Text="Add by ID"
        CssClass="btn btn-default btn-sm"
        OnClick="lbAddPersonById_Click" />
</div>
```

**Tipo de cambio:** Mejora de UI / nueva funcionalidad de busqueda.

### Cambios en la logica de negocio (`PersonMerge.ascx.cs`)

#### a) Refactorizacion del metodo `ppAdd_SelectPerson`

**Antes:** El handler del PersonPicker contenia toda la logica de agregar una persona (verificar duplicado, actualizar perfil de proteccion, recargar grid).

**Despues:** La logica fue extraida al metodo privado `AddPersonToMerge(int personId)`. El handler ahora es un delegado liviano:

```csharp
protected void ppAdd_SelectPerson( object sender, EventArgs e )
{
    int? personId = ppAdd.PersonId;
    if ( personId.HasValue )
    {
        AddPersonToMerge( personId.Value );
    }
    ppAdd.SetValue( null );
}
```

#### b) Nuevo handler `lbAddPersonById_Click`

Maneja el boton "Add by ID". Flujo de validacion antes de agregar:
1. Verifica que el `nbAddPersonId` tenga un valor entero positivo.
2. Verifica que la persona no este ya incluida en el merge.
3. Consulta la base de datos incluyendo `IncludeDeceased = true` e `IncludeNameless = true` — esto es clave para poder hacer merge de registros anonimos/sin nombre creados por el sistema passwordless.
4. Si el ID no existe, muestra `nbError` con mensaje descriptivo.
5. Si pasa todas las validaciones, invoca `AddPersonToMerge(personId)`.

#### c) Nuevo metodo privado `AddPersonToMerge(int personId)`

Centraliza la logica que antes estaba duplicada en `ppAdd_SelectPerson` y `lbRemovePerson_Click`. Cambios adicionales respecto al codigo original:

- Usa `PersonService.PersonQueryOptions` con `IncludeDeceased = true` e `IncludeNameless = true` en lugar del string `"CreatedByPersonAlias.Person,Users"`.
- Usa `.Include()` explicito con EF en lugar de lazy loading implicito:
  ```csharp
  .Include( a => a.CreatedByPersonAlias.Person )
  .Include( a => a.Users )
  ```
- Llama a `SetAddPersonControlsVisibility(people)` despues de reconstruir el grid.

#### d) Nuevo metodo privado `SetAddPersonControlsVisibility`

**Antes:** La visibilidad de `ppAdd` se controlaba con una expresion en linea en dos lugares distintos:
```csharp
ppAdd.Visible = !people.All( a => a.IsBusiness() );
```

**Despues:** Un metodo unico controla los tres controles a la vez:
```csharp
private void SetAddPersonControlsVisibility( List<Person> people )
{
    var canAddPerson = !people.All( a => a.IsBusiness() );
    ppAdd.Visible = canAddPerson;
    nbAddPersonId.Visible = canAddPerson;
    lbAddPersonById.Visible = canAddPerson;
}
```

La regla de negocio se mantiene: si todos los registros en el merge son empresas (`IsBusiness()`), los controles de agregar persona se ocultan.

#### e) Cambio en la consulta del panel de eliminacion (`lbRemovePerson`)

La consulta dentro del handler de eliminacion tambien fue actualizada para usar `PersonQueryOptions` con `IncludeDeceased` e `IncludeNameless`, y `SetAddPersonControlsVisibility` se llama correctamente al reconstruir el grid.

### Impacto en el sistema VidaReal

- **Flujo passwordless:** El sistema de login sin contrasena puede crear registros `Nameless` cuando un telefono o email se usa sin coincidir con una persona existente. Estos registros ahora pueden buscarse por ID para incluirlos en un merge manual, lo que era imposible antes (la busqueda por nombre no los encontraba).
- **Registros fallecidos:** Los miembros fallecidos ahora pueden ser incluidos en merges via ID sin errores.
- **Reduccion de duplicacion de codigo:** La extraccion a `AddPersonToMerge` elimina la posibilidad de que ambos paths (picker y ID) tengan comportamientos divergentes.

---

## 2. GivingOverview — Historial de donaciones por persona

### Archivos
- `RockWeb/Blocks/Crm/PersonDetail/GivingOverview.ascx` (+0 / -6 lineas netas)
- `RockWeb/Blocks/Crm/PersonDetail/GivingOverview.ascx.cs` (+124 / -30 lineas)

### Contexto del bloque
Este bloque se muestra en el perfil de persona de Rock (tab de donaciones). Presenta KPIs de donacion (ultimos 12 meses, ultimos 90 dias, don tipico), un grafico de barras mensual de 36 meses, y un resumen anual por cuenta financiera. En VidaReal, los donantes pueden dar en GTQ (quetzales) y USD, y las transacciones pueden registrar un `ForeignCurrencyCodeValueId` para identificar la moneda.

### Categoria del cambio
**Multi-moneda:** Todos los cambios en este bloque son para mostrar correctamente montos en multiples monedas sin mezclarlos en un solo total.

### Cambios en la UI (`GivingOverview.ascx`)

**Antes:** El footer de la tabla de resumen anual tenia una fila fija `<tr>` con un `Literal` llamado `lTotalAmount`:
```aspx
<tr>
    <th>Total</th>
    <th class="text-right">
        <asp:Literal ID="lTotalAmount" runat="server" />
    </th>
</tr>
```

**Despues:** Reemplazado por un `Literal` llamado `lTotalAmounts` que el code-behind rellena dinamicamente con una fila `<tr>` por moneda:
```aspx
<tfoot>
    <asp:Literal ID="lTotalAmounts" runat="server" />
</tfoot>
```

Esto permite mostrar "Total GTQ: Q1,500.00" y "Total USD: $200.00" como filas separadas.

### Cambios en la logica de negocio (`GivingOverview.ascx.cs`)

#### a) Resumen anual (`rptYearSummary_ItemDataBound`) — totales por moneda

**Antes:** Un solo total decimal `TotalAmount` se formateaba con `FormatAsCurrency()` (usa la moneda predeterminada del sistema).

**Despues:** Se itera sobre `contributionSummary.TotalsByCurrency`, una lista de objetos `CurrencyTotal`, y se genera una fila de encabezado por moneda:
```csharp
foreach ( var ct in contributionSummary.TotalsByCurrency.OrderBy( c => c.CurrencyCodeValueId ) )
{
    var label = ct.CurrencyCodeValueId.HasValue
        ? "Total " + ( DefinedValueCache.Get( ct.CurrencyCodeValueId.Value )?.Value ?? string.Empty )
        : "Total";
    totalRowsHtml.AppendFormat( "<tr><th>{0}</th><th class='text-right'>{1}</th></tr>",
        label,
        ct.Amount.FormatAsCurrency( ct.CurrencyCodeValueId ) );
}
```

#### b) Montos por cuenta en el resumen anual

En la linea que formatea cada cuenta individual:
```csharp
// Antes:
item.TotalAmount.FormatAsCurrency()
// Despues:
item.TotalAmount.FormatAsCurrency( item.ForeignCurrencyCodeValueId )
```

#### c) Grafico de barras mensual — tooltip multi-moneda

**Antes:** El datasource del repeater `rptGivingByMonth` era un `Dictionary<DateTime, decimal>`. El tooltip mostraba solo el total en una moneda.

**Despues:** Se introduce la clase privada `MonthlyChartItem`:
```csharp
private class MonthlyChartItem
{
    public DateTime Date { get; set; }
    public decimal TotalAmount { get; set; }
    public string TooltipText { get; set; }
}
```

El `TooltipText` se construye agrupando por moneda:
```csharp
var currencyParts = monthEntries
    .GroupBy( h => h.ForeignCurrencyCodeValueId )
    .OrderBy( g => g.Key )
    .Select( g => g.Sum( h => h.Amount ).FormatAsCurrency( g.Key ) );

monthlyChartItems.Add( new MonthlyChartItem
{
    Date = currentMonthlyDate,
    TotalAmount = total,
    TooltipText = currentMonthlyDate.ToString( "MMM yyyy" ) + " " + string.Join( " + ", currencyParts )
} );
```

Ejemplo de tooltip resultante: `"Jun 2025 Q1,200.00 + $150.00"`

En `rptGivingByMonth_ItemDataBound`, el cast cambia de `KeyValuePair<DateTime, decimal>` a `MonthlyChartItem`.

#### d) KPI "Last 12 Months" — desglose por moneda

**Antes:** Un unico total sumaba todas las monedas y se mostraba como una sola cifra.

**Despues:** Se agrupa por `ForeignCurrencyCodeValueId` y se concatenan los montos con " + ":
```csharp
var last12ByCurrency = twelveMonthTransactions
    .GroupBy( t => t.ForeignCurrencyCodeValueId )
    .Select( g => new { CurrencyId = g.Key, Total = g.Sum(...) } )
    .OrderBy( x => x.CurrencyId )
    .ToList();

var last12MonthDisplay = string.Join( " + ", last12ByCurrency.Select( c =>
    $"<span class=\"currency-span\">{FormatAsCurrency( c.Total, c.CurrencyId )}</span>" ) );
```

La query de transacciones tambien fue actualizada para incluir `ForeignCurrencyCodeValueId` en la proyeccion `.Select()`.

#### e) KPI "Last 90 Days" — mismo patron

Mismo tratamiento multi-moneda que "Last 12 Months". El calculo del porcentaje de crecimiento (`growthPercent`) mantiene la suma total en todas las monedas para la comparacion porcentual — decision deliberada para no complicar la logica de crecimiento.

#### f) KPI "Typical Gift" — correccion de formato

**Antes:** `giftAmountIqr` (rango intercuartil) se pasaba como decimal crudo al shortcode, mostrando un numero sin formato de moneda.

**Despues:**
```csharp
// Antes:
$"{giftAmountIqr}"
// Despues:
$"{giftAmountIqr.FormatAsCurrency()}"
```

Esto es una correccion de bug independiente del tema multi-moneda.

#### g) Resumen anual — agrupacion y ordenamiento por moneda

**Antes:** El `GroupBy` en summaries usaba `{ Year, AccountId }`.

**Despues:** `{ Year, AccountId, ForeignCurrencyCodeValueId }` — esto evita que donaciones a la misma cuenta en diferentes monedas se sumen incorrectamente.

El ordenamiento de `SummaryRecords` tambien cambia:
```csharp
// Antes:
.OrderBy( s => s.Order )
// Despues:
.OrderBy( s => s.ForeignCurrencyCodeValueId )
.ThenBy( s => s.Order )
```

#### h) Nuevas clases privadas

| Clase | Proposito |
|---|---|
| `CurrencyTotal` | Tupla `(int? CurrencyCodeValueId, decimal Amount)` para totales por moneda |
| `MonthlyChartItem` | Reemplaza `KeyValuePair<DateTime, decimal>` con tooltip pre-calculado |

#### i) Cambio en `ContributionSummary`

```csharp
// Antes:
public decimal TotalAmount { get; set; }
// Despues:
public List<CurrencyTotal> TotalsByCurrency { get; set; }
```

#### j) Metodo `FormatAsCurrency` interno — firma actualizada

```csharp
// Antes:
private string FormatAsCurrency( decimal value )
// Despues:
private string FormatAsCurrency( decimal value, int? currencyCodeValueId = null )
```

Delega a `value.FormatAsCurrency( currencyCodeValueId, 0 )` en lugar de `FormatAsCurrencyWithDecimalPlaces( 0 )`. Se agrego una guardia `if ( val.Length < 2 ) return val` para evitar `ArgumentOutOfRangeException` con strings de un solo caracter.

### Impacto en el sistema VidaReal

- **Correcto para donantes GTQ + USD:** Un donante que ha dado en ambas monedas ahora ve totales separados en lugar de una suma combinada incorrecta (o una suma en la moneda predeterminada del sistema).
- **Sin impacto en donantes de una sola moneda:** Si `ForeignCurrencyCodeValueId` es siempre `null` (moneda del sistema), el comportamiento es identico al anterior — muestra una sola fila "Total".
- **Tooltips del grafico:** Ahora muestran la descomposicion por moneda en el hover, facilitando el analisis visual.
