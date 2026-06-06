# Estructura del proyecto Rock.Blocks — VidaReal fork de Rock 18.1

Rama: `hotfix-18.1`  
Base de comparacion: commit `ca2ca0ec94`

---

## Configuracion del proyecto (.csproj)

- **Target Framework:** .NET 4.7.2 (`net472`)
- **Language Version:** C# 8.0
- **CopyToRockWeb:** `True` (los DLLs se copian automaticamente a `RockWeb/Bin`)

### Dependencia nueva: EPPlus

```xml
<Reference Include="EPPlus">
    <HintPath>..\libs\EPPlus\EPPlus.dll</HintPath>
</Reference>
```

EPPlus es una libreria para leer y escribir archivos Excel (.xlsx). Se agrego para soportar exportacion a Excel en los bloques de VidaReal (probablemente en `DonationDashboard` o `SundayServiceRegistration`).

---

## Bloques VidaReal (nuevos, no en el Rock original)

### `Dar/` — Donaciones

| Archivo | Clase | Descripcion |
|---|---|---|
| `Dar/CybersourceDonationEntry.cs` | `CybersourceDonationEntry` | Bloque de entrada de donacion con gateway Cybersource Inline REST |
| `Dar/DonationDashboard.cs` | `DonationDashboard` | Dashboard de historial y seguimiento de donaciones del feligres |

### `QREVENT/` — Check-in por codigo QR

| Archivo | Clase | Descripcion |
|---|---|---|
| `QREVENT/QRScanner.cs` | `QRScanner` | Componente base de escaneo QR via camara del dispositivo |
| `QREVENT/CelebremosQrCheckIn.cs` | `CelebremosQrCheckIn` | Check-in QR para eventos "Celebremos" (VidAventura) |
| `QREVENT/ReservationScanner.cs` | `ReservationScanner` | Validacion de reservaciones de eventos via codigo QR |
| `QREVENT/SundayServiceRegistration.cs` | `SundayServiceRegistration` | Registro para el servicio dominical del domingo |

### `FamilyHub/` — Hub Familiar

| Archivo | Clase | Descripcion |
|---|---|---|
| `FamilyHub/FamilyHub.cs` | `FamilyHub` | Vista centralizada del nucleo familiar: perfil, grupos, eventos, relaciones conocidas |

### `LayoutCustom/` — Componentes de layout del sitio

| Archivo | Clase | Descripcion |
|---|---|---|
| `LayoutCustom/Header.cs` | `Header` | Header global personalizado del sitio VidaReal |
| `LayoutCustom/Footer.cs` | `Footer` | Footer global personalizado del sitio VidaReal |

### `Security/VRSimpleRegistration.cs` — Registro simplificado VidaReal

| Archivo | Clase | Descripcion |
|---|---|---|
| `Security/VRSimpleRegistration.cs` | `VRSimpleRegistration` | Flujo de registro simplificado de usuario, adaptado para el portal VidaReal |

---

## Bloques del core de Rock (originales, modificados en este fork)

### `Security/`
Todos los bloques de autenticacion del core fueron modificados para el flujo VidaReal:
- `AccountEntry.cs` — registro de cuenta nueva
- `ConfirmAccount.cs` — confirmacion de cuenta via email
- `ForgotUserName.cs` — recuperacion de nombre de usuario
- `Login.cs` — inicio de sesion (incluyendo passwordless/OTP)
- `LoginHistory.cs` — historial de logins
- Oidc: `AuthClientDetail.cs`, `AuthClientList.cs`, `AuthScopeDetail.cs`, `AuthScopeList.cs`
- `RestKeyDetail.cs`, `RestKeyList.cs`
- `SecurityChangeAuditList.cs`, `UserLoginList.cs`

### `Event/RegistrationEntry.cs`
Bloque de registro a eventos, modificado para integracion con los gateways de pago VidaReal (Cybersource, Epay Visanet).

---

## Bloques del core de Rock (sin modificar en este fork)

A continuacion la lista completa de bloques del core de Rock que estan presentes en el proyecto pero no han recibido modificaciones especificas de VidaReal. Estos son bloques estandar de SparkDevNetwork/Rock 18.1.

### `Administration/`
- `ExternalApplicationList.cs`
- `PageProperties.cs`
- `SystemConfiguration.cs`

### `AI/`
- `AIAgentDetail.cs`, `AIAgentList.cs`
- `AIProviderDetail.cs`, `AIProviderList.cs`
- `AISettings.cs`
- `AISkillDetail.cs`, `AISkillList.cs`, `AISkillToolList.cs`
- `ChatBot.cs`

