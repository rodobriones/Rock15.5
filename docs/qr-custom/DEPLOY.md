# Módulo QR Generator — Runbook de despliegue

Arquitectura: `Rock/Model/Qr/ARCHITECTURE.md` · Pruebas manuales: `docs/qr-custom/SMOKE_TESTS.md`

**Fecha límite dura: 22 de octubre de 2026.** Ese día se corta la plataforma externa que este
módulo reemplaza. Todo QR impreso que hoy resuelve por esa plataforma deja de funcionar; los QR
nuevos tienen que estar emitidos bajo el dominio corto propio antes de esa fecha.

**Estado al 2026-09-28 (dev):** 2 códigos en el catálogo, ambos **estáticos**; **0 dinámicos** y
**0 escaneos** atribuidos al módulo. Los 577 escaneos que existen en `Interaction` son de short
links del módulo Wallet, no de éste. Es decir: **el camino dinámico completo —token, redirección,
escaneo, panel de uso— nunca ha corrido de punta a punta.** Ese es el riesgo del despliegue, no
los bugs.

---

## A. Artefactos y orden de despliegue

### A.0 Lo primero que hay que entender

**Este módulo NO es un plugin.** `Plugin.VidaRealQr` produce `com.vidareal.Qr.dll`, que contiene
**solo migraciones**. El módulo de verdad vive dentro del core del fork:

| Dónde vive el código | Qué produce |
|---|---|
| `Rock/Model/Qr/` (dominio: `QrCode`, `QrDesign`, `QrEnums`, `QrStaticPayloads`) y `Rock/Model/Qr/Services/` (`QrCatalogService`, `QrMetricsService`, `QrRenderService`) | **`Rock.dll`** |
| `Rock.Blocks/Qr/` (`QrCodeDetail`, `QrCodeList`, `QrMetrics`, `QrDashboard`) | **`Rock.Blocks.dll`** |
| `Rock.ViewModels/Blocks/Qr/` (los bags) | **`Rock.ViewModels.dll`** |
| `Rock.JavaScript.Obsidian.Blocks/src/Qr/*.obs` | **bundles** en `RockWeb/Obsidian/Blocks/Qr/` |
| `Plugin.VidaRealQr/VidaRealQr/Migrations/` | **`com.vidareal.Qr.dll`** |

Copiar solo el `com.vidareal.Qr.dll` **deja el sitio peor que antes**: las migraciones crean la
tabla, las páginas y los bloques, pero las clases `Rock.Blocks.Qr.*` no existen en el
`Rock.Blocks.dll` viejo y las páginas quedan vacías o tirando error.

**El error que delata un deploy incompleto:** un `MissingMethodException` o
`TypeLoadException` sobre un tipo `Rock.ViewModels.Blocks.Qr.*` significa que faltó
`Rock.ViewModels.dll`. Es la misma trampa que documenta `docs/eventos-custom/SMOKE_TESTS.md` §15.
Los DLLs del fork **van juntos o no van**.

### A.1 Lista exacta de artefactos

Desde la máquina de build, tras compilar `Rock.sln` en Release y luego el plugin
(ver `build_procedure_complete`):

| # | Artefacto | Origen en el repo | Destino en el servidor |
|---|---|---|---|
| 1 | `Rock.dll` | `RockWeb/Bin/Rock.dll` | `RockWeb/Bin/` |
| 2 | `Rock.Blocks.dll` | `RockWeb/Bin/Rock.Blocks.dll` | `RockWeb/Bin/` |
| 3 | `Rock.ViewModels.dll` | `RockWeb/Bin/Rock.ViewModels.dll` | `RockWeb/Bin/` |
| 4 | `qrCodeDetail.obs.js` (+ `.map`) | `RockWeb/Obsidian/Blocks/Qr/` | `RockWeb/Obsidian/Blocks/Qr/` |
| 5 | `qrCodeList.obs.js` (+ `.map`) | `RockWeb/Obsidian/Blocks/Qr/` | `RockWeb/Obsidian/Blocks/Qr/` |
| 6 | `qrMetrics.obs.js` (+ `.map`) | `RockWeb/Obsidian/Blocks/Qr/` | `RockWeb/Obsidian/Blocks/Qr/` |
| 7 | `qrDashboard.obs.js` (+ `.map`) | `RockWeb/Obsidian/Blocks/Qr/` | `RockWeb/Obsidian/Blocks/Qr/` |
| 8 | `com.vidareal.Qr.dll` | `Plugin.VidaRealQr/VidaRealQr/bin/Release/net472/` | `RockWeb/Bin/` |

