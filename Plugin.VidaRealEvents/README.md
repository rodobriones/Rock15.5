# Plugin.VidaRealEvents — Migraciones del módulo Eventos/Boletería

Proyecto de **solo migraciones** (`Rock.Plugin.Migration`, assembly `com.vidareal.Events`) del módulo
custom de eventos/boletería de VidaReal. El código del módulo vive en el core del fork
(`Rock/Model/Eventos/`, `Rock.Blocks/Eventos/`, `Rock.JavaScript.Obsidian.Blocks/src/Eventos/`);
solo las migraciones van en plugin para que sean upgrade-safe y reproducibles.

Documentación del módulo: **`Rock/Model/Eventos/ARCHITECTURE.md`** (mapa de capas — leer primero) ·
`docs/eventos-custom/RESEARCH_Y_PLAN.md` (doc maestro, historial de sesiones en §9.x) ·
`docs/eventos-custom/SMOKE_TESTS.md` (runbook de pruebas runtime).

## Migraciones

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