### `BulkImport/`
- `BulkImportTool.cs`

### `Bus/`
- `BusStatus.cs`, `ConsumerList.cs`, `QueueDetail.cs`, `QueueList.cs`

### `CheckIn/`
- `AttendanceHistoryList.cs`, `AttendanceList.cs`
- `CheckInKiosk.cs`, `CheckInKioskSetup.cs`, `CheckInScheduleBuilder.cs`
- `CloudPrintMonitor.cs`
- `Config/CheckinTypeDetail.cs`
- `Configuration/CheckInLabelDetail.cs`, `CheckInLabelList.cs`, `CheckInSimulator.cs`, `LabelDesigner.cs`

### `Cms/`
- AdaptiveMessage (Detail, List, AdaptationDetail)
- BlockType (Detail, List)
- ContentChannel (Detail, ItemList, List, TypeDetail, TypeList)
- ContentCollection (Detail, View)
- `EmailForm.cs`, `FileAssetManager.cs`
- LavaApplication (Content, Detail, List)
- LavaEndpoint (Detail, List), LavaShortcode (Detail, List)
- Layout (BlockList, Detail, List)
- `LibraryViewer.cs`, `LogSettings.cs`
- MediaAccount (Detail, List), MediaElement (Detail, List), MediaFolder (Detail, List)
- Page (List, RouteDetail, RouteList, Search, ShortLinkClickList, ShortLinkDetail, ShortLinkDialog, ShortLinkList)
- PersistedDataset (Detail, List)
- Personalization (SegmentList, PersonalLinkList, PersonalLinkSectionDetail, PersonalLinkSectionList)
- RequestFilter (Detail, List), Site (Detail, List), `ThemeDetail.cs`

### `Communication/`
- Chat (Configuration, View)
- CommunicationDetail, CommunicationEntry, CommunicationEntryWizard
- CommunicationFlow (Detail, InstanceMessageMetrics, List, Performance)
- CommunicationList, CommunicationSaturationReport, CommunicationTemplateDetail
- `EmailPreferenceEntry.cs`, `NcoaProcess.cs`
- SmsConversations, SmsPipelineList
- Snippet (Detail, TypeDetail)
- SystemCommunication (List, Preview), SystemPhoneNumber (Detail)

### `Connection/`
- `ConnectionOpportunitySignup.cs`

### `Core/`
- AssetStorageProvider (Detail, List)
- AttributeMatrixTemplate (Detail, List), `Attributes.cs`, `AuditList.cs`
- AutomationTrigger (Detail, List)
- BinaryFile (Detail, List), BinaryFileType (Detail, List)
- Campus (Detail, List), `CategoryDetail.cs`
- DefinedType (Detail, List), DefinedValue (List)
- Device (Detail, List), DocumentType (Detail, List)
- EntitySearch (Detail, List), `EventList.cs`
- FollowingEventType (Detail), FollowingSuggestionType (List)
- `InteractionChannelDetail.cs`
- Location (Detail, List), `LogViewer.cs`
- Notes, NoteType (Detail, List), NoteWatch (Detail, List), `NotificationMessageList.cs`
- PersonFollowingList, PersonSignalList, PersonSuggestionList
- RestAction (List), RestController (List)
- ScheduleCategoryExclusionList, Schedule (Detail, List), ScheduledJob (HistoryList, List), `ServiceJobDetail.cs`
- SignalType (List), SignatureDocument (Detail, List), SignatureDocumentTemplate (Detail, List)
- Suggestion (Detail), Tag (Detail, List, Report)

### `Crm/`
- Assessment (List, TypeDetail, TypeList)
- Badge (Detail, List), `Disc.cs`, `FamilyPreRegistration.cs`
- `NamelessPersonList.cs`
- PersonDetail (Badges, GivingConfiguration)
- `PersonDuplicateDetail.cs`, `PersonMergeRequestList.cs`
- Photo (OptOutDetail, Upload, Verify), `SignalTypeDetail.cs`

### `Engagement/`
- AchievementAttempt (Detail, List), AchievementType (Detail, List)
- `CampaignList.cs`, `ConnectionOpportunityList.cs`, `ConnectionTypeList.cs`
- SignUp (AttendanceDetail, Detail, Finder, Register)
- StepParticipant (List), StepProgram (Detail, List), Steps/StepFlow
- StepType (Detail, List), Streak (Detail, List, MapEditor), StreakType (Detail, ExclusionDetail, ExclusionList, List)

