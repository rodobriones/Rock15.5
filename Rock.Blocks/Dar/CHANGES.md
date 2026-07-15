# Módulo DAR — Backend C# — Historial de cambios y documentación

> **Rama:** `hotfix-18.1`
> **Última actualización:** 2026-07-08
>
> Este documento cubre los dos bloques C# del módulo DAR, el flujo completo
> de donación, la integración con Cybersource, las 4 features de anti-fraude,
> el manejo de múltiples monedas y la implementación de reCAPTCHA.
>
> Para la documentación técnica detallada del bloque principal, ver
> [`CybersourceDonationEntry.md`](CybersourceDonationEntry.md).

> **Endurecimiento 2026-06-21 (security review #2–#7).** Tras una revisión de
> OWASP/idempotencia/seguridad se corrigieron 6 hallazgos. El cambio mayor es
> el **patrón de fila `Pending` durable**: la transacción se reserva en BD
> ANTES de cobrar y se promueve a `Approved` después, lo que cierra el doble
> cobro de donantes anónimos (#2) y el hueco "cobrado pero no guardado" (#3).
> Resumen:
> - **#2** Idempotencia ahora aplica también a donantes **anónimos** (clave por `idemKey`, no por persona).
> - **#3** Reserva `Pending` antes del cobro + flag `ambiguous` (timeout/5xx) que **conserva** la fila para que un reintento no recobre.
> - **#4** `sp_getapplock` (cross-node) dentro de una transacción de BD para serializar el check+insert, además del lock in-process.
> - **#5** El **batch se separa por moneda** (`... (GTQ)` / `... (USD)`) para no mezclar totales de control sin conversión FX.
> - **#6** `idemKey` ahora es **obligatorio** y se valida con cap de **32** chars (coincide con el truncado de `ForeignKey`).
> - **#7** Se fuerza **HTTPS** en el Base URL de Cybersource (el PAN nunca sale en claro aunque el admin configure mal).
> - **#8** **Purga oportunista** del rate limiter en memoria para que no crezca sin límite (fuga de memoria / DoS lento).
> - **#1** (PAN crudo atraviesa el servidor → alcance PCI-DSS SAQ D / migrar a Microform) queda como **decisión de negocio**, no abordado en este cambio.

---

## Índice

1. [Propósito del módulo DAR](#1-propósito-del-módulo-dar)
2. [Contexto: qué no existe en Rock original](#2-contexto-qué-no-existe-en-rock-original)
3. [Estructura de archivos](#3-estructura-de-archivos)
4. [Bloques C# — qué hace cada uno](#4-bloques-c--qué-hace-cada-uno)
5. [Flujo completo de donación](#5-flujo-completo-de-donación)
6. [Integración con Cybersource](#6-integración-con-cybersource)
7. [Anti-fraude — las 4 features](#7-anti-fraude--las-4-features)
8. [Manejo de múltiples monedas](#8-manejo-de-múltiples-monedas)
9. [reCAPTCHA Enterprise](#9-recaptcha-enterprise)
10. [Integración NIT / SAT Guatemala](#10-integración-nit--sat-guatemala)
11. [Persistencia en Rock Finance](#11-persistencia-en-rock-finance)
12. [Workflows y email de confirmación](#12-workflows-y-email-de-confirmación)
13. [Historial de commits relevantes](#13-historial-de-commits-relevantes)

---

## 1. Propósito del módulo DAR

El módulo DAR es el sistema de **donaciones en línea de VidaReal**. Permite que
los feligreses realicen donaciones con tarjeta de crédito/débito directamente
desde el sitio web, sin salir al portal de Rock RMS.

Características principales:

- Donaciones con **tarjeta de crédito/débito** (Visa, Mastercard) procesadas
  por **Cybersource REST API** (gateway de Visanet Guatemala, `vdcguatemala`).
- Soporte de **dos monedas**: Quetzales guatemaltecos (GTQ) y Dólares (USD).
- **Recibo fiscal** opcional con validación de NIT ante la API de facturación
  electrónica (`ifacere-fel.com`), conforme al régimen fiscal guatemalteco.
- Cuatro capas de **anti-fraude**: reCAPTCHA Enterprise, Device Fingerprint,
  AVS normalizado e idempotencia transaccional.
- Registro automático de cada donación en el módulo **Rock Finance**
  (`FinancialTransaction`, `FinancialTransactionDetail`, `FinancialBatch`).
- **Dashboard administrativo** para consulta, filtrado y exportación de
  donaciones por cuenta, moneda, fecha y NIT.

---

## 2. Contexto: qué no existe en Rock original

Rock RMS (SparkDevNetwork) incluye bloques de donación propios (NMI, Stripe,
iATS, etc.) que no son compatibles con Cybersource ni con el esquema de
facturación guatemalteca (NIT/FEL). El módulo DAR fue construido desde cero
en el fork `hotfix-18.1` como **código completamente nuevo** bajo el
namespace `Rock.Blocks.Dar`, sin modificar ningún bloque estándar de Rock.

Los únicos archivos del core de Rock que se modificaron son de la capa
`FinancialTransaction` para soportar el campo `ForeignCurrencyCodeValueId`
de forma compatible con el resto del sistema.

---

## 3. Estructura de archivos

```
Rock.Blocks/
  Dar/
    CybersourceDonationEntry.cs          Bloque de donación (2841 líneas)
    CybersourceDonationEntry.md          Documentación técnica detallada
    DonationDashboard.cs                 Bloque de dashboard (500 líneas)
    EmailTemplates/
      confirmacion-donacion.html         Template Lava del email de confirmación

Rock.JavaScript.Obsidian.Blocks/
  src/Dar/
    CybersourceDonationEntry.obs         Frontend Vue 3 del formulario (2888 líneas)
    CybersourceDonationEntry.md          Documentación técnica del frontend
    DonationDashboard.obs                Frontend Vue 3 del dashboard (574 líneas)
    tsconfig.json

Rock/Model/Finance/FinancialTransaction/
    FinancialTransactionService.cs       Modificado: soporte multi-moneda
    MonthlyAccountGivingHistory.cs       Modificado: historial mensual por moneda
```

---

## 4. Bloques C# — qué hace cada uno

### 4.1. `CybersourceDonationEntry` — `CybersourceDonationEntry.cs`

**Namespace:** `Rock.Blocks.Dar`
**Clase base:** `RockBlockType`
**Display name:** `Cybersource Donation Entry`
**Categoría:** `Custom`

Es el bloque principal del módulo. Expone cuatro Block Actions al frontend:

| Action | Descripción |
|---|---|
| `GetObsidianBlockInitialization` | Devuelve `InitBag` al montar: cuentas, email de la persona, modo, claves de Device Fingerprint y reCAPTCHA. |
| `ValidateNitInfo(nit)` | Consulta la API FEL de ifacere para obtener nombre y dirección fiscal del contribuyente. Incluye rate limit de 10 peticiones/minuto por IP. |
| `GetPaymentHistory()` | Devuelve las últimas 100 transacciones del usuario logueado, filtrando por `ForeignKey LIKE 'CYBS|%'`. |
| `ProcessPayment(bag)` | Pipeline completo: validación, reCAPTCHA, NIT re-validado, idempotencia, cobro Cybersource, registro Rock Finance, encolado de workflows. |

El bloque tiene **18 atributos de configuración** agrupados en tres categorías:

- **Finance:** cuentas, gateway, tipo de transacción, prefijo de batch, moneda por defecto, workflows de donación y recibo, key del atributo de NIT en Persona.
- **Gateway:** modo live/test, credenciales de Cybersource (Merchant ID, Key ID, Shared Secret encriptado), host, path, timeout.
- **Security:** site key, API key y proyecto de reCAPTCHA Enterprise, score mínimo.

### 4.2. `DonationDashboard` — `DonationDashboard.cs`

**Namespace:** `Rock.Blocks.Dar`
**Clase base:** `RockBlockType`
**Display name:** `Donation Dashboard`
**Categoría:** `Dar`

Bloque administrativo de solo lectura. Expone tres Block Actions:

| Action | Descripción |
|---|---|
| `GetFilterOptions` | Devuelve las listas de cuentas y monedas disponibles para los filtros. |
| `GetTransactions(filter)` | Consulta `FinancialTransaction` con filtros de fecha, cuenta, moneda, NIT y persona. Devuelve hasta N resultados (configurable, por defecto 500). |
| `ExportToExcel(filter)` | Genera un archivo `.xlsx` (EPPlus) con los mismos datos y lo devuelve como Base64. |

Los filtros disponibles son: rango de fechas, cuentas (múltiples), monedas (múltiples), NIT (busca en el campo `Summary`) y persona (por `PersonAliasGuid`).

El NIT se extrae del campo `Summary` de la transacción, que tiene el formato
`Note | Email: ... | NIT: 12345678 | NIT Nombre: ... | Ref: ... | Auth: ...`.

---

## 5. Flujo completo de donación

El flujo completo, desde que el usuario llena el formulario hasta que la
donación queda registrada en Rock:

```
1. [Frontend] Usuario llena monto, selecciona cuenta y moneda (GTQ/USD)
2. [Frontend] Ingresa datos de tarjeta (PAN, exp, CVV, nombre)
3. [Frontend] Opcionalemente activa recibo fiscal e ingresa NIT
4. [Frontend] → Block Action ValidateNitInfo(nit)
5. [Backend]  Rate limit por IP (10 req/min)
6. [Backend]  Sanea NIT (solo alfanum, max 32)
7. [Backend]  POST XML a ifacere-fel.com con Bearer token
8. [Backend]  Extrae <nombre> y <direccion> del XML respuesta
9. [Frontend] Muestra nombre y dirección en campos read-only
10. [Frontend] Usuario ingresa email y hace click en "Donar"
11. [Frontend] validateForm() — validaciones cliente (Luhn, exp, CVV, email, amex)
12. [Frontend] Modal de confirmación con resumen de cuenta, monto y tarjeta
13. [Frontend] Usuario confirma
14. [Frontend] grecaptcha.enterprise.execute(siteKey, {action:"donation"}) → token
15. [Frontend] → Block Action ProcessPayment(bag)
    bag contiene: accountId, amount, currency, cardName/Number/Exp/CVV,
                  wantsReceipt, nit, donorEmail, idemKey,
                  deviceFingerprintSessionId, recaptchaToken
16. [Backend]  ValidatePaymentRequest() — re-validación servidor
    - accountId > 0
    - email con regex
    - amount 0..99,999,999
    - card 12-19 dígitos
    - AmEx rechazada (BIN 34xx/37xx)
    - expMonth 1-12, expYear dentro de rango
    - cvv 3-4 dígitos
    - currency GTQ|USD
    - Rechaza si wantsReceipt y NIT > 32 chars
17. [Backend]  Si wantsReceipt y hay NIT: re-valida NIT contra ifacere (server-side)
    Esto evita que el cliente envíe nombre/dirección fiscal falsos.
    El nitName y nitAddress son sobreescritos con los valores de la API.
18. [Backend]  VerifyRecaptchaToken() — POST a Google reCAPTCHA Enterprise
    Verifica: token válido, action=="donation", score >= minScore
19. [Backend]  GetAllowedAccountById() — verifica que la cuenta esté en el whitelist
20. [Backend]  ReserveIdempotencySlot() — sp_getapplock + busca IDEM={key} (última hora)
    Autenticado: filtra por personAliasId; anónimo: por AuthorizedPersonAliasId NULL
    Si existe Approved → retorna recuperado sin cobrar
    Si existe Pending  → retorna "en proceso, no reintente" sin cobrar
    Si no existe → inserta fila Pending (token durable) y commitea
21. [Backend]  BuildGatewaySettings() — resuelve credenciales según modo live/test
22. [Backend]  ChargeWithCybersource()
    a. BuildPaymentPayload() — arma el JSON con amount, currency, BillTo, card, DeviceInfo
    b. BuildDigest() — SHA-256 del body en Base64
    c. BuildStringToSign() — concatena headers separados por \n
    d. SignString() — HMAC-SHA256 con el shared secret
    e. POST a /pts/v2/payments con headers de firma
    f. Si HTTP 401 → reintenta con perfil de firma alternativo (hasta 3 perfiles)
    g. Parsea respuesta: status, processorResponseCode, approvalCode, reconciliationId
23. [Backend]  Según resultado del cobro:
    - Aprobado → FinalizeApprovedTransaction(): promueve la fila Pending a Approved,
      completa ForeignKey con claves CYBS, la asigna a un FinancialBatch separado
      por moneda, actualiza ControlAmount, guarda email/NIT de la persona.
    - Rechazo definitivo → DeletePendingTransaction() (permite reintento).
    - Ambiguo (timeout/5xx) o finalización fallida → conserva la fila Pending
      (un reintento con el mismo idemKey NO recobra).
24. [Backend]  Encola DonationWorkflow (siempre, si configurado)
    Encola ReceiptWorkflow (solo si wantsReceipt + nitName validado)
25. [Backend]  Devuelve ProcessPaymentResponseBag {success, message, códigos, history}
26. [Frontend] Si success → modal de éxito + historial actualizado
    Si error → NotificationBox con mensaje localizado
```

---

## 6. Integración con Cybersource

El bloque se comunica directamente con la **REST API de Cybersource** usando
autenticación HMAC-SHA256. No usa ningún SDK oficial de Cybersource; la
integración está implementada desde cero.

### Endpoint

```
POST https://{host}/pts/v2/payments
```

- **Host test:** `apitest.cybersource.com`
- **Host live:** `api.cybersource.com`
- **Timeout:** 30,000 ms (configurable)

### Autenticación

Cybersource usa firma HTTP (`HTTP Signatures`):

1. Se calcula `Digest: SHA-256={base64(sha256(body))}`.
2. Se construye un string con los valores de los headers `host`, `v-c-date`,
   `request-target`, `digest` y `v-c-merchant-id`, separados por `\n`.
3. Se firma ese string con `HMACSHA256(sharedSecretBase64, stringToSign)`.
4. Se incluye el resultado en el header `Signature`.

El bloque intenta hasta **3 perfiles de firma** en orden, porque Cybersource
ha variado a lo largo del tiempo si acepta `date` o `v-c-date`, y
`(request-target)` o `request-target`. Si el primer intento devuelve HTTP 401,
se reintenta con el siguiente perfil.

| Perfil | dateHeaderName | requestTargetHeaderName |
|---|---|---|
| 1 | `v-c-date` | `request-target` |
| 2 | `v-c-date` | `(request-target)` |
| 3 (legacy) | `date` | `(request-target)` |

### Payload del cobro

```json
{
  "clientReferenceInformation": { "code": "ROCK-{12hex}" },
  "processingInformation": { "capture": true },
  "orderInformation": {
    "amountDetails": { "totalAmount": "100.00", "currency": "GTQ" },
    "billTo": {
      "firstName": "...", "lastName": "...",
      "address1": "...", "locality": "Guatemala City",
      "administrativeArea": "GU", "postalCode": "01010",
      "country": "GT", "email": "...", "phoneNumber": "..."
    }
  },
  "paymentInformation": {
    "card": {
      "number": "...", "expirationMonth": "05",
      "expirationYear": "2030", "securityCode": "..."
    }
  },
  "deviceInformation": {
    "ipAddress": "...", "userAgent": "...",
    "fingerprintSessionId": "..."
  }
}
```

### Marcas de tarjeta soportadas

El procesador `vdcguatemala` (Visanet Guatemala) solo procesa **Visa** y
**Mastercard**. American Express (BIN 34xx/37xx) se rechaza en frontend
(mensaje inline al teclear) y en backend (antes de llegar a Cybersource).

---

## 7. Anti-fraude — las 4 features

Estas cuatro features fueron implementadas en el commit `ab22c5b862
(security review + 4 features anti-fraude + reCAPTCHA + normalización)`.

### Feature 1: reCAPTCHA Enterprise

- El frontend carga el script JS de Google reCAPTCHA Enterprise bajo demanda
  en la primera interacción del usuario.
- Inmediatamente antes del submit, ejecuta
  `grecaptcha.enterprise.execute(siteKey, {action:"donation"})` para obtener
  un token.
- El backend (`VerifyRecaptchaToken`) hace un POST a
  `recaptchaenterprise.googleapis.com/v1/projects/{projectId}/assessments`
  con el token, y verifica:
  - `tokenProperties.valid === true`
  - `tokenProperties.action === "donation"` (comparación en tiempo constante)
  - `riskAnalysis.score >= minScore` (por defecto 0.5)
- Si cualquier verificación falla, la donación se rechaza antes del cobro.
- Si los tres atributos (`SiteKey`, `ApiKey`, `ProjectId`) no están
  configurados, la verificación se omite (modo desarrollo).

### Feature 2: Device Fingerprint (Cybersource Decision Manager)

- Al cargar el formulario, el frontend genera un `sessionId` de 32 caracteres
  hexadecimales usando `crypto.getRandomValues`.
- En la primera interacción del usuario (o 3 segundos después del `load`),
  monta un `<iframe>` oculto apuntando a
  `https://h.online-metrix.net/fp/tags?org_id={orgId}&session_id={merchantId}{sessionId}`.
  Este iframe ejecuta el script de ThreatMetrix que recolecta atributos del
  dispositivo (canvas fingerprint, plugins, geo, timing, etc.).
- El mismo `sessionId` se envía al backend como `deviceFingerprintSessionId`.
- El backend lo incluye en el payload de Cybersource como
  `deviceInformation.fingerprintSessionId`, junto con la IP y el User-Agent.
- Cybersource Decision Manager cruza ese fingerprint con su base de datos de
  fraude para calcular un risk score que puede bloquear transacciones de alto
  riesgo.

### Feature 3: AVS — Address Verification System

- `BuildBillTo` construye los datos de facturación que Cybersource usa para
  AVS (comparación de dirección con el banco emisor).
- Para usuarios logueados, usa los datos reales de la cuenta en Rock
  (mailing o home location).
- Para usuarios anónimos, usa el nombre del tarjetahabiente del formulario.
- Normalización defensiva para reducir flags de Decision Manager:
  - Estado `GT` o `GUATEMALA` se corrige a `GU` (código de estado guatemalteco
    correcto; `GT` es el código de país, no de estado).
  - Ciudad `Guatemala` o `Ciudad de Guatemala` se normaliza a `Guatemala City`
    (que coincide con lo que devuelve el geo-IP de Cybersource para IPs GT).
  - Si no hay dirección, usa la dirección de la organización como fallback:
    `19 Avenida 16-02, Zona 10`, ciudad `Guatemala City`, estado `GU`,
    código postal `01010`, país `GT`.
- El objetivo es evitar los flags `COR-BA` y `MM-IPBC` de Decision Manager y
  mejorar la tasa de aprobación.

### Feature 4: Idempotencia transaccional (reforzada 2026-06-21)

- El frontend genera una `idemKey` única por sesión de pago:
  `Date.now().toString(36) + "-" + 8 bytes hex aleatorios` (≈ 25 chars).
- La clave es **obligatoria** y se valida con regex `^[a-zA-Z0-9_-]{1,32}$`.
  El cap de 32 coincide con el truncado de `SanitizeForeignKeyValue`; antes
  admitía 64 y un key largo rompía silenciosamente el lookup `IDEM=` (#6).
- **Patrón de fila `Pending` durable (#2, #3).** El cobro es irreversible, así
  que la transacción se reserva en BD **antes** de llamar a Cybersource:
  1. `ReserveIdempotencySlot` abre una transacción de BD, adquiere
     `sp_getapplock` (cross-node, #4) y busca una transacción previa del mismo
     `idemKey` en la última hora.
     - **Autenticado:** filtra por `AuthorizedPersonAliasId == personAliasId`.
     - **Anónimo:** filtra por `AuthorizedPersonAliasId == null`. Esto da
       idempotencia a donantes anónimos, que antes **no la tenían** (#2).
     - Si existe `Approved` → retorna resultado recuperado sin cobrar.
     - Si existe `Pending` → retorna "en proceso, no reintente" sin cobrar.
     - Si no existe → inserta una fila `Status="Pending"` y commitea. Esta fila
       es el token durable de idempotencia.
  2. `ChargeWithCybersource` (fuera de la transacción de BD).
  3. Según el resultado:
     - **Aprobado** → `FinalizeApprovedTransaction` promueve `Pending` →
       `Approved` (batch, atributos, workflows).
     - **Rechazo definitivo** (4xx con decline) → `DeletePendingTransaction`
       borra la reserva para permitir reintento.
     - **Resultado ambiguo** (`ambiguous`: excepción/timeout/5xx) → se **conserva**
       la fila `Pending`; un reintento con el mismo `idemKey` la encuentra y
       **no recobra**. Cierra el hueco "cobrado pero no guardado" (#3).
     - **Aprobado pero falla la finalización** → también se conserva `Pending`.
- **Locks de concurrencia:**
  - In-process: `ConcurrentDictionary<string,object>` por `cybs-idem:{idemKey}`
    (rápido, single-node).
  - Cross-node: `sp_getapplock @LockOwner='Transaction'` scoped al `idemKey`,
    auto-liberado en commit/rollback (best-effort; si no está disponible, el
    lock in-process sigue cubriendo single-node).
- La clave se rota en el frontend tras un cobro exitoso o un error reintentable
  (CVV incorrecto, fondos insuficientes, etc.). Para errores de red/estados
  desconocidos se conserva, para que el reintento actúe idempotentemente.
- **Residual conocido:** una fila `Pending` que quede huérfana (proceso muere
  entre cobro y finalización) permanece en BD; es visible en el historial como
  "Pendiente" y puede limpiarse manualmente. Es preferible a un doble cobro.

### Seguridad adicional (no en los 4 features principales)

- **Rate limit por IP:** máximo 5 intentos de cobro por IP en 5 minutos, y
  10 validaciones de NIT por IP por minuto. Implementado en memoria con
  `ConcurrentDictionary` (best-effort single-node). Desde 2026-06-21 (#8) hace
  una **purga oportunista** (`MaybeSweepRateLimitBuckets`): cada ≤10 min un solo
  hilo elimina entradas más viejas que 2h (> la ventana máxima de 1h), para que
  el diccionario no crezca sin límite. Las entradas activas nunca se purgan
  porque su `WindowStartUtc` se renueva en cada ventana.
- **Re-validación server-side del NIT:** aunque el cliente ya consultó el NIT,
  el backend lo vuelve a consultar al procesar el pago para evitar que el
  cliente envíe un `nitName` o `nitAddress` falsos en el recibo fiscal.
- **SSRF protection en NIT API:** la URL de la API de NIT debe ser HTTPS, no
  puede ser localhost ni IPs privadas RFC1918, y debe estar en una whitelist
  hardcodeada (`apiv2.ifacere-fel.com`).
- **HTTPS forzado en Cybersource (#7):** `ChargeWithCybersource` rechaza con
  error `CONFIG` cualquier Base URL que no sea `https://`, para que el PAN
  jamás viaje en claro aunque un admin configure mal el atributo.
- **Enmascaramiento de PANs en logs:** `MaskPotentialPan` reemplaza secuencias
  de 13-19 dígitos en cualquier texto de log con `***XXXX`.
- **Comparación en tiempo constante:** la verificación del action de reCAPTCHA
  usa `ConstantTimeEquals` para evitar timing attacks.
- **TLS 1.2/1.3 explícito:** `BuildSecureHttpClient` fuerza TLS 1.2 (y 1.3
  si el runtime lo soporta) sin mutar `ServicePointManager`.
- **Secrets encriptados:** los campos `SharedSecretBase64`, `NitApiBearerToken`
  y `RecaptchaApiKey` se almacenan con `EncryptedTextField` y se desencriptan
  en runtime con `Encryption.DecryptString`.

---

## 8. Manejo de múltiples monedas

El módulo soporta donaciones en **GTQ (Quetzal guatemalteco)** y **USD
(Dólar estadounidense)** de forma nativa, aprovechando el campo
`ForeignCurrencyCodeValueId` de Rock RMS.

### En el formulario (frontend)

- El selector de moneda (GTQ / USD) cambia el símbolo del input hero.
- El monto se procesa siempre en la moneda seleccionada.
- Al cambiar de moneda, el monto se resetea a cero para evitar confusión.

### En el cobro (backend)

- `NormalizeCurrency` convierte el código a mayúsculas y valida que sea
  `GTQ` o `USD`. Cualquier otro valor es rechazado.
- El payload enviado a Cybersource incluye `currency` con el código ISO 4217
  correcto. Cybersource procesa la transacción en esa moneda.

### En Rock Finance

- `ResolveForeignCurrencyCodeValueId` busca el `DefinedValue` de moneda
  en el tipo `FINANCIAL_CURRENCY_CODE`. Si la moneda coincide con la moneda
  de la organización (configurada en Rock), devuelve `null` (comportamiento
  estándar de Rock). Si es diferente (ej. GTQ en una org configurada en USD),
  devuelve el ID del DefinedValue correspondiente.
- `FinancialTransaction.ForeignCurrencyCodeValueId` queda poblado con ese ID.
- `FinancialTransactionDetail.ForeignCurrencyAmount` recibe el monto en la
  moneda extranjera.
- La moneda también se almacena en `ForeignKey` como `CUR={GTQ|USD}`.
- **Batch separado por moneda (#5, 2026-06-21):** el nombre del batch incluye
  la moneda — `"{BatchNamePrefix} (GTQ)"` / `"{BatchNamePrefix} (USD)"` — para
  que el `ControlAmount` nunca sume GTQ y USD en un mismo total (antes lo hacía,
  dejando el total sin sentido). La conversión FX real a moneda de la
  organización requiere una fuente de tasa y queda fuera de alcance (como #1).

### En el Dashboard

- `DonationDashboard` agrupa las monedas disponibles consultando los
  `ForeignCurrencyCodeValueId` distintos en `FinancialTransaction`.
- La moneda de la organización aparece siempre como "Org. (USD)" o similar.
- El footer del dashboard muestra totales separados por moneda.
- La exportación Excel incluye la columna `Moneda` y el total formateado.

---

## 9. reCAPTCHA Enterprise

### Configuración (3 atributos del bloque)

| Atributo | Descripción |
|---|---|
| `RecaptchaSiteKey` | Site key pública de Google reCAPTCHA Enterprise. Se pasa al frontend en `InitBag`. |
| `RecaptchaApiKey` | API key de Google Cloud (encriptado). Solo se usa en el backend para el POST de validación. |
| `RecaptchaProjectId` | ID del proyecto de Google Cloud donde se creó la clave. |
| `RecaptchaMinScore` | Entero 0-100 (equivalente a 0.0-1.0 en reCAPTCHA). Por defecto 50. |

Si cualquiera de los tres primeros está vacío, la verificación se omite
completamente (útil en desarrollo o staging sin acceso a Google Cloud).

### Flujo de implementación

1. **Frontend — carga del script:** `ensureRecaptchaLoaded()` inserta
   `<script async src="https://www.google.com/recaptcha/enterprise.js?render={siteKey}">`
   en el `<head>` una sola vez. La promesa se cachea para evitar doble carga.
2. **Frontend — pre-carga bajo demanda:** el script se pre-carga en la primera
   interacción del usuario o 3 segundos después del `load`, para que esté
   listo cuando el usuario haga submit.
3. **Frontend — generación del token:** justo antes de enviar el pago,
   `getRecaptchaToken()` llama a `grecaptcha.enterprise.execute(siteKey,
   {action:"donation"})`. El token tiene vida útil corta (minutos).
4. **Backend — validación:** `VerifyRecaptchaToken` hace un POST a
   `https://recaptchaenterprise.googleapis.com/v1/projects/{projectId}/assessments?key={apiKey}`
   con el body `{event:{token, siteKey, expectedAction:"donation"}}`.
5. **Backend — decisión:** verifica `tokenProperties.valid`, compara
   `tokenProperties.action` con `"donation"` en tiempo constante, y compara
   `riskAnalysis.score` con `minScore/100`.
6. Si la verificación falla por cualquier razón (red, score bajo, action
   inesperada), se devuelve un error genérico al usuario y se registra la
   excepción en `ExceptionLogService`.

---

## 10. Integración NIT / SAT Guatemala

El bloque se conecta a la API de **ifacere-fel.com** para validar el NIT del
donante y obtener su nombre y dirección fiscal, que son necesarios para emitir
un recibo de donación conforme al régimen de Factura Electrónica en Línea (FEL)
de Guatemala.

### Flujo

1. El usuario activa el toggle "¿Desea recibo?" e ingresa su NIT.
2. El frontend llama a `ValidateNitInfo(nit)`.
3. El backend sanea el NIT (solo alfanumérico, max 32 chars).
4. Aplica rate limit: 10 peticiones/minuto por IP.
5. Valida la URL configurada: debe ser HTTPS, no loopback/privada, y el host
   debe estar en la whitelist (`apiv2.ifacere-fel.com`).
6. Hace `POST` con `Content-Type: application/xml` y `Authorization: Bearer {token}`:
   ```xml
   <RetornaDatosClienteRequest>
     <nit>12345678</nit>
   </RetornaDatosClienteRequest>
   ```
7. Parsea la respuesta con regex (no con un parser XML completo) para extraer
   `<nombre>` y `<direccion>`. Los valores se sanean con `StripUnsafeText`
   (elimina tags HTML, entidades y caracteres de control).
8. Devuelve `{name, address}` al frontend, que los muestra en campos read-only.
9. Al procesar el pago, el backend **vuelve a consultar la API** para sobrescribir
   el `nitName` y `nitAddress` con los valores de la API (no del cliente).
10. El NIT se guarda en el atributo configurado (`PersonNitAttributeKey`) del
    perfil de la persona en Rock, como lista de NITs separados por coma.

---

## 11. Persistencia en Rock Finance

> **Nota 2026-06-21:** desde el endurecimiento de idempotencia, la persistencia
> se hace en dos fases: `BuildPendingTransaction` (reserva la fila como
> `Status="Pending"` antes del cobro) y `FinalizeApprovedTransaction` (la
> promueve a `Approved` y la asigna al batch tras un cobro exitoso). El método
> monolítico `SaveFinancialTransaction` fue eliminado. Los registros creados
> son los mismos:

### `FinancialPaymentDetail`

| Campo | Valor |
|---|---|
| `AccountNumberMasked` | `****1234` (últimos 4 dígitos) |
| `CurrencyTypeValueId` | `CURRENCY_TYPE_CREDIT_CARD` |
| `CreditCardTypeValueId` | Detectado por IIN (Visa, Mastercard, etc.) |
| `NameOnCard` | Nombre del tarjetahabiente del formulario |
| `ExpirationMonth` / `ExpirationYear` | Del formulario (año normalizado a 4 dígitos) |

### `FinancialTransaction`

| Campo | Valor |
|---|---|
| `AuthorizedPersonAliasId` | Si logueado, alias principal de la persona |
| `TransactionDateTime` | `RockDateTime.Now` |
| `FinancialGatewayId` | Del atributo `FinancialGateway` |
| `TransactionTypeValueId` | Del atributo `TransactionType` (por defecto `CONTRIBUTION`) |
| `SourceTypeValueId` | Del atributo `FinancialSourceType` (por defecto `WEBSITE`) |
| `TransactionCode` | `referenceNumber` de Cybersource (el ID de la transacción) |
| `Summary` | Texto legible: `Note \| Email: ... \| NIT: ... \| NIT Nombre: ... \| Ref: ... \| Auth: ... \| ...` |
| `Status` | `"Approved"` |
| `ForeignCurrencyCodeValueId` | DefinedValue de la moneda si es diferente a la de la org |
| `ForeignKey` | `CYBS\|IDEM={k}\|MODE={live\|test}\|CUR={GTQ\|USD}\|RC={code}\|REF={ref}\|AUDIT={audit}\|AUTH={auth}` |

### `FinancialTransactionDetail`

| Campo | Valor |
|---|---|
| `AccountId` | De la cuenta seleccionada en el formulario |
| `Amount` | Monto de la donación |
| `ForeignCurrencyAmount` | Igual al Amount si hay moneda extranjera |

### `FinancialBatch`

Se busca o crea un batch activo por la combinación de:
- Prefijo del nombre **con la moneda** (`"{BatchNamePrefix} (GTQ)"` /
  `"{BatchNamePrefix} (USD)"`, por defecto base `"Cybersource Online Giving"`)
- Tipo de moneda (`CREDIT_CARD`)
- Tipo de tarjeta (Visa, Mastercard, etc.)
- Fecha de la transacción

Se incrementa `ControlAmount` del batch con el monto de la transacción.

> Al separar por moneda, los batches con el nombre antiguo (sin sufijo) dejan
> de recibir entradas nuevas; las donaciones nuevas van a los batches
> `(GTQ)`/`(USD)`. Esto es intencional para que cada total de control sea
> mono-moneda.

### Formato del `ForeignKey`

```
CYBS|IDEM={idemKey}|MODE={live|test}|CUR={GTQ|USD}|RC={responseCode}|REF={referenceNumber}|AUDIT={auditNumber}|AUTH={authorizationNumber}
```

Este campo (max 100 chars) es la fuente de verdad para:
- Idempotencia (búsqueda por `IDEM=`)
- Historial (filtro `LIKE 'CYBS|%'`)
- Modo de la transacción (`MODE=`)
- Moneda (`CUR=`)
- Conciliación bancaria (`REF=`, `AUDIT=`, `AUTH=`)

---

## 12. Workflows y email de confirmación

### Workflows configurables

El bloque puede lanzar hasta dos workflows al completarse una donación:

**`DonationWorkflow`:** se lanza siempre tras una donación exitosa. La entidad
del workflow es el `FinancialTransaction` creado. Se pasan los siguientes
atributos de workflow:

| Atributo | Valor |
|---|---|
| `DonorName` | Nombre del donante (nitName si hay, sino FullName de la persona) |
| `DonationType` | Nombre del fondo/cuenta (`account.PublicName` o `Name`) |
| `Amount` | Monto formateado con 2 decimales |
| `Currency` | GTQ o USD |
| `Nit` | NIT (solo si wantsReceipt) |
| `NitName` | Nombre fiscal validado por la API |
| `NitAddress` | Dirección fiscal validada por la API |
| `DonorEmail` | Email del donante |
| `RockTransactionId` | ID del `FinancialTransaction` creado |
| `ExternalId` | `ROCK-{transactionId}` |
| `ReferenceNumber` | ID de la transacción en Cybersource |
| `AuthorizationNumber` | Código de autorización del banco emisor |
| `ResponseCode` | Código de respuesta del procesador |
| `Mode` | `live` o `test` |

**`ReceiptWorkflow`:** se lanza solo si `wantsReceipt === true` y el NIT fue
validado exitosamente (nitName no vacío). Recibe los mismos atributos que
`DonationWorkflow`. Este workflow es el encargado de generar y enviar el
recibo fiscal (FEL) al donante.

### Template del email de confirmación

El archivo `EmailTemplates/confirmacion-donacion.html` es una plantilla **Lava**
(motor de templates de Rock) diseñada para usarse como cuerpo del email que se
envía al donante desde el workflow.

**Variables disponibles:**

| Variable | Fuente |
|---|---|
| `{{ currency }}` | Atributo de workflow `Currency` |
| `{{ amount }}` | Atributo de workflow `Amount` |
| `{{ symbol }}` | Calculado: `Q` si GTQ, `$` si USD |
| `{{ nit }}` | Atributo de workflow `Nit` |
| `{{ nitName }}` | Atributo de workflow `NitName` |
| `{{ nitAddr }}` | Atributo de workflow `NitAddress` |
| `{{ mode }}` | Atributo de workflow `Mode` (muestra badge "Test" si no es live) |
| `{{ donorName }}` | Atributo de workflow `DonorName` |
| `{{ donorEmail }}` | Atributo de workflow `DonorEmail` |
| `{{ donationType }}` | Atributo de workflow `DonationType` (fila "Tipo de donación", condicional) |

**Cuándo se envía:** el template en sí no se envía directamente; es el
workflow el que lo usa como cuerpo de un paso `Send Email`. El workflow de
donación lo envía siempre; el workflow de recibo lo envía solo si el donante
solicitó recibo y tiene NIT validado.

**Diseño (rediseño 2026-07-08):** header `#272b31` con logo VidaReal, check
verde y monto grande; card blanca con detalles de transacción (incluye la fila
condicional "Tipo de donación"); sección de facturación condicional (solo si
hay NIT); caja de notas; footer `#272b31` con logo e íconos sociales
(Instagram/Facebook/YouTube vía Global Attributes). Fuente Montserrat.
Diseñado para clientes de email sin CSS moderno (usa tablas HTML).

> **Requiere config en el workflow:** para que aparezca la fila "Tipo de
> donación" hay que agregar el atributo de workflow `DonationType` y pegar
> este HTML en el paso `Send Email`. Si el atributo no existe, la fila
> simplemente no se renderiza (es condicional).

---

## 13. Historial de commits relevantes

| Commit | Descripción |
|---|---|
| `ea4ed1b56e` | Módulos iniciales y setup del proyecto |
| `4e2b65b1e2` | Workflow y primer Donation Entry |
| `ed90390516` | Cambios en flujo de donaciones y HTML |
| `67a5c34e4e` | Cambios para manejar Monedas (soporte GTQ/USD) |
| `a8449ef3a5` | Dashboard de donaciones |
| `53feb44d2f` | Cambios Form En Divisa (normalización de monedas en el formulario) |
| `ab22c5b862` | **security review + 4 features anti-fraude + reCAPTCHA + normalización** |
| `4ab7f62ec5` | Version 1.0 — Donaciones y Documentación |
| `b4cc06deea` | Update logos de tarjetas |
| `2b2938d8b4` | Cambios en dashboard, footer, header y donation form |
| `1170b62cef` | Fix velocidad de carga |
| `45fdf0b5ff` | Live final de Donacion |
| `9f55e261e3` | Cambios en sitio y estilos |
| `7b119b9fc4` | Up to date DAR |
| `4f80ff56b0` | BUGS y WA |
| _(sin commit)_ | **Endurecimiento security review #2–#8**: fila `Pending` durable (idempotencia anónima + hueco "cobrado no guardado"), `sp_getapplock` cross-node, batch por moneda, `idemKey` obligatorio cap 32, HTTPS forzado en Cybersource, purga del rate limiter |

---

> **Mantenimiento:** actualizar este archivo cuando se agreguen nuevas features,
> se modifiquen las monedas soportadas, se cambien las integraciones externas
> o se actualice la versión de la API de Cybersource.
