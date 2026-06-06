# PROJECT_CONTEXT.md — Rock CMS fork VidaReal

## Que es este proyecto

**Rock RMS** es un CMS/ChMS (Church Management System) open source creado por SparkDevNetwork. Es el sistema central de gestion para iglesias: personas, familias, finanzas, comunicaciones, eventos, check-in, flujos de trabajo, etc.

**Este repositorio** es el fork que VidaReal usa para personalizar Rock en su contexto:
- Iglesia latina con sitio publico en vidareal.tv
- Operaciones en multiples paises (Guatemala principalmente, con ePay Visanet; otros con Cybersource)
- Interfaz publica en espanol latinoamericano
- Check-in infantil con animaciones y sonidos (grupo VidAventura)
- Donaciones online con pasarelas de pago locales
- Eventos con registro y check-in QR

---

## Stack tecnologico

### Backend (C#)
- **Framework:** ASP.NET Framework 4.8 (no .NET Core)
- **ORM:** Entity Framework (DbContext de Rock: `RockContext`)
- **Patron de bloques:** Cada pagina en Rock muestra "bloques" (plugins). Los bloques Obsidian heredan de `RockBlockType`.
- **BlockActions:** Los endpoints del backend se decoran con `[BlockAction]` y son llamados desde el frontend via `invokeBlockAction("NombreAccion", payload)`.
- **Inicializacion:** `GetObsidianBlockInitialization()` retorna el estado inicial del bloque al cargar la pagina.
- **Plugins externos:** Proyectos C# separados en `Plugin.*` que se despliegan como DLLs en Rock.
- **Namespace VidaReal:** `Rock.Blocks.Dar`, `Rock.Blocks.QREVENT`, `Rock.Blocks.FamilyHub`, `Rock.Blocks.LayoutCustom`, `Rock.WhatsApp`

### Frontend (Vue 3 / TypeScript — Obsidian framework)
- **Archivos:** Extension `.obs` (son Single File Components de Vue 3 con TypeScript)
- **Ubicacion:** `Rock.JavaScript.Obsidian.Blocks/src/`
- **Framework interno:** "Obsidian" — capa de Rock sobre Vue 3. Los componentes reutilizables de Rock estan en `Rock.JavaScript.Obsidian/Framework/`
- **Build:** `npm run build` dentro de `Rock.JavaScript.Obsidian.Blocks/`
- **Convenciones de nombres:** `camelCase.obs` para bloques, `camelCase.partial.obs` para sub-componentes de un bloque
- **Comunicacion backend:** `useInvokeBlockAction` hook para llamar BlockActions

### Base de datos
- **SQL Server** (no hay ORM migrations propias de VidaReal — se usan las de Rock)
- Scripts SQL en `Dev Tools/Sql/` para hardening y configuracion

---

## Modulos y bloques personalizados VidaReal

### 1. DAR (Donaciones) — `Rock.Blocks/Dar/`
El modulo mas complejo. Maneja donaciones online.

**Bloques:**
- `CybersourceDonationEntry.cs` / `CybersourceDonationEntry.obs` — Formulario principal de donacion con Cybersource
- `DonationDashboard.cs` / `DonationDashboard.obs` — Dashboard para ver historial y filtrar transacciones

**Caracteristicas:**
- Multi-moneda (GTQ, USD, MXN, etc.)
- reCAPTCHA v3 para anti-fraude
- Validacion de tarjetas (Luhn, BIN lookup)
- Normalizacion de nombres y datos
- Correo de confirmacion HTML (`EmailTemplates/confirmacion-donacion.html`)
- Integracion con `FinancialTransaction` y `FinancialAccount` de Rock

**Pasarelas (plugins separados):**
- `Plugin.CybersourceInlineRestGateway/` — Para paises con Cybersource (tarjeta credito/debito)
- `Plugin.EpayVisanetGateway/` — Para Guatemala via ePay Visanet SOAP

### 2. QREVENT (Eventos con QR) — `Rock.Blocks/QREVENT/`
Sistema de check-in y registro para eventos especiales.

