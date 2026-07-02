# Plugin.OdooEventSale — Venta de eventos Rock → Odoo (factura FEL)

Workflow action **"Odoo: Registrar Venta de Evento"** (categoría *Vida Real*): por cada
inscripción nueva pagada del módulo de eventos, hace `POST /api/event/sell` al addon
`custom_event_sale_api` de Odoo, que crea cliente + orden + **factura FEL certificada en SAT** + pago.

- Solo factura inscripciones **nuevas con pago**. "Pay later" termina como `SinPago` sin POST. Abonos posteriores contra saldo NO están cubiertos.
- **Toggle "¿Desea factura?" + NIT en la pantalla de pago**: el bloque de inscripción (`RegistrationEntry`) muestra en la pantalla de pago un toggle "¿Desea factura?" y, si se activa, un campo NIT con botón **"Validar NIT"** (mismo UX que el bloque de donaciones). Al validar contra SAT muestra la razón social y habilita el botón Pagar. Apagado → se factura como **CF**. El NIT validado viaja al workflow como atributos pre-poblados (`Nit`, `WantsInvoice`) — **ya no es un campo del formulario de registrante**.
- **NIT**: se normaliza (sin guiones/espacios, mayúsculas — requisito FEL) y se valida en la pantalla de pago contra la API del certificador (`retornarDatosCliente`, Megaprint iFacere). La workflow action **re-valida** el NIT al facturar (validación autoritativa server-side): NIT inexistente en SAT → se factura como **CF** (log del workflow); API caída → se envía el NIT normalizado sin validar (el certificador lo valida al emitir el FEL).
- **Partner siempre actualizado con SAT**: con NIT validado se envían `sat_name` y `sat_address` a Odoo, que **sobreescribe** nombre, dirección y NIT (normalizado) del partner. Es la data real: el módulo `fel_gt` arma el DTE con `partner.vat` (IDReceptor), `partner.name` (NombreReceptor), `partner.email` (CorreoReceptor) y `partner.street/zip/city` (DireccionReceptor). Email y teléfono solo se completan si faltan, nunca se sobreescriben.
- Idempotencia: `payment.reference` = `Guid` de la primera `FinancialTransaction` de la inscripción. Reintentos nunca duplican la factura.
- Desglose de líneas: en pago completo se emite **una línea de evento por registrante** (`event_lines`, su nombre = "evento - asistente", a su costo real) + línea `discount` (código de descuento de Rock) + línea `surcharge` (recargo VisaCuotas / `FeeCoverageAmount` del gateway ePay). Si la suma de las líneas no cuadra con lo cobrado (±0.01) o es pago parcial, cae a **una sola línea** por el monto cobrado. Requiere addon `custom_event_sale_api` ≥ 17.0.1.3.0 (soporta `event_lines`); con un addon viejo, el array se ignoraría — desplegar el addon actualizado.
- Reintentos: ante timeout/red/5xx la action retorna `false` sin completarse; el job *Process Workflows* la reprocesa cada `ProcessingIntervalSeconds` hasta `Max Intentos` (default 5) y luego marca `ErrorPermanente`.

## Compilar y desplegar

```powershell
dotnet build .\OdooEventSale\OdooEventSale.csproj -c Release
Copy-Item .\OdooEventSale\bin\Release\net472\OdooEventSale.dll <ruta>\RockWeb\Bin\
# reciclar el app pool para que MEF descubra la action
```

## Configuración en Rock admin

### 0. Global Attributes (validación de NIT)

`Admin Tools → General Settings → Global Attributes`, crear dos:

| Key | Field Type | Valor |
|---|---|---|
| `OdooNitApiUrl` | Text | `https://apiv2.ifacere-fel.com/api/retornarDatosCliente` (pruebas: `https://dev2.api.ifacere-fel.com/api/retornarDatosCliente`) |
| `OdooNitApiBearerToken` | Encrypted Text | token JWT del certificador (el mismo que usa el bloque de donaciones) |

