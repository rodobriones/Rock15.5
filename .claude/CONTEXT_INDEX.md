# CONTEXT_INDEX.md — Indice Maestro de Contexto (Rock18.1 VidaReal)

> **LEER ESTE ARCHIVO PRIMERO en cualquier sesion nueva de Claude Code.**
> Contiene el mapa completo de toda la documentacion de contexto del repositorio,
> ordenada por modulo y con instrucciones de cuando leer cada archivo.

---

## Como usar este indice

1. Leer este archivo para saber que contexto existe.
2. Ir directamente al archivo del modulo en el que se va a trabajar.
3. Leer `PROJECT_CONTEXT.md` si necesitas contexto tecnico transversal (stack, convenciones, estructura).
4. Leer `CHANGES.md` (raiz) si necesitas el historial de commits o el mapa de archivos nuevos vs modificados.

---

## Archivos de contexto — por modulo

### Contexto General del Repositorio

| Archivo | Proposito | Cuando leerlo |
|---|---|---|
| `C:\Repos\Rock18.1\.claude\PROJECT_CONTEXT.md` | Contexto tecnico completo: stack, modulos, convenciones, areas delicadas, estructura de directorios | Al inicio de cualquier sesion. Referencia tecnica principal. |
| `C:\Repos\Rock18.1\CHANGES.md` | Historia de commits VidaReal, lista de archivos nuevos vs modificados del upstream, instrucciones de build | Cuando necesitas saber que cambio, cuando se hizo, o que archivos son de VidaReal vs upstream. |

---

### Modulo QREVENT (Eventos con QR / Check-in)

| Archivo | Proposito | Cuando leerlo |
|---|---|---|
| `C:\Repos\Rock18.1\QREVENT_QRScanner_Migration_Context.md` | Detalle de la migracion de `QRScanner` de Rock 15.5.1 a Rock 18.1: backend, frontend, integracion ZXing via vendor entry | Al trabajar en `QRScanner.cs` o `qrScanner.obs`, o al entender como funciona la build de ZXing. |
| `C:\Repos\Rock18.1\QREVENT_CelebremosQrCheckIn_Migration_Context.md` | Migracion de `CelebremosQrCheckIn` de Rock 15.5.1 a Rock 18.1: tipo de bloque, imports, ZXing | Al trabajar en `CelebremosQrCheckIn.cs` o `CelebremosQrCheckIn.obs`. |
| `C:\Repos\Rock18.1\Migration_Context_ReservationScanner_FamilyHub.md` | Migracion de `ReservationScanner` y `FamilyHub` de Rock 15.5.1 a Rock 18.1 en un mismo documento | Al trabajar en `ReservationScanner.obs` o al auditar las migraciones de estos dos bloques. |
| `C:\Repos\Rock18.1\Rock.JavaScript.Obsidian.Blocks\src\QREVENT\vendor\CHANGES.md` | Que es ZXing, por que es vendor (no npm directo), version, y como se usa en los bloques QREVENT | Al entender el pipeline de build de ZXing, reportar warnings de sourcemap, o al agregar nuevos bloques que necesiten QR. |
| `C:\Repos\Rock18.1\Dev Tools\Sql\CHANGES.md` | Que hace `QREVENT_SundayService_Hardening.sql`, cuando ejecutarlo, que tablas/constraints/procedures crea, si es idempotente | Antes de deployar QREVENT por primera vez, o al diagnosticar errores de integridad en tablas `SundayService*`. |

**Bloques QREVENT (referencia rapida):**
- `Rock.Blocks/QREVENT/QRScanner.cs` + `src/QREVENT/qrScanner.obs` — Lista de eventos y escaneo QR
- `Rock.Blocks/QREVENT/CelebremosQrCheckIn.cs` + `src/QREVENT/CelebremosQrCheckIn.obs` — Check-in Steps para grupo Celebremos
- `Rock.Blocks/QREVENT/ReservationScanner.cs` + `src/QREVENT/ReservationScanner.obs` — Scanner de reservaciones
- `Rock.Blocks/QREVENT/SundayServiceRegistration.cs` + `src/QREVENT/SundayServiceRegistration.obs` — Registro dominical
- `Rock.JavaScript.Obsidian.Blocks/src/QREVENT/vendor/zxing.lib.ts` — Entry point del vendor bundle ZXing

---

### Modulo FamilyHub (Portal Familiar)

| Archivo | Proposito | Cuando leerlo |
|---|---|---|
| `C:\Repos\Rock18.1\FamilyHub_KnownRelationship_Fix_Context.md` | Bug y fix de `KnownRelationship` bidireccional en FamilyHub: logica de roles inversos, escenarios cubiertos, rediseno visual del bloque | Al trabajar en `FamilyHub.cs` o `FamilyHub.obs`, especialmente si hay problemas con relaciones bidireccionales. |
| `C:\Repos\Rock18.1\Migration_Context_ReservationScanner_FamilyHub.md` | Migracion de FamilyHub de Rock 15.5.1 a Rock 18.1: tipo de bloque, imports, tsconfig wiring | Al entender como FamilyHub fue migrado o al revisar la configuracion de build del modulo. |

**Bloque FamilyHub (referencia rapida):**
- `Rock.Blocks/FamilyHub/FamilyHub.cs` + `src/FamilyHub/FamilyHub.obs`
- `Rock.JavaScript.Obsidian.Blocks/src/FamilyHub/tsconfig.json` — Configuracion TS del modulo

---

### Modulo Eventos / Registration Entry (Upstream modificado)

