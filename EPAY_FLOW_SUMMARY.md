# ePay Flow Summary (Rock 18.1)

## Objetivo
Documentar el flujo de cobro ePay con cuotas y los cambios realizados para:
- Cobrar `monto evento + recargo`.
- Persistir el recargo como `FeeCoverageAmount`.
- Mantener consistencia entre `TotalPaid`, `BalanceDue` y el detalle mostrado al usuario.

## Flujo completo (alto nivel)
1. `RegistrationEntry` calcula costos y define `amountToPayToday`.
2. En pantalla de pago, `GatewayControl` pasa `amountToPay` al control ePay Obsidian.
3. El control ePay tokeniza tarjeta y envía `installmentCode` (si aplica).
4. El gateway C# (`EpayVisanetGateway`) calcula recargo por cuota, arma payload SOAP y cobra en ePay.
5. Se crea `FinancialTransaction` con `Amount` total cobrado y `FeeCoverageAmount` como recargo.
6. `RegistrationEntry.SaveTransaction` persiste detalle de transacción asociado a `Registration`.
7. `RegistrationService.GetTotalPayments` usa `Amount - FeeCoverageAmount` para balance del evento.

## Cambios en plugin ePay (C#)
Archivo: `Plugin.EpayVisanetGateway/EpayVisanetGateway/EpayVisanetGateway.cs`

### SOAP y comunicación
- Namespace SOAP corregido a `http://general_computing.com/paymentgw/types`.
- Header `SOAPAction` corregido a `""`.
- Mejor manejo de `WebException` para parsear fault SOAP cuando el gateway devuelve body con error.

### Audit Number
- Generación actualizada para prefijo `8` (`8xxxxx`).

### Recargo por cuotas
- Se calcula sobre monto base:
  - `baseAmount`
  - `chargeAmount = baseAmount * (1 + surcharge%)`
  - `surchargeAmount = chargeAmount - baseAmount`
- Se persiste en `PaymentInfo`:
  - `paymentInfo.Amount = chargeAmount`
  - `paymentInfo.FeeCoverageAmount = surchargeAmount`
- Se pre-carga un `FinancialTransactionDetail` para asegurar que el recargo quede guardado:
  - `transactionDetail.Amount = chargeAmount`
  - `transactionDetail.FeeCoverageAmount = surchargeAmount`

### Mapa de errores ePay
- Se completó y alineó con el catálogo del módulo Odoo (incluye `93`).
- Mensaje de `93`: `Transaccion no puede completarse, valide configuracion con el adquirente.`

## Cambios en UI del gateway ePay (Obsidian)
Archivos:
- `Plugin.EpayVisanetGateway/ObsidianSource/epayVisanetGatewayControl.obs`
- `Plugin.EpayVisanetGateway/Deploy/Plugins/EpayVisanetGateway/Obsidian/epayVisanetGatewayControl.obs.js`
- `Plugin.EpayVisanetGateway/Deploy/Plugins/EpayVisanetGateway/ObsidianSource/epayVisanetGatewayControl.obs`

Cambio:
- El control ahora recibe prop `amount`.
- Al elegir cuotas, muestra:
  - `% de recargo`
  - monto base
  - recargo calculado en dinero
  - total con recargo

## Cambios en Event / RegistrationEntry (C#)
Archivo: `Rock.Blocks/Event/RegistrationEntry.cs`

Cambio:
- En guardado de transacciones (`SaveTransaction` y guardado de plan):
  - además de `transactionDetail.Amount`, ahora persiste:
  - `transactionDetail.FeeCoverageAmount = paymentInfo.FeeCoverageAmount`

## Cambios en cálculo de pagos del Registration
Archivo: `Rock/Model/Event/Registration/RegistrationService.cs`

Cambio:
- `GetTotalPayments(registrationId)` ahora suma:
  - `Amount - FeeCoverageAmount`
- Resultado:
  - el recargo de cuotas no reduce artificialmente el saldo del evento.

## Impacto en Lava / Confirmaciones
- Para mostrar detalle financiero real:
  - Total pagado (cobrado): `payment.Amount`
  - Recargo: `payment.FeeCoverageAmount`
  - Aplicado al evento: `payment.Amount - payment.FeeCoverageAmount`
- Si hay pagos históricos sin `FeeCoverageAmount`, puede usarse fallback leyendo `surcharge=` en `Transaction.StatusMessage`.

## Checklist de despliegue
1. Publicar DLL plugin ePay (`EpayVisanetGateway.dll`).
2. Publicar DLLs core cambiadas (`Rock.Blocks.dll`, `Rock.dll`) si aplican.
3. Publicar assets Obsidian del plugin (`obs.js` de deploy).
4. Limpiar cache de navegador / hard refresh.
5. Validar en DB un pago con cuotas:
   - `FinancialTransactionDetail.Amount = total cobrado`
   - `FinancialTransactionDetail.FeeCoverageAmount = recargo`

