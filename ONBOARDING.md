# ONBOARDING — Cómo levantar este repo desde cero

> Guía para pasarle el proyecto a otra persona (o a otra máquina): qué NO viene en el
> clon, en qué orden se compila, y cómo se arranca.
>
> **Mantener actualizado:** si agregás un plugin, una dependencia manual o un paso de
> setup nuevo, actualizá este archivo en el mismo commit.

Fork de Rock RMS v18.1 de Iglesia Vida Real, con módulos propios (Eventos/Boletería,
Wallet, DupDetect, Odoo FEL, Translator, gateways Epay/Cybersource, WhatsApp).
El mapa de toda la documentación está en **`.claude/CONTEXT_INDEX.md`** — leerlo primero.

---

## 1. Requisitos

- Windows 10 / Server 2016+ (obligatorio: `System.Net.Http.WinHttpHandler` para APNs del módulo Wallet).
- Visual Studio 2022 con carga de trabajo ASP.NET (.NET Framework 4.7.2) — o `msbuild`/`dotnet build` por consola.
- SQL Server (local o remoto) con una BD de Rock (copia de dev o restore de prod).
- Node.js LTS + npm (para los bundles Obsidian).

## 2. Qué NO viene en el clon (gitignore)

| Ignorado | Qué es | Cómo se repone |
|---|---|---|
| `RockWeb/Bin/*.dll` (todos) | Binarios del sitio | Compilar (sección 3) |
| `RockWeb/Obsidian/` | Bundles JS compilados de los bloques | Build de Obsidian (sección 4) |
| `RockWeb/web.ConnectionStrings.config` | Cadena de conexión | Crear a mano (sección 5) |
| `RockWeb/Plugins/*/*` (todo lo interno) | Plugins webforms/DLLs instalados | **Copiar la carpeta completa del ambiente origen** (ver 2.1) |
| `RockWeb/Content/` | Archivos/media subidos | Copiar del ambiente origen si se necesita |
| `node_modules`, `obj/`, `bin/Debug|Release` | Artefactos | Se regeneran solos |

### 2.1 `RockWeb\Plugins` — copiar a mano SIEMPRE

Tiene su propio `.gitignore` (`*/*`): git solo trackea el readme. Todo lo demás hay que
copiarlo del ambiente origen. Contenido actual:

**Custom VidaReal (irreemplazable, no se compila desde el repo):**
- `com_vidareal\Translator\` — bloques webforms + JS del traductor
- `Qrgenerador\bin\` — DLL del workflow action GenerateQrRegistrant
- `TwilioOverrideWhatsApp\bin\TwilioOverride.dll` — override Twilio para WhatsApp
- `EpayVisanetGateway\` y `CybersourceInlineRestGateway\` — bundle Obsidian (`.obs.js`)
  + fuente del control de pago de cada gateway

**Third-party del Rock Shop** (reinstalables, pero más fácil copiarlos): MailChimp (BEMA),
AcmeCertificate + DataToolkit (Blue Box Moon), LavaTester (centralaz), MinePass
(Mine Cart Studio), MinistryPass (Triumph Tech).

⚠️ Estos plugins del Shop también tienen **DLLs en `RockWeb\Bin` que compilar Rock.sln
NO regenera** — copiarlas del ambiente origen junto con la carpeta Plugins:
`com.bemaservices.MailChimp.dll`, `com.bemaservices.WorkflowExtensions.dll`,
`com.blueboxmoon.AcmeCertificate.dll`, `com.blueboxmoon.DataToolkit.dll`,
`com.minecartstudio.MinePass.Client.dll`, `com.minecartstudio.MinePassCommon.dll`,
`MailChimp.Net.dll`, `MailChimp.Net.V3.dll`,
`tech.triumph.MinistryPass.Client.dll`, `tech.triumph.MinistryPass.Common.dll`.
(`Twilio.dll` no: esa la restaura NuGet con Rock.sln.)

## 3. Compilar — EN ESTE ORDEN

### 3.1 Core (`Rock.sln`)

```powershell
# desde la raíz del repo
dotnet build Rock.sln
# o msbuild Rock.sln /p:Configuration=Debug
```

Esto llena `RockWeb\Bin` con todo el core, que incluye el grueso del trabajo custom:

- `Rock.dll` — módulos Eventos (`Rock/Model/Eventos`), Wallet (`Rock/Model/Wallet`), etc.
- `Rock.Blocks.dll`, `Rock.ViewModels.dll`, `Rock.Rest.dll` — bloques, bags y controllers custom.
- `Rock.WhatsApp.dll` (sí está en el .sln).
- `System.Net.Http.WinHttpHandler.dll` — la restaura NuGet (PackageReference en `Rock/Rock.csproj`).
  ⚠️ En deploys a prod por copia de archivos hay que copiarla a mano (es "nueva" vs Rock vanilla).

### 3.2 Plugins custom (NO están en Rock.sln)

Referencian `RockWeb\Bin\Rock.dll`, por eso van **después** del core. Compilar cada uno y
copiar **solo su DLL propio** (el resto de DLLs de su `bin\` son copias de las de Rock):

| Proyecto | DLL → `RockWeb\Bin` |
|---|---|
| `Plugin.VidaRealEvents` | `com.vidareal.Events.dll` |
| `Plugin.VidaRealWallet` | `com.vidareal.Wallet.dll` |
| `Plugin.VidaRealDupDetect` | `com.vidareal.DupDetect.dll` |
| `Plugin.VidaRealTranslator` | `com.vidareal.Translator.dll` |
| `Plugin.OdooEventSale` | `OdooEventSale.dll` |
| `Plugin.EpayVisanetGateway` | `EpayVisanetGateway.dll` |
| `Plugin.CybersourceInlineRestGateway` | `CybersourceInlineRestGateway.dll` |

```powershell
# desde la raíz del repo, tras compilar Rock.sln
$plugins = @(
  'Plugin.VidaRealEvents\VidaRealEvents',
  'Plugin.VidaRealWallet\VidaRealWallet',
  'Plugin.VidaRealDupDetect\VidaRealDupDetect',
  'Plugin.VidaRealTranslator\VidaRealTranslator',
  'Plugin.OdooEventSale\OdooEventSale',
  'Plugin.EpayVisanetGateway\EpayVisanetGateway',
  'Plugin.CybersourceInlineRestGateway\CybersourceInlineRestGateway'
)
foreach ($p in $plugins) {
  dotnet build $p /p:Configuration=Debug
  Copy-Item "$p\bin\Debug\net472\com.vidareal.*.dll",
            "$p\bin\Debug\net472\OdooEventSale.dll",
            "$p\bin\Debug\net472\*Gateway.dll" RockWeb\Bin -ErrorAction SilentlyContinue
}
```

## 4. Frontend Obsidian (bundles JS)

`RockWeb/Obsidian/` está gitignored; sin este paso los bloques custom (checkout de
eventos, Mis Entradas, scanner, wallet admin, VRSimpleRegistration, etc.) no cargan.

```powershell
cd Rock.JavaScript.Obsidian;        npm ci; npm run build
cd ..\Rock.JavaScript.Obsidian.Blocks; npm ci; npm run build
```

⚠️ Tras tocar partials de Eventos: verificar que el bundle servido es el nuevo
(grep `_ctx.` en el .js; historial de este bug en `docs/eventos-custom/RESEARCH_Y_PLAN.md`).

## 5. Connection string

Crear `RockWeb\web.ConnectionStrings.config` (hay un `.example` como plantilla):

```xml
<connectionStrings>
  <add name="RockContext"
       connectionString="Data Source=SERVIDOR;Initial Catalog=BD;User Id=USUARIO;password=PASSWORD;MultipleActiveResultSets=true"
       providerName="System.Data.SqlClient" />
