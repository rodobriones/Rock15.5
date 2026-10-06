// <copyright>
// Copyright by the Spark Development Network
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
// </copyright>
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;

using Rock.Attribute;
using Rock.Blocks;
using Rock.Data;
using Rock.Enums.Qr;
using Rock.Model;
using Rock.Security;
using Rock.ViewModels.Blocks.Qr.QrCodeDetail;
using Rock.ViewModels.Utility;
using Rock.Web.Cache;

namespace Rock.Blocks.Qr
{
    /// <summary>
    /// Crea y edita un código QR institucional, dinámico o estático.
    /// </summary>
    /// <remarks>
    /// Un solo bloque para los dos modos a propósito: elegir entre estático y dinámico es lo
    /// primero que hay que entender bien, y de lo que la gente se arrepiente cuando ya mandó a
    /// imprimir mil volantes. La interfaz dice la diferencia ANTES de elegir, no en un tooltip.
    /// </remarks>
    [DisplayName( "QR Code Detail" )]
    [Category( "Vida Real > QR" )]
    [Description( "Crea y edita un código QR institucional, dinámico o estático." )]
    [IconCssClass( "ti ti-qrcode" )]

    #region Block Attributes

    [SiteField(
        "Sitio del dominio corto",
        Key = AttributeKey.ShortLinkSite,
        Description = "El sitio de Rock atado al dominio corto. Los tokens de los QR dinámicos se crean bajo este sitio. Sin esto, el bloque solo permite códigos estáticos.",
        IsRequired = false,
        Order = 0 )]

    [IntegerField(
        "Longitud del token",
        Key = AttributeKey.TokenLength,
        Description = "Cuántos caracteres genera el token automático.",
        IsRequired = false,
        DefaultIntegerValue = 7,
        Order = 1 )]

    [LinkedPage(
        "Página del catálogo",
        Key = AttributeKey.ListPage,
        Description = "La página con el bloque QR Code List, para el enlace de volver.",
        IsRequired = false,
        Order = 2 )]

    #endregion

    [Rock.SystemGuid.BlockTypeGuid( "c7d2e5a0-4b31-4c8e-9d02-b40000000102" )]
    public class QrCodeDetail : RockBlockType
    {
        #region Keys

        private static class AttributeKey
        {
            public const string ShortLinkSite = "ShortLinkSite";
            public const string TokenLength = "TokenLength";
            public const string ListPage = "ListPage";
        }

        private static class PageParameterKey
        {
            public const string QrCodeId = "QrCodeId";
        }

        #endregion

        #region Block Initialization

        /// <inheritdoc/>
        public override object GetObsidianBlockInitialization()
        {
            // Se abre con VIEW, no con EDIT: si no, la decisión de que descargar sea una lectura
            // no sirve de nada — quien solo puede ver el catálogo nunca llegaría a esta pantalla
            // para bajar el archivo. Las acciones que modifican (Save, Retire, Reactivate,
            // Preview) siguen exigiendo EDIT cada una por su cuenta, y options.CanEdit le dice al
            // editor si debe mostrarse en modo consulta.
            if ( !CanView() )
            {
                return new { notAuthorized = true };
            }

            using ( var rockContext = new RockContext() )
            {
                var idParam = PageParameter( PageParameterKey.QrCodeId );
                var entity = LoadEntity( rockContext, idParam );

                return new
                {
                    notAuthorized = false,
                    options = GetOptions( rockContext ),
                    code = entity != null
                        ? ToBag( rockContext, entity )
                        : NewBag()
                };
            }
        }

        #endregion

        #region Block Actions

