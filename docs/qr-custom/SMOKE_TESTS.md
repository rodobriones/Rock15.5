# Módulo QR Generator — Runbook de pruebas runtime (smoke tests)

Pruebas en orden de prioridad tras un deploy. Arquitectura: `Rock/Model/Qr/ARCHITECTURE.md`.
Despliegue y configuración: `docs/qr-custom/DEPLOY.md` — **hacer §B de ese documento ANTES de
empezar acá**.

**Estado al 2026-09-28:** en dev hay 2 códigos, **ambos estáticos**. Hay **0 códigos dinámicos**
y **0 escaneos** del módulo (los 577 escaneos de `Interaction` son short links del Wallet). O sea:
**la prueba #2 —el ciclo completo de un QR dinámico— nunca ha corrido.** Es la crítica, igual que
el pago con tarjeta lo era en Eventos.

**Deadline duro: 22 de octubre de 2026.** Los pasos marcados 🔴 **BLOQUEANTE** tienen que pasar
antes de imprimir el primer material. Los demás pueden cerrarse después.

**Dónde mirar cuando algo falla:** `Admin > System > Exception List` y la tabla
`_com_vidareal_Qr_Code`. Ojo con el modo de falla característico de este módulo: **casi todos sus
errores son silenciosos** (404 mudo, dominio equivocado, gráfico corrido un día). Si algo "se ve
bien", eso no es evidencia.

**Cómo llegar a las pantallas:** el menú de Admin Tools solo renderiza dos niveles, así que en
`Digital Tools` aparece únicamente **Códigos QR**. El editor, el panel de **Uso** y la
**Reportería** son nietas y se alcanzan por los botones dentro de los bloques (migración 006) o
por ruta directa: `/qr`, `/qr/codigo/{QrCodeId}`.

---

## 0. Migraciones y arranque (tras reciclar el app pool) 🔴 BLOQUEANTE

1. Reciclar el app pool (copiar cualquier DLL a `RockWeb/Bin` ya lo dispara) y abrir el sitio una
   vez.
   **Esperado:** el sitio levanta sin error.
2. `Admin > Power Tools > SQL`:
   ```sql
   SELECT MigrationNumber, MigrationName FROM [PluginMigration]
   WHERE PluginAssemblyName LIKE '%vidareal.Qr%' ORDER BY MigrationNumber;
   ```
   **Esperado:** 7 filas — `1 QrSetup`, `2 QrMetricsIndex`, `3 QrPages`, `4 QrMetricsPage`,
   `5 QrDashboardPage`, `6 QrCrossLinks`, `7 QrDropRedundantIndex`.
3. ```sql
   SELECT name FROM sys.indexes
   WHERE object_id = OBJECT_ID('dbo.InteractionComponent')
     AND name = 'IX_InteractionComponent_Channel_Entity';
   ```
   **Esperado: 0 filas.** La migración 007 lo borró (era un índice redundante creado por la 002,
   que hoy es no-op). Si devuelve una fila, la 007 no corrió.
4. Abrir `Admin Tools > Digital Tools > Códigos QR`.
   **Esperado:** el catálogo carga, con la barra de búsqueda, los filtros y los botones
   «Reportería» y «Uso» arriba. Sin pantalla en blanco y sin error de bloque.
   **Si la página carga pero el bloque queda vacío o tira error de tipo:** faltó un DLL. Casi
   siempre `Rock.ViewModels.dll` — ver `DEPLOY.md` §A.
5. `F12 > Console` en esa página.
   **Esperado:** sin 404 de `/Obsidian/Blocks/Qr/qrCodeList.obs.js`. Un 404 ahí = faltaron los
   bundles.

---

## 1. Configuración del dominio corto 🔴 BLOQUEANTE

> Esto no es "preparación": es la prueba que más material salva. Ver `DEPLOY.md` §B.

1. `Admin Tools > Power Tools > SQL`, con el nombre del sitio dedicado:
   ```sql
   DECLARE @SiteName NVARCHAR(100) = N'Dominio Corto QR';
   SELECT s.EnabledForShortening, s.RequiresEncryption, s.EnableExclusiveRoutes,
          s.EnableVisitorTracking, s.DefaultPageId
   FROM [Site] s WHERE s.Name = @SiteName;
   SELECT sd.[Order], sd.Domain FROM [SiteDomain] sd
   JOIN [Site] s ON s.Id = sd.SiteId WHERE s.Name = @SiteName ORDER BY sd.[Order];
   ```
   **Esperado:** las cuatro banderas en `1`, `DefaultPageId` no nulo, y el **segundo SELECT
   devuelve UNA sola fila** con el dominio corto de producción.
   **NO-GO si la primera fila del segundo SELECT es `localhost`, una IP, o `personas.vidareal.tv`.**
   Es lo que se va a imprimir dentro de cada código.