Los leen **ambos** lados server-side: el block action `ValidateNitInfo` (pantalla de pago) y la workflow action (re-validación al facturar). El host debe estar en la whitelist del código (`apiv2.ifacere-fel.com` / `dev2.api.ifacere-fel.com`).

### 1. Workflow Type "Odoo - Venta de Evento"

`Admin Tools → Power Tools → Workflow Configuration`, categoría Vida Real:

| Setting | Valor |
|---|---|
| **Automatically Persisted** | ✔ **OBLIGATORIO** — sin esto no hay reintentos ni `EntityId` |
| **Processing Interval** | 300 segundos |
| Logging Level | *Action* en staging, *Error* en producción |

**Atributos del workflow** (todos Text salvo indicado; ninguno requerido):

`OdooStatus`, `FelUuid`, `FelSerie`, `FelNumero`, `OrderName`, `InvoiceName`, `OdooError`, `AttemptCount` (Integer o Text), `RegistrationId` (Integer, opcional — solo para lanzamientos manuales), **`Nit`** (Text) y **`WantsInvoice`** (Boolean) — estos dos los pre-pobla el bloque de inscripción con el NIT capturado en la pantalla de pago.

> ⚠️ **CRÍTICO — el _Key_ (no el Name) debe ser exactamente `Nit` y `WantsInvoice`.** El bloque de inscripción inyecta los valores con `SetAttributeValue("Nit", …)` / `SetAttributeValue("WantsInvoice", …)`, que busca por **Key**. Rock auto-genera el Key a partir del Name al crear el atributo: verifica que quede literalmente `Nit` / `WantsInvoice` (sin sufijos ni espacios). Si el Key no coincide, la inyección se descarta **sin error** y **todo se factura como CF silenciosamente**.

### 2. Activity "Procesar Venta" (Activated with Workflow ✔)

1. **Odoo: Registrar Venta de Evento** — configurar:
   - *Odoo Base URL*: `https://<odoo-host>` (sin slash final)
   - *Odoo API Key*: la generada en Odoo → Ajustes → Event Sale API
   - *Nit Attribute* / *WantsInvoice Attribute*: mapear a los atributos `Nit` (Text) y `WantsInvoice` (Boolean/Text) del workflow — el bloque de inscripción los pre-pobla con el NIT validado en la pantalla de pago. **Sin mapear → todo se factura como CF.**
   - *Max Intentos*: 5 · *Timeout*: 90
   - Mapear cada picker (`OdooStatus Attribute`, `AttemptCount Attribute`, etc.) a su atributo del workflow.
   - La config de la API de NIT (URL + token) ya **no** vive en la action: son **Global Attributes** (ver §0).
2. **Activate Activity** → "Alerta Contabilidad", criteria `OdooStatus` *equals* `PendienteFEL`
3. **Activate Activity** → "Alerta Contabilidad", criteria `OdooStatus` *equals* `PagoManual`
4. **Activate Activity** → "Alerta Contabilidad", criteria `OdooStatus` *equals* `ErrorPermanente`
5. **Complete Workflow**

### 3. Activity "Alerta Contabilidad" (NO auto-activada)

1. **Send Email** a contabilidad. Body sugerido:

```
Estado: {{ Workflow | Attribute:'OdooStatus' }}
Error: {{ Workflow | Attribute:'OdooError' }}
Orden Odoo: {{ Workflow | Attribute:'OrderName' }}
Factura: {{ Workflow | Attribute:'InvoiceName' }}
FEL UUID: {{ Workflow | Attribute:'FelUuid' }}
```

Significado por estado:
- **PendienteFEL** — venta y factura creadas en Odoo pero la certificación SAT falló → recertificar manualmente en Odoo. NO se reintenta el POST.
- **PagoManual** — factura certificada pero el pago no quedó registrado → registrar el pago manualmente en Odoo.
- **ErrorPermanente** — la venta NO llegó a Odoo (config, validación o reintentos agotados) → facturar manualmente.

2. **Complete Workflow**

### 4. Asignar a las plantillas de evento

