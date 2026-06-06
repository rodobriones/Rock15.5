# FamilyHub — Contexto y cambios (VidaReal fork)

## 1. Qué es FamilyHub

FamilyHub es un bloque Obsidian **completamente nuevo**, creado para VidaReal. No existe en el repositorio upstream de SparkDevNetwork/Rock. Permite al usuario autenticado ver y administrar, desde el portal web, los miembros de su familia principal y las personas relacionadas por Known Relationship.

- Backend: `Rock.Blocks/FamilyHub/FamilyHub.cs` — namespace `Rock.Blocks.FamilyHub`, categoría `Custom`.
- Frontend: `Rock.JavaScript.Obsidian.Blocks/src/FamilyHub/FamilyHub.obs`
- Documentación extendida (arquitectura, DTOs, flujos): `Rock.Blocks/FamilyHub/FamilyHub.md`

---

## 2. Diferencias con FamilyPreRegistration (bloque original de Rock)

| Aspecto | FamilyPreRegistration (Rock) | FamilyHub (VidaReal) |
|---|---|---|
| Propósito | Pre-registro anónimo o autenticado de una familia nueva | Gestión post-registro del propio núcleo familiar del usuario logueado |
| Autenticación | Opcional | Obligatoria (rechaza con `ActionBadRequest` si no hay `CurrentPerson`) |
| Alcance | Crea una familia nueva | Lee y edita la familia primaria existente + KR del usuario |
| Estado civil (marital) | No gestionado | Soportado vía `DefinedValue` configurable — aplica `MaritalStatusValueId` a ambas personas |
| Relaciones conocidas (KR) | No gestionado | Lista de roles `GroupTypeRole` de `KnownRelationship` configurable por bloque |
| Foto de perfil | No | Sí — con pipeline de optimización (EXIF + resize 1200px + JPEG 82%) |
| Teléfono | Campo simple | Código país separado + número limpio (`PhoneNumber.CountryCode` string + `PhoneNumber.Number`) |
| Eliminación de miembros | Soportada | Intencionalmente omitida — se canaliza por email a soporte |
| UI | WebForms / Obsidian genérico | Tarjetas CSS con design tokens `--vr-*`, responsive, forzado a light-mode |

---

## 3. Funcionalidades añadidas (sobre el modelo base de Rock)

### 3.1 Gestión de parentesco dual

El bloque maneja dos tipos de relación en un único campo `relationshipValue` del frontend:

- **Known Relationship (KR):** valor numérico (`GroupTypeRoleId`). El bloque crea o actualiza entradas en el grupo `KnownRelationship` del usuario owner.
- **Estado civil (Marital):** valor prefijado `"marital:<guid>"` del `DefinedValue`. Al seleccionarlo, se aplica `MaritalStatusValueId` en ambos perfiles y el cónyuge se incorpora a la familia como Adulto (rol `forceAdultRole: true`).

El dropdown de parentesco combina ambas listas con un separador visual `"— Estado civil —"`.

### 3.2 Lógica de membresía familiar inteligente

- `AddPersonToFamily` determina el rol (Adult/Child) por edad (`BirthDate.Age() < 18`) o por flag explícito (`preferChildRole`, `forceAdultRole`).
- `RemoveFamilyMembershipsExcept` garantiza que una persona pertenezca a una sola familia primaria.
- `EnsurePersonHasPrimaryFamily` crea una familia nueva para personas que quedan huérfanas (ej. tras moverlas a KR-only).
- `DeactivateFamilyGroupIfNoActiveMembers` desactiva grupos familiares vacíos.

### 3.3 Pipeline de foto de perfil

1. El usuario sube la foto en el `ImageEditor` de Rock (flujo de crop nativo).
2. El backend recibe `photoBinaryFileGuid` o `photoBinaryFileId` y llama `ApplyPhotoFromBag`.
3. `OptimizeProfileImageBinaryFile` corrige la orientación EXIF, redimensiona a máximo 1200px y guarda como JPEG calidad 82.
4. El archivo se marca como no-temporal y se asigna como `Person.PhotoId`.

### 3.4 Separación de teléfono en código país + número

- El frontend envía `phoneCountryCode` (ej. `"502"`) y `phoneNumber` (solo dígitos).
- `SaveMobilePhoneV2` guarda en `PhoneNumber.CountryCode` (string) y `PhoneNumber.Number` (via `PhoneNumber.CleanNumber`).
- El default de código país es `"502"` (Guatemala).

### 3.5 Autorización por scope