2. Engranaje del bloque **QR Code Detail** → setting **«Sitio del dominio corto»**.
   **Esperado:** apunta al sitio verificado en el paso 1.
3. Con el setting **vacío** (probarlo una vez, a propósito): abrir el editor de un código nuevo.
   **Esperado:** el tipo «Dinámico» aparece deshabilitado o con aviso, y solo se pueden crear
   estáticos. Es el fail-safe, no un bug. Volver a poner el sitio después.
4. Apuntar el setting a un sitio **sin** `EnabledForShortening` (p. ej. «Rock Check-in») y tratar
   de guardar un dinámico.
   **Esperado:** error explícito — *«El sitio 'X' no tiene 'Enabled For Shortening' activado. Los
   códigos creados bajo ese sitio devolverían 404 al escanearse»*. **No** debe acuñar el token.
   Volver a dejar el sitio correcto.

---

## 2. 🔴 BLOQUEANTE — El ciclo completo de un QR dinámico (LA CRÍTICA)

> Nunca ha corrido. Hacerla entera, en orden, sin saltarse el papel ni el teléfono.

### 2.1 Crear

1. `Códigos QR > Nuevo`, tipo **Dinámico**, nombre `Prueba deploy oct`, destino una página pública
   real (p. ej. `https://vidareal.tv/`), token automático.
   **Esperado:** guarda sin error; la pantalla muestra el QR y la URL corta
   `https://<dominio-corto>/<token>`.
2. En la pantalla, leer la URL corta que muestra el editor.
   **Esperado:** el dominio es el corto de producción, en `https`.
   ⚠️ **Esto solo, NO es evidencia suficiente** — la pantalla la dibuja el mismo servidor. Sigue
   el paso 2.2.

### 2.2 🔴 Verificar el dominio EMBEBIDO en el SVG (la verificación que importa)

3. **Descargar SVG**.
4. Decodificarlo de verdad — **no** mirar la vista previa. Cualquiera de estas:
   - abrir el `.svg` en el navegador y escanearlo desde la pantalla con un teléfono, **mirando la
     URL que el teléfono ofrece abrir ANTES de tocarla**;
   - subirlo a un decodificador de QR offline;
   - o leer de la base lo que el módulo codifica:
     ```sql
     SELECT q.Name,
            ( SELECT TOP 1 sd.Domain FROM SiteDomain sd WHERE sd.SiteId = s.Id ORDER BY sd.[Order] ) AS DominioImpreso,
            s.RequiresEncryption, psl.Token, psl.Url AS Destino
     FROM [_com_vidareal_Qr_Code] q
     JOIN [PageShortLink] psl ON psl.Id = q.PageShortLinkId
     JOIN [Site] s ON s.Id = psl.SiteId
     WHERE q.Name = 'Prueba deploy oct';
     ```
   **Esperado:** el payload decodificado es exactamente
   `https://<dominio-corto-de-producción>/<token>`.
   **NO-GO ante cualquier otra cosa.** Un QR impreso con el dominio equivocado no se parcha con un
   deploy: se reimprime (`DEPLOY.md` §F.4).

### 2.3 🔴 El 302 desde fuera del servidor

5. Desde una máquina que **no** sea el servidor de Rock:
   ```bash
   curl -I https://<dominio-corto>/<token>
   ```
   **Esperado:**
   ```
   HTTP/1.1 302 Found
   Location: https://vidareal.tv/
   ```
   **404** ⇒ `EnabledForShortening` apagado (el 404 mudo: no hay excepción ni log) o token bajo
   otro sitio. **200 con HTML** ⇒ cayó en una página; revisar `EnableExclusiveRoutes`.
   **Error de certificado** ⇒ falta el TLS del dominio corto.

### 2.4 🔴 Imprimir y escanear con un teléfono real

6. Imprimir el SVG **en papel** (no mostrarlo en pantalla) a tamaño real de uso, p. ej. 3 cm de
   lado. **Guardar ese papel: se reutiliza en el paso 2.6.**
7. Escanearlo con un **iPhone** y con un **Android**, ninguno de los dos siendo el de quien hizo
   el deploy.
   **Esperado:** ambos ofrecen abrir `https://<dominio-corto>/<token>` y al abrirlo llegan a
   `https://vidareal.tv/`, sin advertencia de sitio no seguro.

