# Cybersource Inline REST Gateway — Contexto para Claude Code

## Qué es Cybersource y por qué VidaReal lo usa

Cybersource es una pasarela de pago global de Visa Inc., ampliamente usada en el mercado estadounidense e internacional. VidaReal lo usa para procesar donaciones en dólares desde su módulo DAR (Donaciones y Recaudación) integrado con Rock RMS. La pasarela acepta tarjetas Visa, Mastercard, Amex y Discover, y se autentica con un esquema HMAC-SHA256 firmado, sin redirigir al usuario a una pantalla externa.

Este plugin fue creado completamente desde cero para VidaReal — no existe en el Rock original de SparkDevNetwork.

---

## Arquitectura del plugin

El plugin tiene tres capas que trabajan juntas:

```
Plugin.CybersourceInlineRestGateway/
├── CybersourceInlineRestGateway/
│   ├── CybersourceInlineRestGateway.cs          ← Clase principal (GatewayComponent)
│   └── CybersourceInlineRestGatewayTokenController.cs  ← REST endpoint de tokenizacion
├── ObsidianSource/
│   └── cybersourceInlineRestGatewayControl.obs  ← Componente Vue (Obsidian UI)
├── Deploy/
│   ├── CybersourceInlineRestGateway.dll         ← DLL compilado listo para despliegue
│   └── Plugins/CybersourceInlineRestGateway/
│       └── Obsidian/cybersourceInlineRestGatewayControl.obs.js  ← JS compilado
└── README.md
```

**Codigo fuente disponible:** Si. Toda la logica esta en `CybersourceInlineRestGateway.cs`.

**Deploy:** `Deploy/CybersourceInlineRestGateway.dll` es el binario listo para copiar al servidor. La DLL va en `bin/` de RockWeb y los assets Obsidian van en `RockWeb/Plugins/CybersourceInlineRestGateway/`.

---

## Flujo de pago: tokenizacion inline

El modelo es "inline" (tambien llamado "unhosted"): los datos de tarjeta nunca salen del browser directo a Cybersource. En cambio:

```
1. El usuario ingresa datos de tarjeta en el control Obsidian (Vue).
   - Validacion Luhn en cliente, deteccion de marca, formateo automatico.

2. Al hacer submit, el control Vue hace POST a:
   POST /api/CybersourceInlineRestGateway/CreatePaymentToken
   Body: { gatewayGuid, cardNumber, expirationMonth, expirationYear, securityCode, nameOnCard }

3. El TokenController recibe el request, valida el gateway y los datos de tarjeta,
   y almacena los datos encriptados en RockCache con TTL de 15 minutos.
   Devuelve un token corto: "csir-cache-{guid}"

4. El control Vue emite GatewayEmitStrings.Success con ese token.
   Rock usa ese token como ReferencePaymentInfo.ReferenceNumber.

5. Cuando Rock procesa el cobro, llama a EpayVisanetGateway.Charge().
   El metodo recupera los datos de tarjeta del cache usando el token,
   construye el payload JSON y hace POST directo a la API REST de Cybersource
   server-to-server (autenticado con HMAC-SHA256).

6. Cybersource responde con un ID de transaccion y status.
   El gateway marca el token como usado en cache y retorna FinancialTransaction.
```

**Nota de seguridad:** Los datos crudos de tarjeta pasan por el servidor de Rock (en memoria/cache) antes de ir a Cybersource. El CVV se almacena temporalmente en RockCache durante hasta 15 minutos. Esto cumple con el modelo inline de Cybersource pero implica que el servidor Rock tiene acceso breve a PAN y CVV. El token expira automaticamente y se elimina del cache al cobrar exitosamente.

---

## Clases internas y su proposito

### `CybersourceInlineRestGateway` (clase principal)
Hereda de `GatewayComponent` e implementa `IAutomatedGatewayComponent` e `IObsidianHostedGatewayComponent`.

| Metodo | Proposito |
|--------|-----------|
| `GetObsidianControlFileUrl()` | Devuelve la ruta del JS compilado del control Vue |
| `GetObsidianControlSettings()` | Pasa configuracion al control: gatewayGuid, tokenizeEndpoint, promptForNameOnCard |
| `Charge()` | Ejecuta el cobro: recupera datos del cache, llama a Cybersource REST, retorna `FinancialTransaction` |
| `Credit()` | Reembolso parcial o total via `POST /pts/v2/payments/{id}/refunds` |
| `TryGetChargeCardData()` | Extrae datos de tarjeta desde `CreditCardPaymentInfo` o `ReferencePaymentInfo` (token) |
| `ValidateInlineCardData()` | Valida numero de tarjeta (longitud 12-19), CVV (3-4 digitos), vencimiento |
| `GetCfg()` | Lee atributos del gateway (modo live/test, credenciales) y los desencripta |
| `IsPaymentTokenCharged()` | Verifica en DB si el token ya fue cobrado (anti-duplicado) |
| `FetchPaymentTokenTransaction()` | Recupera la transaccion existente asociada a un token |
| `AutomatedCharge()` | **No implementado** — devuelve error. Sin vault remoto no hay cobro recurrente. |

