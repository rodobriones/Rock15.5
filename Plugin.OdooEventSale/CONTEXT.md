# CONTEXT — Integración Rock Eventos → Odoo FEL

> Documento de contexto para retomar el trabajo en una sesión futura.
> Estado al 2026-06-15: **implementado y compilado** (incluye NIT en pantalla de pago, ver §8). Pendiente: build del bundle Obsidian + config y pruebas en staging.

## 1. Objetivo

Por cada **inscripción nueva pagada** del módulo de eventos de Rock (Event Registration, pago con `Plugin.EpayVisanetGateway`), generar automáticamente en Odoo: cliente + orden de venta + **factura FEL certificada en SAT** + pago registrado. El disparo es un **workflow de Rock** con una action custom que hace `POST /api/event/sell` al addon de Odoo.

## 2. Piezas y repos

| Pieza | Ubicación | Estado |
|---|---|---|
| Workflow action "Odoo: Registrar Venta de Evento" | `C:\Repos\Rock18.1\Plugin.OdooEventSale\OdooEventSale\PostEventSaleToOdoo.cs` | Compilada; DLL copiada a `RockWeb\Bin` |
| Addon Odoo 17 `custom_event_sale_api` (v17.0.1.2.0) | `C:\Repos\Iglesia1\custom_event_sale_api` | Modificado; **pendiente `-u` (upgrade) en la BD** |
| Módulo FEL (referencia, no se tocó) | `C:\Repos\Iglesia1\fel_gt` + `fel_megaprint` | Certifica al postear la factura |
| Configuración manual en Rock admin | `Plugin.OdooEventSale\README.md` | Documentada, **no aplicada aún** |
| Plan original aprobado | `~\.claude\plans\lee-esto-c-repos-iglesia1-custom-event-s-flickering-music.md` | Ejecutado |

Referencias usadas:
- Contrato del API Odoo: `custom_event_sale_api\docs\API.md` y `docs\GUIA_ROCK.md`
- Manual FEL del certificador: `C:\Users\Rodolfo Rodriguez\Downloads\Manual de implementacion Servicios FEL 2.1 (3) (3) (1) (1).pdf` (§6.7 retornarDatosCliente, pág. 24)
- Patrón de validación de NIT: `Rock.Blocks\Dar\CybersourceDonationEntry.cs` (método `LookupNitFromExternalApi`) + su `.obs`

## 3. Decisiones tomadas (del usuario)

1. **NIT en la pantalla de pago** (cambio 2026-06-15): el NIT ya NO es un campo del formulario de registrante. Se captura en la **pantalla de pago** del bloque core `RegistrationEntry` (frontend Obsidian), con botón "Validar NIT" contra SAT — mismo UX que el bloque de donaciones. Vacío/inválido → `CF`.
2. **Toggle "¿Desea factura?"** en la misma pantalla de pago (no en el formulario): apagado → CF directo sin validar NIT. El NIT validado viaja al workflow como atributos pre-poblados `Nit`/`WantsInvoice`.
3. Descuentos y recargos van como **líneas separadas** en la factura, con productos propios en Odoo (EVENT-DISC / EVENT-SURCH); el API se extendió con un array `lines` retrocompatible.
4. Alcance: **solo inscripción nueva pagada**. Abonos posteriores contra saldo NO se facturan (limitación aceptada y documentada).
5. **Partners siempre se actualizan con la data de SAT** (razón social y dirección) — "esa es la data real".

## 4. Flujo end-to-end

```
Inscripción pagada (RegistrationEntry → ePay Visanet)
  └─ ProcessPostSave lanza el workflow (RegistrationTemplate.RegistrationWorkflowType)
       └─ Action PostEventSaleToOdoo:
            1. Resuelve Registration (entity → Workflow.EntityId → attr RegistrationId)
            2. Guard: sin cobro real (charged ≤ 0) → SinPago (1er intento espera un ciclo por carrera)
            3. reference = Guid de la PRIMERA FinancialTransaction (idempotencia)
            4. Arma líneas: evento (qty×price) − descuento + recargo VisaCuotas; debe cuadrar
               con lo cobrado (±0.01) o cae a UNA línea por el monto cobrado
            5. Toggle factura + NIT: normaliza (solo alfanumérico, mayúsculas) y valida
               contra retornarDatosCliente; obtiene razón social y dirección de SAT
            6. POST /api/event/sell (X-API-KEY, timeout 90s — FEL certifica dentro del request)
            7. Mapea respuesta → OdooStatus y decide completar (true) o reintentar (false)
       └─ Si OdooStatus ∈ {PendienteFEL, PagoManual, ErrorPermanente} → activity "Alerta Contabilidad" (email)
```

