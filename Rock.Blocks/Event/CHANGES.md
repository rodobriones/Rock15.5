# RegistrationEntry — Cambios VidaReal vs upstream Rock (hotfix-18.1)

## 1. Contexto

`RegistrationEntry` es el bloque Obsidian oficial de Rock para registro a eventos. VidaReal no creó un bloque nuevo — **modificó el bloque existente** con cambios muy focalizados. Esta documentación registra exactamente qué se cambió y por qué.

El commit base de comparación es `ca2ca0ec94` (upstream Rock). Los commits de VidaReal sobre este bloque son:

- `ee2fa59514` — Events, flujos i18n
- `ebe7f8d459` — Epay, eventos y cobros

---

## 2. Cambios en RegistrationEntry.cs (backend)

El diff total es de **+4 líneas, -1 línea** sobre el archivo original (más de 4700 líneas sin cambios). Los cambios son quirúrgicos:

### 2.1 Propagación de FeeCoverageAmount al detalle de transacción (2 lugares)

**Commit:** `ebe7f8d459`

```csharp
// Antes (línea ~4590 y ~4713):
transactionDetail.Amount = paymentInfo.Amount;
transactionDetail.AccountId = ...;

// Después:
transactionDetail.Amount = paymentInfo.Amount;
transactionDetail.FeeCoverageAmount = paymentInfo.FeeCoverageAmount;  // ← NUEVO
transactionDetail.AccountId = ...;
```

Se agrega en dos lugares:
1. En el flujo de pago directo (transacción simple).
2. En el flujo de plan de pagos (`payment plan`).

**Propósito:** Asegurar que el monto de cobertura de cargo (surcharge de cuotas, comisión de gateway Epay) se persista en `FinancialTransactionDetail.FeeCoverageAmount`. Sin esta línea, el campo quedaba en null incluso si el gateway enviaba un valor.

### 2.2 Fix en el query string de return URL (sesión de redirect)

**Commit:** `ebe7f8d459`

```csharp
// Antes (línea ~404):
queryString.Remove(PageParameterKey.RegistrationSessionGuid);
queryString.Add(PageParameterKey.RegistrationSessionGuid, session.Guid.ToString());

// Después:
queryString.Remove(PageParameterKey.RegistrationSessionGuid);
queryString.Remove(ReturnUrlSessionPrefix);                        // ← NUEVO
queryString.Add(ReturnUrlSessionPrefix, session.Guid.ToString()); // ← CAMBIADO
```

**Propósito:** Al construir la URL de retorno para gateways de redirección (ej. Epay, PushPay), ahora se usa `ReturnUrlSessionPrefix` en lugar de `PageParameterKey.RegistrationSessionGuid` para el parámetro que lleva el GUID de sesión. Esto corrige un bug donde el gateway de redirección devolvía al usuario con un parámetro que Rock no reconocía correctamente, causando que la sesión de pago se perdiera.

---

## 3. Cambios en RegistrationService.cs (backend)

El diff es de **+2 líneas, -1 línea**:

### 3.1 Exclusión de FeeCoverageAmount del balance de pago

**Archivo:** `Rock/Model/Event/Registration/RegistrationService.cs`

```csharp
// Antes:
public decimal GetTotalPayments(int registrationId)
{
    return GetPayments(registrationId)
        .Select(p => p.Amount).DefaultIfEmpty()
        .Sum();
}

// Después:
public decimal GetTotalPayments(int registrationId)
{
    return GetPayments(registrationId)
        // Fee coverage (e.g. installment surcharge) should not reduce registration balance due.
        .Select(p => p.Amount - (p.FeeCoverageAmount ?? 0m)).DefaultIfEmpty()
        .Sum();
}
```

**Propósito:** El balance pendiente de una inscripción se calcula como `Costo total - Total pagado`. Si `FeeCoverageAmount` (comisión del gateway o surcharge de cuota) se sumaba al total pagado, el balance debido se reducía artificialmente — el sistema consideraba que el asistente había pagado más de lo que realmente aportó al costo del evento. Al excluir el `FeeCoverageAmount` de la suma, el balance refleja correctamente solo el monto aplicado al registro.

---

## 4. Cambios en el frontend Obsidian (archivos .obs / .ts)

