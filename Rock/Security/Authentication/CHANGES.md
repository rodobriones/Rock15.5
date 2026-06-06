# Authentication — Cambios VidaReal (hotfix-18.1)

Este documento describe los cambios realizados por VidaReal sobre el subsistema de autenticación del core de Rock CMS. Los cambios son modificaciones en el working tree (sin commit propio en este branch) sobre la base del upstream `hotfix-18.1` de Spark Development Network.

---

## 1. PasswordlessAuthentication.cs

**Archivo:** `Rock/Security/Authentication/PasswordlessAuthentication.cs`
**Lineas cambiadas:** ~60 (6 bloques independientes)
**Impacto:** Alto — es el proveedor central de autenticacion sin contrasena

### 1.1 Traduccion de mensajes de error al espanol

Rock original entrega todos los mensajes de error en ingles. VidaReal los traduce al espanol para que el usuario final (hablante de espanol) vea mensajes comprensibles en la interfaz.

| Metodo / contexto | Mensaje original (ingles) | Mensaje VidaReal (espanol) |
|---|---|---|
| `Authenticate` — codigo invalido/expirado | `"Code invalid or expired"` | `"Codigo invalido o expirado"` |
| `Authenticate` — codigo invalido (segunda validacion) | `"Code is invalid"` | `"El codigo es invalido"` |
| `SendOneTimePasscode` — falta email/telefono | `"Please provide Email or Phone for passwordless login."` | `"Por favor proporciona un correo o telefono para iniciar sesion."` |
| `SendOneTimePasscode` — comunicacion inactiva | `"The Passwordless Login Confirmation system communication needs to be active..."` | `"La comunicacion de confirmacion de inicio de sesion sin contrasena debe estar activa."` |
| `SendOneTimePasscode` — SMS no pudo enviarse | `"Unable to send confirmation code. Make sure to use a mobile phone..."` | `"No fue posible enviar el codigo de confirmacion. Asegurate de usar un numero de telefono movil..."` |
| `SendOneTimePasscode` — destinatario SMS desuscrito | `"We're unable to send a confirmation code to the number provided. ..."` | `"No fue posible enviar el codigo de confirmacion al numero proporcionado. ..."` |
| `SendOneTimePasscode` — fallo general de envio | `"Verification code failed to send"` | `"No fue posible enviar el codigo de verificacion"` |
| `AuthenticateNewPasswordlessUser` — persona seleccionada invalida | `"The selected person is invalid"` | `"La persona seleccionada no es valida"` |

**Riesgo de regresion:** Bajo. Son cambios de cadena de texto. Si Spark actualiza estos mensajes en versiones futuras, el diff puede generar conflictos menores de merge.

---

### 1.2 Soporte de numeros de telefono con codigo de pais (cambio funcional — alto impacto)

**Metodo afectado:** `GetMatchingPeopleQuery` (privado, lineas ~650-693)

#### Problema que resuelve

El frontend de VidaReal (bloque Obsidian de login) envia numeros de telefono **siempre con codigo de pais** en formato `+<codigo><digitos>` (por ejemplo `+50212345678` para Guatemala). Sin embargo, los registros `PhoneNumber` en la base de datos de Rock pueden tener el numero almacenado:

- **Sin codigo de pais:** `Number = "12345678"`, `CountryCode = ""` (registros legado o importados)
- **Con codigo de pais separado:** `CountryCode = "502"`, `Number = "12345678"`

#### Implementacion original de Rock (eliminada)

```csharp
var phoneNumberService = new PhoneNumberService( rockContext );
// ...
var personIdsByPhoneNumber = phoneNumberService.GetPersonIdsByNumber( phoneNumber );
peopleQuery = peopleQuery.Where( p => personIdsByPhoneNumber.Contains( p.Id ) );
```

`PhoneNumberService.GetPersonIdsByNumber` hace una comparacion directa de la cadena limpia del numero. Si el frontend envia `50212345678` y la base de datos tiene `12345678`, no hay coincidencia -> el usuario no puede autenticarse aunque su numero este registrado.

#### Implementacion VidaReal (nueva)

