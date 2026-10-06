# Módulo QR Generator — Arquitectura

Reemplaza una plataforma externa de generación de QR que **se corta el 22 de octubre de 2026**.
Misma forma que Eventos: el código vive en el core del fork, las migraciones en un plugin propio;
las dependencias apuntan al dominio y los bloques son adaptadores delgados que no llevan lógica.

La diferencia con Eventos es de fondo: **este módulo no inventa la redirección — la toma prestada
del core.** Un QR dinámico *es* un `PageShortLink` de Rock. Lo que el módulo agrega es lo que Rock
no tiene: catálogo, diseño, códigos estáticos y reportería.

## Capas

```
┌─ Adaptadores de ENTRADA (Rock.Blocks/Qr/*.cs → Rock.Blocks.dll)
│    QrCodeDetail   editor: crear/editar, diseño, vista previa, descargas SVG/PNG,
│                   dar de baja y reactivar. Dueño del block setting "Sitio del dominio
│                   corto" (SiteField) — el único lugar donde se decide bajo qué sitio se
│                   acuñan los tokens.
│    QrCodeList     catálogo: qué existe, a dónde apunta, cuánto se usa, qué está dormido.
│    QrMetrics      panel de USO: escaneos en el tiempo, top, dispositivo, procedencia,
│                   día y hora.
│    QrDashboard    REPORTERÍA del programa: catálogo, gobernanza, salud, contra el
│                   período anterior del mismo largo.
│    → autorización (CanView/CanEdit sobre el BlockCache), PageParameter, block settings,
│      mapeo bag↔dominio. NUNCA lógica de negocio.
│
├─ NÚCLEO DE APLICACIÓN (Rock/Model/Qr/Services/ → Rock.dll)
│    QrCatalogService  ciclo de vida y payload. BuildPayload (lo que se codifica),
│                      GetCurrentDestination (lee del short link, nunca de una copia),
│                      TryCreateShortLink / TryUpdateDestination, Retire, ValidateIntegrity,
│                      y los payloads estáticos (WIFI:, vCard, mailto:, tel:) con su escape.
│    QrRenderService   render puro: PNG y SVG, nivel de corrección de error, zona de
│                      silencio de 4 módulos, contraste mínimo, tamaño físico de impresión,
│                      composición del logo en SVG. Sin base, sin HTTP, sin efectos.
│    QrMetricsService  lectura de Interaction/InteractionComponent filtrando por los
│                      PageShortLinkId del catálogo: conteos, serie, dispositivo, geo,
│                      día, hora, campaña.
│
└─ DOMINIO (Rock/Model/Qr/*.cs + Rock/Enums/Qr → Rock.dll)
     QrCode            la única tabla propia: _com_vidareal_Qr_Code. Entity<T> de Rock,
                       registrada por reflexión vía IRockEntity (no hay DbSet en
                       RockContext y no hace falta).
     QrDesign          colores, tamaño de logo. MaxLogoSizePercent = 20, MinContrastRatio = 3.0.
     QrStaticPayloads  QrWifiPayload / QrVCardPayload (lo que se guarda en StaticContent
                       como JSON para esos dos tipos).
     QrEnums           QrCodeType (Dinamico/Estatico), QrStaticContentType, QrEccLevel
                       (espejo del enum de QRCoder, para no acoplar el dominio a la librería).

Bags: Rock.ViewModels/Blocks/Qr/**  → Rock.ViewModels.dll
Front: Rock.JavaScript.Obsidian.Blocks/src/Qr/*.obs → RockWeb/Obsidian/Blocks/Qr/*.obs.js
Migraciones: Plugin.VidaRealQr/VidaRealQr/Migrations/ → com.vidareal.Qr.dll (SOLO migraciones)
```

**Son CUATRO DLLs más los bundles.** El `com.vidareal.Qr.dll` por sí solo crea tablas y páginas
sobre un core que no tiene los bloques. Camino completo: `docs/qr-custom/DEPLOY.md`.