### `Event/`
- EventCalendar (Detail, ItemList), EventItem (Detail, OccurrenceList)
- InteractiveExperiences (ExperienceManager, ExperienceManagerOccurrences, ExperienceVisualizer, InteractiveExperienceDetail, LiveExperience, LiveExperienceOccurrences)
- `RegistrationListLava.cs`
- RegistrationInstance (ActiveList, FeeList, LinkageList, List, PaymentList)

### `Example/`
- `ControlGallery.cs`, `FieldTypeGallery.cs`, `ObsidianGalleryList.cs`

### `Finance/`
- BenevolenceRequest (List), BenevolenceType (Detail, List)
- Business (ContactList, Detail, List)
- FinancialAccount (Detail, List), FinancialBatch (Detail, List)
- FinancialGateway (Detail, List), FinancialPersonBankAccountList
- FinancialPersonSavedAccountDetail, FinancialPledge (Detail, Entry, List)
- FinancialScheduledTransactionList
- FinancialStatementTemplate (Detail, List)
- FundraisingDonationList, FundraisingList
- `SavedAccountList.cs`, `VolunteerGenerosityAnalysis.cs`

### `Group/`
- `GroupArchivedList.cs`, `GroupAttendanceDetail.cs`, `GroupMemberList.cs`
- `GroupMemberScheduleTemplateDetail.cs`, `GroupPlacement.cs`, `GroupRegistration.cs`
- GroupRequirementType (Detail, List), `GroupTypeList.cs`
- Scheduling (GroupMemberScheduleTemplateList, GroupScheduler, GroupScheduleToolbox)

### `Lms/`
- LearningClass (ActivityCompletionDetail, ActivityCompletionList, ActivityDetail, AnnouncementDetail, ContentPageDetail, Detail, List)
- LearningCourse (Detail, List), LearningGradingSystem (Detail, List, ScaleDetail, ScaleList)
- `LearningParticipantDetail.cs`
- LearningProgramCompletion (Detail, List), LearningProgram (Detail, List)
- LearningSemester (Detail, List)
- Public (PublicLearningClassEnrollment, ClassWorkspace, CourseDetail, CourseList, ProgramList)

### `Mobile/`
- `CheckIn/CheckIn.cs`, `MobileLayoutDetail.cs`

### `Prayer/`
- PrayerComment (List), PrayerRequest (Detail, Entry, List)

### `Reporting/`
- `DynamicData.cs`, `Insights.cs`
- InteractionComponent (Detail), InteractionDetail
- MergeTemplate (Detail, List), `MetricValueDetail.cs`, `PageParameterFilter.cs`
- `PersistedDataViewList.cs`, `PowerBiAccountRegister.cs`
- `ReportList.cs`, `ServiceMetricsEntry.cs`, `TithingOverview.cs`

### `Tv/`
- AppleTv (AppDetail, PageDetail, PageList), RokuApplication (Detail), RokuPage (Detail)
- `TvApplicationList.cs`, `TvPageList.cs`

### `Utility/`
- `RealTimeDebugger.cs`, `RealTimeVisualizer.cs`, `SmsTestTransport.cs`, `StarkDetail.cs`

### `WebFarm/`
- `WebFarmNodeDetail.cs`, `WebFarmNodeLogList.cs`, `WebFarmSettings.cs`

### `WorkFlow/`
- FormBuilder (FormBuilderDetail, FormTemplateDetail, y helpers)
- `WorkflowEntry.cs`, `WorkflowList.cs`, `WorkflowTriggerDetail.cs`

---

## Clases de infraestructura (no son bloques UI)

| Archivo | Descripcion |
|---|---|
| `RockDetailBlockType.cs` | Clase base para bloques de detalle |
| `RockEntityDetailBlockType.cs` | Clase base para bloques de detalle de entidad |
| `RockEntityListBlockType.cs` | Clase base para bloques de lista de entidad |
| `RockListBlockType.cs` | Clase base para bloques de lista |
| `RockObsidianBlockType.cs` | Clase base para bloques Obsidian |
| `RockObsidianDetailBlockType.cs` | Clase base para bloques de detalle Obsidian |
| `ServerSentEventsBlockActionResult.cs` | Result para streaming Server-Sent Events |
| `ExtensionMethods/` | Metodos de extension utilitarios del proyecto |

---

## Resumen de conteo

| Categoria | Cantidad de archivos .cs |
|---|---|
| Bloques nuevos VidaReal (Dar, QREVENT, FamilyHub, LayoutCustom, VRSimpleRegistration) | 10 |
| Bloques del core de Rock | ~230 |
| Clases de infraestructura y helpers | ~10 |
| **Total** | **~250** |