**Bloques:**
- `QRScanner.cs` / `qrScanner.obs` — Lista de eventos + escaneo de QR para registrar asistencia
- `CelebremosQrCheckIn.cs` / `CelebremosQrCheckIn.obs` — Check-in especifico para grupo "Celebremos" con Steps de Rock
- `ReservationScanner.cs` / `ReservationScanner.obs` — Scanner para reservaciones pre-hechas
- `SundayServiceRegistration.cs` / `SundayServiceRegistration.obs` — Registro para servicio dominical

**Dependencias tecnicas:**
- `Rock.JavaScript.Obsidian.Blocks/src/QREVENT/vendor/zxing.lib.ts` — Libreria ZXing para leer QR desde camara
- Usa `RegistrationInstance`, `RegistrationRegistrant`, Rock Steps (`StepProgram`, `StepStatus`)
- Constantes hardcodeadas: `ATTR_ID_ASISTIO = 8400`, `ATTR_ID_FECHA = 8401`, `StepProgramIdCelebremos = 5`

**Bloques WebForms legacy (no migrados a Obsidian):**
- `RockWeb/Blocks/QREVENT/EventParticipants.ascx` — Lista de participantes de evento
- `RockWeb/Blocks/SundayService/SundayServiceCapacityAdmin.ascx` — Admin de capacidad

### 3. FamilyHub — `Rock.Blocks/FamilyHub/`
Portal para que los miembros gestionen su familia en Rock.

**Bloque:** `FamilyHub.cs` / `FamilyHub.obs`

**Funcionalidad:**
- Ver miembros del grupo familiar del usuario autenticado
- Agregar/editar relaciones conocidas (Known Relationships)
- Editar estado civil y parentesco
- Carga y muestra de fotos de perfil (thumbnail via GDI+)

**Advertencia:** Este bloque tuvo un bug conocido con `KnownRelationship` que se documenta en `Migration_Context_ReservationScanner_FamilyHub.md` y `FamilyHub_KnownRelationship_Fix_Context.md` en la raiz.

### 4. LayoutCustom (Header/Footer) — `Rock.Blocks/LayoutCustom/`
Bloques de presentacion para el layout del sitio externo de VidaReal.

**Bloques:**
- `Header.cs` / `header.obs` — Header del sitio (navegacion, logo)
- `Footer.cs` / `footer.obs` — Footer del sitio

Estos son bloques Obsidian que reemplazan los includes HTML estaticos. El C# es minimo (solo declaracion del bloque); toda la logica esta en los `.obs`.

### 5. Security (Autenticacion) — modificaciones al upstream

**ZONA DELICADA.** Estos archivos son del core de Rock pero con modificaciones VidaReal.

**Archivos C# modificados:**
- `Rock/Security/Authentication/PasswordlessAuthentication.cs` — Proveedor de autenticacion sin contrasena (OTP via email o SMS)
- `Rock/Security/Authentication/OneTimePasscode/MatchingPersonResult.cs` — Resultado de matching de persona por OTP
- `Rock/Model/Security/RemoteAuthenticationSessionService.cs` — Sesiones de autenticacion remota
- `Rock.Blocks/Security/Login.cs` — Mapeo de personas en seleccion passwordless (agregado `Category = p.PhotoUrl`)