## La decisión fundacional: el destino vive en el `PageShortLink`, nunca se copia

`QrCode` **no tiene columna de destino**. Para un código dinámico, la verdad sobre a dónde apunta
está en `PageShortLink.Url` (y en sus horarios), y el catálogo la lee en cada carga vía
`QrCatalogService.GetCurrentDestination` → `PageShortLinkCache.GetCurrentUrl`.

Por qué, y no "por pureza":

1. **La pantalla nativa de Rock sigue existiendo y sigue funcionando.** `Digital Tools > Short
   Links` edita esas URLs. Una copia en `_com_vidareal_Qr_Code` se desincroniza el día que alguien
   la use, y entonces el catálogo miente sobre material que está impreso.
2. **Es la misma lectura que hace el redirect.** `GetCurrentUrl` resuelve el horario vigente ahora
   mismo — exactamente lo que va a servir el route handler. Cualquier otra fuente sería una
   segunda opinión.
3. **La escritura tiene que pasar por `SaveChanges`.** `TryUpdateDestination` modifica la entidad y
   deja que el `RockContext` del llamador commitee: ése es el único camino que corre
   `PageShortLink.UpdateCache` y vacía `PageShortLinkCache`. Un `UPDATE` por SQL directo deja el
   caché sirviendo el destino viejo **hasta que reinicie el sitio**, porque esa entrada de caché no
   expira.

Lo que sí es propio del módulo son las dos cosas que el core no cubre: **los códigos estáticos**
(no tienen token ni redirección y aun así hay que listarlos, atribuirlos y volver a descargarlos) y
**el diseño**.

Corolarios: dar de baja un código **no** borra el short link (el papel sigue resolviendo, el
catálogo registra que está fuera de servicio) y la FK `QrCode → PageShortLink` es
`ON DELETE NO ACTION` a propósito — borrar el short link desde la pantalla nativa no puede hacer
desaparecer en silencio la ficha de un código que puede estar en una valla.

## Trampas del core, documentadas

| Trampa | Qué pasa | Cómo la trata el módulo |
|---|---|---|
| **`Site.EnabledForShortening`** | En `RockRouteHandler` toda la rama de short links vive dentro de `if ( site.EnabledForShortening )`. Apagada, el handler **ni consulta el token**: no hay excepción, no hay log, no hay rastro. **404 mudo**, descubierto después de imprimir. | `QrCodeDetail.SyncShortLink` se **niega a acuñar** un token bajo un sitio sin la bandera y lo dice con el motivo. |
| **`Site.DefaultDomainUri`** (`Site.cs:745-766`) | Es `SiteDomains.OrderBy(d => d.Order).First()`. **El dominio impreso es el del `Order` más bajo**, no "el principal". En dev el sitio «Rock RMS» tiene `localhost` en `Order 0`: apuntar ahí el block setting imprime `http://localhost/<token>` en cada QR, y la vista previa se ve perfecta. | No hay defensa de código posible —cualquiera de esos dominios es legítimo para el sitio—. Es **configuración obligatoria y verificación por SQL**: `DEPLOY.md` §B. **Riesgo nº 1 del proyecto.** |
| **`PageShortLinkService.GetByToken`** | `items.Where(s => s.SiteId == siteId).FirstOrDefault() ?? items.First()` — si el token no existe en ese sitio, **cae al primero que encuentre en cualquier sitio**. Además `PageShortLink` **no tiene índice único sobre `(SiteId, Token)`** (solo `IX_Token` no único e `IX_SiteId`). | Nunca se usa `GetByToken` para decidir si un token está libre: se usa `VerifyUniqueToken`, que sí filtra por sitio. Un token automático se **reacuña hasta 5 veces**; uno pedido a mano **no se reemplaza en silencio** (es el que va impreso). La ventana entre verificación y commit **sigue abierta**: cerrarla exige el índice único en la tabla del core, que este módulo no agrega. |
| **Filtro Lava `CreateShortLink`** | Su fallback de sitio es `OrderBy( s => s.EnabledForShortening ).Take(1)` (`LavaFilters.cs:4148`) — **ascendente**, así que elige el primer sitio **NO** habilitado para acortar: lo contrario de su propio comentario. | El módulo **no usa ese filtro**. El `siteId` es parámetro requerido de `TryCreateShortLink` y no hay fallback. |
| **Borrar un `Site`** | `PageShortLinkConfiguration`: `HasRequired(p => p.Site)…WillCascadeOnDelete( true )`. Borrar el sitio del dominio corto **borra todos sus tokens**, y cada QR impreso deja de resolver, de inmediato y sin vuelta. | Documentado como prohibición explícita en `DEPLOY.md` §F.3. Para "apagar" el módulo se quita el View de la página, no se toca el sitio. |
| **El menú de Admin Tools solo renderiza dos niveles** | «Códigos QR» se ve bajo Digital Tools, pero el editor, el panel de Uso y la Reportería son **nietas** y no aparecen nunca. (Le pasa igual a «Short Links» con su hija «Link».) Moverlas a la raíz perdería la herencia de seguridad. | **Migración 006**: llena los block settings de página para que los tres bloques se enlacen entre sí. La navegación vive dentro de los bloques, no en el menú. |