```csharp
var digits = new string( ( phoneNumber ?? string.Empty ).Where( char.IsDigit ).ToArray() );
if ( digits.Length > 0 )
{
    var personIdsByPhoneNumber = new PhoneNumberService( rockContext ).Queryable().AsNoTracking()
        .Where( n =>
            // Almacenado con codigo de pais: "502" + "12345678" == "50212345678"
            ( n.CountryCode + n.Number ) == digits
            // Almacenado sin codigo de pais: "12345678" == "12345678"
            || n.Number == digits
            // Fallback para formatos parciales / legado
            || ( digits.Length >= 7 && n.Number.EndsWith( digits ) ) )
        .Select( n => n.PersonId )
        .Distinct();

    peopleQuery = peopleQuery.Where( p => personIdsByPhoneNumber.Contains( p.Id ) );
}
else
{
    // Si no hay digitos validos, no hacer match con nadie (evita falsos positivos)
    peopleQuery = peopleQuery.Where( p => false );
}
```

**Las tres condiciones de matching:**

1. `(n.CountryCode + n.Number) == digits` — cubre registros con codigo de pais separado en la columna `CountryCode`.
2. `n.Number == digits` — cubre registros donde el numero completo (incluyendo codigo de pais) esta en `Number` o donde el frontend envio sin codigo de pais.
3. `digits.Length >= 7 && n.Number.EndsWith( digits )` — fallback de sufijo para formatos legado donde el numero almacenado es un sufijo del numero enviado.

**Cambio adicional:** Se elimina la variable local `phoneNumberService` que se declaraba antes del bloque `if`. Ahora se instancia `PhoneNumberService` directamente dentro del bloque que lo necesita (solo cuando `phoneNumber` tiene contenido).

**Riesgo:** Medio-alto. La clausula `EndsWith` del fallback puede generar falsos positivos si dos personas tienen numeros que terminan en los mismos digitos. Se recomienda revisar si esta clausula es necesaria una vez que la base de datos este normalizada con codigos de pais.

---

### 1.3 Propagacion de PhotoUrl al resultado de seleccion de persona

**Metodo afectado:** `AuthenticateNewPasswordlessUser` (privado, lineas ~470-490)

Cuando multiples personas coinciden con el email o telefono proporcionado, Rock muestra una pantalla de seleccion de persona. VidaReal extendio este flujo para incluir la foto de perfil de cada persona, mejorando la UX.

#### Implementacion original de Rock

```csharp
var matchingPersonResults = matchingPeople
    .Select( p => new PasswordlessMatchingPersonState
    {
        PersonId = p.Id,
        FullName = p.FullName
    } )
    .Select( p => new MatchingPersonResult
    {
        State = GetEncryptedMatchingPersonState( p ),
        FullName = p.FullName
    } )
    .ToList();
```

El primer `.Select` proyectaba directamente a `PasswordlessMatchingPersonState`, perdiendo el objeto `Person` original (y con el, `PhotoUrl`). El segundo `.Select` solo podia leer lo que habia en `PasswordlessMatchingPersonState`.

#### Implementacion VidaReal

```csharp
var matchingPersonResults = matchingPeople
    .Select( p => new
    {
        State = new PasswordlessMatchingPersonState
        {
            PersonId = p.Id,
            FullName = p.FullName
        },
        p.PhotoUrl          // capturado del objeto Person antes de perderlo
    } )
    .Select( x => new MatchingPersonResult
    {
        State = GetEncryptedMatchingPersonState( x.State ),
        FullName = x.State.FullName,
        PhotoUrl = x.PhotoUrl   // propagado al resultado final
    } )
    .ToList();
```

Se introduce un tipo anonimo intermedio que preserva `PhotoUrl` del objeto `Person` antes de construir `PasswordlessMatchingPersonState`. Luego `MatchingPersonResult` recibe el valor. Este cambio requirio agregar la propiedad `PhotoUrl` a `MatchingPersonResult` (ver seccion 3).

---

## 2. OneTimePasscode/MatchingPersonResult.cs

**Archivo:** `Rock/Security/Authentication/OneTimePasscode/MatchingPersonResult.cs`
**Lineas cambiadas:** 5 lineas anadidas
**Impacto:** Bajo — solo agrega una propiedad al modelo de retorno

### Cambio

Se agrega la propiedad `PhotoUrl` a la clase `MatchingPersonResult`:

```csharp
/// <summary>
/// The photo URL of the matching person.
/// </summary>
public string PhotoUrl { get; set; }
```

### Por que

Esta propiedad es el complemento necesario del cambio 1.3 en `PasswordlessAuthentication.cs`. El flujo de seleccion de persona cuando hay multiples coincidencias devuelve una lista de `MatchingPersonResult` hacia el bloque Obsidian del frontend. Al agregar `PhotoUrl`, el frontend puede mostrar el avatar de cada persona en la pantalla de seleccion, haciendo mas facil para el usuario identificarse.