### 2.5 🔴 El escaneo tiene que llegar al panel de Uso

8. Esperar ~1 minuto y abrir `Códigos QR > Uso` (botón dentro del catálogo).
   **Esperado:** el código `Prueba deploy oct` aparece con **escaneos ≥ 2** (los dos teléfonos),
   con fecha del último escaneo de hoy, y la serie del gráfico muestra el pico de hoy.
9. En el desglose de dispositivo.
   **Esperado:** aparecen `Mobile` y los sistemas operativos de los dos teléfonos.
10. En el desglose de procedencia.
    **Esperado:** país / ciudad resueltos. **Si dice «Desconocido» para todo**, verificar que
    exista `RockWeb/App_Data/Geolocation/ip-geo-current.mmdb`; es degradación aceptada, no
    bloqueante (`DEPLOY.md` §D.2).
11. Si el contador queda en 0 con el 302 ya confirmado:
    ```sql
    SELECT TOP 10 i.Id, i.InteractionDateTime, i.PersonAliasId
    FROM [Interaction] i
    JOIN [InteractionComponent] ic ON ic.Id = i.InteractionComponentId
    JOIN [InteractionChannel] ch ON ch.Id = ic.InteractionChannelId
    WHERE ch.Name LIKE '%Short Link%' AND ic.EntityId = <PageShortLinkId>
    ORDER BY i.Id DESC;
    ```
    Cero filas ⇒ la tarea `AddShortLinkInteraction` del core no corrió: revisar `Exception List`.

### 2.6 🔴 Cambiar el destino y re-escanear EL MISMO PAPEL

> **Esta es la razón de ser del módulo.** Si falla, el proyecto no cumple su promesa.

12. Volver al editor del código y cambiar el destino a otra URL real (p. ej.
    `https://vidareal.tv/eventos`). Guardar.
    **Esperado:** guarda sin error. **El token NO cambia** y la URL corta que muestra la pantalla
    es la misma de antes.
13. `curl -I https://<dominio-corto>/<token>` de nuevo.
    **Esperado:** `302` con `Location: https://vidareal.tv/eventos`. **Si sigue devolviendo el
    destino viejo**, el caché de `PageShortLinkCache` no se invalidó — pasa si alguien editó la
    URL por SQL directo en vez de por la pantalla; volver a guardar desde la interfaz.
14. **Escanear EL MISMO PAPEL del paso 6** con un teléfono.
    **Esperado:** llega a `https://vidareal.tv/eventos`. El papel no cambió y el destino sí.
15. Volver al panel de Uso.
    **Esperado:** el contador de escaneos **siguió sumando sobre el mismo código** (no se creó uno
    nuevo); la historia de escaneos previos al cambio de destino **no se perdió**.

### 2.7 El catálogo lee el destino del short link, nunca una copia 🔴 BLOQUEANTE

16. `Admin Tools > Digital Tools > Short Links` (pantalla **nativa de Rock**, no la del módulo).
    Buscar el token del paso 2.1 y **editar la URL desde ahí** a un tercer destino
    (p. ej. `https://vidareal.tv/contacto`). Guardar.
17. Volver a `Códigos QR` (el catálogo del módulo) y refrescar.
    **Esperado:** la columna de destino del código muestra **`https://vidareal.tv/contacto`** — el
    valor nuevo. El catálogo lo lee del `PageShortLink` en cada carga, nunca de una copia propia.
    **Si muestra el destino anterior, hay una copia en algún lado y es un bug de diseño**, no de
    refresco.
18. Abrir el editor de ese código.
    **Esperado:** el campo «Destino» también trae el valor nuevo.

---

## 3. Códigos estáticos, uno de cada tipo

> No requieren dominio corto ni sitio: se pueden probar aunque §1 no esté lista.

1. **Nuevo > Estático > Enlace (URL)**, escribiendo el dominio **sin esquema**:
   `vidareal.tv`. Guardar, descargar SVG y escanearlo.
   **Esperado:** el teléfono abre `https://vidareal.tv` — el módulo antepone `https://` a un
   dominio pelado a propósito (sin eso el teléfono lo trata como búsqueda).
2. **Nuevo > Estático > Red wifi**: SSID, contraseña, WPA. Guardar, descargar SVG, escanear con un
   teléfono.
   **Esperado:** el teléfono ofrece **conectarse a la red**, no abrir una URL.
   - Probar además un SSID **con un punto y coma o una coma en el nombre** (p. ej. `Vida;Real`).
     **Esperado:** se conecta igual — el payload escapa los cinco caracteres estructurales. Si se
     conecta a una red equivocada o no ofrece nada, el escape falló.
   - **Esperado en el catálogo:** la columna de destino de este código está **vacía**. Es
     deliberado: la contraseña viaja dentro del código y no se lista en pantalla.
