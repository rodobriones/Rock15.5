# Cambios en Rock.JavaScript.Obsidian.Blocks — VidaReal fork de Rock 18.1

Rama: `hotfix-18.1`  
Base de comparacion: commit `ca2ca0ec94`

---

## package.json — Dependencias nuevas

Se agrego una seccion `"dependencies"` (antes el archivo solo tenia `"devDependencies"`):

```json
"dependencies": {
    "@zxing/browser": "^0.1.5",
    "@zxing/library": "^0.21.3"
}
```

**Por que `@zxing`:**  
La libreria ZXing ("Zebra Crossing") es la implementacion de decodificacion de codigos de barras y QR mas usada. Se agrego como `dependency` (no `devDependency`) porque se usa en tiempo de ejecucion en el navegador, no solo durante el build.

- `@zxing/library` — nucleo de decodificacion (soporta QR Code, Code 128, EAN, etc.)
- `@zxing/browser` — adaptador para usar la camara del dispositivo via `getUserMedia` en el navegador

**Donde se usa:**  
En el modulo `QREVENT` (ver `src/QREVENT/vendor/zxing.lib.ts`), que expone ZXing de forma controlada para el bloque de escaner QR (`QRScanner.obs`, `CelebremosQrCheckIn.obs`, `ReservationScanner.obs`).

**Nota:** La separacion `devDependencies` vs `dependencies` es semantica en este proyecto porque Rollup bundlea todo. La distincion es util para documentar que ZXing es una dependencia de runtime y no puede eliminarse sin afectar funcionalidad.

---

## tsconfig.json — Nuevos modulos registrados

Se agregaron tres nuevas rutas de modulos al proyecto raiz de TypeScript:

```json
{ "path": "./src/Dar" },
{ "path": "./src/FamilyHub" },
{ "path": "./src/QREVENT" },
```

Cada una de estas carpetas tiene su propio `tsconfig.json` local (project references de TypeScript) que define que archivos compila y como se integra con el build de Rollup.

**Estado actual del tsconfig.json raiz:**

```
src/Administration    src/AI           src/Bus
src/CheckIn          src/Cms          src/Communication
src/Core             src/Crm
src/Dar              (NUEVO VidaReal)
src/Engagement       src/Event        src/Example
src/FamilyHub        (NUEVO VidaReal)
src/Finance          src/Group        src/Lms
src/Mobile           src/Prayer
src/QREVENT          (NUEVO VidaReal)
src/Reporting        src/Security     src/Tv
src/Utility          src/WebFarm      src/WorkFlow
```

Nota: `src/LayoutCustom` esta en uso (bloques `header.obs` y `footer.obs` existen) pero no aparece en el `tsconfig.json` raiz. Sus archivos se compilarian si tienen tsconfig local o si son incluidos por otra ruta.

---

## Estructura de nuevas carpetas VidaReal

### `src/Dar/` — Modulo de Donaciones

Bloques Obsidian para el flujo de donaciones de VidaReal.

| Archivo | Descripcion |
|---|---|
| `CybersourceDonationEntry.obs` | Formulario principal de donacion con gateway Cybersource |
| `DonationDashboard.obs` | Dashboard de seguimiento de donaciones |
| `CybersourceDonationEntry.md` | Documentacion interna del bloque |
| `tsconfig.json` | Configuracion TypeScript del modulo |

Bloque C# correspondiente: `Rock.Blocks/Dar/CybersourceDonationEntry.cs`, `Rock.Blocks/Dar/DonationDashboard.cs`

### `src/QREVENT/` — Modulo de Check-in por QR

Bloques Obsidian para el sistema de check-in de eventos mediante codigos QR.

| Archivo | Descripcion |
|---|---|
| `qrScanner.obs` | Componente base de escaneo QR (usa ZXing via camara) |
| `CelebremosQrCheckIn.obs` | Check-in para eventos "Celebremos" de VidAventura |
| `ReservationScanner.obs` | Escaner para validar reservaciones de eventos |
| `SundayServiceRegistration.obs` | Registro para el servicio dominical |
| `vendor/zxing.lib.ts` | Wrapper/facade sobre `@zxing/browser` y `@zxing/library` |
| `tsconfig.json` | Configuracion TypeScript del modulo |

