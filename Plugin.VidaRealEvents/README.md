# Plugin.VidaRealEvents — Migraciones del módulo Eventos/Boletería

Proyecto de **solo migraciones** (`Rock.Plugin.Migration`, assembly `com.vidareal.Events`) del módulo
custom de eventos/boletería de VidaReal. El código del módulo vive en el core del fork
(`Rock/Model/Eventos/`, `Rock.Blocks/Eventos/`, `Rock.JavaScript.Obsidian.Blocks/src/Eventos/`);
solo las migraciones van en plugin para que sean upgrade-safe y reproducibles.

Documentación del módulo: **`Rock/Model/Eventos/ARCHITECTURE.md`** (mapa de capas — leer primero) ·
`docs/eventos-custom/RESEARCH_Y_PLAN.md` (doc maestro, historial de sesiones en §9.x) ·
`docs/eventos-custom/SMOKE_TESTS.md` (runbook de pruebas runtime).

## Migración consolidada para producción (2026-07-02)

El assembly expone **UNA sola migración**: `017_ProductionSetup.cs` (`[MigrationNumber(17, "18.1")]`),
que ejecuta EN ORDEN los 17 pasos históricos de abajo dentro de una sola transacción (todo-o-nada).
Los archivos 001–017 siguen en el folder pero **ya no llevan `[MigrationNumber]`**: son "pasos" que
solo corren a través de la consolidada — el SQL que corre en producción es byte-idéntico al que
construyó la instancia de desarrollo.

**Deploy a producción (instancia limpia):** copiar `com.vidareal.Events.dll` a `RockWeb/Bin` (junto
con los demás DLLs del fork), reciclar el app pool, y verificar:
```sql
SELECT MigrationNumber, MigrationName FROM [PluginMigration] WHERE PluginAssemblyName LIKE '%vidareal.Events%';
-- Esperado: una fila — 17 / ProductionSetup
```

**En dev no pasa nada:** Rock registra cada número individualmente y dev ya tiene 1–17 en
`[PluginMigration]` ⇒ la consolidada (nº 17) se salta.

> ⚠️ **La PRÓXIMA migración debe numerarse 18 (o mayor).** Nunca reutilizar 1–16: correrían en
> producción (que solo registró el 17) pero no en dev, y las instancias divergirían.

## Pasos históricos (ejecutados por la 017 consolidada, en este orden)

| # | Archivo | Qué hace |
|---|---|---|
| 001 | `001_EventsSetup.cs` | Crea las 6 tablas base `_com_vidareal_Events_*` (Event, TicketType, PromoCode, Order, Ticket, CheckinLog) con FKs, índices y UNIQUEs. |
| 002 | `002_AddEntityForeignColumns.cs` | Agrega `ForeignId/ForeignGuid/ForeignKey` que 001 omitió (Entity\<T\> las mapea; sin ellas toda consulta falla). |
| 003 | `003_EventsPages.cs` | Páginas internas (Eventos, Admin, Scanner) y externas (Checkout, Mis Entradas), rutas, bloques, seguridad base, block-setting Checkout Page. |
| 004 | `004_EventsCleanupHoldsSp.cs` | SP `sp_VidaRealEventsCleanupExpiredHolds` (limpieza de holds expirados; llamar con `@Now = RockDateTime.Now`). |
| 005 | `005_AddEventHeaderStyle.cs` | Columna `Event.HeaderStyle` (hero persistente/condensado del checkout). |
| 006 | `006_AddEventCategory.cs` | Columna `Event.Category` (badge del hero). |
| 007 | `007_AddTicketQrBinaryFileType.cs` | BinaryFileType seguro (`RequiresViewSecurity`) para los QR de tickets. |
| 008 | `008_PolishEventsPages.cs` | Page Menu del landing, seguridad del contenedor, checkout fuera del nav, nombres en español. |
| 009 | `009_EventsReportPage.cs` | Página Reportería (`eventos/reporteria`) + bloque Event Report. |
| 010 | `010_EventsMenuSection.cs` | Sección intermedia "Boletería" (flyout del menú interno clickeable). |
| 011 | `011_EventStaffAssignments.cs` | Tabla `EventStaff` (persona↔evento, `CanScan`/`CanViewReport`) + páginas Scanner/Reportería a View-autenticados + Edit Rock Administration en Reportería. |
| 012 | `012_OnlyAdminsFullAccess.cs` | Acceso total SOLO `RSR - Rock Administration`: borra el Edit de Staff Workers en Scanner (003) y Reportería. |
| 013 | `013_AdminPageAdministrate.cs` | Allow explícito ADMINISTRATE a Rock Administration en la página Admin (gestionar Permisos exige ADMINISTRATE; es deny-por-defecto). |
| 014 | `014_AttendeeQuestions.cs` | Preguntas al asistente: columnas `TicketType.QuestionsJson` + `Ticket.AnswersJson` + categoría de Person Attributes "Preguntas de Eventos" (el catálogo maestro). |
| 015 | `015_QuestionCatalogPage.cs` | Página "Catálogo de Preguntas" (`eventos/preguntas`) + bloque Question Catalog (CRUD de preguntas y plantillas; plantillas en System Setting `com_vidareal_EventQuestionTemplates`). |
| 016 | `016_FixCategoryGuidAndCatalogSecurity.cs` | Corrige el guid de la categoría "Preguntas de Eventos" (30xx→35xx; colisionaba con la página Eventos) + Edit explícito Admins/Staff en la página del catálogo. |
| 017 | `017_PromoCodeUniqueAndMaintenanceJob.cs` | Índice **UNIQUE `(EventId,Code)`** en PromoCode (dedupe defensivo + reemplaza el IX no único; cierra la carrera del `.Any()`) + registra el ServiceJob **`Rock.Jobs.EventsMaintenance`** (cron cada 5 min, INSERT idempotente por Guid): limpia holds expirados (`sp_...CleanupExpiredHolds @Now`) y reconcilia órdenes atascadas en `Charging`. |

