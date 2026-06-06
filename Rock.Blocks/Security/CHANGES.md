# Rock.Blocks/Security — Cambios y personalizaciones VidaReal

## Estado de la rama: `hotfix-18.1`

---

## 1. Login.cs — Modificacion de archivo original de Rock

### Que se cambio

Archivo: `Rock.Blocks/Security/Login.cs`
Lineas modificadas: 3 (adicion de una propiedad en un selector LINQ)

**Diff exacto (git diff HEAD):**

```diff
-                    Text = p.FullName
+                    Text = p.FullName,
+                    Category = p.PhotoUrl
```

### Contexto del cambio

En el metodo que procesa el resultado de autenticacion passwordless (login sin contrasena), cuando el codigo OTP es valido pero el correo o telefono coincide con **multiples personas** en Rock, el bloque devuelve una lista de personas para que el usuario elija cual es el.

La lista se construye como `List<ListItemBag>` donde cada item tiene:
- `Value` = el `State` cifrado de la persona (para identificarla en el siguiente paso)
- `Text` = el nombre completo (`FullName`)
- `Category` = **[NUEVO]** la URL de la foto de perfil (`PhotoUrl`)

### Por que se hizo

El frontend Obsidian del bloque Login (en `Rock.JavaScript.Obsidian.Blocks/src/Security/Login/`) muestra esta lista al usuario para que seleccione su cuenta. El campo `Category` en `ListItemBag` es un campo de uso libre que se reutiliza aqui para transportar la URL de la foto sin necesidad de crear un ViewModel personalizado.

El objetivo es que la pantalla de seleccion de persona muestre la foto de perfil junto al nombre, mejorando la experiencia de identificacion cuando hay cuentas duplicadas (caso comun en iglesias con familias).

### Impacto

- Cambio compatible hacia atras: `Category` es nullable, el frontend lo ignora si viene vacio.
- No afecta el flujo de autenticacion ni la logica de seguridad.
- Solo afecta la presentacion en el paso `PersonSelectionRequired` del login passwordless.

### Origen

Modificacion de VidaReal sobre el archivo original de Rock. El archivo base es `SparkDevNetwork/Rock` rama `hotfix-18.1`.

---

## 2. VRSimpleRegistration.cs — Bloque nuevo de VidaReal

### Que es

Archivo: `Rock.Blocks/Security/VRSimpleRegistration.cs`
Estado git: sin trackear (nuevo, no commiteado)

Bloque Obsidian de **registro simplificado** para nuevos usuarios que llegaron via autenticacion passwordless pero **no tienen cuenta en Rock**.

Este bloque cubre el flujo que en el Rock original seria manejado por `AccountEntry.cs` (bloque de registro completo). VidaReal lo reimplemento de forma mas simple y controlada.

### Cuando se usa

1. El usuario intenta iniciar sesion con email o telefono (passwordless).
2. El codigo OTP es valido.
3. Rock no encuentra ninguna persona con ese email/telefono (`IsRegistrationRequired = true`).
4. El bloque Login redirige a la pagina de registro con el `?State=<token_cifrado>`.
5. `VRSimpleRegistration` recibe ese token, valida la sesion y muestra el formulario de registro.

### Seguridad

El bloque **bloquea el acceso directo**. Si no viene un `State` valido en el query string (token cifrado de una sesion passwordless activa y no usada), devuelve `IsBlocked = true` y redirige al login. Esto previene que cualquiera abra la pagina de registro sin haber pasado por la validacion OTP.

La validacion usa `PasswordlessAuthentication.GetDecryptedAuthenticationState()` y `RemoteAuthenticationSessionService.VerifyRemoteAuthenticationSession()`.

### ViewModel de inicializacion: VRSimpleRegistrationInitBox

Enviado al frontend al cargar la pagina:

| Campo | Tipo | Descripcion |
|---|---|---|
| `IsBlocked` | bool | Si es true, el frontend debe redirigir al login |
| `Email` | string | Email ya validado por passwordless (prellenado, no editable) |
| `PhoneNumber` | string | Telefono ya validado (si aplica) |
| `PhoneCountryCode` | string | Codigo pais del telefono |
| `CountryCodes` | List&lt;string&gt; | Codigos disponibles: `+502`, `+503`, `+1` |
| `DefaultCountryCode` | string | `+502` (Guatemala) |
| `State` | string | Token cifrado a reenviar en el submit |
| `IsCampusPickerShown` | bool | Siempre true (VidaReal siempre muestra campus) |
| `Campuses` | List&lt;VRCampusItem&gt; | Lista de campus activos de Rock |
| `LoginPageUrl` | string | URL del login para el enlace "volver" |
| `ReturnUrl` | string | URL de retorno tras registro exitoso |

### Accion de servidor: Register(VRRegisterRequestBag)