## La única dependencia de API `internal` del core

`QrMetricsService.GetGeoBreakdown` usa **`Rock.Net.Geolocation.IpGeoLookup`**, que es
`internal sealed`. Solo compila porque este servicio vive **dentro de `Rock.dll`**; desde un
plugin sería inalcanzable.

Se usa porque el camino que registra un escaneo de short link
(`Rock.Tasks.AddShortLinkInteraction`) llama la sobrecarga vieja de
`InteractionService.CreateInteraction`, que arma sesión y dispositivo pero **no** hace el lookup de
geolocalización. Verificado contra la base: 577 escaneos, 577 con IP, **0 con
`InteractionSessionLocation`**. La IP está; la ubicación hay que derivarla.

**Es el punto a revisar en cada upgrade de Rock.** Un `internal` no tiene contrato de
compatibilidad: puede cambiar de firma, de espacio de nombres o desaparecer sin aviso ni nota de
release, y el síntoma sería un error de compilación del fork (ruidoso, que es lo bueno) o —peor—
un cambio de comportamiento silencioso.

Mitigaciones ya puestas: se agrupa por IP en SQL antes de resolver (tantos lookups como IPs
distintas, no como escaneos), hay un tope duro de lookups con el resto en un bucket **rotulado**
(no disfrazado de «Desconocido»), y se devuelven **solo agregados** por país/región/ciudad — nunca
la IP ni el recuento de un individuo.

## Checklist de upgrade de Rock

Correr esto **antes** de dar por bueno un upgrade del fork:

1. **¿El core indexó algo nuevo sobre `InteractionComponent`?**
   ```sql
   SELECT i.name, c.name AS ColName, ic.key_ordinal, ic.is_included_column
   FROM sys.indexes i
   JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
   JOIN sys.columns c ON c.object_id = i.object_id AND c.column_id = ic.column_id
   WHERE i.object_id = OBJECT_ID('dbo.InteractionComponent')
   ORDER BY i.name, ic.is_included_column, ic.key_ordinal;
   ```
   Hoy el predicado del módulo (`EntityId IN (…) AND InteractionChannelId = …`) lo cubre
   `IX_EntityId_ChannelId`. **Consultar `sys.indexes`, no el `EntityTypeConfiguration`**: los
   índices de Rock viven en las migraciones del core, no en la configuración de EF. Confundir las
   dos cosas es lo que produjo la migración 002 —un índice redundante sobre una tabla caliente—
   que hoy es no-op y que la 007 borra.