## Migraciones posteriores a la consolidada (corren por sí solas, en dev Y en producción)

| # | Archivo | Qué hace |
|---|---------|----------|
| 018 | `018_EventSessions.cs` | Columna `Event.SessionsJson`: agenda de sesiones para eventos de varios días/horarios (JSON `[{Date,Start,End,Label}]`). Null = evento de un solo bloque. |
| 019 | `019_OrderDeliveryEmail.cs` | Columna `Order.DeliveryEmail`: correo al que se envían las entradas (elegido en el paso de pago; null = perfil del comprador). |
| 020 | `020_EventVisibilityAndCalendar.cs` | Columnas `Event.Visibility` (0=Público/1=Privado/2=Con contraseña) + `Event.AccessPassword`; BlockType **Event Calendar** + página pública `eventos/calendario`; cablea "Checkout Page" del calendario y "Calendar Page" del checkout (botón "Volver al inicio"). |
| 021 | `021_EventWorkflows.cs` | Workflow launcher: columnas `RegistrationWorkflowTypeId` + `CheckinWorkflowTypeId` en `Event` Y `TicketType` (INT **sin FK** a propósito: un WorkflowType borrado solo deja de lanzarse). Inscripción = orden pagada (se lanza por ticket); check-in = ingreso Ok. El estado "Archivado" del evento es solo enum (sin SQL). |
| 022 | `022_EventSpeakersBannerMinistry.cs` | Campos de presentación del rediseño 2026: `Event.Ministry` (área que organiza — DISTINTO de `Category`, que es el badge del hero; guarda el TEXTO para que el calendario filtre sin joins) + DefinedType **"Ministerios de Eventos"** con 6 valores; `Event.BannerBinaryFileId` (banner apaisado del paso 1, FK sin cascade; null cae a la imagen del hero); `Event.SpeakersJson` (ponentes del paso 1, mismo criterio que `SessionsJson`). Los tres opcionales: sin llenarlos, ningún evento cambia de aspecto. |
| 023 | `023_EventLogo.cs` | `Event.LogoBinaryFileId`: logo cuadrado del ministerio, a la izquierda del badge y el título en el hero del checkout. **Tercera** imagen del evento — ver la tabla de abajo. **La próxima migración debe ser la 24+.** |

### Las tres imágenes de un evento

| Columna | Dónde se dibuja |
|---|---|
| `ImageBinaryFileId` | portada del hero del checkout, header condensado, tarjetas del calendario y de Mis Entradas |
| `BannerBinaryFileId` | banner apaisado del paso 1 del checkout (022) |
| `LogoBinaryFileId` | logo sobre la portada, en el hero (023) |


## Modelo de permisos (desde 011–013)

- **RSR - Rock Administration**: ve todo (scanner y reportería) y es el único que gestiona la vista **Permisos** de Event Admin.
- **RSR - Staff Workers**: gestiona eventos (Edit en la página Admin) pero para escanear/ver reportes necesita asignación.
- **Cualquier usuario autenticado**: solo los eventos asignados en `EventStaff` (rutas directas `eventos/scanner` y `eventos/reporteria`).

## Build y deploy

```bash
dotnet build Plugin.VidaRealEvents/VidaRealEvents/VidaRealEvents.csproj
cp Plugin.VidaRealEvents/VidaRealEvents/bin/Debug/net472/com.vidareal.Events.dll RockWeb/Bin/
# El DLL NO se autocopia. Reciclar el app pool de Rock => corren las migraciones pendientes.
```

### ⚠️ El plugin NO viaja solo: son CUATRO DLLs, y el orden importa

Desde la 022 el modelo `Event` tiene columnas que sólo existen si las migraciones corrieron.
Si se despliega `Rock.dll` y se recicla SIN el plugin, **toda consulta a `Event` truena** —
venta de boletos incluida.

| DLL | Qué lleva | Si falta |
|---|---|---|
| `com.vidareal.Events.dll` | las migraciones (crea las columnas) | `Rock.dll` pide columnas inexistentes: el módulo entero cae |
| `Rock.dll` | modelo `Event`, `EventSpeakerService` | el servidor no sabe de los campos nuevos |
| `Rock.ViewModels.dll` | `EventBag` (`BannerUrl`, `Speakers`, `LogoUrl`, `SessionTimes`) | **`MissingMethodException: set_BannerUrl`** al abrir el checkout |
| `Rock.Blocks.dll` | los block actions | los bloques no responden |

Regla: **copiar las cuatro y recién entonces reciclar, una sola vez.** Las migraciones corren en
el arranque, antes de atender la primera petición, así que las columnas existen para cuando el
modelo nuevo las pida. Los `.obs.js` de `RockWeb/Obsidian/Blocks/Eventos/` son estáticos y
pueden ir antes o después.

Y respaldar la base antes: el plugin toca el esquema. Las migraciones traen `Down()`, pero
revertir con gente comprando no es algo para improvisar.