`QRCoder.dll` **ya está** en `RockWeb/Bin` (el módulo Eventos lo usa para los QR de boletos). Si
en este servidor no estuviera, va también: `packages/QRCoder.1.3.9/lib/net40/QRCoder.dll`.

No hay archivos `.ascx`, ni `App_Code`, ni `.cs` sueltos que copiar. Este módulo **no toca
`App_Code`**, así que no aplica la trampa de `appcode_deploy_prod` (copiar un `.cs` en caliente
tumba el sitio).

### A.2 Orden

El orden importa por una sola razón: **cualquier copia a `RockWeb/Bin` recicla el app pool**, y
el reciclo es lo que dispara las migraciones del plugin. Si el `com.vidareal.Qr.dll` entra antes
que los DLLs del core, las migraciones corren contra un core que todavía no tiene el modelo.

1. **Ventana y respaldo.** Fuera de horario de servicio. **Respaldo full de la base ANTES de
   nada** — las migraciones 001 y 003 crean tabla, páginas, bloques, categoría y un grupo de
   seguridad; no hay un `Down()` probado end-to-end.
2. **Detener el sitio** (IIS: Stop del sitio, no solo del app pool). Evita que un request entre
   con media mezcla de DLLs.
3. **Copiar los bundles Obsidian** (#4–#7) a `RockWeb/Obsidian/Blocks/Qr/` (crear la carpeta si
   no existe). No reciclan nada; van primero para que el navegador nunca pida un bundle que no
   está.