El diff total acumula ~1519 líneas añadidas y ~261 eliminadas en 16 archivos. El cambio dominante es la internacionalización (i18n) ES/EN, con mejoras de UX secundarias.

### 4.1 Sistema de i18n EN/ES (utils.partial.ts — cambio principal)

**Archivo:** `Rock.JavaScript.Obsidian.Blocks/src/Event/RegistrationEntry/utils.partial.ts`

Se agregó (~242 líneas nuevas) un sistema de traducción embebido en el bloque:

```typescript
const registrationEntryText: Record<string, { en: string; es: string; }> = {
    actionPay:    { en: "Pay",      es: "Pagar" },
    actionNext:   { en: "Next",     es: "Siguiente" },
    labelTotal:   { en: "Total",    es: "Total" },
    // ... ~80 claves más
};
```

Funciones exportadas:
- `getRegistrationEntryUiLanguage()` — lee el idioma desde `localStorage` (clave `rock.obsidian.uiLanguage`), default `"es"`.
- `setRegistrationEntryUiLanguage(lang)` — persiste el idioma.
- `isSpanishUiLanguage()` — helper booleano para bifurcar lógica.
- `localizeRegistrationEntryTerm(term)` — traduce términos de plantilla (registrant, fee, discount code) al español.
- `getRegistrationEntryText(key, replacements?)` — función principal `t()`, soporta interpolación `{token}`.

**Propósito:** VidaReal opera en Guatemala (español). El bloque original de Rock está completamente en inglés. En lugar de hacer un fork complejo o esperar soporte oficial de i18n, se embebió un diccionario de ~80 claves con sus traducciones EN/ES. El idioma por defecto es español y hay un selector EN/ES visible en la UI.

### 4.2 Selector de idioma EN/ES en el shell del bloque

**Archivo:** `registrationEntry.obs`

Se agregó un widget flotante con dos botones `EN` / `ES`. Al hacer clic se llama `setRegistrationEntryUiLanguage` y se actualiza `uiLanguage` (ref reactivo), lo que dispara la reactividad del template y re-renderiza con el nuevo idioma.

### 4.3 Transiciones de paso (Transition Vue)

**Archivo:** `registrationEntry.obs`

Los pasos del flujo (`Intro`, `Registrants`, `Review`, `Payment`, `Success`) ahora están envueltos en `<Transition name="re-step" mode="out-in">` con un `<div :key="currentStep">`. Esto produce una animación de fade entre pasos, mejorando la percepción de progreso.

### 4.4 Indicador móvil de paso actual

**Archivo:** `registrationEntry.obs`

Se agregó un elemento `div.registration-mobile-step` visible solo en `d-md-none` (móvil) que muestra el nombre del paso actual y el contador `X / N`:

```html
<div class="registration-mobile-step d-md-none">
    <span class="registration-mobile-step-pill">{{ mobileStepTitle }}</span>
    <span class="registration-mobile-step-count">{{ progressTrackerIndex + 1 }} / {{ progressTrackerItems.length }}</span>
</div>
```

### 4.5 Numeración de registrantes en español

**Archivo:** `registrationEntry.obs`

El título del paso de registrante (ej. "Second Registrant") usaba `NumberFilter.toOrdinal()` (primero, segundo...). En español esto no aplica — se usa el número arábigo directamente:

```typescript
if (isSpanishUiLanguage()) {
    title = `${registrantSingularTitleCase} ${registrantIndex}`;
}
else {
    title = toTitleCase(`${toWord(registrantIndex)} ${registrantSingularTitleCase}`);
}
```

### 4.6 Pantalla de pago (payment.partial.obs)

- `amountToPayText` ahora usa `toCurrencyOrNull(amount, currencyInfo)` en lugar de `$${amount.toFixed(2)}`. Respeta la configuración de moneda de la organización Rock (`CurrencyInfoBag` desde `viewModel.currencyInfo`).
- `gatewayValidationFields` pasa por un mapeador que traduce nombres de campo del gateway ("Card Number" → "Numero de tarjeta", "Expiration Date" → "Fecha de expiracion", etc.) cuando el idioma es español.
- Se agregó `try/catch` alrededor de `submitPayment()` para mostrar `messageUnexpectedError` en lugar de lanzar una excepción sin capturar.
- El componente `RockValidation` fue reemplazado por un `NotificationBox` de tipo `validation` con lista `<ul>` de errores, lo que da más control visual.
- Se limpia `gatewayErrorMessage`, `gatewayValidationFields` y `submitErrorMessage` al inicio de `onNext()` para evitar mensajes de error residuales de un intento previo.
- Estilos CSS scoped nuevos para el pill de monto pagado y las tarjetas de método de pago.