**Archivos Obsidian modificados (traduccion + UI VidaReal):**
Todos en `Rock.JavaScript.Obsidian.Blocks/src/Security/`:
- `login.obs` — Pantalla principal de login, legend: "Iniciar sesion"
- `accountEntry.obs` — Creacion de cuenta nueva
- `confirmAccount.obs` — Confirmacion de cuenta
- `forgotUserName.obs` — Recuperar usuario
- `codeBox.obs` / `codeBoxCharacter.partial.obs` — Input de codigo OTP de 6 digitos
- `Login/credentialLogin.partial.obs` — Login con usuario/contrasena ("Contraseña" en espanol)
- `Login/passwordlessLoginStartStep.partial.obs` — Paso 1 passwordless: seleccionar email o telefono
- `Login/passwordlessLoginVerifyStep.partial.obs` — Paso 2 passwordless: ingresar OTP
- `Login/loginMethodPicker.partial.obs` — Selector de metodo de login
- `AccountEntry/registrationStep.partial.obs` — Paso de registro: "Crea tu cuenta / Unete a la comunidad VidaReal.tv"
- `AccountEntry/registrationStepAccountInfo.partial.obs` — Info de cuenta en registro
- `AccountEntry/registrationStepPersonInfo.partial.obs` — Info personal en registro
- `AccountEntry/completedStep.partial.obs` — Paso final del registro
- `AccountEntry/duplicatePersonSelectionStep.partial.obs` — Seleccion en caso de persona duplicada
- `AccountEntry/existingAccountStep.partial.obs` — Cuenta existente
- `AccountEntry/phoneNumberDetails.partial.obs` — Detalles de telefono
- `ConfirmAccount/accountConfirmation.partial.obs` — Confirmacion de cuenta
- `ConfirmAccount/changePassword.partial.obs` — Cambio de contrasena
- `ConfirmAccount/deleteConfirmation.partial.obs` — Confirmacion de eliminacion

**Bloque nuevo (sin commit al 2026-06-04):**
- `Rock.Blocks/Security/VRSimpleRegistration.cs` — Registro simplificado post-passwordless
- `Rock.JavaScript.Obsidian.Blocks/src/Security/vrSimpleRegistration.obs` — Frontend del bloque anterior
- **Proposito:** Cuando un usuario se autentica via passwordless por primera vez y Rock no tiene sus datos completos, este bloque captura nombre, apellido, email/telefono y crea el perfil.

### 6. WhatsApp — `Rock.WhatsApp/`
Modulo nuevo de comunicacion.

**Clase principal:** `Rock.WhatsApp.Communication.Transport.WhatsAppTransport`
- Implementa `TransportComponent` de Rock
- Usa WhatsApp Business Cloud API (Meta)
- Configuracion: Phone Number ID, Access Token, plantillas de mensajes
- Webhook entrante: `RockWeb/Webhooks/WhatsAppSms.ashx`

---

## Areas que NO deben tocarse sin extremo cuidado

### 1. Core de autenticacion (`Rock/Security/Authentication/`)
Cualquier cambio aqui puede dejar a todos los usuarios sin poder iniciar sesion. Antes de modificar:
- Entender el flujo completo de passwordless (email OTP y SMS OTP)
- Tener un usuario admin con contrasena tradicional como respaldo
- Probar en ambiente de desarrollo antes de production

### 2. `Rock/Model/` — modelos de datos
Son las entidades de Entity Framework. Cambios incorrectos pueden corromper datos o romper migraciones.

### 3. `RockWeb/` — bloques WebForms heredados
Muchos bloques `.ascx` son del upstream. Modificarlos puede romper funcionalidades inesperadas. Preferir crear bloques Obsidian nuevos.

### 4. `Rock.JavaScript.Obsidian/Framework/` — framework base
Los controles base de Rock (DatePicker, RockValidation, etc.). Hay cambios VidaReal aqui — modificar con cuidado y comparar contra upstream antes de cambiar.

### 5. Plugins de pasarela (`Plugin.*/`)
Manejan dinero real. Cualquier cambio requiere pruebas exhaustivas en sandbox antes de production.

---

## Convenciones del proyecto

### Nombres de bloques VidaReal
- Prefijo `VR` para bloques completamente nuevos (ej: `VRSimpleRegistration`)
- Sin prefijo para bloques que modifican upstream pero mantienen la misma funcion
- Categoria Rock: `"VidaReal > Security"`, `"Custom"`, `"Dar"`, `"QREVENT"`

### Estilo de codigo C#
- Seguir el estilo de Rock RMS (espacios, no tabs; llaves en nueva linea para clases/metodos)
- Los BlockActions retornan `BlockActionResult` (via `ActionOk()`, `ActionBadRequest()`, etc.)
- Usar `RockContext` siempre dentro de `using` para evitar leaks de contexto