4. **Copiar los tres DLLs del core** (#1–#3) a `RockWeb/Bin` — **los tres en la misma pasada**.
5. **Copiar `com.vidareal.Qr.dll`** (#8) a `RockWeb/Bin`.
6. **Arrancar el sitio** y abrirlo una vez para forzar el arranque de Rock (las migraciones de
   plugin corren en el arranque, no en el primer request a la página del módulo).
7. **Verificar que las 7 migraciones quedaron registradas** (§C.1).
8. **Configurar el sitio del dominio corto** (§B) — *antes* de que nadie cree un código dinámico.
9. **Meter gente en el grupo de seguridad** (§E).
10. **Correr el smoke test bloqueante** (`docs/qr-custom/SMOKE_TESTS.md`).

### A.3 Por qué `Plugin.VidaRealQr` NO se agregó a `Rock.sln`

Se verificó el 2026-09-28: **`Rock.sln` no contiene NINGÚN proyecto `Plugin.*`** — ni
`Plugin.VidaRealEvents`, ni `Wallet`, ni `DupDetect`, ni `Translator`, ni los gateways. Los 45
proyectos de la solución son todos del core del fork. Agregar el de QR sería el único plugin
adentro, contra la convención del repo, y además **rompería el build**:

`Plugin.VidaRealQr/VidaRealQr/VidaRealQr.csproj` referencia `..\..\RockWeb\Bin\Rock.dll`,
`Rock.Common.dll` y `Rock.Enums.dll` por `HintPath` — es decir, referencia la **salida** de
`Rock.sln`. Dentro de la misma solución eso es un ciclo de orden de build: MSBuild no sabe que
tiene que compilar `Rock.csproj` primero (no hay `ProjectReference`), y en un checkout limpio
—con `RockWeb/Bin` vacío, porque está en `.gitignore`— el plugin falla por referencias no
resueltas. Es exactamente el motivo por el que los otros siete plugins se compilan en un **paso
2 separado**, después de `Rock.sln`.

**La acción correcta no es tocar el `.sln`, es corregir el procedimiento de build**, que hoy
enumera 7 plugins y tiene que enumerar 8:

```powershell
$plugins = @(
    @{Path="Plugin.VidaRealEvents\VidaRealEvents";                     DLL="com.vidareal.Events.dll"},
    @{Path="Plugin.VidaRealWallet\VidaRealWallet";                     DLL="com.vidareal.Wallet.dll"},
    @{Path="Plugin.VidaRealDupDetect\VidaRealDupDetect";               DLL="com.vidareal.DupDetect.dll"},
    @{Path="Plugin.VidaRealTranslator\VidaRealTranslator";             DLL="com.vidareal.Translator.dll"},
    @{Path="Plugin.VidaRealQr\VidaRealQr";                             DLL="com.vidareal.Qr.dll"},      # <-- NUEVO
    @{Path="Plugin.OdooEventSale\OdooEventSale";                       DLL="OdooEventSale.dll"},
    @{Path="Plugin.EpayVisanetGateway\EpayVisanetGateway";             DLL="EpayVisanetGateway.dll"},
    @{Path="Plugin.CybersourceInlineRestGateway\CybersourceInlineRestGateway"; DLL="CybersourceInlineRestGateway.dll"}
)
```

El `.csproj` es SDK-style, así que los archivos de migración nuevos (incluida la **007**) se
incluyen solos: no hay que editar el `.csproj` al agregar migraciones.

---

## B. Configuración del sitio del dominio corto

### ⚠️⚠️ LA TRAMPA DEL `Order` — EL RIESGO NÚMERO UNO DEL PROYECTO ⚠️⚠️

# Lo que se imprime dentro del QR es `Site.DefaultDomainUri` + el token.

Y `DefaultDomainUri` es, literalmente (`Rock/Model/CMS/Site/Site.cs`, líneas 745-766):

```csharp
string protocol = this.RequiresEncryption ? "https://" : "http://";
string host = this.SiteDomains.OrderBy( d => d.Order ).Select( d => d.Domain ).FirstOrDefault();
```

# Es el PRIMER SiteDomain por `Order`. No el "principal", no el del certificado: el de `Order` más bajo.

**Esto no es hipotético. Así está dev hoy** (consultado 2026-09-28):

| SiteId | Site | Order | Domain |
|---|---|---|---|
| 1 | Rock RMS | **0** | **`localhost`** |
| 1 | Rock RMS | 1 | `20.55.2.175` |
| 1 | Rock RMS | 2 | `personas.vidareal.tv` |

Y el sitio «Rock RMS» tiene `EnabledForShortening = 1` y `RequiresEncryption = 0`.

Si alguien apunta el block setting **«Sitio del dominio corto»** a «Rock RMS», entonces:

- `DefaultDomainUri` = **`http://localhost`**;
- **cada QR impreso codifica `http://localhost/<token>`**;
- la vista previa en pantalla **se ve perfecta** (el que la mira está en esa máquina);
- el `curl` desde el servidor **también responde 302**;
- y el error aparece **cuando el material ya está impreso** y alguien lo escanea con un teléfono
  que no es el servidor. Un teléfono resolviendo `localhost` va a sí mismo.

**Un QR mal impreso no se parcha con un deploy. Se reimprime.** Por eso esta sección es
obligatoria y va antes de crear el primer código dinámico.

El bloque ya bloquea el caso de `EnabledForShortening` apagado (devuelve un error explícito al
guardar), pero **no puede saber que el dominio del `Order 0` es el equivocado** — cualquiera de
esos tres es un dominio legítimo para el sitio interno. La defensa es de configuración, no de
código.

### B.1 Lo que hay que crear: un sitio DEDICADO

**No reutilizar «Rock RMS» ni «External Website».** El módulo necesita un sitio propio, cuya
única razón de existir es ser el dueño de los tokens del dominio corto.

`Admin Tools > CMS Configuration > Sites > + Add Site`:

- **Name:** `Dominio Corto QR` (o el que se acuerde; se selecciona por nombre en el block setting).
- **Domain(s):** **UNO SOLO.** El dominio corto de producción y nada más. Si por alguna razón
  tiene que haber más de uno, **el correcto va en `Order = 0`** y se verifica con la consulta de
  §B.3 — no de memoria, no mirando la pantalla de Rock, que lista los dominios sin mostrar el
  `Order`.
- **Theme / Layout:** el mínimo que Rock permita. El sitio no sirve páginas: solo redirige.
- **Default Page:** una página cualquiera existente. Sin `DefaultPageId` el route handler tira
  `SystemException("Invalid Site Configuration")` cuando llega un token que no existe, en vez de
  un 404 limpio.

### B.2 Las cinco banderas, qué hacen y qué pasa si faltan

| Bandera | Valor | Qué hace | Qué pasa si falta |
|---|---|---|---|
| **`EnabledForShortening`** | **1** | Es la puerta de entrada. En `RockRouteHandler` la rama entera de short links está dentro de un `if ( site.EnabledForShortening )`. | **404 MUDO.** El handler **ni siquiera busca el token**: no consulta `PageShortLink`, no loguea nada, no hay excepción, no hay rastro en `Exception List`. El QR impreso simplemente lleva a una página de error. El síntoma («el QR no sirve») no apunta a ningún lado. Es el peor modo de falla del módulo, y la razón por la que el bloque se niega a acuñar tokens bajo un sitio sin esta bandera. |
| **`RequiresEncryption`** | **1** | Fuerza `https://` en `DefaultDomainUri`. | El QR codifica `http://`. Cámaras de iOS y Android muestran aviso de sitio no seguro antes de abrirlo; muchos usuarios no siguen. Y quedó **impreso en `http`**. |
| **`EnableExclusiveRoutes`** | **1** | Limita las rutas del sitio a las suyas: una petición a este dominio no cae en rutas de otro sitio. | Un token que coincida con el nombre de una ruta de otro sitio (`/login`, `/give`, cualquier ruta de página) se resuelve como esa página en vez de redirigir. Con tokens de 7 caracteres es improbable pero no imposible, y el modo de falla otra vez es silencioso. |
| **`EnableVisitorTracking`** | **1** | Hace que Rock cree/lea la cookie de visitante en este sitio. Es lo que permite que un escaneo se atribuya a una persona conocida. | El módulo sigue contando escaneos, pero **el "escaneos identificados" del panel de uso queda en cero para siempre**. No es un error visible: es un número que miente hacia abajo. |
| **`DefaultPageId`** | cualquiera válida | Destino cuando no hay match. | `SystemException: Invalid Site Configuration` en vez de 404. |

### B.3 Verificación por SQL — obligatoria, no opcional

Mirar la pantalla de Rock **no alcanza**: no muestra el `Order` de los dominios.

```sql
-- Sustituir el nombre por el del sitio que se creó.
DECLARE @SiteName NVARCHAR(100) = N'Dominio Corto QR';

SELECT  s.Id, s.Name,
        s.EnabledForShortening,   -- debe ser 1
        s.RequiresEncryption,     -- debe ser 1
        s.EnableExclusiveRoutes,  -- debe ser 1
        s.EnableVisitorTracking,  -- debe ser 1
        s.DefaultPageId           -- NO debe ser NULL
FROM [Site] s WHERE s.Name = @SiteName;

-- EL DOMINIO QUE SE VA A IMPRIMIR ES LA PRIMERA FILA DE ESTO:
SELECT  sd.[Order], sd.Domain
FROM [SiteDomain] sd
JOIN [Site] s ON s.Id = sd.SiteId
WHERE s.Name = @SiteName
ORDER BY sd.[Order];
```

**La primera fila del segundo `SELECT` es, carácter por carácter, lo que va impreso.** Si dice
otra cosa que el dominio corto de producción, **parar el despliegue**.

De paso, la foto de todos los sitios habilitados para acortar (en dev, al 2026-09-28, hay cuatro:
`Rock RMS`, `External Website`, `pagina de prueba`, `prueba hans` — cualquiera de ellos es
seleccionable por error en el block setting):

```sql
SELECT s.Id, s.Name, s.EnabledForShortening, s.RequiresEncryption,
       ( SELECT TOP 1 sd.Domain FROM SiteDomain sd WHERE sd.SiteId = s.Id ORDER BY sd.[Order] ) AS DominioQueSeImprime
FROM [Site] s WHERE s.EnabledForShortening = 1 ORDER BY s.Id;
```

> Nota: un sitio habilitado para acortar **sin ningún `SiteDomain`** no falla — `DefaultDomainUri`
> cae al Global Attribute `PublicApplicationRoot` (en prod: `https://personas.vidareal.tv/`). O
> sea: el QR imprimiría el dominio del sitio de personas. Silencioso otra vez.

### B.4 Infraestructura fuera de Rock

- **DNS** del dominio corto apuntando al mismo servidor/balanceador de Rock.
- **Binding en IIS** para ese host.
- **Certificado TLS válido** para ese host (lo exige `RequiresEncryption = 1`). Sin certificado,
  el teléfono muestra advertencia y el QR ya está impreso.

### B.5 Block settings del módulo

`Admin Tools > Digital Tools > Códigos QR` → engranaje del bloque **QR Code Detail**
(o directamente en la página `qr/codigo/{QrCodeId}`):

| Setting | Valor |
|---|---|
| **Sitio del dominio corto** | el sitio dedicado de §B.1. **Sin esto el bloque solo permite códigos estáticos** (no es un error: es el fail-safe). |
| **Longitud del token** | 7 (default) |
| **Página del catálogo** | ya la cablea la migración 003 |

Los enlaces cruzados entre catálogo / uso / reportería los cablea la migración **006** y no hay
que tocarlos. Existen porque el menú de Admin Tools de Rock 18 **solo renderiza dos niveles**:
«Códigos QR» se ve bajo Digital Tools, pero el editor, el panel de uso y la reportería son nietas
y **no aparecen nunca en el menú**. Se llega a ellas por los botones dentro de los bloques.

El setting **«Días sin escaneo para marcar dormido»** (default 90) está en los tres bloques de
listado/reportería. Si se cambia, cambiarlo en los tres o los números no cuadran entre pantallas.

---

## C. Verificación post-deploy que NO se hace mirando la pantalla

La vista previa del editor es **la evidencia menos confiable que hay**: la dibuja el mismo
servidor que sirve la página, con el mismo dominio resuelto desde la misma máquina. Un QR
apuntando a `localhost` se ve impecable ahí.

### C.1 Migraciones

```sql
SELECT MigrationNumber, MigrationName
FROM [PluginMigration]
WHERE PluginAssemblyName LIKE '%vidareal.Qr%'
ORDER BY MigrationNumber;
-- Esperado: 1 QrSetup, 2 QrMetricsIndex, 3 QrPages, 4 QrMetricsPage,
--           5 QrDashboardPage, 6 QrCrossLinks, 7 QrDropRedundantIndex
```

La **2** aparece registrada pero hoy es un **no-op documentado** (creaba un índice redundante
sobre `InteractionComponent`; Rock 18 ya trae `IX_EntityId_ChannelId` que cubre el mismo
predicado). La **7** borra ese índice donde la 2 ya había corrido en su versión original:

```sql
-- Debe devolver 0 filas después del deploy:
SELECT name FROM sys.indexes
WHERE object_id = OBJECT_ID('dbo.InteractionComponent')
  AND name = 'IX_InteractionComponent_Channel_Entity';
```

Y la tabla del catálogo existe:

```sql
SELECT COUNT(*) FROM [_com_vidareal_Qr_Code];
```

### C.2 🔴 El SVG: confirmar el dominio embebido — LA VERIFICACIÓN QUE IMPORTA

1. Crear un código **dinámico** de prueba (destino: cualquier página pública real).
2. En el editor, **Descargar SVG**.
3. Abrir el `.svg` descargado con un editor de texto (`notepad`, `code`, `cat`). Es XML plano.
   El payload **no** está en texto claro dentro del SVG —es una matriz de rectángulos— así que:
4. **Decodificarlo de verdad.** Cualquiera de estas tres:
   - abrir el SVG en un navegador y **escanearlo desde la pantalla con un teléfono**, mirando la
     URL que el teléfono ofrece abrir **antes** de tocarla;
   - subirlo a un decodificador offline de QR;
   - o, sin salir del servidor, leer lo que el módulo va a codificar directamente de la base:

   ```sql
   SELECT  q.Id, q.Name,
           s.Name AS Sitio,
           ( SELECT TOP 1 sd.Domain FROM SiteDomain sd WHERE sd.SiteId = s.Id ORDER BY sd.[Order] ) AS DominioImpreso,
           s.RequiresEncryption,
           psl.Token,
           psl.Url AS DestinoActual
   FROM [_com_vidareal_Qr_Code] q
   JOIN [PageShortLink] psl ON psl.Id = q.PageShortLinkId
   JOIN [Site] s ON s.Id = psl.SiteId
   WHERE q.PageShortLinkId IS NOT NULL;
   ```

   `DominioImpreso` + `Token` es exactamente la URL dentro del código.

**Criterio de aceptación: el dominio decodificado es el dominio corto de producción, en `https`.
Cualquier otra cosa —`localhost`, una IP, `personas.vidareal.tv`— es un NO-GO.**

### C.3 El 302

Desde una máquina que **no** sea el servidor:

```bash
curl -I https://<dominio-corto>/<token>
```

Esperado:

```
HTTP/1.1 302 Found
Location: https://<destino-configurado>
```

- **404** → casi siempre `EnabledForShortening = 0` en el sitio (el 404 mudo de §B.2), o el token
  se acuñó bajo otro sitio.
- **200 con HTML** → cayó en una página en vez de redirigir: revisar `EnableExclusiveRoutes` y
  que no haya una ruta catchall compitiendo.
- **Error de certificado** → falta el TLS del dominio corto (§B.4).

### C.4 El teléfono real

Imprimir el código —papel, no pantalla— y escanearlo con **un iPhone y un Android** distintos del
que se usó para probar. Es la única prueba que cubre a la vez el dominio, el DNS, el certificado,
el contraste y el tamaño de impresión. El módulo fuerza corrección de error **H** cuando hay logo
y mantiene una zona de silencio de 4 módulos, pero nada de eso compensa un contraste bajo sobre
papel mate.

### C.5 El escaneo tiene que llegar al panel

Tras escanear, en `Códigos QR > Uso` el contador del código sube. Si no:

```sql
-- El canal de short links y las interacciones del token de prueba
SELECT TOP 20 i.Id, i.InteractionDateTime, i.PersonAliasId, i.InteractionSessionId
FROM [Interaction] i
JOIN [InteractionComponent] ic ON ic.Id = i.InteractionComponentId
JOIN [InteractionChannel] ch ON ch.Id = ic.InteractionChannelId
WHERE ch.Name LIKE '%Short Link%' AND ic.EntityId = <PageShortLinkId>
ORDER BY i.Id DESC;
```

Cero filas con un 302 confirmado ⇒ el registro de interacción del core no corrió; revisar que la
tarea `AddShortLinkInteraction` no esté fallando en `Admin > System > Exception List`.

---

## D. Comprobaciones de ambiente

### D.1 `@@DATEFIRST` y `@@LANGUAGE` — el gráfico de día de semana

```sql
SELECT @@DATEFIRST AS DateFirst, @@LANGUAGE AS Lang;
```

**Dev (verificado 2026-09-28): `7` / `us_english`.** Producción **tiene que coincidir**.

Por qué importa: `QrMetricsService.GetWeekdayBreakdown` agrupa con
`SqlFunctions.DatePart("weekday", …)`, cuyo valor **depende de `@@DATEFIRST`**, que a su vez sale
del idioma del login de SQL. Con `us_english` → `DATEFIRST = 7` → el día `1` es **domingo**, que
es el orden del arreglo `{Domingo, Lunes, …, Sábado}` del servicio. Con un login en **Español**
→ `DATEFIRST = 1` → el día `1` es **lunes**, y **todo el gráfico se corre un día entero**.

El modo de falla es el peor posible: no hay error, no hay celda vacía, y los números siguen
sumando el total correcto. Simplemente el pico del domingo aparece rotulado como lunes, y nadie
se entera hasta que alguien programa una campaña el día equivocado.

Si producción devuelve `Español` / `1`: **no cambiar el idioma del login** (afecta a todo Rock).
Lo que corresponde es mover el cálculo a C# —como ya hace `GetTimeline`— donde `DayOfWeek` es
siempre domingo = 0 sin importar el servidor ni la cultura. Anotarlo como bug bloqueante del
panel, **no** del camino de impresión/redirección.

### D.2 La base de geolocalización

```powershell
Test-Path 'C:\inetpub\<sitio>\RockWeb\App_Data\Geolocation\ip-geo-current.mmdb'
```

En dev existe (junto con `ip-geo-current-etag.txt`).

- **Si está:** el panel de uso muestra país / región / ciudad de los escaneos.
- **Si falta:** el módulo **no falla** — `QrMetricsService.GetGeoBreakdown` agrupa todo bajo
  «Desconocido» y el resto del panel (serie, top, dispositivo, día, hora) funciona igual. Es una
  degradación, no un incidente.

Rock la descarga sola de `https://rockrms.blob.core.windows.net/resources/ip-geo/ip-geo-current.mmdb`;
si el servidor tiene salida bloqueada, hay que copiar el archivo a mano desde dev.

**No copiar el `.mmdb` a un servidor sin verificar la licencia de la base** si es una MaxMind
comercial; la que Rock descarga viene con la instalación.

### D.3 Recordatorio de datos personales

El desglose geográfico se deriva de la **IP** de cada escaneo. El servicio devuelve **solo
agregados** por país/región/ciudad y nunca la IP ni el recuento de un individuo — está así a
propósito. **No exportar `Interaction.InteractionSession` ni IPs a ningún entregable externo.**

---

## E. Seguridad — el grupo se crea VACÍO a propósito

La migración 003 crea el grupo de seguridad **«Generador de QR»**
(`Guid c7d2e5a0-4b31-4c8e-9d02-b40000000501`) y le da View + Edit sobre la página del catálogo,
con **deny explícito a todos los demás**. Los `RSR - Rock Administration` entran por su propia
regla.

**El grupo nace sin un solo miembro, y eso es deliberado:** una migración no decide quién de
comunicación y TI puede emitir material impreso institucional. Pero la consecuencia es concreta:

> **Hasta que alguien meta gente en el grupo, NADIE fuera de Rock Administration ve el módulo.**
> Ni en el menú ni por URL directa — el bloque valida `CanEdit()` por su cuenta, así que esconder
> el menú no es lo único que protege.

Verificar y poblar:

```sql
SELECT g.Id, g.Name, ( SELECT COUNT(*) FROM GroupMember gm WHERE gm.GroupId = g.Id ) AS Miembros
FROM [Group] g WHERE g.Guid = 'c7d2e5a0-4b31-4c8e-9d02-b40000000501';
-- En dev al 2026-09-28: 0 miembros.
```

Poblarlo desde la interfaz (`Admin Tools > Security > Security Roles > Generador de QR`), **no
por SQL**: Rock tiene que disparar sus invalidaciones de caché de autorización.

Qué otorga la membresía:

- ver el catálogo, el panel de uso y la reportería;
- **crear y editar códigos**, lo que incluye **cambiar el destino de un QR ya impreso**. Es el
  permiso sensible: quien está en este grupo puede redirigir material que ya circula.

Tratarlo como rol de publicación institucional. Empezar corto (comunicación + TI) y ampliar
después; el modelo ya deja la costura para permisos por **categoría** si más adelante se quiere
autoservicio por ministerio (`QrCode.CategoryId`), pero **eso no está implementado** — hoy es
todo o nada.

---

## F. Rollback

### F.1 Qué se puede revertir y qué no

| Situación | Reversible |
|---|---|
| Bug en la UI, en el panel, en las descargas | **Sí** — F.2 |
| Sitio del dominio corto mal configurado, todavía sin imprimir | **Sí** — corregir §B y regenerar |
| **QR ya impresos con el dominio equivocado** | **NO.** El dominio está en el papel. Ver F.4 |

### F.2 Rollback de código (el caso normal)

Los cuatro DLLs y los bundles son la unidad. Para volver atrás:

1. Detener el sitio.
2. Restaurar desde el respaldo previo al deploy: `Rock.dll`, `Rock.Blocks.dll`,
   `Rock.ViewModels.dll` y `com.vidareal.Qr.dll` en `RockWeb/Bin`, más
   `RockWeb/Obsidian/Blocks/Qr/`. **Guardar copia de los cuatro DLLs anteriores ANTES del deploy
   es parte del paso A.2.1** — sin eso el rollback obliga a recompilar la revisión anterior.
3. Arrancar el sitio.

**Las migraciones NO se revierten al bajar los DLLs.** La tabla `_com_vidareal_Qr_Code`, las
páginas, los bloques y el grupo siguen ahí. Eso es correcto y deseable: son inertes sin el código
(las páginas quedan con bloques que no resuelven) y borrarlas destruiría el catálogo.

### F.3 Si hay que desactivar el módulo sin desinstalarlo

El camino barato y sin pérdida de datos: **quitar el View de la página del catálogo** a todos
menos Rock Administration (`Admin Tools > … > Códigos QR > Security`), o marcar la página como no
visible. Los short links **siguen resolviendo** —los QR impresos no se rompen— y el catálogo
queda intacto.

**Nunca** borrar el `Site` del dominio corto para "desactivar": `PageShortLink` tiene FK a `Site`
**con cascade**, así que borrar el sitio **borra todos los tokens** y cada QR impreso deja de
resolver, de forma inmediata e irreversible.

**Nunca** borrar un `PageShortLink` desde la pantalla nativa de Rock para limpiar: la FK del
catálogo es `ON DELETE NO ACTION` a propósito (la entrada del catálogo sobrevive), pero el token
muere y con él el papel. Para retirar un código existe **Dar de baja** en el editor, que lo saca
del listado activo y **deja el short link resolviendo**.

### F.4 Si ya se imprimió con el dominio equivocado

No hay rollback de software. El plan es de comunicación:

1. **No borrar el short link viejo** — mientras exista, quien lo escanee llega a algún lado.
2. Si el dominio equivocado es un dominio propio y resoluble (p. ej. `personas.vidareal.tv` en
   vez del corto), **crear el mismo token bajo ese sitio también**, apuntando al mismo destino.
   El papel sigue sirviendo y se gana tiempo. (Cuidado: `PageShortLinkService.GetByToken` no
   tiene índice único por sitio y hace `items.Where(SiteId).FirstOrDefault() ?? items.First()` —
   con el mismo token en dos sitios, el que no matchea el sitio cae al **primero que encuentre**.
   Verificar el 302 en ambos dominios.)
3. Si el dominio equivocado es `localhost` o una IP interna: **no hay rescate técnico**. Se
   reimprime. Estimar costo y plazo contra el 22-oct **el mismo día** que se detecta.

### F.5 La migración 007 y el rollback

`007_QrDropRedundantIndex` borra un índice que sobra; su `Down()` está **vacío a propósito** y no
lo recrea. Bajar el `com.vidareal.Qr.dll` a una versión anterior **no** vuelve a crear el índice,
y no hace falta: el core ya cubre el predicado con `IX_EntityId_ChannelId`. No es una regresión.

---

## Checklist final antes de dar por cerrado el deploy

- [ ] Respaldo full de la base + copia de los 4 DLLs previos, guardados y localizables.
- [ ] Las 8 piezas de §A.1 en el servidor, en el orden de §A.2.
- [ ] `PluginMigration` muestra 1–7 para `com.vidareal.Qr`.
- [ ] `IX_InteractionComponent_Channel_Entity` ya **no** existe.
- [ ] Sitio dedicado del dominio corto creado, con **un solo** `SiteDomain`, verificado **por SQL**.
- [ ] Las cuatro banderas en 1 y `DefaultPageId` no nulo.
- [ ] DNS + binding IIS + certificado TLS del dominio corto.
- [ ] Block setting «Sitio del dominio corto» apuntando a ese sitio.
- [ ] **SVG de un código dinámico decodificado: el dominio embebido es el correcto, en `https`.**
- [ ] `curl -I` desde fuera del servidor → 302 con el `Location` esperado.
- [ ] Escaneo con un teléfono real, impreso en papel.
- [ ] El escaneo aparece en el panel de Uso.
- [ ] `@@DATEFIRST` / `@@LANGUAGE` de producción = `7` / `us_english`.
- [ ] `ip-geo-current.mmdb` presente (o degradación aceptada y anotada).
- [ ] Grupo «Generador de QR» con miembros reales.
- [ ] `docs/qr-custom/SMOKE_TESTS.md` — todos los pasos marcados **BLOQUEANTE** pasados.