</connectionStrings>
```

## 6. Base de datos

- Usar una **copia de la BD existente** (dev o prod). No arrancar contra BD vacía: los
  módulos custom asumen datos/config (Global Attributes, páginas, esquemas `_com_vidareal_*`).
- Las **migraciones de plugins** (`[PluginMigration]` de `com.vidareal.Events` 001–021,
  `com.vidareal.Wallet` 001–013, DupDetect, etc.) corren solas al arrancar el sitio.
  ⚠️ La numeración de cada assembly es propia — leer el `README.md` del plugin ANTES de
  agregar una migración nueva.

## 7. Arrancar

1. Apuntar un sitio de IIS (o IIS Express desde VS, proyecto `RockWeb`) a `RockWeb\`.
2. Primer arranque es lento: `Rock.WebStartup` corre migraciones core + de plugins.
3. Verificar en el log (`RockWeb/App_Data/Logs`) que las plugin migrations corrieron.
4. Copiar cualquier DLL a `RockWeb\Bin` recicla el app pool solo — no hace falta iisreset.
5. **La PRIMERA generación de PDF de boletos descarga Chromium (~150 MB)** a
   `App_Data/ChromeEngine` y puede tardar minutos; las siguientes son rápidas. El servidor
   necesita salida a internet para eso (alternativa sin descarga: System Setting
   `PDF_EXTERNAL_RENDER_ENDPOINT`).

## 8. Config/secretos que viven FUERA del repo

Todo esto está en la BD (Global Attributes) o en archivos locales — viene "gratis" si se
copia la BD, pero hay que saber que existe:

- **Wallet/Apple**: Global Attributes `AppleWalletPassP12` + password (cert Pass Type ID
  `pass.tv.vidareal.eventos`, vence 2027-08-05; assets fuente en `Documents\AppleWalletCert`
  de la máquina original). Google Wallet: `GoogleWalletIssuerId` / `GoogleWalletServiceAccountJson`.
- **Odoo FEL**: credenciales y Global Attributes de NIT (ver `Plugin.OdooEventSale/README.md`).
- **Gateways Epay/Cybersource**: configuración en Admin → Financial Gateways.
- **WhatsApp**: tokens en la config del transporte. Además, la URL del webhook
  `RockWeb/Webhooks/WhatsAppSms.ashx` debe estar registrada en el panel de
  **Meta for Developers** apuntando al dominio del ambiente (ver `CHANGES.md`).

## 8b. Repos relacionados (el proyecto no es solo este repo)

- `C:\Repos\Iglesia1\custom_event_sale_api` — **addon de Odoo 17** que recibe la venta de
  eventos y emite la factura FEL (contrato en su `docs/API.md`). Sin él no hay facturación.
  Deploy del addon: reiniciar Odoo con `-u custom_event_sale_api`.

## 9. Dónde está la documentación

| Qué | Dónde |
|---|---|
| Índice maestro de docs | `.claude/CONTEXT_INDEX.md` |
| Contexto técnico transversal | `.claude/PROJECT_CONTEXT.md` |
| Historial de cambios vs upstream | `CHANGES.md` |
| Eventos/Boletería (doc maestro) | `docs/eventos-custom/RESEARCH_Y_PLAN.md` + `Rock/Model/Eventos/ARCHITECTURE.md` |
| Wallet (doc maestro + runbook deploy) | `docs/wallet-module/RESEARCH_Y_PLAN.md` |
| Smoke tests runtime | `docs/eventos-custom/SMOKE_TESTS.md` |