### Estilo de codigo Obsidian (.obs)
- Vue 3 Composition API con `<script setup lang="ts">`
- Los estilos van en `<style>` al final del archivo
- Nombres de clases CSS propios de VidaReal usan prefijos como `cy`, `pl`, `ae` segun el bloque
- Los textos en espanol van directamente en el template (sin sistema de i18n formal — hardcodeado en espanol)

### Git
- Commits en espanol, estilo informal
- No hay CI/CD automatico — despliegue manual
- Branch de trabajo: `hotfix-18.1` (no merge a upstream)

---

## Estructura de directorios relevantes

```
C:\Repos\Rock18.1\
├── Rock/                          # Core framework (upstream, modificar con cuidado)
│   ├── Model/                     # Entidades EF
│   ├── Security/Authentication/   # Proveedores de autenticacion (DELICADO)
│   └── ...
├── Rock.Blocks/                   # Bloques servidor C# (mix upstream + VidaReal)
│   ├── Dar/                       # NUEVO VidaReal: donaciones
│   ├── FamilyHub/                 # NUEVO VidaReal: portal familiar
│   ├── LayoutCustom/              # NUEVO VidaReal: header/footer
│   ├── QREVENT/                   # NUEVO VidaReal: check-in eventos
│   └── Security/
│       ├── Login.cs               # Upstream modificado (cambio minimo)
│       └── VRSimpleRegistration.cs # NUEVO VidaReal (sin commit)
├── Rock.JavaScript.Obsidian.Blocks/src/  # Frontend Vue/TypeScript
│   ├── Dar/                       # NUEVO VidaReal
│   ├── FamilyHub/                 # NUEVO VidaReal
│   ├── LayoutCustom/              # NUEVO VidaReal
│   ├── QREVENT/                   # NUEVO VidaReal
│   └── Security/                  # Upstream + traduccion VidaReal (muchos archivos)
├── Rock.JavaScript.Obsidian/Framework/  # Framework base (upstream, modificado mínimo)
├── Plugin.CybersourceInlineRestGateway/ # Plugin pasarela pago
├── Plugin.EpayVisanetGateway/           # Plugin pasarela pago Guatemala
├── Rock.WhatsApp/                       # NUEVO VidaReal: transporte WhatsApp
├── RockWeb/                             # ASP.NET webapp
│   ├── Blocks/QREVENT/            # Bloques WebForms QREVENT
│   ├── Blocks/SundayService/      # Bloques WebForms SundayService
│   ├── Blocks/CheckIn/            # Upstream con modificaciones menores
│   └── Themes/VidAventuracheckin/ # NUEVO VidaReal: tema check-in infantil
├── Dev Tools/Sql/                 # Scripts SQL de configuracion
├── CHANGES.md                     # ESTE ARCHIVO — documento raiz
└── .claude/
    ├── PROJECT_CONTEXT.md         # Este archivo — contexto para Claude Code
    ├── settings.json              # Permisos de Claude Code
    └── settings.local.json        # Permisos locales
```

---

## Datos de contacto y acceso

- **Email IT:** serviciosit@vidareal.tv
- **Repositorio local:** `C:\Repos\Rock18.1`
- **Branch activo:** `hotfix-18.1`
- **Developer principal:** rodobriones (Rodolfo Rodriguez)

---

## Checklist para inicio de sesion de Claude Code

- [ ] Leer `CHANGES.md` en la raiz del repo para contexto de alto nivel
- [ ] Leer este archivo (`.claude/PROJECT_CONTEXT.md`) para contexto tecnico
- [ ] Revisar `git status` para ver archivos modificados sin commit
- [ ] Si hay archivos `.md` de contexto de sesion anterior en la raiz (ej: `AI_HANDOFF_*.md`, `*_Migration_Context.md`), leerlos — contienen contexto de trabajo previo
- [ ] Preguntar al usuario en que area especifica va a trabajar antes de explorar el codigo

---

## Archivos de contexto clave

Estos archivos `.md` en la raiz del repositorio documentan sesiones de trabajo anteriores y son fuente de contexto para Claude Code:

| Archivo | Modulo | Descripcion |
|---|---|---|
| `AI_HANDOFF_ROCK18_EVENT_CRM.md` | Eventos / CRM | i18n en Event/RegistrationEntry y Crm/FamilyPreRegistration, reglas ES/EN, DatePicker, pitfalls Vue, template Lava recomendado |
| `EPAY_FLOW_SUMMARY.md` | DAR / ePay | Flujo completo de cobro con cuotas ePay Visanet: SOAP, FeeCoverageAmount, calculo de balance, checklist de despliegue |
| `FamilyHub_KnownRelationship_Fix_Context.md` | FamilyHub | Bug y fix de KnownRelationship bidireccional, rediseno visual del bloque |
| `Migration_Context_ReservationScanner_FamilyHub.md` | QREVENT / FamilyHub | Migracion de ReservationScanner y FamilyHub de Rock 15.5.1 a Rock 18.1 |
| `QREVENT_CelebremosQrCheckIn_Migration_Context.md` | QREVENT | Migracion de CelebremosQrCheckIn de Rock 15.5.1 a Rock 18.1 |
| `QREVENT_QRScanner_Migration_Context.md` | QREVENT | Migracion de QRScanner de Rock 15.5.1 a Rock 18.1, integracion ZXing vendor |

El indice maestro de todos estos archivos esta en `.claude/CONTEXT_INDEX.md` — ese es el primer archivo a leer en una sesion nueva.

---

## Base de datos / SQL

La base de datos es **SQL Server**. No hay migraciones ORM propias de VidaReal — se usan las de Rock RMS.
Los scripts SQL de VidaReal estan en `Dev Tools/Sql/` dentro del repositorio.

### Script de hardening QREVENT

**Archivo:** `Dev Tools/Sql/QREVENT_SundayService_Hardening.sql`

Debe ejecutarse **una sola vez al deployar el modulo QREVENT por primera vez** en cada ambiente. Crea:

- Constraints de integridad en tablas `SundayServiceSlot`, `SundayServiceHold`, `SundayServiceReservation`
- Indice unico filtrado `UX_SundayServiceReservation_ActivePerson` (evita doble-reservacion por persona)
- Stored procedures:
  - `sp_SundayServiceHoldUpsert` — Crea o renueva un hold temporal de asientos (con bloqueo optimista, max 8 personas, max 3 minutos)
  - `sp_SundayServiceReservationCancel` — Cancela una reservacion activa y recalcula el conteo del slot
  - `sp_SundayService_ConfirmFromHold` — DESHABILITADO (uso obsoleto; apunta a usar `sp_SundayServiceReservationConfirm`)

**Es idempotente:** las constraints y el indice se crean solo si no existen. Los procedures usan `CREATE OR ALTER`.
**Advertencia:** Fallara si hay datos invalidos en las tablas antes de ejecutarse.

Documentacion completa en `Dev Tools/Sql/CHANGES.md`.

---

## Configuracion Claude Code

### `.claude/settings.json` (permisos de proyecto)

```json
{
  "permissions": {
    "allow": [
      "Read(//c/Users/Rodolfo Rodriguez/Desktop/Rodolfo backuo/Downloads/woocommerce-epay/**)",
      "Bash(dotnet build:*)"
    ],
    "additionalDirectories": [
      "c:\\Repos\\Rock18.1\\Plugin.CybersourceInlineRestGateway\\Dependencies"
    ]
  }
}
```

- Permite leer archivos del backup de woocommerce-epay en el Desktop.
- Permite cualquier variante de `dotnet build` via Bash.
- Agrega el directorio de dependencias de Cybersource como directorio adicional accesible.

### `.claude/settings.local.json` (permisos locales de la maquina)

Permisos adicionales especificos del entorno de desarrollo local de `rodobriones`:
- MSBuild de Visual Studio 2022 Community (`C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\...`)
- Comandos `python` para scripts de utilidad
- Comandos `grep` para busqueda en archivos `.cs` y `.ashx`
- `find` para busqueda de archivos de migracion en plugins
- PowerShell para generacion de GUIDs
- `WebFetch` hacia `developers.facebook.com` (para documentacion de WhatsApp Business API)

