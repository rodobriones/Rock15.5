# Correo del Pase Digital — notas de la plantilla

Plantilla: [`correo-pase-digital.html`](correo-pase-digital.html). Se pega tal cual en el cuerpo
de una comunicación de Rock.

> ⚠️ **Lava parsea el contenido de los comentarios HTML.** Un `{% raw %}{% if %}{% endraw %}` de
> ejemplo dentro de `<!-- -->` truena con *"Lava Error: Invalid 'if' tag"*. Por eso esta
> documentación vive aquí y no en la cabecera del `.html`. Si se documenta algo dentro de la
> plantilla, que sea sin llaves.

## Migración desde MinistryPass

| | Filtro |
|---|---|
| Antes (plugin MinistryPass) | `{% raw %}{{ Person | GetMinistryPassUrl:1 }}{% endraw %}` |
| Ahora (módulo Wallet propio) | `{% raw %}{{ Person | WalletPassUrl:'f0a1b2c3-d4e5-4f60-8a01-940000000002' }}{% endraw %}` |

El plugin viejo recibía el **id** de su plantilla; el nuestro recibe el **Guid** de la
`WalletTemplate`.

### Cuál Guid

| Plantilla | Guid | Uso |
|---|---|---|
| **VidaAventura** | `f0a1b2c3-d4e5-4f60-8a01-940000000002` | ✅ el pase de la persona. QR = Alternate Id (el que lee el check-in); "ASISTO A:" = su campus. La migración 016 lo generalizó como pase digital de la iglesia — el nombre en la tabla quedó viejo. |
| Entrada de evento | `f0a1b2c3-d4e5-4f60-8a01-940000000001` | boletos del módulo de Eventos; QR = código del boleto. |
| VidaReal - QR | `13947258-84fe-4470-a33e-35798158cc14` | creada a mano en el admin, **incompleta**: `Barcode.Message` vacío ⇒ QR sin contenido. No usar. |

El filtro emite el pase si la persona todavía no lo tiene y devuelve la URL de descarga:

```
{PublicApplicationRoot}/api/vidareal/wallet/v1/download/{serial}?token={token}
```

Esa URL **detecta el dispositivo sola**: entrega el `.pkpass` en iOS y redirige a Google Wallet
en Android. No hacen falta dos links.

Si algo no resuelve (persona sin PersonAlias primario, plantilla inactiva,
`PublicApplicationRoot` vacío) devuelve **cadena vacía** y no lanza excepción — por eso el botón
va dentro de un condicional: sin él saldría con `href` vacío.

## Sobre acortar el link: en este correo, NO

El botón dice "Descarga tu pase" — la URL no se ve. Acortarla no aporta nada y **debilita una
credencial**, porque el link *es* el acceso al pase:

| | Entropía | Generador |
|---|---|---|
| URL directa | serial + token = **2 GUID (256 bits)** | `Guid.NewGuid()` |
| Short link de Rock | 10 caracteres de un alfabeto de **25 letras** (~47 bits) | `System.Random` sembrado con `Guid.GetHashCode()` — **no criptográfico** |

Ver `PageShortLink.Logic.cs`: `alphaCharacters` son 25 letras (sin dígitos, sin la O) y `_random`
es un `System.Random`. Sirve para links de campaña; no para una credencial.

### Y jamás un token derivado del Person.Id

Un token tipo `pase-{Person.Id}` produce `personas.vidareal.tv/pase-123`: **enumerable**.
Cualquiera prueba `pase-1`, `pase-2`, … y cosecha los pases de todas las personas. Descartado.

### Si hiciera falta acortarlo (WhatsApp, SMS, impreso)

Sólo donde la URL se ve. Con token **aleatorio** (nunca derivado) y `siteId` explícito, y sabiendo
que se acumula una fila de `PageShortLink` por render:

```
{% raw %}{% assign paseUrl = Person | WalletPassUrl:'f0a1b2c3-d4e5-4f60-8a01-940000000002' %}
{% if paseUrl != '' %}
  {% assign paseUrl = paseUrl | CreateShortLink:'',3 %}
{% endif %}{% endraw %}
```

El `{% raw %}{% if %}{% endraw %}` NO es opcional: encadenar `WalletPassUrl | CreateShortLink`
directo revienta con *"Entity Validation Error: [PageShortLink] The Url field is required"*
cuando el primero devuelve cadena vacía.

### Por qué el sitio 3 y no el 1 (si se acorta)

Parece al revés — el sitio correcto es uno **sin dominios**:

- `Site.DefaultDomainUri` toma el primer `SiteDomain` por `Order`. El sitio **1 (Rock RMS)** tiene
  `localhost` en Order 0 (`personas.vidareal.tv` está en Order 2): daría `http://localhost/TOKEN`.
- Un sitio **sin dominios** cae al `PublicApplicationRoot` (`https://personas.vidareal.tv/`).
  El **3** además tiene `EnabledForShortening = true`.

Sin `siteId` Rock tampoco elige bien: el fallback de `LavaFilters.cs` ordena
`OrderBy( s => s.EnabledForShortening )` — ascendente, o sea los **no** habilitados primero, al
revés de lo que dice su propio comentario. Por eso los 6 short links del pase que ya existen
quedaron en *Rock Check-in*; funcionan sólo porque ese sitio no tiene dominios.

## Antes de enviar

- Probar con **una** persona, desde *Send Test* de una comunicación (no desde el editor de
  plantillas: ahí no hay `Person` en contexto y todo sale vacío, incluido el saludo).
- Abrirlo **desde el teléfono**: la URL decide por dispositivo. En un navegador de escritorio el
  `.pkpass` se descarga como archivo suelto.
- **La URL lleva el token de autenticación del pase en el querystring**: quien tenga ese link
  descarga el pase de esa persona. El correo es personal y no debe reenviarse a grupos.
- El texto dice "tu check-in y el de tu familia" porque el pase ya es de toda la iglesia. Si el
  envío es sólo a papás de VidAventura, el texto original ("registrar a tus hijos") es más
  específico y funciona igual.

## Atributos globales que usa (verificados)

| Key | Valor |
|---|---|
| `PublicApplicationRoot` | `https://personas.vidareal.tv/` |
| `walletpassimg` | `Content/WALLETPASSOF.jpg` |
| `EmailFooterLogo` | `Content/VIDAREAL.TV_LogoEvangelica-ng.png` |
| `IconoInstagramnegro` … `IconoXnegro` | `Content/icons-07.png` … `icons-12.png` |
