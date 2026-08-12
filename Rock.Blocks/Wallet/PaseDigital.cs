using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace Rock.Blocks.Wallet
{
    /// <summary>
    /// Tarjeta web del pase digital de la iglesia: renderiza en una página de Rock el MISMO
    /// diseño del pase de Apple/Google Wallet (navy, logo VidaReal.tv, foto, NOMBRE /
    /// ASISTO A = campus de la persona, QR = Alternate Id de la persona autenticada). Las
    /// imágenes salen de la propia WalletTemplate (strip + logo), así la web y el wallet
    /// siempre lucen igual. Botón opcional "Guardar en mi teléfono" que emite el pase real
    /// vía WalletService (misma ruta que el filtro Lava WalletPassUrl).
    /// </summary>
    [DisplayName( "Pase Digital" )]
    [Category( "Wallet" )]
    [Description( "Tarjeta del pase digital de la persona autenticada, con el diseño del pase de wallet." )]

    [TextField(
        "Plantilla de wallet",
        Description = "Guid de la WalletTemplate cuyo diseño/imágenes usa la tarjeta y con la que se emite el pase.",
        IsRequired = true,
        DefaultValue = PaseDigital.DefaultTemplateGuid,
        Order = 0,
        Key = AttributeKey.TemplateGuid )]
    [BooleanField(
        "Mostrar botón de wallet",
        Description = "Muestra el botón \"Guardar en mi teléfono\" (solo si Apple o Google Wallet están configurados). Oculto por defecto (pedido 2026-08-10).",
        DefaultBooleanValue = false,
        Order = 1,
        Key = AttributeKey.ShowWalletButton )]

    [Rock.SystemGuid.BlockTypeGuid( "f0a1b2c3-d4e5-4f60-8a01-950000000002" )]
    public class PaseDigital : RockBlockType
    {
        /// <summary>Guid del seed de la plantilla del pase de check-in (migración 009 del plugin Wallet).</summary>
        public const string DefaultTemplateGuid = "f0a1b2c3-d4e5-4f60-8a01-940000000002";

        private static class AttributeKey
        {
            public const string TemplateGuid = "TemplateGuid";
            public const string ShowWalletButton = "ShowWalletButton";
        }

        public override object GetObsidianBlockInitialization()
        {
            var person = RequestContext?.CurrentPerson;
            if ( person == null )
            {
                return new InitBag { isAuthenticated = false };
            }

            using ( var rockContext = new RockContext() )
            {
                var bag = new InitBag
                {
                    isAuthenticated = true,
                    personName = ShortName( person ),
                    // "Asisto a": campus de la familia principal (mismo dato que el Lava
                    // {{ Person | Campus | Property:'Name' }} de la plantilla de wallet).
                    campusName = person.GetCampus()?.Name,
                    showWalletButton = GetAttributeValue( AttributeKey.ShowWalletButton ).AsBoolean()
                        && ( WalletService.IsAppleConfigured() || WalletService.IsGoogleConfigured() )
                };

                var alternateId = GetOrCreateAlternateId( rockContext, person );
                if ( alternateId.IsNotNullOrWhiteSpace() )
                {
                    bag.code = alternateId;
                    bag.qrUrl = "/GetQRCode.ashx?data=" + Uri.EscapeDataString( alternateId );
                }

                // Imágenes de la plantilla (mismo strip/logo del pase de wallet).
                var template = GetTemplate( rockContext );
                if ( template != null )
                {
                    var fileService = new BinaryFileService( rockContext );
                    bag.bannerUrl = ToImageUrl( fileService, template.StripBinaryFileId );
                    bag.logoUrl = ToImageUrl( fileService, template.LogoBinaryFileId );
                }

                return bag;
            }
        }

        /// <summary>
        /// Emite (o reusa) el pase de wallet de la persona y devuelve la URL de descarga
        /// (iPhone → hoja de Apple Wallet, Android → Guardar en Google Wallet). Se invoca al
        /// hacer clic — no en el init — para no emitir pases a quien solo ve la página.
        /// </summary>
        [BlockAction( "GetWalletUrl" )]
        public BlockActionResult GetWalletUrl()
        {
            var person = RequestContext?.CurrentPerson;
            if ( person?.PrimaryAliasId == null )
            {
                return ActionBadRequest( "Inicia sesión para guardar tu pase." );
            }

            var templateGuid = GetAttributeValue( AttributeKey.TemplateGuid ).AsGuidOrNull();
            var root = ( GlobalAttributesCache.Value( "PublicApplicationRoot" ) ?? string.Empty ).Trim();
            if ( templateGuid == null || root.IsNullOrWhiteSpace() )
            {
                return ActionBadRequest( "El módulo de wallet no está configurado." );
            }

            using ( var rockContext = new RockContext() )
            {
                var data = new Dictionary<string, string>
                {
                    ["AlternateId"] = GetOrCreateAlternateId( rockContext, person )
                };

                var pass = WalletService.GetOrIssuePass( rockContext, templateGuid.Value, person, person.PrimaryAliasId, data );

                var url = root.TrimEnd( '/' )
                    + $"/api/vidareal/wallet/v1/download/{Uri.EscapeDataString( pass.SerialNumber )}?token={Uri.EscapeDataString( pass.AuthenticationToken )}";

                return ActionOk( new WalletUrlResponseBag { url = url } );
            }
        }

        #region Helpers

        private WalletTemplate GetTemplate( RockContext rockContext )
        {
            var guid = GetAttributeValue( AttributeKey.TemplateGuid ).AsGuidOrNull();
            return guid.HasValue ? new WalletTemplateService( rockContext ).Get( guid.Value ) : null;
        }

        /// <summary>Primer nombre + primer apellido (mismo criterio que el pase de wallet).</summary>
        private static string ShortName( Person person )
        {
            var first = ( person.NickName ?? string.Empty ).Split( ' ' ).FirstOrDefault();
            var last = ( person.LastName ?? string.Empty ).Split( ' ' ).FirstOrDefault();
            return $"{first} {last}".Trim();
        }

        private static string ToImageUrl( BinaryFileService fileService, int? binaryFileId )
        {
            if ( !binaryFileId.HasValue )
            {
                return null;
            }

            var file = fileService.Get( binaryFileId.Value );
            return file != null ? "/GetImage.ashx?guid=" + file.Guid : null;
        }

        /// <summary>
        /// Alternate Id de la persona (search key core, el mismo que escanea el check-in y que
        /// lleva el QR del pase de wallet); lo crea si no existe. Misma lógica que el filtro
        /// Lava WalletPassUrl (privado en Rock.Lava, no reutilizable desde aquí).
        /// </summary>
        private static string GetOrCreateAlternateId( RockContext rockContext, Person person )
        {
            var searchTypeId = DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.PERSON_SEARCH_KEYS_ALTERNATE_ID.AsGuid() )?.Id;
            if ( searchTypeId == null )
            {
                return null;
            }

            var searchKeyService = new PersonSearchKeyService( rockContext );
            var existing = searchKeyService.Queryable()
                .Where( k => k.SearchTypeValueId == searchTypeId.Value && k.PersonAlias.PersonId == person.Id )
                .Select( k => k.SearchValue )
                .FirstOrDefault();
            if ( existing.IsNotNullOrWhiteSpace() )
            {
                return existing;
            }

            var searchKey = new PersonSearchKey
            {
                PersonAliasId = person.PrimaryAliasId,
                SearchTypeValueId = searchTypeId.Value,
                SearchValue = PersonSearchKeyService.GenerateRandomAlternateId( true, rockContext )
            };
            searchKeyService.Add( searchKey );
            rockContext.SaveChanges();

            return searchKey.SearchValue;
        }

        #endregion

        #region View Models

        public class InitBag
        {
            public bool isAuthenticated { get; set; }
            public string personName { get; set; }
            public string campusName { get; set; }
            public string code { get; set; }
            public string qrUrl { get; set; }
            public string bannerUrl { get; set; }
            public string logoUrl { get; set; }
            public bool showWalletButton { get; set; }
        }

        public class WalletUrlResponseBag
        {
            public string url { get; set; }
        }

        #endregion
    }
}