---

## Modulos ordenados por complejidad (onboarding)

Del mas simple al mas complejo, para onboarding de nuevos desarrolladores:

### 1. LayoutCustom (Header/Footer) — Complejidad: BAJA
- Solo bloques de presentacion. El C# es minimo (solo declaracion).
- Toda la logica en `.obs` (Vue 3).
- Sin integracion con modelos de Rock.
- **Archivos:** `Rock.Blocks/LayoutCustom/`, `src/LayoutCustom/`

### 2. WhatsApp Transport — Complejidad: BAJA-MEDIA
- Proyecto C# independiente (`Rock.WhatsApp/`).
- Implementa un `TransportComponent` de Rock.
- Sin frontend propio.
- Integracion con WhatsApp Business Cloud API (Meta) via HTTP.
- **Archivos:** `Rock.WhatsApp/`, `RockWeb/Webhooks/WhatsAppSms.ashx`

### 3. FamilyHub — Complejidad: MEDIA
- Un solo bloque con backend + frontend.
- Logica de negocio: relaciones bidireccionales (KnownRelationship) — tiene bug conocido, ver contexto.
- Integracion con modelos `Group`, `GroupMember`, `PersonAlias`, `Photo` de Rock.
- **Archivos:** `Rock.Blocks/FamilyHub/`, `src/FamilyHub/`

### 4. QREVENT — Complejidad: MEDIA-ALTA
- Multiples bloques (QRScanner, CelebremosQrCheckIn, ReservationScanner, SundayServiceRegistration).
- Integracion con Steps de Rock, RegistrationInstance, SQL custom (stored procedures).
- Uso de ZXing via vendor bundle (pipeline de build especial).
- Constantes hardcodeadas en el backend (`ATTR_ID_ASISTIO = 8400`, `ATTR_ID_FECHA = 8401`, `StepProgramIdCelebremos = 5`).
- **Archivos:** `Rock.Blocks/QREVENT/`, `src/QREVENT/`, `Dev Tools/Sql/QREVENT_SundayService_Hardening.sql`

### 5. Security (Autenticacion) — Complejidad: ALTA / ZONA DELICADA
- Modificaciones al core de autenticacion de Rock (PasswordlessAuthentication, RemoteAuthenticationSession).
- Muchos archivos `.obs` traducidos al espanol (login, accountEntry, confirmAccount, etc.).
- Bloque nuevo `VRSimpleRegistration` en desarrollo activo (sin commit).
- **Impacto critico:** un error puede dejar a todos los usuarios sin acceso.
- **Archivos:** `Rock/Security/Authentication/`, `Rock.Blocks/Security/`, `src/Security/`

### 6. DAR (Donaciones) — Complejidad: MUY ALTA
- El modulo mas complejo del proyecto.
- Dos pasarelas de pago (Cybersource via REST, ePay Visanet via SOAP).
- Multi-moneda, reCAPTCHA, validacion de tarjetas, normalizacion de datos.
- Recargo por cuotas ePay con `FeeCoverageAmount` (logica distribuida en plugin + Rock.Blocks + Rock.Model).
- **Impacto critico:** maneja dinero real. Requiere pruebas exhaustivas en sandbox antes de production.
- **Archivos:** `Rock.Blocks/Dar/`, `src/Dar/`, `Plugin.EpayVisanetGateway/`, `Plugin.CybersourceInlineRestGateway/`

### 7. Event/RegistrationEntry y Crm/FamilyPreRegistration — Complejidad: ALTA
- Modificaciones al upstream de Rock (no bloques propios).
- i18n ES/EN con diccionarios en `utils.partial.ts`.
- Interaccion con DatePicker del framework base.
- Flujo de registro con pago (integracion con pasarelas via `GatewayControl`).
- **Archivos:** `src/Event/RegistrationEntry/`, `src/Crm/FamilyPreRegistration/`, `Rock.Blocks/Event/RegistrationEntry.cs`, `Rock/Model/Event/Registration/`