        /// <summary>
        /// Guarda el código, creando o actualizando el short link detrás de uno dinámico.
        /// </summary>
        [BlockAction]
        public BlockActionResult Save( QrCodeBag bag )
        {
            if ( !CanEdit() )
            {
                return ActionForbidden( "No tenés permiso para editar códigos QR." );
            }

            if ( bag == null )
            {
                return ActionBadRequest( "No se recibió el código." );
            }

            using ( var rockContext = new RockContext() )
            {
                var service = new QrCodeService( rockContext );
                var entity = LoadEntity( rockContext, bag.IdKey );
                var isNew = entity == null;

                if ( isNew )
                {
                    entity = new QrCode { IsActive = true };
                    service.Add( entity );
                }

                var applyError = ApplyBag( rockContext, entity, bag, isNew );
                if ( applyError.IsNotNullOrWhiteSpace() )
                {
                    return ActionBadRequest( applyError );
                }

                var designError = QrRenderService.ValidateDesign( entity.GetDesign() );
                if ( designError.IsNotNullOrWhiteSpace() )
                {
                    return ActionBadRequest( designError );
                }

                if ( entity.QrType == QrCodeType.Dinamico )
                {
                    var shortLinkError = SyncShortLink( rockContext, entity, bag );
                    if ( shortLinkError.IsNotNullOrWhiteSpace() )
                    {
                        return ActionBadRequest( shortLinkError );
                    }
                }

                var integrityError = QrCatalogService.ValidateIntegrity( entity );
                if ( integrityError.IsNotNullOrWhiteSpace() )
                {
                    return ActionBadRequest( integrityError );
                }

                // El único commit. Es también lo que vacía PageShortLinkCache vía
                // PageShortLink.UpdateCache — un UPDATE por SQL directo dejaría un link con
                // calendario sirviendo su destino viejo desde un cache que no expira.
                rockContext.SaveChanges();

                return ActionOk( ToBag( rockContext, entity ) );
            }
        }

        /// <summary>
        /// Renderiza el diseño actual sin guardar, para que el editor muestre el render real del
        /// servidor y no una aproximación.
        /// </summary>
        [BlockAction]
        public BlockActionResult Preview( QrCodeBag bag )
        {
            if ( !CanEdit() )
            {
                return ActionForbidden( "No tenés permiso." );
            }

            if ( bag == null )
            {
                return ActionBadRequest( "Falta el código." );
            }

            var design = FromDesignBag( bag.Design );
            var designBag = ToDesignBag( design );

            using ( var rockContext = new RockContext() )
            {
                var payload = bag.QrType == ( int ) QrCodeType.Dinamico
                    ? ( bag.ShortLinkUrl.IsNotNullOrWhiteSpace() ? bag.ShortLinkUrl : PreviewShortLinkUrl( bag.Token ) )
                    : QrCatalogService.BuildStaticPayload( ( QrStaticContentType? ) bag.StaticContentType, bag.StaticContent );

                if ( payload.IsNullOrWhiteSpace() )
                {
                    designBag.ValidationMessage = designBag.ValidationMessage ?? "Falta el contenido del código.";
                    return ActionOk( new { svg = ( string ) null, design = designBag } );
                }

                // Se sigue renderizando aunque la validación falle: el contraste bajo o el código
                // invertido son avisos sobre la fiabilidad AL IMPRIMIR, no errores de render.
                // Dejar la vista previa en blanco esconde justamente lo que el usuario necesita
                // ver para corregirlo. La descarga sí queda bloqueada, en DownloadSvg/DownloadPng.
                var logo = GetLogoBytes( rockContext, bag.LogoBinaryFile.GetEntityId<BinaryFile>( rockContext ), out var mimeType );

                return ActionOk( new
                {
                    svg = QrRenderService.RenderSvg( payload, design, 10, 0, logo, mimeType ),
                    design = designBag
                } );
            }
        }

        /// <summary>
        /// Devuelve el código como SVG, con tamaño físico opcional. Es el formato de imprenta:
        /// vectorial, y el único que lleva el logo compuesto del lado del servidor.
        /// </summary>
        [BlockAction]
        public BlockActionResult DownloadSvg( string idKey, double printSideCm = 0 )
        {
            return RenderForDownload( idKey, ( rockContext, entity, design, logo, mimeType ) => new
            {
                fileName = BuildFileName( entity, "svg" ),
                mimeType = "image/svg+xml",
                content = QrRenderService.RenderSvg(
                    QrCatalogService.BuildPayload( entity, rockContext ), design, 10, printSideCm, logo, mimeType )
            } );
        }

