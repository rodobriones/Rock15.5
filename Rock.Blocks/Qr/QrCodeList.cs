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
using Rock.ViewModels.Blocks.Qr.QrCodeList;
using Rock.ViewModels.Utility;
using Rock.Web.Cache;

namespace Rock.Blocks.Qr
{
    /// <summary>
    /// El catálogo de códigos QR institucionales.
    /// </summary>
    /// <remarks>
    /// Es la entrega que cumple el objetivo número uno del módulo: hoy nadie sabe cuántos QR
    /// existen ni quién los creó. Ordena por escaneos porque así se encuentran los dos casos que
    /// importan — el QR que explotó y el que lleva meses muerto.
    /// </remarks>
    [DisplayName( "QR Code List" )]
    [Category( "Vida Real > QR" )]
    [Description( "Catálogo de códigos QR institucionales: qué existen, a dónde apuntan y cuánto se usan." )]
    [IconCssClass( "ti ti-list-search" )]

    #region Block Attributes

    [LinkedPage(
        "Página de detalle",
        Key = AttributeKey.DetailPage,
        Description = "La página con el bloque QR Code Detail.",
        IsRequired = false,
        Order = 0 )]

    [LinkedPage(
        "Página de reportería",
        Key = AttributeKey.DashboardPage,
        Description = "La página con el bloque QR Dashboard.",
        IsRequired = false,
        Order = 1 )]

    [LinkedPage(
        "Página de uso",
        Key = AttributeKey.MetricsPage,
        Description = "La página con el bloque QR Metrics.",
        IsRequired = false,
        Order = 2 )]

    [IntegerField(
        "Días sin escaneo para marcar dormido",
        Key = AttributeKey.StaleDays,
        Description = "Un código dinámico sin escaneos en este plazo se marca como dormido en la lista.",
        IsRequired = false,
        DefaultIntegerValue = DefaultStaleDays,
        Order = 3 )]

    #endregion

    [Rock.SystemGuid.BlockTypeGuid( "c7d2e5a0-4b31-4c8e-9d02-b40000000103" )]
    public class QrCodeList : RockBlockType
    {
        #region Keys

        private static class AttributeKey
        {
            public const string DetailPage = "DetailPage";
            public const string DashboardPage = "DashboardPage";
            public const string MetricsPage = "MetricsPage";
            public const string StaleDays = "StaleDays";
        }

        #endregion

        #region Constants

        /// <summary>
        /// Días sin escaneo tras los cuales un código se marca como dormido, cuando el bloque no
        /// tiene el atributo configurado.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Un trimestre es el ciclo natural de la mayoría del material impreso de la iglesia:
        /// menos que eso marca dormido un QR de un evento estacional que sigue siendo válido. El
        /// mismo valor vive en <see cref="QrMetrics"/> y en <see cref="QrDashboard"/>, porque los
        /// tres bloques se configuran por separado y tienen que arrancar diciendo lo mismo.
        /// </para>
        /// <para>
        /// <strong>Para el cliente:</strong> acá el dormido se calcula en el navegador a partir de
        /// <c>lastScanDateTime</c>. Un código que nunca se escaneó solo está dormido si su
        /// <c>createdDateTime</c> ya pasó este mismo plazo — si no, uno creado ayer nace dormido.
        /// Ese es el criterio que aplican <see cref="QrMetrics"/> y <see cref="QrDashboard"/> del
        /// lado del servidor, y por eso la fila trae la fecha de creación.
        /// </para>
        /// </remarks>
        private const int DefaultStaleDays = 90;

        #endregion

        #region Block Initialization

        /// <inheritdoc/>
        public override object GetObsidianBlockInitialization()
        {
            if ( !CanView() )
            {
                return new { notAuthorized = true };
            }

            using ( var rockContext = new RockContext() )
            {
                return new
                {
                    notAuthorized = false,
                    options = GetOptions( rockContext ),
                    rows = GetRows( rockContext )
                };
            }
        }

        #endregion

        #region Block Actions