### 4.7 Pantalla de éxito (success.partial.obs)

- Se eliminó el componente `SaveFinancialAccountForm` (guardar cuenta para pagos futuros). VidaReal no expone esta funcionalidad al usuario final.
- El HTML del mensaje de éxito pasa por un post-procesador que:
  1. Elimina referencias de cuentas (`(Acct #: ... Ref #: ...)`) por seguridad.
  2. Reemplaza strings en inglés fijos (`"Congratulations"`, `"Total Cost:"`, etc.) cuando el idioma es español.

### 4.8 Resumen de costos (costSummary.partial.obs)

- Todos los literales ("Description", "Amount", "Total", "Payment Plan", etc.) pasaron por `t()`.
- El display de montos usa el símbolo de la moneda desde `currencyInfo` en lugar del hardcoded `$`.
- Se agregó la condición `&& shouldShowAmountDueSummary` para ocultar el "Amount Due" cuando corresponde (evita confusión en flujos con plan de pagos activo).
- Se agregaron media queries responsive para `fee-totals-options` en móvil.

### 4.9 Formulario de registrante (registrant.partial.obs)

- `RockForm` ahora usa `:hideErrors="true"` y `@visibleValidationChanged`. Los errores de validación se muestran en un `NotificationBox` custom (con lista `<ul>`) en lugar del box genérico de Rock, y se traducen al español cuando corresponde.
- Labels de los dropdowns de familia, invitado y familiar se pasan por `t()`.
- El título del documento de firma se pasa por `t("headingPleaseSignDocumentFor", {...})`.

---

## 5. Resumen ejecutivo de los cambios por área

| Área | Alcance del cambio | Propósito |
|---|---|---|
| `RegistrationEntry.cs` (C#) | +4 líneas | Persistir `FeeCoverageAmount` en transacciones; fix de query string en redirect de gateway |
| `RegistrationService.cs` (C#) | +2 líneas | Excluir `FeeCoverageAmount` del cálculo de balance para que no reduzca artificialmente el saldo pendiente |
| `utils.partial.ts` | +242 líneas | Sistema completo de i18n EN/ES con ~80 claves de texto y persistencia en localStorage |
| `registrationEntry.obs` | +140 líneas | Selector idioma, transiciones de pasos, indicador móvil, numeración en español |
| `payment.partial.obs` | +160 líneas | Multi-moneda, traducción de errores de gateway, UX de errores, estilos |
| `success.partial.obs` | +120 líneas | Traducción del mensaje de éxito, eliminación de datos de cuenta, remoción de SaveFinancialAccountForm |
| `costSummary.partial.obs` | +100 líneas | Traducción de labels, multi-moneda, responsive móvil |
| `registrant.partial.obs` | +60 líneas | Errores de validación custom traducidos |
| Resto de partials | Menor | Traducción de botones y labels (Previous/Next/Apply/etc.) |

---

## 6. Notas de mantenimiento

- El sistema i18n es **interno al bloque** — no usa el mecanismo de traducción de Rock ni archivos `.resx`. Si Rock en el futuro provee i18n nativa para Obsidian, este sistema deberá migrarse.
- El idioma default es `"es"`. Si se desplegara este bloque en un sitio solo en inglés, cambiar el default en `getRegistrationEntryUiLanguage()`.
- `FeeCoverageAmount` requiere que el gateway (Epay) envíe ese campo en `PaymentInfo`. Verificar que el adaptador de Epay lo populea antes de llamar `ProcessPaymentWithTransaction`.
- La remoción de `SaveFinancialAccountForm` en `success.partial.obs` es intencional para VidaReal. Si otro tenant necesita esa funcionalidad, restaurar el componente y sus computed (`gatewayGuid`, `transactionCode`, `gatewayPersonIdentifier`, `enableSaveAccount`).