        /// <summary>
        /// Devuelve el código como data URI PNG.
        /// </summary>
        /// <remarks>
        /// Solo color: QRCoder 1.3.9 no soporta logo en ningún renderer que evite GDI+, así que un
        /// PNG con logo lo rasteriza el navegador a partir del SVG. La respuesta dice cuál de los
        /// dos casos aplica en vez de descartar el logo en silencio.
        /// </remarks>
        [BlockAction]
        public BlockActionResult DownloadPng( string idKey, int pixelsPerModule = 20 )
        {
            return RenderForDownload( idKey, ( rockContext, entity, design, logo, mimeType ) => new
            {
                fileName = BuildFileName( entity, "png" ),
                mimeType = "image/png",
                content = QrRenderService.RenderPngDataUri(
                    QrCatalogService.BuildPayload( entity, rockContext ), design, pixelsPerModule ),
                logoRenderedClientSide = design.LogoSizePercent > 0
            } );
        }

        /// <summary>
        /// Da de baja un código en vez de borrarlo. Un QR no se borra de verdad mientras pueda
        /// existir en papel.
        /// </summary>
        [BlockAction]
        public BlockActionResult Retire( string idKey )
        {
            if ( !CanEdit() )
            {
                return ActionForbidden( "No tenés permiso." );
            }

            using ( var rockContext = new RockContext() )
            {
                var entity = LoadEntity( rockContext, idKey );
                if ( entity == null )
                {
                    return ActionBadRequest( "No se encontró el código QR." );
                }

                QrCatalogService.Retire( entity );
                rockContext.SaveChanges();

                return ActionOk( ToBag( rockContext, entity ) );
            }
        }