3. **Nuevo > Estático > Contacto (vCard)**: nombre, apellido, organización, teléfono, correo.
   Guardar, descargar SVG, escanear.
   **Esperado:** el teléfono ofrece **agregar el contacto**, con nombre y teléfono correctos.
4. Probar rápido **Texto**, **Correo** y **Teléfono**.
   **Esperado:** texto plano; `mailto:` abre el cliente de correo; `tel:` abre el marcador con el
   número **sin espacios**.
5. Intentar guardar un estático **sin contenido**.
   **Esperado:** error de validación *«Un código estático necesita contenido»*. No guarda.

---

## 4. Descargas: SVG y PNG, con y sin logo

1. En un código cualquiera, sin logo: **Descargar SVG**.
   **Esperado:** archivo `.svg`, abre en el navegador, se ve el código.
2. Mismo código: **Descargar PNG**.
   **Esperado:** archivo `.png` nítido.
3. Poner un **tamaño de impresión** (p. ej. 4 cm) y descargar SVG.
   **Esperado:** el SVG trae el tamaño físico; al imprimirlo mide ~4 cm de lado.
   Con menos de 2 cm **esperado:** aviso — por debajo de ese tamaño el código deja de leer de
   cerca.
4. Subir un **logo** y ponerlo al 15 %. Descargar **SVG**.
   **Esperado:** el logo aparece centrado dentro del código, y el código **sigue escaneando**
   (el módulo fuerza corrección de error **H** en cuanto hay logo).
5. Con el logo puesto, descargar **PNG**.
   **Esperado:** el PNG **también trae el logo**. Lo compone el navegador a partir del SVG —
   QRCoder 1.3.9 no sabe poner logos sin GDI+, así que el servidor devuelve el PNG solo con color
   y el bloque lo rasteriza del SVG. **Si el PNG sale sin logo, ese fallback del cliente no
   corrió** (revisar la consola del navegador: puede ser presión de memoria en el canvas).
6. Subir un logo y pedir **más de 20 %**.
   **Esperado:** se topa en 20 % con aviso; más allá ni la corrección H alcanza.
7. Elegir colores de **bajo contraste** (p. ej. gris claro sobre blanco).
   **Esperado:** aviso de contraste — el mínimo es 3.0. Un código de bajo contraste escanea en
   pantalla y falla impreso.
8. **Escanear un código con logo IMPRESO EN PAPEL**, no en pantalla.
   **Esperado:** lee. Es la única prueba que cubre la pérdida de nitidez de la impresión.

---

## 5. Dar de baja y reactivar

1. En un código dinámico, **Dar de baja**.
   **Esperado:** sale del listado de activos (aparece con el filtro de inactivos), con fecha de
   baja.
2. `curl -I https://<dominio-corto>/<token>` del código dado de baja.
   **Esperado: sigue devolviendo 302.** Es deliberado: dar de baja **no** borra el short link,
   porque el código puede estar impreso en una valla. Si devuelve 404, algo borró el short link.
3. **Reactivar** el mismo código.
   **Esperado:** vuelve al listado de activos, sin fecha de baja, y el token y el destino son los
   mismos de antes.
4. Confirmar que **no hay botón de borrar** un código.
   **Esperado:** no existe. Un QR no se borra mientras pueda existir en papel.

---

## 6. Reportería y panel de Uso

1. Abrir `Códigos QR > Reportería`.
   **Esperado:** los indicadores del programa (cuántos códigos, cuántos dinámicos/estáticos, quién
   los creó, cuántos dormidos) **cada uno con su valor del período anterior** al lado.
2. Cambiar el rango de fechas.
   **Esperado:** los números y la comparación se recalculan; el período anterior es del mismo
   largo.
3. Panel de **Uso**: revisar el gráfico de **día de la semana**.
   **Esperado:** siempre los **siete** días, en orden `Domingo…Sábado`, con ceros donde no hubo
   escaneos (no días ausentes).
   ⚠️ **Verificación obligatoria del ambiente:** `SELECT @@DATEFIRST, @@LANGUAGE;` debe dar
   **`7` / `us_english`**. Con un login en Español (`DATEFIRST = 1`) **el gráfico entero se corre
   un día** sin ningún error visible y los totales siguen cuadrando. Si producción devuelve otra
   cosa, **anotarlo como bug del panel** (`DEPLOY.md` §D.1) — no bloquea la impresión.