**Retry (verificado contra el código de Rock):** `Execute` retorna `false` sin marcar la action completa → el workflow queda activo y el job *Process Workflows* lo reprocesa cada `ProcessingIntervalSeconds` (300s). Los workflow attributes se persisten aunque retorne false (`WorkflowService.cs:119-146`), por eso el contador `AttemptCount` sobrevive. En reintentos `entity == null`: la Registration se recarga desde `Workflow.EntityId`. La action **fuerza `IsPersisted = true`** al reintentar (no depende de configuración). No hay loop en el mismo request: la activity guarda `LastProcessedDateTime` y el while de `ProcessActivities` la salta.

**Estados (`OdooStatus`):** `Exito` · `PendienteFEL` (factura creada, SAT falló → alerta, NO reintentar) · `PagoManual` (FEL ok, pago no registrado → alerta) · `SinPago` (pay later) · `Reintentando` (transitorio, hasta Max Intentos=5) · `ErrorPermanente` (401/validation/intentos agotados/orden cancelada → alerta).

## 5. NIT y FEL (sesión 2026-06-11)

- **FEL exige el NIT sin guiones**: la action lo normaliza con `char.IsLetterOrDigit` + mayúsculas (réplica server-side del `onNitInput` de donaciones). Odoo además tiene su propio `_normalize_nit`.
- **Validación**: `POST retornarDatosCliente` (Megaprint iFacere, XML + Bearer token — manual FEL §6.7). Ocurre en DOS lados server-side: el block action `ValidateNitInfo` (pantalla de pago, UX) y la workflow action `LookupNit` (re-validación autoritativa al facturar). La config es **Global Attributes** (cambio 2026-06-15): `OdooNitApiUrl` (prod `https://apiv2.ifacere-fel.com/api/retornarDatosCliente`; pruebas `https://dev2.api.ifacere-fel.com/...`) y `OdooNitApiBearerToken` (encriptado, el mismo del bloque de donaciones). Host en whitelist del código (`apiv2.ifacere-fel.com`/`dev2.api.ifacere-fel.com`). NO se pasa `sat_name`/`sat_address` por el workflow: la action los re-deriva.
  - Válido → se envían `partner.sat_name` y `partner.sat_address` en el payload.
  - No existe en SAT → se factura **CF** (log en el workflow). La venta nunca se bloquea (no hay usuario en línea para corregir, a diferencia de donaciones).
  - API caída → NIT normalizado sin validar (el certificador valida al emitir; si falla → `PendienteFEL`).
- **Por qué actualizar el partner importa**: `fel_gt/models/account.py:215-240` arma el DTE con los campos del partner: `IDReceptor`=`vat` (quita guiones), `NombreReceptor`=`name`, `CorreoReceptor`=`email`, `DireccionReceptor`=`street/zip/city`. **No envía solo el NIT.**
- **Odoo** (`_find_or_create_partner`): si llegan `sat_name`/`sat_address` → **sobreescribe siempre** `name`, `street` y `vat` (normaliza NITs viejos con guion). Email/teléfono solo se completan si faltan.

## 6. API Odoo — cambios hechos (requieren upgrade del módulo)

- `lines: [{type: "discount"|"surcharge", name, price, quantity}]` opcional (máx 10; discount price<0, surcharge >0; total redondeado > 0). Retrocompatible: sin `lines` el comportamiento es idéntico.
- Productos nuevos en `data/default_product.xml`: EVENT-DISC, EVENT-SURCH + config params `discount_product_id`/`surcharge_product_id`; configurables en Ajustes → Event Sale API.
- `partner.sat_name`/`partner.sat_address` (ver §5).
- Respuesta: nuevo campo `payment_state` de la factura (Rock lo usa para detectar `already_processed` sin pago → `PagoManual`).
- Endurecimiento: rechazo de NaN (`math.isfinite`) en todos los montos, `quantity: 0` ya no se vuelve 1, nombres de línea a 500 chars.
- ⚠️ **Post-upgrade manual**: asignar IVA 12% "incluido en precio" a EVENT-DISC y EVENT-SURCH (igual que EVENT-GEN).

## 7. Revisión multi-agente (bugs encontrados y corregidos)

Se corrieron 3 agentes de revisión (addon, action vs motor de workflows, contrato de integración). Corregido:
- NaN evadía la validación de Odoo (alta) · pérdida silenciosa de la alerta PagoManual vía `already_processed` (alta, fix con `payment_state`) · retry dependía de marcar Persisted a mano (fix: la action lo fuerza) · excepciones transitorias terminaban en ErrorPermanente (ahora reintentan) · descuento 100% + recargo se marcaba SinPago (guard ahora usa `charged`) · factura podía quedar en total ≤ 0 por redondeo · registrantes en waitlist inflaban conteo/NIT · `already_processed` con orden cancelada → alerta · include `Registrants.Fees` (N+1) · advertencia en log si total facturado ≠ cobrado (±0.02, detecta IVA mal configurado).