`CanEditPerson` solo permite editar personas que:
1. Sean miembros activos de la familia primaria del usuario, O
2. Sean miembros activos del `KnownRelationship` group donde el usuario es Owner.

---

## 4. Configuración del bloque (BlockAttributes)

| Attribute Key | Tipo | Descripción |
|---|---|---|
| `AvailableKnownRelationshipRoles` | `CustomEnhancedListField` | Roles KR disponibles (por ID, GUID o nombre). Si vacío, se usan todos excepto Owner. Pobla el dropdown de parentesco. |
| `AvailableMaritalStatusOptions` | `CustomEnhancedListField` | GUIDs de `DefinedValue` del tipo marital `b4b92c3f-a935-40e1-a00b-ba484ead613b`. Si vacío, no aparece la sección marital. |

---

## 5. Fix de KnownRelationship (bugs corregidos)

El archivo `FamilyHub.md` (junto al `.cs`) contiene la documentación completa de bugs corregidos. Los principales:

### 5.1 KR residual al asignar estado civil (Bug 1 — Alta)
Al cambiar el parentesco a estado civil (`"marital:<guid>"`), los Known Relationships previos (ej. "Tío", "Primo") no se limpiaban. La persona conservaba roles contradictorios (KR + marital simultáneamente).

**Corrección:** `SaveMember` (Path A — Marital) ahora llama `SaveKnownRelationshipFromMeToPerson(..., null, ...)` después de aplicar el marital, lo que elimina cualquier KR previo hacia esa persona.

### 5.2 MaritalStatus residual al volver a KR (Bug 1b — Alta)
Al cambiar de estado civil a Known Relationship (Path B), `MaritalStatusValueId` persistía en ambos perfiles. La lógica anterior dependía de `GetMaritalRelationshipValue`, que sólo retornaba no-null si el target seguía siendo Adulto activo en la familia Y la opción marital seguía habilitada en la configuración del bloque. Si alguna de esas condiciones fallaba, el marital quedaba residual.

**Corrección:** Path B ahora limpia `MaritalStatusValueId` directamente si el valor presente está en `GetConfiguredMaritalDefinedValueIds()`. El `currentPerson` se limpia solo si su marital coincidía con el del target (evita romper otra relación pre-existente).

### 5.3 Cónyuge queda como Child (Bug 1c — Alta)
Al pasar de KR a estado civil con una persona que venía con `BirthDate < 18` o con GroupMember previo como Child, quedaba como Child en la familia del cónyuge.

**Corrección:** Se agregó el parámetro `forceAdultRole` a `AddPersonToFamily`. `ApplyMaritalStatus` lo invoca con `forceAdultRole: true`. Un cónyuge siempre queda como Adulto.

### 5.4 N+1 queries en GetPeopleMerged (Bug 2 — Alta)
Por cada miembro del grupo familiar se ejecutaban 3-4 queries adicionales. Una familia de 10 personas generaba ~40 roundtrips.

**Corrección:** Batching — una sola query para teléfonos, una para roles KR, `MaritalStatusValueId` e `IsAdultInCurrentFamily` proyectados en la query principal de familia. Resultado: 5 queries totales independiente del tamaño.

### 5.5 Código duplicado en persistencia de foto (Bug 3 — Media)
La lógica de guardar foto aparecía dos veces (una en Path A, otra en Path B).

**Corrección:** Extraído a `ApplyPhotoFromBag(rockContext, person, bag)` — se invoca una sola vez al final de `SaveMember`.

### 5.6 NPE en GetFamilyGroupTypeId (Bug 4 — Media)
7 llamadas a `GroupTypeCache.GetFamilyGroupType().Id` sin null-guard.

**Corrección:** Helper `GetFamilyGroupTypeId()` que retorna `int?`; todos los callers hacen early-return si es null.

---

## 6. Archivos de contexto relacionados

- `Rock.Blocks/FamilyHub/FamilyHub.md` — documentación extendida de arquitectura generada durante el desarrollo.
- El archivo `FamilyHub_KnownRelationship_Fix_Context.md` mencionado en la instrucción no fue encontrado en el raíz del repositorio (puede haber sido eliminado o su ruta es diferente).
- El archivo `Migration_Context_ReservationScanner_FamilyHub.md` tampoco fue encontrado en el raíz.

---

## 7. Commits relevantes

| Commit | Descripción |
|---|---|
| `2958358094` | Push inicial — FamilyHub base |
| Commits intermedios | Evolución del UI y lógica KR |
| `4f80ff56b0` | BUGS y WA — fix de los 3 bugs de KR + batching |