En cada `Registration Template` con venta: campo **Registration Workflow** = "Odoo - Venta de Evento". (Se dispara al completar cada inscripción nueva.)

### 5. Campos de facturación (pantalla de pago — sin configuración de formulario)

**No se agregan campos al formulario de registrante.** El toggle "¿Desea factura?", el campo NIT y el botón "Validar NIT" están integrados en la **pantalla de pago** del bloque `RegistrationEntry` (frontend Obsidian). El NIT validado viaja al workflow como atributos `Nit`/`WantsInvoice` (ver §2.1 y §1).

Requisitos para que funcione la validación de NIT en la pantalla de pago:
- Los **Global Attributes** `OdooNitApiUrl` y `OdooNitApiBearerToken` configurados (ver §0).
- El bundle de Obsidian recompilado (`npm run build` en `Rock.JavaScript.Obsidian.Blocks`) y desplegado a `RockWeb\Obsidian\Blocks\Event\`.

### 6. En Odoo

Ajustes → Event Sale API: verificar productos **Evento Genérico / Descuento Evento / Recargo Evento** (los tres con IVA 12% *incluido en precio*), diario de pago `card`, diario de venta con *Generar FEL*, y API key.

## Estados (`OdooStatus`)

| Estado | Significado | ¿Workflow completa? |
|---|---|---|
| `Exito` | Factura FEL certificada y pago registrado | Sí |
| `PendienteFEL` | Venta creada, FEL falló → alerta | Sí |
| `PagoManual` | FEL ok, pago no registrado → alerta | Sí |
| `SinPago` | Inscripción sin pago, no se envió | Sí |
| `Reintentando` | Error transitorio, reintenta en el próximo ciclo | No (queda activo) |
| `ErrorPermanente` | Config/validación/intentos agotados → alerta | Sí |

## Notas operativas

- La action fuerza `IsPersisted = true` al reintentar, pero igual configurar **Automatically Persisted** en el workflow type: sin persistencia el primer intento fallido no deja rastro consultable.
- Si el servidor se recicla a mitad de un procesamiento, un workflow puede quedar atascado con `IsProcessing = 1` y el job lo ignora para siempre. Rescate:

```sql
UPDATE [Workflow] SET IsProcessing = 0
WHERE IsProcessing = 1 AND CompletedDateTime IS NULL
  AND ModifiedDateTime < DATEADD(MINUTE, -30, GETDATE());
```

- Cada POST puede tardar hasta 90s (la certificación FEL corre dentro del request) y el job de workflows procesa en serie: con muchos eventos simultáneos y Odoo lento, otros workflows se retrasan. Es el costo aceptado del diseño síncrono.

## Checklist de pruebas (staging)

1. Inscripción pagada con NIT → `Exito`; en Odoo: orden confirmada, factura **Pagada**, UUID FEL, total = monto cobrado por ePay.
2. Con código de descuento → línea "Descuento Evento" negativa, total cuadra.
3. Pago con VisaCuotas → línea "Recargo Evento", total = cargo real a la tarjeta.
4. Sin NIT → partner CF. Sin pago (pay later) → `SinPago`, sin POST.
4b. NIT con guion (`1234567-8`) → llega a Odoo sin guion y validado; el partner queda con la razón social y dirección de SAT (verificar que se sobreescriban aunque el partner ya existiera con otro nombre). NIT inexistente (p.ej. `9999999999`) → factura CF + entrada en el log del workflow. Con *NIT API URL* vacío → NIT normalizado sin validar.
4c. Toggle "¿Desea factura?" apagado con NIT escrito → factura CF (no se llama la API de NIT). Toggle encendido sin NIT → CF.
5. Pago parcial (minimum payment) → una sola línea por lo pagado.
6. Odoo apagado → `Reintentando` cada 5 min (ver Workflow log); al encender → `Exito`. Apagado permanente → `ErrorPermanente` al 5.º intento + email.
7. Fallo del certificador FEL → `PendienteFEL` + email.
8. Reintento manual con la misma referencia → `already_processed`, misma factura.