**Riesgo #1 — RESUELTO (2026-06-18, verificado en pruebas + código):** el certificador **NO rechaza** la línea de descuento negativa, pero **tampoco la conserva**. El módulo `fel_gt` (`fel_gt/models/account.py`, función `descuento_lineas()` ~línea 78, llamada en `_post()` ~línea 268) hace, al certificar, lo siguiente con cualquier línea de `price_total < 0`:
  1. Le pone `price_unit = 0` (la línea EVENT-DISC queda en **Q0.00** en la factura).
  2. Reparte el monto del descuento **proporcionalmente entre las líneas positivas** (EVENT-GEN, EVENT-SURCH), bajando su importe. **El total de la factura se conserva** (cliente paga correcto).
  Motivo: el **DTE de SAT no admite líneas con monto negativo**; el descuento se expresa como `Descuento` por línea sobre los productos reales. → **Consecuencia esperada: EVENT-DISC NUNCA se ve como -5 en una factura certificada GT; sale en Q0. NO es bug.**
  - Interruptor en el **diario**: campo `no_usar_descuento_fel` ("No usar descuento cuando hay lineas negativas en FEL"). Off (default) = usa el campo `discount` de las líneas positivas; On = baja directamente el `price_unit` de las positivas.
  - Lo único cosmético: queda un renglón "Descuento" en Q0. Si molesta, la alternativa es **no mandar línea de descuento separada desde Rock** y netear el descuento en el monto del evento (decisión pendiente del lado de Rock).

**Limitaciones conocidas:** abonos posteriores no se facturan (la referencia es siempre la 1ª transacción; un 2º pago devolvería `already_processed`) · el POST de 90s corre en serie con otros workflows (costo aceptado) · workflows atascados con `IsProcessing=1` tras un reciclaje: query de rescate en el README.

## 8. NIT en pantalla de pago (sesión 2026-06-15)

Se implementó la validación de NIT **en vivo en la pantalla de pago** (lo que en §8 anterior era "pendiente opcional"). Se forkeó el bloque core `RegistrationEntry`:
- **Frontend**: `RegistrationEntry/payment.partial.obs` (toggle "¿Desea factura?" + input NIT + botón "Validar NIT" + razón social readonly; guard en `onNext` y `:disabled` en Pagar), `RegistrationEntry/types.partial.ts` (state `wantsInvoice/nit/nitName/nitAddress`), `registrationEntry.obs` (init + `getRegistrationEntryBlockArgs` manda `nit`/`wantsInvoice`), viewmodel `registrationEntryArgsBag.d.ts`.
- **Backend**: `RegistrationEntryArgsBag.cs` (props `Nit`/`WantsInvoice`), `RegistrationEntry.cs` (block action `ValidateNitInfo` + helpers anti-SSRF/rate-limit; `ProcessPostSave` pasa `{Nit, WantsInvoice}` al `LaunchWorkflow`, ~línea 5557).
- **Workflow action** `PostEventSaleToOdoo.cs`: lee `Nit`/`WantsInvoice` de workflow attributes (pickers); config NIT desde Global Attributes; conserva `LookupNit` (re-valida) y ahora loguea el caso `NotConfigured`.
- **Revisión multi-agente (2026-06-15)**: 4 agentes (core C#, action, frontend, integración). Veredicto: correcto end-to-end. Fixes aplicados: log de `NotConfigured` en la action; cap de memoria en `_nitRateBuckets`; advertencia ⚠️ en README sobre el Key exacto `Nit`/`WantsInvoice` (si no coincide → CF silencioso). **Todo compila**: plugin 0/0, Rock.Blocks 0 errores, frontend lint+typecheck limpio.

### Qué falta
1. **Frontend build**: `npm run build` en `Rock.JavaScript.Obsidian.Blocks` para regenerar `registrationEntry.obs.js` y desplegarlo a `RockWeb\Obsidian\Blocks\Event\` (los .cs ya están en `RockWeb\Bin`).
2. **Global Attributes**: crear `OdooNitApiUrl` y `OdooNitApiBearerToken` (README §0).
3. **Odoo staging**: `-u custom_event_sale_api`, asignar IVA a productos nuevos, verificar diario card y API key.
4. **Rock staging**: reciclar app pool, crear el Workflow Type "Odoo - Venta de Evento" (Persisted ✔, interval 300s) con atributos incluyendo `Nit` (Text) y `WantsInvoice` (Boolean) — **Key exacto**; mapear los pickers de la action; asignarlo a los RegistrationTemplate. (Ya NO se agregan campos al formulario.)
5. **Correr el checklist** del README — NIT con guion, NIT inexistente, toggle apagado, descuento (línea EVENT-DISC saldrá en Q0 con el descuento repartido en las líneas positivas — comportamiento FEL esperado, ver §7 Riesgo #1), cuotas, pago parcial, Odoo caído, idempotencia.