        /// <summary>
        /// Vuelve a poner en uso un código dado de baja.
        /// </summary>
        /// <remarks>
        /// Dar de baja no borra nada — el short link sigue resolviendo — así que reactivar es
        /// solo quitarle la marca. Sin esta acción, dar de baja era irreversible desde la
        /// interfaz y había que tocar la base.
        /// </remarks>
        [BlockAction]
        public BlockActionResult Reactivate( string idKey )
        {
            if ( !CanEdit() )
            {
                return ActionForbidden( "No tenés permiso." );
            }

            using ( var rockContext = new RockContext() )
            {
                var entity = LoadEntity( rockContext, idKey );
                if ( entity == null )
                {
                    return ActionBadRequest( "No se encontró el código QR." );
                }

                entity.IsActive = true;
                entity.RetiredDateTime = null;
                rockContext.SaveChanges();

                return ActionOk( ToBag( rockContext, entity ) );
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Camino común de las dos descargas: cargar, validar y renderizar.
        /// </summary>
        /// <remarks>
        /// Descargar pide VIEW, no EDIT, a diferencia de Save/Retire/Reactivate/Preview. Bajar un
        /// archivo no modifica nada: el código ya existe, ya está aprobado y probablemente ya está
        /// impreso, y su contenido —la URL corta— es público por definición, va en el papel. Con
        /// EDIT, alguien de un ministerio que sí puede ver el catálogo no podía re-descargar el
        /// archivo de un código que su propio equipo mandó a imprimir, y terminaba pidiéndoselo a
        /// comunicación por chat. Preview se queda en EDIT porque renderiza un bag sin guardar,
        /// incluido el logo que trae adentro: es parte de la edición, no de la consulta.
        /// </remarks>
        private BlockActionResult RenderForDownload(
            string idKey,
            Func<RockContext, QrCode, QrDesign, byte[], string, object> render )
        {
            if ( !CanView() )
            {
                return ActionForbidden( "No tenés permiso." );
            }

            using ( var rockContext = new RockContext() )
            {
                var entity = LoadEntity( rockContext, idKey );
                if ( entity == null )
                {
                    return ActionBadRequest( "No se encontró el código QR." );
                }

                var payload = QrCatalogService.BuildPayload( entity, rockContext );
                if ( payload.IsNullOrWhiteSpace() )
                {
                    return ActionBadRequest( "Este código todavía no tiene contenido que codificar." );
                }

                var design = entity.GetDesign();
                var validation = QrRenderService.ValidateDesign( design );
                if ( validation.IsNotNullOrWhiteSpace() )
                {
                    return ActionBadRequest( validation );
                }

                var logo = GetLogoBytes( rockContext, entity.LogoBinaryFileId, out var mimeType );

                return ActionOk( render( rockContext, entity, design, logo, mimeType ) );
            }
        }

        /// <summary>
        /// Carga el código por IdKey, Guid o Id.
        /// </summary>
        private QrCode LoadEntity( RockContext rockContext, string idParam )
        {
            if ( idParam.IsNullOrWhiteSpace() || idParam == "0" )
            {
                return null;
            }

            var query = new QrCodeService( rockContext ).Queryable()
                .Include( q => q.PageShortLink )
                .Include( q => q.Category )
                .Include( q => q.LogoBinaryFile );

            var guid = idParam.AsGuidOrNull();
            if ( guid.HasValue )
            {
                return query.FirstOrDefault( q => q.Guid == guid.Value );
            }

            var id = Rock.Utility.IdHasher.Instance.GetId( idParam ) ?? idParam.AsIntegerOrNull();

            return id.HasValue ? query.FirstOrDefault( q => q.Id == id.Value ) : null;
        }

        /// <summary>
        /// Crea o actualiza el <see cref="PageShortLink"/> detrás de un código dinámico.
        /// </summary>
        private string SyncShortLink( RockContext rockContext, QrCode entity, QrCodeBag bag )
        {
            var siteId = GetShortLinkSiteId();

            if ( !siteId.HasValue )
            {
                return "No hay un sitio configurado para el dominio corto. Configurá el bloque antes de crear códigos dinámicos.";
            }

            // No acuñar un token que el route handler nunca va a buscar. Sin EnabledForShortening,
            // RockRouteHandler se salta toda la rama de short links y el código impreso devuelve
            // 404 en silencio — el fallo aparecería recién después de imprimir.
            var site = SiteCache.Get( siteId.Value );
            if ( site != null && !site.EnabledForShortening )
            {
                return $"El sitio '{site.Name}' no tiene 'Enabled For Shortening' activado. " +
                    "Los códigos creados bajo ese sitio devolverían 404 al escanearse. Activá la bandera y volvé a intentar.";
            }

            if ( bag.Destination.IsNullOrWhiteSpace() )
            {
                return "El destino del código dinámico es obligatorio.";
            }

            if ( !entity.PageShortLinkId.HasValue && entity.PageShortLink == null )
            {
                var createError = QrCatalogService.TryCreateShortLink(
                    rockContext, siteId.Value, bag.Destination, bag.Token, null, out var shortLink );

                if ( createError.IsNotNullOrWhiteSpace() )
                {
                    return createError;
                }

                entity.PageShortLink = shortLink;
                return null;
            }

            return QrCatalogService.TryUpdateDestination( rockContext, entity, bag.Destination );
        }

        /// <summary>
        /// El sitio atado al dominio corto, desde la configuración del bloque.
        /// </summary>
        /// <remarks>
        /// Siempre explícito, nunca inferido. El fallback del filtro Lava <c>CreateShortLink</c>
        /// elige el primer sitio NO habilitado para acortar, y un token creado bajo el sitio
        /// equivocado simplemente no resuelve en el dominio corto.
        /// </remarks>
        private int? GetShortLinkSiteId()
        {
            var value = GetAttributeValue( AttributeKey.ShortLinkSite );

            if ( value.IsNullOrWhiteSpace() )
            {
                return null;
            }

            // SiteFieldType guarda el Id ENTERO, no el Guid: su GetPrivateValue hace
            // `SiteCache.GetId( publicValue.AsGuid() ).ToStringSafe()`. La versión anterior leía
            // este ajuste con AsGuidOrNull(), que sobre "12" devuelve null — así que el bloque
            // creía que no había sitio configurado y deshabilitaba los códigos dinámicos SIEMPRE,
            // sin importar la configuración. Se acepta el Guid igual por si otra ruta lo escribe así.
            var siteId = value.AsIntegerOrNull();
            if ( siteId.HasValue )
            {
                return SiteCache.Get( siteId.Value )?.Id;
            }

            var siteGuid = value.AsGuidOrNull();

            return siteGuid.HasValue ? SiteCache.Get( siteGuid.Value )?.Id : null;
        }

        /// <summary>
        /// URL de ejemplo para la vista previa de un código dinámico que todavía no se guardó.
        /// </summary>
        private string PreviewShortLinkUrl( string token )
        {
            var siteId = GetShortLinkSiteId();
            var domain = siteId.HasValue ? SiteCache.Get( siteId.Value )?.DefaultDomainUri?.ToString() : null;

            domain = domain.IsNullOrWhiteSpace() ? "https://ejemplo.gt/" : domain.TrimEnd( '/' ) + "/";

            return domain + ( token.IsNullOrWhiteSpace() ? "preview" : token.Trim() );
        }

        /// <summary>
        /// Opciones del editor, incluido si los códigos dinámicos están disponibles.
        /// </summary>
        private QrCodeDetailOptionsBag GetOptions( RockContext rockContext )
        {
            var options = new QrCodeDetailOptionsBag
            {
                CanEdit = CanEdit(),
                MaxLogoSizePercent = QrDesign.MaxLogoSizePercent,
                MinContrastRatio = QrDesign.MinContrastRatio,
                MinPrintSideCm = QrRenderService.MinPrintSideCm,
                ListPageUrl = this.GetLinkedPageUrl( AttributeKey.ListPage ),
                StaticContentTypes = new List<ListItemBag>
                {
                    new ListItemBag { Value = ( ( int ) QrStaticContentType.Url ).ToString(), Text = "Enlace (URL)" },
                    new ListItemBag { Value = ( ( int ) QrStaticContentType.Texto ).ToString(), Text = "Texto" },
                    new ListItemBag { Value = ( ( int ) QrStaticContentType.Email ).ToString(), Text = "Correo" },
                    new ListItemBag { Value = ( ( int ) QrStaticContentType.Telefono ).ToString(), Text = "Teléfono" },
                    new ListItemBag { Value = ( ( int ) QrStaticContentType.VCard ).ToString(), Text = "Contacto (vCard)" },
                    new ListItemBag { Value = ( ( int ) QrStaticContentType.Wifi ).ToString(), Text = "Red wifi" }
                }
            };

            var siteId = GetShortLinkSiteId();
            var site = siteId.HasValue ? SiteCache.Get( siteId.Value ) : null;

            if ( site == null )
            {
                options.HasShortDomain = false;
                options.ShortDomainMessage = "No hay un sitio configurado para el dominio corto, así que solo se pueden crear códigos estáticos. Configurá el bloque para habilitar los dinámicos.";
            }
            else if ( !site.EnabledForShortening )
            {
                // Esta bandera envuelve TODA la rama de short links de RockRouteHandler. Apagada,
                // Rock ni busca el token: 404 sin excepción y sin log. Es un bloqueo duro.
                options.HasShortDomain = false;
                options.ShortDomainMessage = $"El sitio '{site.Name}' no tiene 'Enabled For Shortening' activado. " +
                    "Sin esa bandera, Rock ni siquiera busca el token y todo código impreso devuelve 404 en silencio.";
            }
            else
            {
                options.HasShortDomain = true;
                options.ShortDomain = site.DefaultDomainUri?.ToString();

                var warnings = new List<string>();

                if ( !site.EnableExclusiveRoutes )
                {
                    warnings.Add( $"El sitio '{site.Name}' no tiene 'Enable Exclusive Routes': un token que exista en otro sitio puede resolver al destino equivocado." );
                }

                if ( !site.EnableVisitorTracking )
                {
                    warnings.Add( $"El sitio '{site.Name}' no tiene 'Enable Visitor Tracking': los escaneos anónimos no se van a poder distinguir entre sí." );
                }

                options.ShortDomainMessage = warnings.Any() ? string.Join( " ", warnings ) : null;
            }

            options.Categories = GetCategories( rockContext );

            return options;
        }

        /// <summary>
        /// Categorías bajo las que se puede archivar un código.
        /// </summary>
        private List<ListItemBag> GetCategories( RockContext rockContext )
        {
            var entityTypeId = EntityTypeCache.GetId<QrCode>();
            if ( !entityTypeId.HasValue )
            {
                return new List<ListItemBag>();
            }

            return new CategoryService( rockContext ).Queryable()
                .Where( c => c.EntityTypeId == entityTypeId.Value )
                .OrderBy( c => c.Order ).ThenBy( c => c.Name )
                .Select( c => new { c.Guid, c.Name } )
                .ToList()
                .Select( c => new ListItemBag { Value = c.Guid.ToString(), Text = c.Name } )
                .ToList();
        }

        /// <summary>
        /// Un código nuevo, con los valores por defecto del diseño.
        /// </summary>
        private QrCodeBag NewBag()
        {
            return new QrCodeBag
            {
                IdKey = null,
                QrType = ( int ) QrCodeType.Dinamico,
                IsTypeLocked = false,
                IsActive = true,
                Design = ToDesignBag( new QrDesign().WithDefaults() )
            };
        }

        /// <summary>
        /// Mapea la entidad a su bag, leyendo el destino del short link en vez de copiarlo.
        /// </summary>
        private QrCodeBag ToBag( RockContext rockContext, QrCode entity )
        {
            var design = entity.GetDesign();

            var bag = new QrCodeBag
            {
                IdKey = entity.IdKey,
                Name = entity.Name,
                Description = entity.Description,
                Category = entity.Category?.ToListItemBag(),
                QrType = ( int ) entity.QrType,

                // El tipo de un código guardado queda congelado. Convertir un estático en la base
                // sería trivial y engañoso: lo impreso seguiría siendo estático.
                IsTypeLocked = entity.Id > 0,

                StaticContentType = ( int? ) entity.StaticContentType,
                StaticContent = entity.StaticContent,
                LogoBinaryFile = entity.LogoBinaryFile?.ToListItemBag(),
                IsActive = entity.IsActive,
                RetiredDateTime = entity.RetiredDateTime?.ToRockDateTimeOffset(),
                Design = ToDesignBag( design )
            };

            if ( entity.QrType == QrCodeType.Dinamico && entity.PageShortLinkId.HasValue )
            {
                var shortLink = entity.PageShortLink ?? new PageShortLinkService( rockContext ).Get( entity.PageShortLinkId.Value );

                if ( shortLink != null )
                {
                    bag.Token = shortLink.Token;
                    bag.ShortLinkUrl = shortLink.ShortLinkUrl;

                    // El campo EDITABLE es el destino BASE (shortLink.Url), no el vigente.
                    // Traer acá GetCurrentDestination() —que en un enlace con calendario devuelve
                    // la URL de la ventana activa— hacía que abrir el código y darle Guardar sin
                    // tocar nada escribiera esa URL encima de Url y borrara el destino de
                    // respaldo, en silencio y sin notarse hasta que el calendario expiraba.
                    bag.Destination = shortLink.Url;

                    // Lo vigente viaja aparte, de solo lectura, para que la pantalla pueda decir
                    // a dónde resuelve hoy sin que eso sea lo que se guarda.
                    bag.EffectiveDestination = QrCatalogService.GetCurrentDestination( entity, rockContext );
                    bag.HasSchedules = shortLink.IsScheduled;
                }

                var metrics = QrMetricsService.GetCodeMetrics( rockContext, entity.PageShortLinkId.Value );
                bag.ScanCount = metrics.TotalScans;
                bag.LastScanDateTime = metrics.LastScanDateTime?.ToRockDateTimeOffset();
            }

            var payload = QrCatalogService.BuildPayload( entity, rockContext );
            if ( payload.IsNotNullOrWhiteSpace() && QrRenderService.ValidateDesign( design ).IsNullOrWhiteSpace() )
            {
                var logo = GetLogoBytes( rockContext, entity.LogoBinaryFileId, out var mimeType );
                bag.PreviewSvg = QrRenderService.RenderSvg( payload, design, 10, 0, logo, mimeType );
            }

            return bag;
        }

        /// <summary>
        /// Vuelca el bag sobre la entidad.
        /// </summary>
        /// <returns>Un mensaje de error, o <c>null</c> si el bag se pudo aplicar entero.</returns>
        private string ApplyBag( RockContext rockContext, QrCode entity, QrCodeBag bag, bool isNew )
        {
            entity.Name = bag.Name;
            entity.Description = bag.Description;
            entity.IsActive = bag.IsActive;
            entity.CategoryId = bag.Category.GetEntityId<Category>( rockContext );

            // El Guid del logo llega del cliente y NO es de fiar. Sin validarlo, cualquiera con
            // acceso a este bloque podía mandar el Guid de un BinaryFile ajeno y recuperar su
            // contenido: Preview lo devuelve incrustado como data:{mime};base64 dentro del SVG.
            // Se exige VIEW sobre el BinaryFileType, el mismo criterio que aplica GetFile.ashx.
            var requestedLogoId = bag.LogoBinaryFile.GetEntityId<BinaryFile>( rockContext );

            if ( requestedLogoId.HasValue )
            {
                var logo = GetAuthorizedImageFile( rockContext, requestedLogoId.Value );

                if ( logo == null )
                {
                    return "No se puede usar ese archivo como logo: no tenés permiso para verlo, o no es una imagen.";
                }

                entity.LogoBinaryFileId = logo.Id;

                // El ImageUploader sube el logo como archivo TEMPORAL. Si no se marca permanente,
                // el job de limpieza de Rock lo borra y el código se queda sin logo días después
                // de haberse impreso, sin que nadie toque nada.
                // Se hace DESPUÉS de la validación a propósito: marcar permanente es escribirle a
                // un archivo, y no se le escribe a un archivo que no se tiene permiso de ver.
                if ( logo.IsTemporary )
                {
                    logo.IsTemporary = false;
                }
            }
            else
            {
                entity.LogoBinaryFileId = null;
            }

            var design = FromDesignBag( bag.Design );

            // Invariante: sin logo no hay tamaño de logo. Al vaciar el ImageUploader el cliente
            // puede seguir mandando el logoSizePercent viejo; persistirlo hace que GetEccLevel
            // fuerce ECC H — un código bastante más denso — para reservar sitio a un logo que ya
            // no existe. Se fuerza acá para que la invariante valga aunque llegue un bag
            // inconsistente.
            if ( !entity.LogoBinaryFileId.HasValue )
            {
                design.LogoSizePercent = 0;
            }

            entity.SetDesign( design );

            // El tipo solo se puede fijar mientras el código es nuevo.
            if ( isNew )
            {
                entity.QrType = ( QrCodeType ) bag.QrType;
            }

            if ( entity.QrType == QrCodeType.Estatico )
            {
                entity.StaticContentType = ( QrStaticContentType? ) bag.StaticContentType;
                entity.StaticContent = bag.StaticContent;
            }

            return null;
        }

        /// <summary>
        /// Devuelve el <see cref="BinaryFile"/> solo si la persona actual puede verlo y es una
        /// imagen; si no, <c>null</c>.
        /// </summary>
        /// <remarks>
        /// La seguridad de los archivos de Rock vive en el <see cref="BinaryFileType"/>, que es lo
        /// que consulta <c>GetFile.ashx</c> antes de servir un archivo. Este bloque tiene que
        /// aplicar el mismo criterio porque hace exactamente lo mismo: entregar el contenido del
        /// archivo al cliente, solo que incrustado en base64 dentro del SVG.
        /// El filtro de mime no es cosmético: sin él, un .pdf o un .docx igual se embebería y
        /// viajaría entero al navegador.
        /// </remarks>
        private BinaryFile GetAuthorizedImageFile( RockContext rockContext, int binaryFileId )
        {
            var binaryFile = new BinaryFileService( rockContext ).Get( binaryFileId );

            if ( binaryFile == null || !binaryFile.BinaryFileTypeId.HasValue )
            {
                return null;
            }

            // La propiedad de navegación puede no venir cargada; se resuelve igual que en
            // GetFile.ashx antes de preguntar por permisos.
            binaryFile.BinaryFileType = binaryFile.BinaryFileType
                ?? new BinaryFileTypeService( rockContext ).Get( binaryFile.BinaryFileTypeId.Value );

            if ( binaryFile.BinaryFileType == null
                || !binaryFile.BinaryFileType.IsAuthorized( Authorization.VIEW, RequestContext?.CurrentPerson ) )
            {
                return null;
            }

            if ( binaryFile.MimeType.IsNullOrWhiteSpace()
                || !binaryFile.MimeType.StartsWith( "image/", StringComparison.OrdinalIgnoreCase ) )
            {
                return null;
            }

            return binaryFile;
        }

        /// <summary>
        /// Carga los bytes del logo, o null si no hay o si no se puede usar.
        /// </summary>
        /// <remarks>
        /// Devuelve null en vez de lanzar: un logo al que no se tiene acceso degrada el render a
        /// un código sin logo, que sigue siendo válido. Tirar una excepción acá rompería la vista
        /// previa y la descarga de un código que por lo demás está bien.
        /// </remarks>
        private byte[] GetLogoBytes( RockContext rockContext, int? binaryFileId, out string mimeType )
        {
            mimeType = null;

            if ( !binaryFileId.HasValue )
            {
                return null;
            }

            var binaryFile = GetAuthorizedImageFile( rockContext, binaryFileId.Value );
            if ( binaryFile == null )
            {
                return null;
            }

            mimeType = binaryFile.MimeType;

            using ( var stream = binaryFile.ContentStream )
            {
                if ( stream == null )
                {
                    return null;
                }

                using ( var memory = new System.IO.MemoryStream() )
                {
                    stream.CopyTo( memory );
                    return memory.ToArray();
                }
            }
        }

        /// <summary>
        /// Nombre de archivo legible a partir del nombre del código.
        /// </summary>
        private static string BuildFileName( QrCode entity, string extension )
        {
            var name = ( entity.Name ?? "qr" ).Trim();
            var safe = new string( name.Select( c => char.IsLetterOrDigit( c ) ? char.ToLowerInvariant( c ) : '-' ).ToArray() );

            while ( safe.Contains( "--" ) )
            {
                safe = safe.Replace( "--", "-" );
            }

            safe = safe.Trim( '-' );

            return $"qr-{( safe.IsNullOrWhiteSpace() ? "codigo" : safe )}.{extension}";
        }

        private static QrDesignBag ToDesignBag( QrDesign design )
        {
            return new QrDesignBag
            {
                ForegroundColor = design.ForegroundColor,
                BackgroundColor = design.BackgroundColor,
                LogoSizePercent = design.LogoSizePercent,
                ContrastRatio = Math.Round( QrRenderService.GetContrastRatio( design.ForegroundColor, design.BackgroundColor ), 2 ),
                ValidationMessage = QrRenderService.ValidateDesign( design ),
                EccLevel = QrRenderService.GetEccLevel( design ).ToString()
            };
        }

        private static QrDesign FromDesignBag( QrDesignBag bag )
        {
            if ( bag == null )
            {
                return new QrDesign().WithDefaults();
            }

            return new QrDesign
            {
                ForegroundColor = bag.ForegroundColor,
                BackgroundColor = bag.BackgroundColor,
                LogoSizePercent = bag.LogoSizePercent
            }.WithDefaults();
        }

        /// <summary>
        /// Solo comunicación y TI crean QR. La validación va en el bloque, no en la navegación:
        /// quien no está en el grupo tampoco llega por URL directa.
        /// </summary>
        private bool CanEdit()
        {
            return BlockCache.IsAuthorized( Authorization.EDIT, RequestContext?.CurrentPerson )
                || BlockCache.IsAuthorized( Authorization.ADMINISTRATE, RequestContext?.CurrentPerson );
        }

        /// <summary>
        /// Quién puede consultar y descargar un código ya existente. Ver el catálogo y bajar el
        /// archivo son la misma operación de lectura; ver <see cref="RenderForDownload"/>.
        /// </summary>
        private bool CanView()
        {
            return BlockCache.IsAuthorized( Authorization.VIEW, RequestContext?.CurrentPerson ) || CanEdit();
        }

        #endregion
    }
}