2. **¿Cambió `IpGeoLookup`?** Firma de `GetGeolocation`, forma del resultado
   (`CountryCode`/`RegionName`/`City`), ruta y nombre del `.mmdb`, o su accesibilidad.
3. **¿`AddShortLinkInteraction` empezó a geolocalizar?** Si el core migró a la sobrecarga nueva de
   `CreateInteraction`, `InteractionSessionLocation` deja de venir vacío:
   ```sql
   SELECT COUNT(*) AS ConLocation
   FROM Interaction i
   JOIN InteractionSession s ON s.Id = i.InteractionSessionId
   WHERE s.InteractionSessionLocationId IS NOT NULL;
   ```
   Si da > 0 para escaneos nuevos, **`GetGeoBreakdown` se simplifica a un JOIN contra
   `InteractionSessionLocation`**: desaparecen el tope de lookups, el bucket de no resueltos, el
   costo sincrónico dentro de la carga de la página **y la dependencia `internal`**. Es la
   simplificación más valiosa que le puede pasar a este módulo.
4. **¿Sigue `DefaultDomainUri` ordenando por `Order`?** Si el core agrega un concepto de "dominio
   principal", la trampa nº 1 cambia de forma y `DEPLOY.md` §B hay que reescribirlo.
5. **¿`PageShortLink` ganó un índice único sobre `(SiteId, Token)`?** Si sí, el bucle de reacuñado
   de `TryCreateShortLink` se puede reemplazar por un catch del error de duplicado, que sí cierra
   la carrera.
6. **¿El menú de Admin Tools renderiza más de dos niveles?** Si sí, la migración 006 sigue siendo
   correcta pero deja de ser imprescindible.
7. **¿QRCoder subió de versión?** Si un renderer sin GDI+ gana soporte de logo, el logo del PNG
   deja de rasterizarse en el navegador y se compone en el servidor.

## Lo que se pospuso deliberadamente

- **Accesibilidad completa.** **No hay auditoría** de navegación por teclado, de lectores de
  pantalla ni de los gráficos del panel; los bloques usan los componentes estándar de Obsidian y
  hasta ahí llega la garantía. Se pospuso por el 22-oct; es deuda reconocida, no un descuido. Lo
  que **sí** está resuelto es el contraste del código generado (mínimo 3.0, validado al guardar),
  porque ahí un fallo no se corrige: se imprime.
- **Tests unitarios.** No hay ninguno. Los candidatos naturales son puros y baratos
  (`QrCatalogService.BuildStaticPayload` con su escape de WIFI y vCard, `ValidateIntegrity`,
  `QrRenderService.GetContrastRatio` / `GetEccLevel` / `GetRecommendedSideCm`). Se pospusieron
  porque **el riesgo real de este módulo no es lógica pura: es configuración e infraestructura** —
  dominio embebido, banderas del sitio, DNS, certificado— que ningún test unitario toca. Por eso el
  entregable de calidad es `docs/qr-custom/SMOKE_TESTS.md` y no una suite.
- **Permisos por categoría (autoservicio por ministerio).** Hoy es todo-o-nada: el grupo «Generador
  de QR» ve y edita todo el catálogo. **La costura ya está en el modelo** — `QrCode.CategoryId`
  apunta a `Category` de Rock, con su categoría raíz creada por la migración 003, y el catálogo ya
  filtra por categoría usando el **Guid** y no el nombre (hay categorías homónimas bajo padres
  distintos). Agregar seguridad por categoría no requiere cambio de esquema.
- **Códigos dinámicos con horario.** `PageShortLink` soporta destinos programados y
  `GetCurrentDestination` ya los lee correctamente; el **editor** no los expone. Leer sí, escribir
  todavía no.
- **Conversión de estático a dinámico.** No se ofrece y no se va a ofrecer: el contenido de un
  código estático está en el papel y no se puede cambiar. La UI declara la diferencia antes de la
  elección, no en un tooltip.