**Nota:** `MatchingPersonResult` esta marcada como `[RockInternal("1.15")]`, lo que significa que Spark no garantiza compatibilidad entre versiones para esta clase. Si Spark la modifica en un hotfix futuro, habra que resolver el conflicto manualmente.

---

## 3. RemoteAuthenticationSessionService.cs

**Archivo:** `Rock/Model/Security/RemoteAuthenticationSessionService.cs`
**Lineas cambiadas:** 5 lineas (4 eliminadas, 1 modificada)
**Impacto:** Medio — cambia el juego de caracteres del codigo OTP generado

### Cambio

**Metodo afectado:** `RandomString` (privado, linea ~159)

#### Implementacion original de Rock

```csharp
// Removed vowels to prevent bad words,
// the number nine to prevent other immature references,
// and other characters that can cause confusion.
const string AllowedChars = "BCDFGHJKLMNPRSTXZ245678";
```

Rock usa un juego de 23 caracteres alfanumericos (solo consonantes y algunos numeros) para evitar que el codigo generado forme palabras inapropiadas en ingles o cause confusion visual (0 vs O, 1 vs I, etc.).

#### Implementacion VidaReal

```csharp
const string AllowedChars = "0123456789";
```

VidaReal reemplaza el juego de caracteres por digitos del 0 al 9 exclusivamente.

### Por que

1. **Familiaridad del usuario:** Los usuarios latinoamericanos estan mas acostumbrados a codigos OTP de solo numeros (como los de bancos, WhatsApp, Google). Un codigo alfanumerico como `KZRP4B` genera friccion y errores de escritura.
2. **Entrada en teclado movil:** En dispositivos moviles, un codigo de solo numeros permite usar el teclado numerico directamente, reduciendo errores.
3. **Sin preocupacion por palabras inapropiadas en espanol:** El filtrado original estaba disenado para evitar palabras en ingles. Al usar solo numeros, el problema no aplica en ningun idioma.

**Efecto en seguridad:** El codigo sigue teniendo 6 caracteres (`GeneratedCodeLength = 6`). Con solo digitos el espacio de posibilidades baja de 23^6 (~148 millones) a 10^6 (1 millon). Esto es aceptable dado que:
- El codigo expira en el tiempo configurado en `PasswordlessSignInDailyIpThrottle`.
- Hay limite de intentos por IP (`ValidateIpCountWithinLimits`).
- El codigo se invalida tras el primer uso exitoso.

---

## Relacion entre los tres cambios

```
Frontend Obsidian (login.obs / credentialLogin.partial.obs)
        |
        | Envia: telefono "+50212345678", email, IP
        v
PasswordlessAuthentication.SendOneTimePasscode()
        |
        | Llama a:
        v
RemoteAuthenticationSessionService.StartRemoteAuthenticationSession()
        |
        | Genera codigo OTP con: "0123456789" (solo digitos)  <-- CAMBIO #3
        v
        [SMS/Email enviado al usuario]
        |
        | Usuario ingresa el codigo
        v
PasswordlessAuthentication.Authenticate()
        |
        | Valida codigo, busca personas con:
        v
GetMatchingPeopleQuery()
        |
        | Matching por telefono con/sin codigo de pais  <-- CAMBIO #1.2
        v
        [0 personas] -> error
        [1 persona]  -> autenticacion directa
        [N personas] -> pantalla de seleccion con nombre + foto  <-- CAMBIO #1.3 + #2
```

---

## Merge con upstream de Spark

Al recibir actualizaciones de Spark (nuevos hotfixes de la rama 18.x), los archivos que probablemente generen conflictos son:

| Archivo | Riesgo de conflicto | Que vigilar |
|---|---|---|
| `PasswordlessAuthentication.cs` | Alto | Cualquier cambio de Spark en `GetMatchingPeopleQuery`, `AuthenticateNewPasswordlessUser`, o los mensajes de error |
| `RemoteAuthenticationSessionService.cs` | Bajo | Cambios en `RandomString` o en `AllowedChars` |
| `MatchingPersonResult.cs` | Bajo | Cambios en las propiedades de la clase |

Estrategia recomendada al hacer merge: usar `git diff HEAD...origin/hotfix-18.1` para identificar que cambio Spark y verificar manualmente cada bloque antes de aceptar la version del upstream.