Endpoint `[BlockAction]` invocado desde el frontend al enviar el formulario.

**Parametros recibidos (`VRRegisterRequestBag`):**

| Campo | Tipo | Descripcion |
|---|---|---|
| `FirstName` | string | Nombre del usuario |
| `LastName` | string | Apellido |
| `Gender` | int | Genero (enum Rock: 0=Unknown, 1=Male, 2=Female) |
| `Email` | string | Email (puede venir del form si no estaba en el State) |
| `PhoneNumber` | string | Telefono (puede venir del form) |
| `PhoneCountryCode` | string | Codigo pais del telefono |
| `CampusGuid` | string | GUID del campus seleccionado |
| `State` | string | Token cifrado de la sesion passwordless |
| `ReturnUrl` | string | URL de retorno (enviada desde el form) |

**Logica de la accion:**

1. Valida el `State` — si expirado, retorna `ActionBadRequest`.
2. Verifica la sesion `RemoteAuthenticationSession` — si ya usada o invalida, retorna error.
3. Construye una entidad `Person` con:
   - Nombre y apellido del form
   - Email: prioriza el del State (ya validado), luego el del form
   - Telefono: prioriza el del State, luego el del form
   - `RecordStatus` = Active
   - `ConnectionStatus` = Participant
   - `RecordType` = Person
4. Guarda la persona con `PersonService.SaveNewPerson()` asignando campus.
5. Agrega el telefono movil si existe.
6. Crea un `UserLogin` de tipo `PasswordlessAuthentication` (External) confirmado.
7. Marca la sesion remota como completada con `CompleteRemoteAuthenticationSession()`.
8. Resuelve la URL de retorno (prioridad: `bag.ReturnUrl` > `returnurl` query param > `ReturnPage` atributo > `/`).
9. Retorna `ActionOk` con `RedirectUrl`.

**Respuesta (`VRRegisterResponseBag`):**

```json
{ "isSuccess": true, "redirectUrl": "/page-de-destino" }
```

### Atributos de bloque configurables

| Key | Descripcion | Requerido |
|---|---|---|
| `ReturnPage` | Pagina de destino tras registro exitoso | No |
| `LoginPage` | Pagina de login (para redirigir si el state fallo) | No |

### Codigos pais soportados

Actualmente hardcodeados en `GetObsidianBlockInitialization()`:
- `+502` Guatemala (default)
- `+503` El Salvador
- `+1` Estados Unidos / Canada

### GUIDs registrados

```
EntityTypeGuid: E0AE2775-BFB2-4F28-A7C3-9FC968C42A86
BlockTypeGuid:  61C805E0-F228-4DCA-9934-3F12FEC67C7D
```

Estos GUIDs son de VidaReal y deben registrarse en la base de datos Rock via migracion o manualmente en `EntityType` y `BlockType`.

### Archivo frontend pendiente

El bloque requiere un archivo `.obs` en:
```
Rock.JavaScript.Obsidian.Blocks/src/Security/vrSimpleRegistration.obs
```
Este archivo Vue/Obsidian debe:
- Llamar `GetObsidianBlockInitialization` al montar y leer `VRSimpleRegistrationInitBox`
- Si `IsBlocked == true`, redirigir a `LoginPageUrl`
- Mostrar formulario: nombre, apellido, genero, email/telefono (prellenados), campus
- En submit, invocar la accion `register` con `VRRegisterRequestBag`
- Si respuesta exitosa, redirigir a `RedirectUrl`

### Diferencia con AccountEntry.cs (Rock original)

| Aspecto | AccountEntry.cs (Rock) | VRSimpleRegistration.cs (VidaReal) |
|---|---|---|
| Complejidad | Alto — multiples pasos, duplicados, confirmacion email | Minimo — un solo paso |
| Deteccion de duplicados | Si, con flujo de seleccion | No — asume persona nueva |
| Confirmacion de email | Opcional (configurable) | No — la sesion passwordless ya valido |
| Campus | Opcional | Siempre visible |
| Configuracion | Muchos atributos | Solo 2 atributos |
| Uso de State OTP | No | Si — es la fuente de verdad del email/telefono |

---

## Archivos sin modificar en Security/

Los siguientes archivos son Rock original sin cambios de VidaReal:

- `AccountEntry.cs` — Registro completo multi-paso
- `ConfirmAccount.cs` — Confirmacion de cuenta via email
- `ForgotUserName.cs` — Recuperacion de usuario
- `LoginHistory.cs` — Historial de intentos de login
- `UserLoginList.cs` — Administracion de logins
- `RestKeyDetail.cs` / `RestKeyList.cs` — API keys
- `SecurityChangeAuditList.cs` — Auditoria de seguridad
- `Oidc/` — Bloques de OpenID Connect