### `CybersourceInlineRestGatewayTokenController`
Web API controller expuesto en `/api/CybersourceInlineRestGateway/CreatePaymentToken`.
Endpoint publico (AllowAnonymous), valida que el GatewayGuid corresponda a este plugin y llama a `InlinePaymentTokenStore.CreateToken()`.

### `InlinePaymentTokenStore`
Cache interno en memoria (RockCache con region `CybersourceInlineRestGateway`).
- Tokens con prefijo `csir-cache-{guid}`, TTL 15 minutos.
- Al cobrar exitosamente: elimina datos de tarjeta del cache y registra el transactionCode como marcador de exito por 1 dia.
- Valida que el token pertenezca al mismo gateway (por GatewayGuid).

### `CybersourceRestClient`
Cliente HTTP que construye la firma HMAC-SHA256 requerida por la API REST de Cybersource.
- Firma incluye: `host`, `v-c-date`, `request-target`, `digest` (SHA-256 del body), `v-c-merchant-id`.
- Soporta reintentos con backoff exponencial (configurable: 2 reintentos por defecto).
- Usa idempotency key (hash SHA-256) para evitar cobros duplicados en retry.
- Timeout configurable (por defecto 30 segundos).

### `InlineCardData`
Modelo interno que viaja en el cache: cardNumber, expirationMonth, expirationYear, securityCode, nameOnCard, gatewayGuid, createdUtc.

---

## Configuracion necesaria en Rock

Los atributos se configuran en `Admin > Financial Gateways > [Cybersource Inline REST Gateway]`:

| Atributo | Descripcion |
|----------|-------------|
| Use Live Mode | Activa produccion. Por defecto: false (test). |
| Test/Live Base URL | URL base de la API (test: `https://apitest.cybersource.com`, live: `https://api.cybersource.com`) |
| Payments Path | Ruta del endpoint de pagos (default: `/pts/v2/payments`) |
| Test/Live Merchant Id | ID del comercio asignado por Cybersource |
| Test/Live Key Id | ID de la llave HMAC en el portal de Cybersource |
| Test/Live Shared Secret (Base64) | Secreto compartido HMAC en Base64 — almacenado encriptado con el cifrado de Rock |
| Timeout (ms) | Timeout HTTP en milisegundos (default: 30000) |
| Retry Count | Numero de reintentos en errores transientes (default: 2) |
| Prompt for Name On Card | Si el formulario solicita nombre del titular |

**Las credenciales de produccion nunca se hardcodean en el codigo.** Se leen desde los atributos del gateway, desencriptados en runtime con `Rock.Security.Encryption.DecryptString()`.

---

## Funcionalidades implementadas vs no implementadas

| Funcionalidad | Estado |
|---------------|--------|
| Cobro unico (Charge) con tarjeta | Implementado |
| Reembolso (Credit) | Implementado |
| Formulario inline Obsidian (Vue) | Implementado |
| Pagos programados (Scheduled) | NO implementado |
| AutomatedCharge / vault remoto | NO implementado |
| Cuentas guardadas (SavedAccount) | NO soportado |
| Deteccion de marca de tarjeta | Implementado (cliente) |
| Validacion Luhn | Implementado (cliente) |
| Idempotencia en cobros | Implementado (HMAC hash como key) |

---

## Relacion con el modulo DAR (Donaciones)

El plugin se integra con el bloque de donaciones de Rock (modulo Financial/Giving). El flujo tipico es:

1. El bloque de donacion DAR muestra el control Obsidian del gateway.
2. El donante ingresa sus datos de tarjeta.
3. Al confirmar, el control tokeniza y Rock llama a `Charge()`.
4. La transaccion queda en `FinancialTransaction` ligada al `FinancialGateway` configurado como Cybersource.

El plugin soporta unicamente pagos de una sola vez (`TRANSACTION_FREQUENCY_ONE_TIME`). No se pueden programar donaciones recurrentes automaticas con este gateway.

---

## Estado del despliegue

- `Deploy/CybersourceInlineRestGateway.dll` — binario listo para copiar.
- `Deploy/Plugins/CybersourceInlineRestGateway/Obsidian/cybersourceInlineRestGatewayControl.obs.js` — asset JS compilado.
- Fuente TypeScript/Vue en `ObsidianSource/cybersourceInlineRestGatewayControl.obs` — requiere compilacion Obsidian para regenerar el `.obs.js`.

Para recompilar el C#:
```powershell
dotnet build .\CybersourceInlineRestGateway.sln -c Release
```

---

## Advertencias de seguridad

- **Datos de tarjeta en cache:** El PAN (numero de tarjeta) y CVV se almacenan en RockCache durante hasta 15 minutos. Si el servidor RockCache es comprometido en esa ventana, los datos son vulnerables.
- **Endpoint publico (AllowAnonymous):** `/api/CybersourceInlineRestGateway/CreatePaymentToken` no requiere autenticacion. Cualquiera puede enviarlo datos de tarjeta. No hay rate-limiting en el plugin; depende de la configuracion de IIS/WAF.
- **El secreto HMAC** debe estar en Base64 puro sin saltos de linea extra. El cliente lo limpia de `\r\n` antes de usarlo.
- **No hay vault remoto:** Los datos de tarjeta no se guardan permanentemente. Cada pago requiere que el usuario ingrese la tarjeta de nuevo.
