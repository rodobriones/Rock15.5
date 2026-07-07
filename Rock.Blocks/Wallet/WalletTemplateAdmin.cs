using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.ViewModels.Utility;
using Rock.Web.Cache;

namespace Rock.Blocks.Wallet
{
    /// <summary>
    /// Administración de plantillas del módulo Wallet: diseñar los pases de Apple/Google Wallet
    /// (colores, imágenes, campos con Lava, código de barras) que los demás módulos consumen
    /// vía WalletService.
    /// </summary>
    [DisplayName( "Wallet Template Admin" )]
    [Category( "Wallet" )]
    [Description( "Diseño de plantillas de pases de Apple Wallet y Google Wallet." )]
    [Rock.SystemGuid.BlockTypeGuid( "f0a1b2c3-d4e5-4f60-8a01-950000000001" )]
    public class WalletTemplateAdmin : RockBlockType
    {
        public override object GetObsidianBlockInitialization()
        {
            using ( var rockContext = new RockContext() )
            {
                return new InitBag
                {
                    canEdit = CanEdit(),
                    appleConfigured = WalletService.IsAppleConfigured(),
                    googleConfigured = WalletService.IsGoogleConfigured(),
                    templates = GetTemplateBags( rockContext )
                };
            }
        }

        [BlockAction( "GetTemplates" )]
        public BlockActionResult GetTemplates()
        {
            using ( var rockContext = new RockContext() )
            {
                return ActionOk( new TemplatesResponseBag { templates = GetTemplateBags( rockContext ) } );
            }
        }

        [BlockAction( "SaveTemplate" )]
        public BlockActionResult SaveTemplate( SaveTemplateRequestBag bag )
        {
            if ( !CanEdit() )
            {
                return ActionBadRequest( "No tienes permiso para editar plantillas." );
            }

            var template = bag?.template;
            if ( template == null || template.name.IsNullOrWhiteSpace() )
            {
                return ActionBadRequest( "El nombre es obligatorio." );
            }

            using ( var rockContext = new RockContext() )
            {
                var service = new WalletTemplateService( rockContext );

                WalletTemplate entity;
                if ( template.id > 0 )
                {
                    entity = service.Get( template.id );
                    if ( entity == null )
                    {
                        return ActionBadRequest( "La plantilla no existe." );
                    }

                    // La seed "Entrada de evento" la consume el módulo de Eventos por guid:
                    // desactivarla rompería los botones de wallet de Mis Entradas para todos.
                    if ( entity.Guid == Rock.Model.WalletService.EventTicketTemplateGuid.AsGuid() && !template.isActive )
                    {
                        return ActionBadRequest( "Esta plantilla la usa el módulo de Eventos y no se puede desactivar." );
                    }
                }
                else
                {
                    entity = new WalletTemplate();
                    service.Add( entity );
                }

                entity.Name = template.name.Truncate( 150, false );
                entity.Description = template.description;
                entity.IsActive = template.isActive;
                entity.PassStyle = ( Rock.Enums.Wallet.PassStyle ) template.passStyle;
                entity.AppleDesignJson = template.apple != null
                    ? Newtonsoft.Json.JsonConvert.SerializeObject( template.apple )
                    : null;
                entity.GoogleDesignJson = template.google != null
                    ? Newtonsoft.Json.JsonConvert.SerializeObject( template.google )
                    : null;

                entity.IconBinaryFileId = ResolveImage( rockContext, template.icon );
                entity.LogoBinaryFileId = ResolveImage( rockContext, template.logo );
                entity.StripBinaryFileId = ResolveImage( rockContext, template.strip );

                rockContext.SaveChanges();

                // Cambió el diseño: los pases YA emitidos con esta plantilla se refrescan en
                // background (push a los teléfonos) para que estrenen el diseño.
                QueueRefreshForTemplate( entity.Id );

                return ActionOk( new TemplatesResponseBag { templates = GetTemplateBags( rockContext ), savedId = entity.Id } );
            }
        }

        [BlockAction( "DeleteTemplate" )]
        public BlockActionResult DeleteTemplate( DeleteTemplateRequestBag bag )
        {
            if ( !CanEdit() )
            {
                return ActionBadRequest( "No tienes permiso para editar plantillas." );
            }

            using ( var rockContext = new RockContext() )
            {
                var service = new WalletTemplateService( rockContext );
                var entity = service.Get( bag?.templateId ?? 0 );
                if ( entity == null )
                {
                    return ActionBadRequest( "La plantilla no existe." );
                }

                if ( entity.Guid == Rock.Model.WalletService.EventTicketTemplateGuid.AsGuid() )
                {
                    return ActionBadRequest( "Esta plantilla la usa el módulo de Eventos y no se puede eliminar." );
                }

                var hasPasses = new Rock.Model.WalletPassService( rockContext ).Queryable()
                    .Any( p => p.WalletTemplateId == entity.Id );
                if ( hasPasses )
                {
                    return ActionBadRequest( "No se puede eliminar: hay pases emitidos con esta plantilla. Desactívala en su lugar." );
                }

                service.Delete( entity );
                rockContext.SaveChanges();

                return ActionOk( new TemplatesResponseBag { templates = GetTemplateBags( rockContext ) } );
            }
        }

        #region Helpers