Bloques C# correspondientes: `Rock.Blocks/QREVENT/QRScanner.cs`, `CelebremosQrCheckIn.cs`, `ReservationScanner.cs`, `SundayServiceRegistration.cs`

### `src/FamilyHub/` — Modulo Hub Familiar

| Archivo | Descripcion |
|---|---|
| `FamilyHub.obs` | Vista principal del hub familiar (perfil, grupos, eventos familiares) |
| `tsconfig.json` | Configuracion TypeScript del modulo |

Bloque C# correspondiente: `Rock.Blocks/FamilyHub/FamilyHub.cs`

### `src/Wallet/` — Modulo Wallet (pases Apple/Google)

| Archivo | Descripcion |
|---|---|
| `walletTemplateAdmin.obs` | Admin de plantillas de pases (colores, imagenes, campos Lava, barcode, preview). 2026-08-10: campos Google `subheader` y `logoImageUrl` (rediseño VidaAventura) |
| `paseDigital.obs` | Tarjeta web del pase digital de la iglesia con el diseño del pase de wallet (navy #0e3a5c, logo + foto de la plantilla, NOMBRE / ASISTO A = campus, QR de check-in). Mobile-first full-bleed: lienzo navy de orilla a orilla (breakout `100vw` del contenedor del theme); ≥480px tarjeta centrada 420px. Botón de wallet oculto por defecto. 2026-08-10 (antes `vidaAventuraPass.obs`) |

Bloque C# correspondiente: `Rock.Blocks/Wallet/WalletTemplateAdmin.cs`

### `src/LayoutCustom/` — Bloques de Layout Global

| Archivo | Descripcion |
|---|---|
| `header.obs` | Header global personalizado del sitio VidaReal |
| `footer.obs` | Footer global personalizado del sitio VidaReal |

Bloques C# correspondientes: `Rock.Blocks/LayoutCustom/Header.cs`, `Rock.Blocks/LayoutCustom/Footer.cs`

---

## Bloques existentes modificados

Ademas de los modulos nuevos, se modificaron bloques del core de Rock para adaptarlos a VidaReal:

| Modulo | Archivos | Razon probable |
|---|---|---|
| `Security/` | `accountEntry.obs`, `login.obs`, `confirmAccount.obs`, `forgotUserName.obs`, y sus partials | Personalizacion del flujo de autenticacion/registro para VidaReal |
| `Security/` | `codeBox.obs`, `codeBoxCharacter.partial.obs` | Componente de ingreso de codigo OTP (passwordless) |
| `Crm/FamilyPreRegistration/` | `preRegistrationChild.partial.obs`, `preRegistrationChildren.partial.obs`, `utils.partial.ts`, `familyPreRegistration.obs` | Ajustes al registro de familia para flujos de VidaReal |
| `Event/RegistrationEntry/` | Multiples partials | Modificaciones al modulo de registro de eventos |
| `Communication/CommunicationEntry/` | `communicationMediumEmail`, `Sms`, `PushNotification` | Ajustes al editor de comunicaciones |
| `Finance/financialAccountDetail.obs` | 1 archivo | Ajuste menor a detalle de cuenta financiera |
| `Engagement/achievementTypeDetail.obs` | 1 archivo | Ajuste menor |

---

## Resumen de cambios en archivos de configuracion

| Archivo | Cambio |
|---|---|
| `package.json` | Agregada seccion `"dependencies"` con `@zxing/browser` y `@zxing/library` |
| `tsconfig.json` | Agregadas referencias a `./src/Dar`, `./src/FamilyHub`, `./src/QREVENT` |
| `src/Dar/tsconfig.json` | Nuevo (configura compilacion del modulo Dar) |
| `src/FamilyHub/tsconfig.json` | Nuevo (configura compilacion del modulo FamilyHub) |
| `src/QREVENT/tsconfig.json` | Nuevo (configura compilacion del modulo QREVENT) |
| `package-lock.json` | Actualizado automaticamente al agregar dependencias ZXing |
