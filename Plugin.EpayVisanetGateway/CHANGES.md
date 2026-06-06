# ePay Visanet Gateway — Contexto para Claude Code

## Qué es ePay Visanet y para qué mercado

ePay Visanet es la pasarela de pagos electronica del Banco Industrial y Credomatic para Guatemala y Centroamerica. Opera sobre la red Visanet (ahora Cybersource/Visa). A diferencia de la API REST de Cybersource global, ePay Visanet expone un servicio **SOAP/XML** tradicional bajo el namespace `http://general_computing.com/paymentgw/types`.

VidaReal lo usa para cobros en quetzales (GTQ) en eventos con registro, particularmente con soporte para **pagos en cuotas (VisaCuotas)** — funcionalidad propia del mercado guatemalteco que aplica un recargo porcentual sobre el monto base.

Este plugin fue creado completamente desde cero para VidaReal — no existe en el Rock original de SparkDevNetwork.

---

## Arquitectura del plugin

```
Plugin.EpayVisanetGateway/
├── EpayVisanetGateway/
│   ├── EpayVisanetGateway.cs               ← Clase principal (GatewayComponent) + todas las clases internas
│   └── EpayVisanetGatewayTokenController.cs ← REST endpoint de tokenizacion
├── ObsidianSource/
│   └── epayVisanetGatewayControl.obs        ← Componente Vue (Obsidian UI) con soporte de cuotas
├── Deploy/
│   └── Plugins/EpayVisanetGateway/
│       ├── Obsidian/epayVisanetGatewayControl.obs.js     ← JS compilado listo para deploy
│       └── ObsidianSource/epayVisanetGatewayControl.obs  ← Copia del fuente
└── EpayVisanetGateway.sln
```

**Nota importante:** A diferencia del plugin Cybersource, el directorio `Deploy/` de ePay NO contiene una `EpayVisanetGateway.dll` precompilada en la raiz. Solo contiene los assets Obsidian. La DLL compilada esta en `EpayVisanetGateway/bin/Debug/net472/EpayVisanetGateway.dll`. Se requiere compilar desde el `.sln` para obtener la DLL de Release.

**Codigo fuente disponible:** Si. Todo en un unico archivo `EpayVisanetGateway.cs` (~1000 lineas).

---

## Flujo de pago y diferencias con Cybersource

El modelo es identico en el front-end (inline/tokenizacion via RockCache), pero el back-end es completamente diferente: en vez de REST+JSON usa SOAP+XML.

```
1. El usuario ingresa datos de tarjeta en el control Obsidian (Vue).
   - Incluye opcion de cuotas (VisaCuotas) si esta habilitada.
   - Muestra recargo calculado en tiempo real.

2. Al hacer submit, el control Vue hace POST a:
   POST /api/EpayVisanetGateway/CreatePaymentToken
   Body: { gatewayGuid, cardNumber, expirationMonth, expirationYear,
           securityCode, nameOnCard, installmentCode }

3. El TokenController valida datos y los almacena en RockCache (TTL 15 min).
   Devuelve token: "epay-cache-{guid}"

4. Rock usa ese token como ReferencePaymentInfo.ReferenceNumber.

5. Al cobrar, EpayVisanetGateway.Charge() :
   a. Recupera datos de tarjeta del cache.
   b. Calcula recargo si hay cuotas: chargeAmount = base * (1 + surcharge%).
   c. Genera un auditNumber secuencial (prefijo "8", formato "8XXXXX").
   d. Construye un envelope SOAP y hace POST HTTP directo al endpoint ePay
      (sin biblioteca WCF — SOAP manual con XmlDocument).
   e. ePay responde con responseCode, authorizationNumber, referenceNumber.
   f. Si responseCode == "00" → aprobado. Cualquier otro → error.
   g. Si hay excepcion SOAP, se intenta automaticamente una reversa (messageType=0400).

6. Se crea FinancialTransaction con:
   - TransactionCode = referenceNumber de ePay
   - ForeignKey = token del cache
   - Status = "AUTHORIZED"
   - StatusMessage incluye responseCode, authorizationNumber, surcharge
   - FinancialTransactionDetail.Amount = monto total cobrado (con recargo)
   - FinancialTransactionDetail.FeeCoverageAmount = monto del recargo de cuotas
   - Atributos custom: EpayAuditNumber, EpayAuthorizationNumber
```

**Diferencia clave vs Cybersource:**
| Aspecto | Cybersource | ePay Visanet |
|---------|-------------|--------------|
| Protocolo | REST + JSON | SOAP + XML (manual) |
| Autenticacion | HMAC-SHA256 firmado | Usuario/password en el body SOAP |
| Reintentos | Exponential backoff automatico | Reversa manual en excepcion |
| Cuotas | No soportado | Soportado (VisaCuotas) |
| Moneda default | USD | GTQ |
| IPs requeridas | No | Si (shopperIP, merchantServerIP, paymentgwIP) |
| Numero de auditoria | No | Si (EpayAuditNumber, generado localmente) |