        private bool CanEdit()
        {
            return BlockCache.IsAuthorized( Rock.Security.Authorization.EDIT, RequestContext?.CurrentPerson );
        }

        /// <summary>
        /// ListItemBag del uploader (value = guid del BinaryFile) → Id, marcando el archivo como
        /// permanente (se sube temporal; mismo patrón que la imagen del evento).
        /// </summary>
        private static int? ResolveImage( RockContext rockContext, ListItemBag image )
        {
            var guid = image?.Value?.AsGuidOrNull();
            if ( !guid.HasValue )
            {
                return null;
            }

            var binaryFile = new BinaryFileService( rockContext ).Get( guid.Value );
            if ( binaryFile == null )
            {
                return null;
            }

            if ( binaryFile.IsTemporary )
            {
                binaryFile.IsTemporary = false;
            }

            return binaryFile.Id;
        }

        private static void QueueRefreshForTemplate( int templateId )
        {
            if ( !WalletService.IsAppleConfigured() && !WalletService.IsGoogleConfigured() )
            {
                return;
            }

            EventsRuntime.QueueBackgroundWork( $"WalletTemplateRefresh-{templateId}", ct =>
            {
                try
                {
                    List<int> passIds;
                    using ( var rockContext = new RockContext() )
                    {
                        passIds = new Rock.Model.WalletPassService( rockContext ).Queryable()
                            .Where( p => p.WalletTemplateId == templateId
                                && p.Status == Rock.Enums.Wallet.WalletPassStatus.Active )
                            .Select( p => p.Id )
                            .ToList();
                    }

                    foreach ( var passId in passIds )
                    {
                        using ( var rockContext = new RockContext() )
                        {
                            var pass = new Rock.Model.WalletPassService( rockContext ).Get( passId );
                            if ( pass != null )
                            {
                                WalletService.RefreshPass( rockContext, pass );
                            }
                        }
                    }
                }
                catch ( Exception ex )
                {
                    ExceptionLogService.LogException( new Exception( $"WalletTemplateAdmin: fallo el refresh de pases de la plantilla {templateId}.", ex ) );
                }
            } );
        }

        private static List<TemplateBag> GetTemplateBags( RockContext rockContext )
        {
            var passCounts = new Rock.Model.WalletPassService( rockContext ).Queryable()
                .GroupBy( p => p.WalletTemplateId )
                .Select( g => new { g.Key, Count = g.Count() } )
                .ToDictionary( g => g.Key, g => g.Count );

            var fileGuids = new BinaryFileService( rockContext ).Queryable();

            return new WalletTemplateService( rockContext ).Queryable()
                .OrderBy( t => t.Name )
                .ToList()
                .Select( t => new TemplateBag
                {
                    id = t.Id,
                    guid = t.Guid.ToString(),
                    name = t.Name,
                    description = t.Description,
                    isActive = t.IsActive,
                    passStyle = ( int ) t.PassStyle,
                    apple = ParseDesign<PassTemplateResolver.AppleDesign>( t.AppleDesignJson ),
                    google = ParseDesign<PassTemplateResolver.GoogleDesign>( t.GoogleDesignJson ),
                    icon = ToImageBag( rockContext, t.IconBinaryFileId ),
                    logo = ToImageBag( rockContext, t.LogoBinaryFileId ),
                    strip = ToImageBag( rockContext, t.StripBinaryFileId ),
                    passCount = passCounts.TryGetValue( t.Id, out var count ) ? count : 0
                } )
                .ToList();
        }

        private static T ParseDesign<T>( string json ) where T : class
        {
            if ( json.IsNullOrWhiteSpace() )
            {
                return null;
            }

            try
            {
                return Newtonsoft.Json.JsonConvert.DeserializeObject<T>( json );
            }
            catch
            {
                return null;
            }
        }

        private static ListItemBag ToImageBag( RockContext rockContext, int? binaryFileId )
        {
            if ( !binaryFileId.HasValue )
            {
                return null;
            }

            var file = new BinaryFileService( rockContext ).Get( binaryFileId.Value );
            if ( file == null )
            {
                return null;
            }

            return new ListItemBag { Value = file.Guid.ToString(), Text = file.FileName };
        }

        #endregion

        #region View Models

        public class InitBag
        {
            public bool canEdit { get; set; }
            public bool appleConfigured { get; set; }
            public bool googleConfigured { get; set; }
            public List<TemplateBag> templates { get; set; }
        }

        public class TemplateBag
        {
            public int id { get; set; }
            public string guid { get; set; }
            public string name { get; set; }
            public string description { get; set; }
            public bool isActive { get; set; }
            public int passStyle { get; set; }
            public PassTemplateResolver.AppleDesign apple { get; set; }
            public PassTemplateResolver.GoogleDesign google { get; set; }
            public ListItemBag icon { get; set; }
            public ListItemBag logo { get; set; }
            public ListItemBag strip { get; set; }
            public int passCount { get; set; }
        }

        public class TemplatesResponseBag
        {
            public List<TemplateBag> templates { get; set; }
            public int? savedId { get; set; }
        }

        public class SaveTemplateRequestBag
        {
            public TemplateBag template { get; set; }
        }

        public class DeleteTemplateRequestBag
        {
            public int templateId { get; set; }
        }

        #endregion
    }
}
