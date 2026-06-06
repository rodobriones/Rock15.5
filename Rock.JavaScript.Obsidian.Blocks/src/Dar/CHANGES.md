# Módulo DAR — Frontend Obsidian — Historial de cambios y documentación

> **Rama:** `hotfix-18.1`
> **Última actualización:** 2026-06-04
>
> Este documento cubre los dos bloques Vue 3 (Obsidian) del módulo DAR,
> el flujo de UI del formulario de donaciones, las validaciones del cliente,
> el dashboard administrativo y el template de email de confirmación.
>
> Para la documentación técnica detallada del bloque principal, ver
> [`CybersourceDonationEntry.md`](CybersourceDonationEntry.md).

---

## Índice

1. [Propósito del módulo frontend DAR](#1-propósito-del-módulo-frontend-dar)
2. [Estructura de archivos](#2-estructura-de-archivos)
3. [CybersourceDonationEntry.obs — formulario de donación](#3-cybersourcedonationentryobs--formulario-de-donación)
   - 3.1 [Flujo de UI paso a paso](#31-flujo-de-ui-paso-a-paso)
   - 3.2 [Pasos y secciones del formulario](#32-pasos-y-secciones-del-formulario)
   - 3.3 [Validaciones del cliente](#33-validaciones-del-cliente)
   - 3.4 [Historial de pagos](#34-historial-de-pagos)
4. [DonationDashboard.obs — panel administrativo](#4-donationdashboardobs--panel-administrativo)
   - 4.1 [Métricas y columnas de la tabla](#41-métricas-y-columnas-de-la-tabla)
   - 4.2 [Filtros disponibles](#42-filtros-disponibles)
   - 4.3 [Agrupamiento y totales](#43-agrupamiento-y-totales)
   - 4.4 [Exportación Excel](#44-exportación-excel)
5. [Template de email de confirmación](#5-template-de-email-de-confirmación)
6. [Anti-fraude del cliente](#6-anti-fraude-del-cliente)
7. [Manejo de monedas en el frontend](#7-manejo-de-monedas-en-el-frontend)
8. [Decisiones de diseño](#8-decisiones-de-diseño)

---

## 1. Propósito del módulo frontend DAR

Los bloques frontend del módulo DAR son componentes **Vue 3 Single File
Components** (`.obs`) que se ejecutan dentro del framework **Obsidian** de
Rock RMS. Obsidian es la capa de bloque interactivo de Rock que permite
renderizar componentes Vue en páginas de RockWeb.

El módulo frontend tiene dos bloques:

- **`CybersourceDonationEntry.obs`:** el formulario público de donaciones
  que ve el feligrés. Es completamente nuevo, sin equivalente en Rock estándar.
- **`DonationDashboard.obs`:** el panel administrativo de consulta de
  donaciones, también nuevo.

Ambos se comunican exclusivamente con sus contrapartes C# (`CybersourceDonationEntry.cs`
y `DonationDashboard.cs`) a través de `invokeBlockAction`, el mecanismo de
llamadas AJAX de Obsidian.

---

## 2. Estructura de archivos

```
Rock.JavaScript.Obsidian.Blocks/
  src/Dar/
    CybersourceDonationEntry.obs   Formulario de donación (2888 líneas)
      <template>    líneas 1-445   UI completa
      <script>      líneas 447-1466 Lógica TypeScript
      <style>       líneas 1468-2888 CSS con prefijo .cy*
    CybersourceDonationEntry.md    Documentación técnica detallada
    DonationDashboard.obs          Dashboard (574 líneas)
      <template>    líneas 1-238   Filtros + tabla + footer
      <style>       líneas 219-238 Clases de grupo y detalle
      <script>      líneas 240-574 Lógica TypeScript
    tsconfig.json
```

---

## 3. CybersourceDonationEntry.obs — formulario de donación

### 3.1. Flujo de UI paso a paso

El formulario guía al usuario a través de los siguientes pasos. No son pasos
explícitos numerados; el formulario es una sola pantalla que se completa de
arriba a abajo:

```
┌─────────────────────────────────────────────────────┐
│  [Test]                           (badge modo test)  │
│                                                      │
│  Donación          Historial      (tabs superiores)  │
│                                                      │
│  Q      0.00                      (monto ATM hero)  │
│  [ GTQ ]  [ USD ]                (switch moneda)    │
├─────────────────────────────────────────────────────┤
│  Datos de donación                                   │
│  Tipo: [ Diezmos ▼ ]  Nota: ___________             │
├─────────────────────────────────────────────────────┤
│  Tarjeta                                             │
│  Número de tarjeta: ________________ [Visa][MC]     │
│  Vencimiento: MM/AA    CVV: ___                     │
│  Nombre del tarjetahabiente: ______________          │
├─────────────────────────────────────────────────────┤
│  ¿Desea su recibo de donación?    [  toggle  ]       │
│  (si activo:)                                        │
│  NIT: _________  [Validar NIT]                      │
│  Nombre / Razón Social: _______ (read-only)         │
│  Dirección: ___________________ (read-only)         │
├─────────────────────────────────────────────────────┤
│  Correo Electrónico *: _____________________         │
│                                                      │
│           [ DONAR ]                                  │
│  *No se guardará la información de su tarjeta.      │
└─────────────────────────────────────────────────────┘
```

Al hacer click en **DONAR**, si la validación es exitosa, aparece un
**modal de confirmación** con el resumen de la donación:

```
┌─────────────────────────────────────────────────────┐
│  Confirmar donación                                  │
│  Revisa los detalles antes de proceder.             │
│                                                      │
│  Tipo      Diezmos                                   │
│  Monto     Q250.00                                   │
│  Tarjeta   Visa 1234                                 │
│  Titular   NOMBRE APELLIDO                           │
│                                                      │
│  [ Cancelar ]    [ Confirmar donación ]             │
│  *No se guardará la información de su tarjeta.      │
└─────────────────────────────────────────────────────┘
```

Al confirmar:
1. Se genera el token de reCAPTCHA.
2. Se envía el pago al backend.
3. Aparece un **overlay de carga** ("Procesando donación / No cierre esta ventana").
4. Si exitoso: aparece un **overlay de éxito** con checkmark verde y el
   mensaje `"¡Gracias por su donación!"`.
5. Si fallido: se muestra el error en un `NotificationBox`.

### 3.2. Pasos y secciones del formulario

#### Monto (ATM-style input)

El input del monto funciona como un cajero automático: los dígitos empujan
desde la derecha. Escribir `5` con `12.34` visible resulta en `1.235`, mostrando
`12.35`. Esto se logra almacenando el valor en **centavos** como fuente de
verdad (`centValue` ref) y derivando el display de él.

El ancho del input se ajusta dinámicamente al número de dígitos para que no
haya espacio vacío alrededor del número.

El switch GTQ/USD cambia el símbolo y resetea el monto a cero.

#### Cuenta (dropdown personalizado)

Un dropdown de cuentas financieras construido desde cero (no un `<select>`
estándar) con soporte para:
- Check visual en la opción seleccionada.
- Cierre al hacer click fuera (directiva `v-click-outside`).
- Animación de apertura.

#### Tarjeta

- **Número de tarjeta:** auto-formatea con espacios cada 4 dígitos (o 4-6-5
  para Amex) mientras el usuario escribe. Detecta la marca por los primeros
  dígitos (BIN) y muestra los logos de Visa y Mastercard. El logo de la marca
  detectada se amplía; el otro se desenfoca.
- **Vencimiento:** inserta el `/` automáticamente al escribir el tercer dígito
  (formato MM/AA).
- **CVV:** solo acepta dígitos, 3 para Visa/Mastercard, 4 para Amex. El
  campo es tipo `password` para ocultar el valor.
- **Nombre:** texto libre, max 120 chars.

#### Recibo fiscal (NIT)

Toggle que muestra u oculta la sección de recibo. Al activarlo:
- Campo de NIT (solo caracteres alfanuméricos, máx 32).
- Botón "Validar NIT" que llama al backend.
- Si la validación es exitosa, aparecen campos read-only con nombre y dirección.
- Si el usuario modifica el NIT después de validar, los campos read-only se
  borran y el usuario debe volver a validar antes de poder enviar.
- El botón "Donar" queda deshabilitado mientras `wantsReceipt === true` y
  `nitName` está vacío.

#### Email

Campo de email requerido siempre (logueado o no). Si el usuario está logueado
y su cuenta tiene email, se pre-rellena. Es el correo al que llega la
confirmación y el recibo.

### 3.3. Validaciones del cliente

`validateForm()` ejecuta todas las siguientes validaciones antes de mostrar el
modal de confirmación. Los errores se muestran debajo de cada campo.

| Campo | Regla |
|---|---|
| `accountId` | Debe ser mayor a 0 (cuenta seleccionada) |
| `amount` | Debe ser mayor a 0 |
| `cardName` | Mínimo 3 caracteres |
| `cardNumber` | Luhn check + longitud 12-19 dígitos + no puede ser AmEx |
| `expDate` | Formato MM/AA, mes 1-12, no vencida |
| `cvv` | 3 dígitos (4 si Amex) — solo dígitos |
| `nit` | Si `wantsReceipt`, debe estar ingresado y `nitName` debe estar poblado |
| `donorEmail` | Regex `^[^\s@]+@[^\s@]+\.[^\s@]+$` |
| `note` | Máximo 250 caracteres |
| `currency` | Solo `GTQ` o `USD` |

#### Algoritmo de Luhn

```ts
function luhnCheck(pan: string): boolean {
    let sum = 0, alt = false;
    for (let i = pan.length - 1; i >= 0; i--) {
        let d = Number(pan[i]);
        if (alt) { d *= 2; if (d > 9) d -= 9; }
        sum += d;
        alt = !alt;
    }
    return /^\d{12,19}$/.test(pan) && sum % 10 === 0;
}
```

#### Bloqueo de American Express

El merchant `vdcguatemala` (Visanet Guatemala) no soporta AmEx. Para evitar
que el usuario llene todo el formulario y reciba un rechazo al cobrar:

1. `onCardNumberInput` detecta la marca en tiempo real. Si es AmEx (BIN `34`
   o `37`), muestra el mensaje `"El sistema no soporta American Express.
   Le sugerimos usar Visa o Mastercard."` inmediatamente bajo el campo.
2. `validateForm` lo verifica de nuevo al submit (defensa en profundidad).
3. El backend también lo rechaza antes de llegar a Cybersource.

Cuando el merchant habilite AmEx, eliminar los bloques en `onCardNumberInput`
y `validateForm` (frontend) y en `ValidatePaymentRequest` (backend).

#### Detección de marca por IIN

```ts
function detectCardBrand(pan: string): CardBrand {
    if (!pan) return "unknown";
    if (/^4/.test(pan)) return "visa";
    if (/^(5[1-5]|2(?:2[2-9]|[3-6]\d|7[01]|720))/.test(pan)) return "mastercard";
    if (/^3[47]/.test(pan)) return "amex";
    if (/^(6011|65|64[4-9])/.test(pan)) return "discover";
    return "unknown";
}
```

### 3.4. Historial de pagos

La tab "Historial" (solo visible para usuarios logueados) muestra las últimas
100 transacciones del usuario provenientes del módulo DAR (identificadas por
`ForeignKey LIKE 'CYBS|%'`).

Cada fila muestra:
- Fecha y hora formateada
- Estado (badge: "Aprobada" en verde, "Rechazada" en rojo)
- Monto con símbolo de moneda (Q o $)
- Nombre de la cuenta (fondo al que se donó)

Al completar una donación exitosa, el historial se actualiza automáticamente
con la nueva transacción sin necesidad de hacer click en "Actualizar".

---

## 4. DonationDashboard.obs — panel administrativo

`DonationDashboard.obs` es el panel administrativo para que el personal de
VidaReal pueda consultar y analizar las donaciones recibidas.

### 4.1. Métricas y columnas de la tabla

La tabla principal muestra las siguientes columnas por transacción:

| Columna | Descripción |
|---|---|
| Fecha | Fecha corta de la transacción |
| Persona | Nombre (NickName + LastName) del donante |
| NIT | Extraído del campo `Summary` de la transacción (formato `NIT: 12345678`) |
| Cuenta(s) | Nombre de la cuenta financiera (o cuentas separadas por coma) |
| Total | Monto total formateado con la moneda correspondiente |
| Moneda | Badge con el código de moneda (GTQ, USD, etc.) |
| Código | `TransactionCode` de Cybersource + últimos 4 dígitos de la tarjeta |

Al hacer click en una fila, si la transacción tiene más de un detalle (más de
una cuenta), se expanden las sub-filas con el monto de cada cuenta.

El footer muestra **totales acumulados por moneda** para todos los resultados
visibles.

### 4.2. Filtros disponibles

El panel de filtros está colapsable (click en el encabezado "Filtros").

| Filtro | Tipo | Descripción |
|---|---|---|
| Desde / Hasta | date | Rango de fechas de la transacción |
| NIT | text | Busca en el campo `Summary` (`NIT: {valor}`) |
| Cuentas | multi-select | Filtra por cuenta financiera (ctrl+click para múltiples) |
| Moneda | multi-select | Filtra por moneda (la opción "Org." cubre transacciones sin moneda extranjera) |

El rango de fechas se inicializa al mes en curso (primer día del mes hasta hoy).
El botón "Limpiar" resetea todos los filtros al estado inicial.

La búsqueda solo se ejecuta al presionar el botón "Buscar" (o Enter en el
campo NIT). No hay búsqueda automática al cambiar filtros.

### 4.3. Agrupamiento y totales

Los resultados se pueden agrupar mediante los botones "Agrupar por":

| Opción | Agrupación |
|---|---|
| Ninguno | Sin agrupamiento, tabla plana |
| Persona | Por nombre del donante |
| NIT | Por número de NIT extraído del Summary |
| Moneda | Por código de moneda |
| Cuenta | Por nombre de la cuenta financiera |

Cuando hay agrupamiento, cada grupo muestra:
- Un encabezado con la clave del grupo y el conteo de transacciones.
- Badges con los subtotales por moneda de ese grupo.

### 4.4. Exportación Excel

El botón "Exportar Excel" (visible si el atributo `AllowExport` está activo)
llama al Block Action `ExportToExcel` del backend, que genera el archivo con
**EPPlus** y lo devuelve como Base64.

El frontend decodifica el Base64 y usa `URL.createObjectURL` para disparar
la descarga. El nombre del archivo es
`donaciones_{yyyy-MM-dd_HHmm}.xlsx`.

La exportación usa los mismos filtros que la búsqueda actual. Las columnas
del Excel son: Fecha, Persona, NIT, Cuentas, Total, Monto (numérico),
Moneda, Codigo, Tarjeta, Resumen.

---

## 5. Template de email de confirmación

**Archivo:** `Rock.Blocks/Dar/EmailTemplates/confirmacion-donacion.html`

Es una plantilla Lava (motor de templates de Rock) diseñada para usarse como
cuerpo de un correo electrónico enviado desde un Workflow de Rock.

### Cuándo se envía

El template en sí no se envía directamente. Es el workflow configurado en el
atributo `DonationWorkflow` (y/o `ReceiptWorkflow`) del bloque el que usa
este template como cuerpo de un paso `Send Email`. Se envía al correo
`{{ donorEmail }}` de la persona que donó.

- El **workflow de donación** siempre se ejecuta tras una donación exitosa y
  envía este email.
- El **workflow de recibo** se ejecuta solo si el donante solicitó recibo y
  tiene NIT validado, y puede enviar un email adicional con el recibo FEL.

### Variables disponibles en Lava

| Variable | Atributo de workflow | Descripción |
|---|---|---|
| `{{ currency }}` | `Currency` | GTQ o USD |
| `{{ amount }}` | `Amount` | Monto con 2 decimales (ej: `250.00`) |
| `{{ symbol }}` | Calculado | `Q` si GTQ, `$` si USD |
| `{{ nit }}` | `Nit` | NIT del donante (vacío si no solicitó recibo) |
| `{{ nitName }}` | `NitName` | Nombre o razón social del contribuyente |
| `{{ nitAddr }}` | `NitAddress` | Dirección fiscal del contribuyente |
| `{{ mode }}` | `Mode` | `live` o `test` (muestra badge "Test" si no es live) |
| `{{ donorName }}` | `DonorName` | Nombre del donante (fiscal si hay NIT, de perfil si no) |
| `{{ donorEmail }}` | `DonorEmail` | Correo electrónico del donante |

Todas las variables se procesan con filtros Lava de seguridad (`StripHtml`,
`Truncate`, `Escape`) para evitar XSS en el email.

### Estructura del email

```
┌────────────────────────────────────┐
│ [Test] (badge si mode != live)     │  Header negro redondeado
│                                    │
│     ✓                              │  Círculo verde con checkmark
│  ¡Gracias por tu donación!         │  Título
│  Tu generosidad hace la diferencia │  Subtítulo
│                                    │
│         Q 250.00                   │  Monto grande (serif)
│           GTQ                      │  Código moneda
├────────────────────────────────────┤
│  Detalles de la transacción        │  Card blanca
│  Donante  | Juan Pérez             │
│  Correo   | juan@email.com         │
│  Monto    | Q250.00 GTQ            │
├────────────────────────────────────┤  (solo si hay NIT)
│  Datos de facturación              │  Card blanca NIT
│  NIT       | 12345678              │
│  Razón social | EMPRESA SA         │
│  Dirección  | Ciudad de Guatemala  │
├────────────────────────────────────┤
│  ✓ Tu donación fue recibida...     │  Nota verde (pill)
│    Tu recibo llegará en breve.     │  (condicional si hay NIT)
├────────────────────────────────────┤
│  Este correo fue enviado a ...     │  Footer negro redondeado
│  No respondas a este mensaje.      │
│  Vidareal.tv                       │
└────────────────────────────────────┘
```

El diseño usa tablas HTML para compatibilidad con clientes de email que no
soportan flexbox/grid (Gmail, Outlook, Apple Mail). El color negro es
`#000000` (brand VidaReal).

---

## 6. Anti-fraude del cliente

Los cuatro mecanismos de anti-fraude tienen su implementación tanto en el
backend (C#) como en el frontend (Vue). Esta sección describe la parte del
cliente.

### 6.1. Device Fingerprint

Al cargar el componente (`onMounted`), se genera un `sessionId` de 32 caracteres
hexadecimales usando `crypto.getRandomValues` (CSPRNG). Este ID es estable
para toda la sesión de página (no cambia entre reintentos).

El `<iframe>` que carga el script de ThreatMetrix de Cybersource se monta de
forma diferida: en la primera interacción del usuario (pointerdown, focusin,
scroll, touchstart, keydown) o como fallback 3 segundos después del evento
`load`. Esto evita bloquear el critical rendering path.

```html
<iframe
    v-if="deviceFingerprintReady && deviceFingerprintUrl"
    :src="deviceFingerprintUrl"
    sandbox="allow-scripts"
    referrerpolicy="no-referrer"
    style="position:absolute;top:-5000px;left:-5000px;..."
    aria-hidden="true"
    tabindex="-1">
</iframe>
```

La URL del iframe concatena `merchantId + sessionId` como `session_id`:
```
https://h.online-metrix.net/fp/tags?org_id={orgId}&session_id={merchantId}{sessionId}
```

El mismo `sessionId` se envía al backend como `deviceFingerprintSessionId`
en el payload de `ProcessPayment`.

### 6.2. reCAPTCHA Enterprise

El script de reCAPTCHA se carga bajo demanda con la misma estrategia de
diferimiento que Device Fingerprint (primera interacción o 3s post-load).
La promesa de carga se cachea para que múltiples llamadas no inserten el
`<script>` más de una vez.

Justo antes de enviar el pago, `getRecaptchaToken()` solicita un token fresco:
```ts
grecaptcha.enterprise.ready(async () => {
    const token = await grecaptcha.enterprise.execute(siteKey, { action: "donation" });
});
```

El token se incluye en el payload de `ProcessPayment` como `recaptchaToken`.
Si el sitio no tiene `siteKey` configurado, la función devuelve `""` y el
backend omite la verificación.

### 6.3. Idempotencia

```ts
function generateIdemKey(): string {
    const arr = new Uint8Array(8);
    crypto.getRandomValues(arr);
    const rand = Array.from(arr).map(b => b.toString(16).padStart(2, "0")).join("");
    return Date.now().toString(36) + "-" + rand;
}
```

La clave se genera al montar el componente y se rota en estos casos:
- Tras un cobro exitoso (para permitir una segunda donación independiente).
- Tras un error que se considera seguro para reintentar (CVV incorrecto,
  fondos insuficientes, tarjeta vencida, etc.).

Para errores de red o estados desconocidos, se **conserva** la misma clave
para que un reintento inmediato llegue al backend con la misma clave y el
backend pueda detectar si ya se procesó.

### 6.4. Limpieza de datos sensibles

El PAN, fecha de vencimiento y CVV se borran del estado reactivo inmediatamente
antes de enviar el request al backend (para que no queden visibles en Vue
DevTools mientras el request está en vuelo) y de nuevo en el `finally` del
try-catch. Además, hay un guard atómico no-reactivo `submitInFlight` para
evitar re-entradas aunque el atacante manipule `busy.value` desde DevTools.

---

## 7. Manejo de monedas en el frontend

### Switch de moneda

El selector GTQ / USD es un toggle de dos botones. Al cambiar:
- El símbolo del input hero cambia (`Q` → `$` o viceversa).
- El monto se resetea a 0 centavos para evitar que el usuario done el mismo
  número en la moneda equivocada.
- Los errores de campo se limpian.

### Formateo de montos

```ts
function formatMoney(value: number, currencyCode?: string): string {
    const currency = normalizeCurrency(currencyCode);  // GTQ o USD
    const numStr = value.toLocaleString("en-US", {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    });
    return currency === "GTQ" ? `Q${numStr}` : `$${numStr}`;
}
```

Ejemplos: `Q1,234.56`, `$500.00`.

### En el historial (CybersourceDonationEntry)

Cada entrada del historial tiene su propio `currency` extraído del campo
`ForeignKey` (`CUR=`), así que el historial puede mostrar mezcla de GTQ y
USD para el mismo usuario.

### En el dashboard (DonationDashboard)

La lógica de moneda en el dashboard trata `null` y el DefinedValueId de la
moneda de la organización como el mismo bucket "Org. (USD)". Las monedas
extranjeras aparecen con su código ISO. Esto evita que las transacciones en
GTQ (si GTQ es la moneda extranjera de la org) aparezcan duplicadas.

---

## 8. Decisiones de diseño

### Por qué un bloque Obsidian completamente nuevo

Rock RMS tiene bloques de donación estándar (TransactionEntry, etc.), pero
no son compatibles con Cybersource REST ni con el esquema de NIT/FEL de
Guatemala. Usar el bloque estándar habría requerido modificar código core de
Rock con alto riesgo de conflictos al actualizar. El enfoque de bloque nuevo
bajo namespace `Dar` aísla el código completamente.

### Por qué el input del monto usa centavos como fuente de verdad

El comportamiento ATM (dígitos empujan desde la derecha) es el estándar en
Guatemala para interfaces de punto de venta. Evita confusión cuando el usuario
escribe `250` y espera ver `250.00`, no `2.50`. Almacenar centavos como entero
elimina errores de punto flotante.

### Por qué se re-valida el NIT en el backend al cobrar

Para evitar que un cliente malintencionado modifique el `nitName` o
`nitAddress` en el payload (con DevTools o con un proxy) y obtenga un recibo
fiscal con datos falsos. El backend sobrescribe los valores del cliente con
los de la API externa en cada cobro.

### Por qué se usa Teleport para modales y overlays

Obsidian renderiza el bloque dentro de la estructura del Panel de Rock, que
tiene `overflow: hidden` y z-index limitado. Los modales y overlays de pantalla
completa necesitan escapar de ese árbol DOM para cubrir toda la ventana.
`<Teleport to="body">` mueve los elementos al `<body>` sin romper la
reactividad de Vue.

### Por qué el Device Fingerprint se carga diferido

El iframe de ThreatMetrix hace requests a `h.online-metrix.net` al cargarse.
Si se monta sincrónicamente con el componente, aparece en las métricas de
Lighthouse como recurso bloqueante. Al diferirlo a la primera interacción,
el usuario ya está comprometido con la página y los recursos se cargan en
paralelo con su interacción. El usuario típicamente tarda varios segundos en
llenar el formulario, suficiente para que ThreatMetrix recolecte datos.

### Por qué el CSS usa el prefijo `.cy*`

Obsidian no soporta `<style scoped>` en `.obs` files de la misma manera que
un SFC de Vue estándar en una aplicación con build. Para evitar colisiones
con las clases de Bootstrap de Rock RMS (`.panel`, `.form-control`, etc.),
todos los estilos del bloque usan el prefijo `.cy` (por Cybersource / Custom).

### Por qué DonationDashboard no tiene búsqueda automática

Con hasta 500 transacciones por búsqueda y posibles joins a múltiples tablas,
ejecutar la consulta en cada cambio de filtro generaría demasiadas requests.
El modelo "configura filtros y presiona Buscar" es más predecible y reduce
la carga del servidor.

---

> **Mantenimiento:** actualizar este archivo cuando cambien las secciones del
> formulario, se agreguen nuevos filtros al dashboard, se modifiquen las
> variables del template de email o se actualicen las reglas de validación.