        /// <summary>
        /// Recarga las filas. La lista completa de QR institucionales es de decenas, no de miles,
        /// así que se trae entera y el filtrado vive en el cliente.
        /// </summary>
        [BlockAction]
        public BlockActionResult Refresh()
        {
            if ( !CanView() )
            {
                return ActionForbidden( "No tenés permiso." );
            }

            using ( var rockContext = new RockContext() )
            {
                return ActionOk( new { rows = GetRows( rockContext ) } );
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Arma las filas del catálogo, resolviendo escaneos y último escaneo en dos consultas
        /// agregadas — no una por fila.
        /// </summary>
        private List<QrCodeRowBag> GetRows( RockContext rockContext )
        {
            var codes = new QrCodeService( rockContext ).Queryable()
                .AsNoTracking()
                .Include( q => q.Category )
                .Include( q => q.PageShortLink )
                .Include( q => q.CreatedByPersonAlias.Person )
                .OrderBy( q => q.Name )
                .ToList();

            var shortLinkIds = codes
                .Where( c => c.PageShortLinkId.HasValue )
                .Select( c => c.PageShortLinkId.Value )
                .Distinct()
                .ToList();

            var scanCounts = QrMetricsService.GetScanCounts( rockContext, shortLinkIds );
            var lastScans = QrMetricsService.GetLastScanDates( rockContext, shortLinkIds );

            var rows = new List<QrCodeRowBag>( codes.Count );

            foreach ( var code in codes )
            {
                var row = new QrCodeRowBag
                {
                    IdKey = code.IdKey,
                    Name = code.Name,
                    Description = code.Description,
                    CategoryName = code.Category?.Name,

                    // El Guid viaja además del nombre porque el filtro del catálogo debe agrupar
                    // por identidad, no por texto: hay categorías homónimas bajo padres distintos.
                    CategoryGuid = code.Category?.Guid.ToString(),
                    QrType = ( int ) code.QrType,
                    CreatedByName = code.CreatedByPersonAlias?.Person?.FullName,
                    CreatedDateTime = code.CreatedDateTime?.ToRockDateTimeOffset(),
                    IsActive = code.IsActive
                };

                if ( code.QrType == QrCodeType.Dinamico && code.PageShortLinkId.HasValue )
                {
                    var id = code.PageShortLinkId.Value;

                    row.ShortLinkUrl = code.PageShortLink?.ShortLinkUrl;

                    // Leído del short link en cada carga, nunca copiado: si alguien lo edita desde
                    // la pantalla nativa de Rock, el catálogo no miente.
                    row.Destination = QrCatalogService.GetCurrentDestination( code, rockContext );

                    row.ScanCount = scanCounts.TryGetValue( id, out var count ) ? count : 0;
                    row.LastScanDateTime = lastScans.TryGetValue( id, out var last )
                        ? last.ToRockDateTimeOffset()
                        : ( DateTimeOffset? ) null;
                }

                // Destination queda null para estáticos a propósito: el contenido de un QR de wifi
                // lleva la contraseña adentro y no se muestra en una lista.

                rows.Add( row );
            }

            return rows;
        }

        /// <summary>
        /// Opciones del catálogo.
        /// </summary>
        private QrCodeListOptionsBag GetOptions( RockContext rockContext )
        {
            var entityTypeId = EntityTypeCache.GetId<QrCode>();

            var categories = entityTypeId.HasValue
                ? new CategoryService( rockContext ).Queryable()
                    .Where( c => c.EntityTypeId == entityTypeId.Value )
                    .OrderBy( c => c.Order ).ThenBy( c => c.Name )
                    .Select( c => new { c.Guid, c.Name } )
                    .ToList()
                    .Select( c => new ListItemBag { Value = c.Guid.ToString(), Text = c.Name } )
                    .ToList()
                : new List<ListItemBag>();

            return new QrCodeListOptionsBag
            {
                Categories = categories,
                CanEdit = CanEdit(),
                DetailPageUrl = this.GetLinkedPageUrl( AttributeKey.DetailPage, new Dictionary<string, string> { { "QrCodeId", "((Key))" } } ),
                DashboardPageUrl = this.GetLinkedPageUrl( AttributeKey.DashboardPage ),
                MetricsPageUrl = this.GetLinkedPageUrl( AttributeKey.MetricsPage ),
                StaleDays = GetAttributeValue( AttributeKey.StaleDays ).AsIntegerOrNull() ?? DefaultStaleDays
            };
        }

        private bool CanView()
        {
            return BlockCache.IsAuthorized( Authorization.VIEW, RequestContext?.CurrentPerson );
        }

        private bool CanEdit()
        {
            return BlockCache.IsAuthorized( Authorization.EDIT, RequestContext?.CurrentPerson )
                || BlockCache.IsAuthorized( Authorization.ADMINISTRATE, RequestContext?.CurrentPerson );
        }

        #endregion
    }
}