4. Gráfico de **hora**.
   **Esperado:** 24 buckets, coherentes con la hora de los escaneos de prueba.
5. Lista de **códigos dormidos** (sin escaneos en 90 días por defecto).
   **Esperado:** el código de prueba **no** aparece ahí (se escaneó hoy); un dinámico viejo sin uso
   sí.
6. Contador de **escaneos identificados**.
   **Esperado:** > 0 si alguien escaneó con sesión iniciada en ese navegador. **Si queda
   permanentemente en 0**, revisar `EnableVisitorTracking = 1` en el sitio del dominio corto.
7. Navegación entre las tres pantallas (Catálogo ↔ Uso ↔ Reportería) por los botones.
   **Esperado:** los seis enlaces funcionan. Los cablea la migración 006, porque el menú de Admin
   Tools no muestra las nietas.

---

## 7. Seguridad 🔴 BLOQUEANTE

1. ```sql
   SELECT g.Name, ( SELECT COUNT(*) FROM GroupMember gm WHERE gm.GroupId = g.Id ) AS Miembros
   FROM [Group] g WHERE g.Guid = 'c7d2e5a0-4b31-4c8e-9d02-b40000000501';
   ```
   **Esperado tras el deploy:** 0 miembros — el grupo «Generador de QR» se crea vacío a propósito.
2. Entrar con un usuario **sin** membresía y **sin** Rock Administration.
   **Esperado:** no ve «Códigos QR» en el menú **y** entrando por URL directa `/qr` recibe
   "no autorizado". El bloque valida por su cuenta; esconder el menú no es lo único que protege.
3. Agregar a ese usuario al grupo desde `Admin Tools > Security > Security Roles > Generador de QR`
   (**por la interfaz, no por SQL** — Rock tiene que invalidar el caché de autorización).
   **Esperado:** tras re-login ve el módulo completo y **puede crear y cambiar destinos**.
4. Dejar el grupo con los miembros reales de comunicación y TI.
   **Esperado:** cerrar el deploy con el grupo poblado. **Con 0 miembros, nadie fuera de Rock
   Administration ve el módulo.**

---

## 8. Regresión sobre el resto de Rock

> Este módulo comparte tablas y pantallas con el core y con el Wallet. Confirmar que no se rompió
> nada ajeno.

1. `Admin Tools > Digital Tools > Short Links`: la pantalla nativa lista los short links,
   incluidos los del módulo QR y los del **Wallet**.
   **Esperado:** funciona igual que antes; los tokens del Wallet siguen resolviendo.
2. Escanear un pase digital del **Wallet** (short link de ese módulo).
   **Esperado:** sigue funcionando y su escaneo **no** aparece en el panel de Uso del módulo QR
   (que filtra por los short links de su propio catálogo).
3. `Admin > System > Exception List` tras toda la sesión de pruebas.
   **Esperado:** sin excepciones nuevas con prefijo de Rock.Blocks.Qr ni de
   `AddShortLinkInteraction`.

---

## Resumen de bloqueantes para el 22-oct

| # | Prueba | Por qué bloquea |
|---|---|---|
| 0 | Migraciones 1–7 registradas y el índice de la 002 borrado | Sin esto el módulo no existe en el servidor |
| 1 | Sitio del dominio corto verificado **por SQL**, incluido el `Order` de los `SiteDomain` | El `Order 0` es lo que se imprime; un error acá se reimprime, no se parcha |
| 2.2 | Dominio **decodificado** del SVG = dominio corto de producción | Única evidencia real; la pantalla no sirve |
| 2.3 | `curl -I` desde fuera → 302 | Descarta el 404 mudo de `EnabledForShortening` |
| 2.4 | Escaneo con teléfono real, impreso en papel | Cubre DNS, TLS, contraste y tamaño a la vez |
| 2.5 | El escaneo aparece en el panel de Uso | Si no, el módulo no mide y la promesa de reportería no se cumple |
| 2.6 | Cambiar destino y re-escanear **el mismo papel** | Es la razón de ser del módulo |
| 2.7 | El catálogo refleja el destino editado desde la pantalla nativa | Confirma que el destino no está duplicado |
| 7 | Grupo «Generador de QR» poblado | Con 0 miembros nadie puede usar el módulo |

Todo lo demás (§3 estáticos, §4 descargas, §5 baja/reactivación, §6 reportería, §8 regresión) es
**deseable antes del 22-oct pero no bloquea la primera impresión**, con una excepción: si el
material que se va a imprimir **lleva logo**, el paso **4.8** (escanear un código con logo impreso
en papel) pasa a ser bloqueante para ese material.