| Archivo | Proposito | Cuando leerlo |
|---|---|---|
| `C:\Repos\Rock18.1\AI_HANDOFF_ROCK18_EVENT_CRM.md` | Contexto completo de i18n en Event/RegistrationEntry y Crm/FamilyPreRegistration: reglas de idioma, decisiones de DatePicker, pitfalls Vue, template Lava recomendado para SuccessText | Al trabajar en cualquier aspecto de registro de eventos, internacionalizacion ES/EN, o DatePicker. Contiene el prompt base para iniciar nueva IA en este modulo. |

**Archivos clave (referencia rapida):**
- `Rock.JavaScript.Obsidian.Blocks/src/Event/registrationEntry.obs` — Shell del flujo
- `Rock.JavaScript.Obsidian.Blocks/src/Event/RegistrationEntry/utils.partial.ts` — Diccionario i18n + utilidades
- `Rock.JavaScript.Obsidian.Blocks/src/Crm/familyPreRegistration.obs` — Pre-registro familiar
- `Rock.JavaScript.Obsidian.Blocks/src/Crm/FamilyPreRegistration/utils.partial.ts` — Diccionario i18n CRM

---

### Modulo DAR (Donaciones)

| Archivo | Proposito | Cuando leerlo |
|---|---|---|
| `C:\Repos\Rock18.1\EPAY_FLOW_SUMMARY.md` | Flujo completo de cobro ePay con cuotas y recargos: SOAP, `FeeCoverageAmount`, calculo de balance, checklist de despliegue | Al trabajar en el plugin ePay, en `RegistrationEntry.cs` (guardado de transacciones), o en `RegistrationService.cs` (calculo de pagos). |

**Archivos clave (referencia rapida):**
- `Rock.Blocks/Dar/CybersourceDonationEntry.cs` + `src/Dar/CybersourceDonationEntry.obs` — Formulario de donacion
- `Rock.Blocks/Dar/DonationDashboard.cs` + `src/Dar/DonationDashboard.obs` — Dashboard de donaciones
- `Plugin.EpayVisanetGateway/EpayVisanetGateway/EpayVisanetGateway.cs` — Gateway ePay Guatemala
- `Plugin.CybersourceInlineRestGateway/` — Gateway Cybersource
- `Rock.Blocks/Dar/EmailTemplates/confirmacion-donacion.html` — Template de confirmacion de donacion

---

### Modulo Security (Autenticacion — Upstream modificado, ZONA DELICADA)

No hay archivos de contexto separados para Security. Usar `PROJECT_CONTEXT.md` que documenta todos los archivos modificados.

**Archivos clave (referencia rapida):**
- `Rock/Security/Authentication/PasswordlessAuthentication.cs` — DELICADO: proveedor passwordless
- `Rock.Blocks/Security/Login.cs` — Mapeo de personas, modificado con `Category = p.PhotoUrl`
- `Rock.JavaScript.Obsidian.Blocks/src/Security/` — Todos los bloques de autenticacion traducidos al espanol
- `Rock.Blocks/Security/VRSimpleRegistration.cs` — NUEVO (sin commit): registro simplificado post-passwordless
- `Rock.JavaScript.Obsidian.Blocks/src/Security/vrSimpleRegistration.obs` — NUEVO (sin commit)

---

### SQL de Configuracion y Hardening

| Archivo | Proposito | Cuando leerlo |
|---|---|---|
| `C:\Repos\Rock18.1\Dev Tools\Sql\CHANGES.md` | Descripcion de todos los scripts SQL de VidaReal en este directorio | Al necesitar ejecutar scripts SQL en un ambiente nuevo o diagnosticar problemas de integridad de datos. |
| `C:\Repos\Rock18.1\Dev Tools\Sql\QREVENT_SundayService_Hardening.sql` | Script SQL real de hardening: constraints, indice unico y stored procedures para SundayService | Al deployar QREVENT por primera vez o al diagnosticar errores en reservaciones. |

---

### Configuracion de Claude Code

| Archivo | Proposito | Cuando leerlo |
|---|---|---|
| `C:\Repos\Rock18.1\.claude\settings.json` | Permisos globales de Claude Code para este proyecto: `dotnet build`, directorio adicional de dependencias Cybersource | Si hay errores de permiso al ejecutar herramientas. |
| `C:\Repos\Rock18.1\.claude\settings.local.json` | Permisos locales adicionales: MSBuild, python, grep, PowerShell GUIDs, WebFetch Facebook | Si hay errores de permiso con comandos especificos de la maquina local. |

---

## Orden recomendado de lectura para un Claude Code nuevo

1. **Este archivo** (`CONTEXT_INDEX.md`) — ya lo estas leyendo.
2. `.claude/PROJECT_CONTEXT.md` — contexto tecnico base.
3. `CHANGES.md` (raiz) — historial y mapa de archivos.
4. El archivo de contexto del **modulo especifico** en el que se va a trabajar (ver tabla de cada modulo arriba).

**No es necesario leer todos los archivos de contexto en cada sesion.** Solo leer los relevantes al area de trabajo.

---

## Estado del repositorio (2026-06-04)

- **Branch activo:** `hotfix-18.1`
- **Ultimo commit:** `4f80ff56b0` — "BUGS y WA" (13 de mayo de 2026)
- **Archivos sin commit (nuevos, sin seguimiento):**
  - `Rock.Blocks/Security/VRSimpleRegistration.cs`
  - `Rock.JavaScript.Obsidian.Blocks/src/Security/vrSimpleRegistration.obs`
- **Archivos con cambios sin commitear (modified):** Ver `git status` para lista actual.