---

## Clases internas y su proposito

### `EpayVisanetGateway` (clase principal)
Hereda de `GatewayComponent` e implementa `IAutomatedGatewayComponent` e `IObsidianHostedGatewayComponent`.

| Metodo | Proposito |
|--------|-----------|
| `GetObsidianControlFileUrl()` | Ruta del JS compilado del control Vue |
| `GetObsidianControlSettings()` | Pasa config al control: gatewayGuid, tokenizeEndpoint, promptForNameOnCard, enableInstallments, installmentOptions |
| `Charge()` | Cobro: calcula recargo, llama SOAP, retorna `FinancialTransaction` con surcharge persistido |
| `Credit()` | Reembolso via messageType=0202 usando el EpayAuditNumber guardado en atributos |
| `TryGetChargeCardData()` | Extrae datos de tarjeta desde cache usando el token |
| `ValidateInlineCardData()` | Valida numero (12-19 digitos), CVV (3-4), vencimiento |
| `GetCfg()` | Lee y desencripta credenciales del gateway |
| `GetInstallmentSurcharge()` | Busca el porcentaje de recargo para un installmentCode dado |
| `GetEpayErrorMessage()` | Mapea codigos de respuesta ePay a mensajes en espanol (catalogo completo) |
| `GetClientIp()` | Obtiene IP del cliente via X-Forwarded-For o REMOTE_ADDR |
| `GetServerIp()` | Obtiene IP del servidor via DNS |
| `AutomatedCharge()` | **No implementado** |

### `EpayVisanetGatewayTokenController`
Endpoint `/api/EpayVisanetGateway/CreatePaymentToken` (AllowAnonymous).
Identico en estructura al de Cybersource, pero adicionalmente recibe `installmentCode` en el request y lo almacena en `InlineCardData`.

### `EpaySoapClient`
Cliente SOAP completamente manual (sin WCF ni bibliotecas SOAP externas):
- Construye el XML envelope con `StringBuilder`.
- Namespace: `http://general_computing.com/paymentgw/types`.
- SOAPAction header: `""` (cadena vacia — requerido por ePay).
- Parsea la respuesta XML con `XmlDocument` buscando `responseCode`, `authorizationNumber`, `referenceNumber`.
- Maneja `WebException` y parsea el body de error para extraer el responseCode cuando ePay devuelve fault HTTP.

### `EpayAuditNumberManager`
Generador de numeros de auditoria sequenciales en memoria:
- Prefijo `8` + 5 digitos (`8XXXXX`, rango 800001 a 899999).
- Thread-safe con `lock`.
- **Advertencia:** El contador se resetea en cada reinicio de la aplicacion. En produccion con multiple instancias (load balancer) podria haber colisiones de auditNumber.

### `InlinePaymentTokenStore`
Cache con region `EpayVisanetGateway`, prefijo de token `epay-cache-`. Funcionamiento identico al de Cybersource.

### `InstallmentOption`
Modelo de cuota: `{ months: int, code: string, surcharge: decimal }`.
Se deserializa desde el atributo JSON del gateway.

### `EpayCfg`
Configuracion en runtime: WsdlUrl, ServerIp, MerchantUser, MerchantPasswd, TerminalId, MerchantId, TimeoutSec.

### `EpaySoapResponse`
Resultado del SOAP: ResponseCode, AuthorizationNumber, ReferenceNumber, AuditNumber, ActionCode.

---

## Configuracion necesaria en Rock

Los atributos se configuran en `Admin > Financial Gateways > [ePay Visanet Gateway]`:

| Atributo | Descripcion |
|----------|-------------|
| Use Live Mode | Activa produccion. Default: false |
| Test/Live WSDL URL | URL del servicio SOAP (test: `epaytestvisanet.com.gt`, live: `epayvisanet.com.gt`) |
| Test/Live Server IP | IP del servidor ePay (incluida en el body SOAP como `paymentgwIP`) |
| Test/Live Merchant User | Usuario del comercio |
| Test/Live Merchant Password | Contrasena — almacenada encriptada |
| Test/Live Terminal ID | ID de terminal asignado por ePay |
| Test/Live Merchant ID | ID del comercio asignado por ePay |
| Timeout (seconds) | Timeout de la llamada SOAP (default: 60 segundos) |
| Prompt for Name On Card | Si el formulario pide nombre del titular |
| Enable Installments | Habilitar opcion de cuotas VisaCuotas |
| Installment Options (JSON) | Array JSON con opciones de cuotas: `[{"months":3,"code":"VC03","surcharge":5.0}]` |

---

## Cuotas VisaCuotas — detalle

Cuando `Enable Installments = true` y hay opciones configuradas:

1. El control Obsidian muestra un checkbox "Pago por cuotas".
2. Al activarlo, aparece un selector de opciones (por ejemplo: "3 cuotas (+5%)", "6 cuotas (+8%)").
3. El control muestra en tiempo real: monto base, monto de recargo en moneda, total a cobrar.
4. El `installmentCode` seleccionado (ej: "VC03") se envia al tokenizar.
5. En `Charge()`:
   - `chargeAmount = baseAmount * (1 + surcharge/100)`, redondeado a 2 decimales.
   - `surchargeAmount = chargeAmount - baseAmount`.
   - El `additionalData` del SOAP se llena con el `installmentCode`.
   - `paymentInfo.Amount` se sobreescribe con `chargeAmount` (total real cobrado).
   - `paymentInfo.FeeCoverageAmount` = `surchargeAmount`.

**Impacto en balance del evento:**
`RegistrationService.GetTotalPayments()` usa `Amount - FeeCoverageAmount` para calcular cuanto del pago aplica al balance del evento. Esto evita que el recargo de cuotas infle artificialmente el saldo pagado.

---

## Mapa de codigos de error ePay

El gateway incluye un catalogo completo de codigos de respuesta ePay:

| Codigo | Significado |
|--------|-------------|
| 00 | Aprobada |
| 01/02 | Refirase al emisor |
| 05 | No aceptada por banco |
| 13 | Monto invalido |
| 19 | Intente de nuevo |
| 31 | Tarjeta no soportada |
| 35/36/37/38 | Errores de anulacion |
| 41 | Tarjeta extraviada |
| 43 | Tarjeta robada |
| 51 | Sin fondos |
| 57/58 | Transaccion no permitida |
| 65 | Limite excedido |
| 89 | Terminal invalida |
| 91 | Emisor no disponible |
| 93 | Valide configuracion con el adquirente |
| 94 | Transaccion duplicada |
| 96 | Error del sistema |

---

## Funcionalidades implementadas vs no implementadas

| Funcionalidad | Estado |
|---------------|--------|
| Cobro unico (Charge) | Implementado |
| Reembolso (Credit) | Implementado |
| Cuotas VisaCuotas | Implementado |
| Formulario inline Obsidian | Implementado |
| Reversa automatica en excepcion SOAP | Implementado |
| Pagos programados (Scheduled) | NO implementado |
| AutomatedCharge / vault remoto | NO implementado |
| Cuentas guardadas (SavedAccount) | NO soportado |

---

## Referencia a EPAY_FLOW_SUMMARY.md

El archivo `C:\Repos\Rock18.1\EPAY_FLOW_SUMMARY.md` documenta los cambios del ciclo de desarrollo de cuotas. Puntos clave:

- **Namespace SOAP** fue corregido a `http://general_computing.com/paymentgw/types` (era incorrecto).
- **SOAPAction header** debe ser `""` (cadena vacia entre comillas dobles).
- **AuditNumber** usa prefijo `8` (`8XXXXX`).
- **Recargo** se persiste como `FeeCoverageAmount` en `FinancialTransactionDetail`.
- **`RegistrationService.GetTotalPayments()`** fue modificado en `Rock/Model/Event/Registration/RegistrationService.cs` para restar `FeeCoverageAmount` y que el recargo no cuente como pago del evento.
- **`RegistrationEntry.cs`** fue modificado para persistir `FeeCoverageAmount` al guardar transacciones.
- En Lava/confirmaciones: usar `payment.Amount - payment.FeeCoverageAmount` para el monto aplicado al evento.

---

## Estado del despliegue

Para desplegar:
1. Compilar el C# con `dotnet build .\EpayVisanetGateway.sln -c Release`.
2. Copiar `bin/Release/net472/EpayVisanetGateway.dll` al `bin/` de RockWeb.
3. Copiar `Deploy/Plugins/EpayVisanetGateway/` a `RockWeb/Plugins/EpayVisanetGateway/`.
4. Si se modifico `Rock.dll` o `Rock.Blocks.dll`, desplegar esas DLLs core tambien.
5. Limpiar cache de navegador (hard refresh) despues del deploy.

---

## Advertencias de seguridad

- **Datos de tarjeta en cache:** Igual que Cybersource, PAN y CVV viven en RockCache hasta 15 minutos. Riesgo si el cache es comprometido.
- **Credenciales SOAP en body:** Usuario y contrasena del comercio viajan en cada request SOAP (no hay token de sesion). El protocolo usa HTTPS, pero el modelo es menos robusto que HMAC firmado.
- **AuditNumber en memoria:** El contador `EpayAuditNumberManager` es estatico y en memoria. En entornos con multiples instancias o reinicios frecuentes puede haber numeros de auditoria duplicados. ePay podria rechazar con codigo `94` (duplicado).
- **IPs en SOAP:** El plugin envia `shopperIP` y `merchantServerIP` en el body SOAP. Con proxies o CDN, `shopperIP` puede ser la IP del proxy, no del cliente real. El campo `X-Forwarded-For` se usa pero solo toma la primera IP de la lista.
- **AllowAnonymous en endpoint de tokenizacion:** No hay rate-limiting en el plugin. Depende de WAF/IIS.
