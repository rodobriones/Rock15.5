// <copyright>
// Copyright by the Spark Development Network
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;

using Rock;
using Rock.Attribute;
using Rock.Blocks;
using Rock.Data;
using Rock.Model;
using Rock.Utility;
using Rock.ViewModels.Utility;
using Rock.Web.Cache;

namespace Rock.Blocks.Dar
{
    [DisplayName( "Donation Dashboard" )]
    [Category( "Dar" )]
    [Description( "Dashboard personalizado para ver y filtrar transacciones de donaciones por cuenta, moneda y NIT." )]
    [IconCssClass( "fa fa-chart-bar" )]

    [AccountsField(
        "Cuentas",
        Key = AttributeKey.AccountsFilter,
        Description = "Cuentas financieras visibles en el dashboard. Dejar vacío para mostrar todas las cuentas activas.",
        IsRequired = false,
        Order = 0 )]

    [TextField(
        "NIT Person Attribute Key",
        Key = AttributeKey.PersonNitAttributeKey,
        Description = "Llave del atributo de persona donde se almacena la lista de NITs (ej. 'PersonNit'). Debe coincidir con el bloque de donación.",
        DefaultValue = "",
        IsRequired = false,
        Order = 1 )]

    [IntegerField(
        "Máx. Resultados",
        Key = AttributeKey.MaxResults,
        Description = "Número máximo de transacciones a cargar por búsqueda.",
        DefaultIntegerValue = 500,
        IsRequired = false,
        Order = 2 )]

    [BooleanField(
        "Permitir Exportación",
        Key = AttributeKey.AllowExport,
        Description = "Permitir exportar los resultados a CSV.",
        DefaultBooleanValue = true,
        Order = 3 )]

    [Rock.SystemGuid.EntityTypeGuid( "E3A4B5C6-1D2E-4F7A-8B9C-0D1E2F3A4B5C" )]
    [Rock.SystemGuid.BlockTypeGuid( "F4B5C6D7-2E3F-5A8B-9C0D-1E2F3A4B5C6D" )]
    public class DonationDashboard : RockBlockType
    {
        #region Keys

        private static class AttributeKey
        {
            public const string AccountsFilter = "AccountsFilter";
            public const string PersonNitAttributeKey = "PersonNitAttributeKey";
            public const string MaxResults = "MaxResults";
            public const string AllowExport = "AllowExport";
        }

        #endregion

        #region Initialization

        public override object GetObsidianBlockInitialization()
        {
            using ( var rockContext = new RockContext() )
            {
                return new InitBag
                {
                    allowExport = GetAttributeValue( AttributeKey.AllowExport ).AsBoolean(),
                    filterOptions = GetFilterOptionsInternal( rockContext )
                };
            }
        }

        #endregion

        #region Private Methods

        private FilterOptionsBag GetFilterOptionsInternal( RockContext rockContext )
        {
            var restrictedGuids = GetAttributeValue( AttributeKey.AccountsFilter )
                .SplitDelimitedValues()
                .AsGuidList();

            var accountQry = new FinancialAccountService( rockContext )
                .Queryable()
                .Where( a => a.IsActive );

            if ( restrictedGuids.Any() )
                accountQry = accountQry.Where( a => restrictedGuids.Contains( a.Guid ) );

            var accounts = accountQry
                .OrderBy( a => a.Order )
                .ThenBy( a => a.Name )
                .Select( a => new
                {
                    a.Guid,
                    a.Name,
                    a.PublicName
                } )
                .ToList()
                .Select( a => new ListItemBag
                {
                    Value = a.Guid.ToString(),
                    Text = a.PublicName.IsNotNullOrWhiteSpace() ? a.PublicName : a.Name
                } )
                .ToList();

            var usedCurrencyIds = new FinancialTransactionService( rockContext )
                .Queryable()
                .Where( t => t.ForeignCurrencyCodeValueId.HasValue )
                .Select( t => t.ForeignCurrencyCodeValueId.Value )
                .Distinct()
                .ToList();

            var orgCurrencyInfo = new RockCurrencyCodeInfo();
            var orgCurrencyDvId = orgCurrencyInfo.CurrencyCodeDefinedValueId;
            var orgCurrencyCode = orgCurrencyInfo.CurrencyCode ?? orgCurrencyInfo.Symbol ?? "";

            var currencies = new List<ListItemBag>
            {
                new ListItemBag { Value = "0", Text = $"Org. ({orgCurrencyCode})" }
            };

            foreach ( var id in usedCurrencyIds )
            {
                // Skip the org's own currency — it's already covered by the "Org." entry to avoid duplicates
                if ( id == orgCurrencyDvId )
                    continue;

                var dv = DefinedValueCache.Get( id );
                if ( dv != null )
                    currencies.Add( new ListItemBag { Value = id.ToString(), Text = dv.Value } );
            }

            return new FilterOptionsBag
            {
                accounts = accounts,
                currencies = currencies,
                orgCurrencySymbol = orgCurrencyInfo.Symbol ?? ""
            };
        }

        /// <summary>
        /// Extracts the NIT value from a transaction Summary string.
        /// Summary format built by CybersourceDonationEntry: "Note | NIT: 12345678 | NIT Nombre: Juan | Ref: ABC | Auth: XYZ"
        /// </summary>
        private static string ExtractNitFromSummary( string summary )
        {
            if ( string.IsNullOrEmpty( summary ) )
                return "";

            var marker = "NIT: ";
            var idx = summary.IndexOf( marker, StringComparison.OrdinalIgnoreCase );
            if ( idx < 0 )
                return "";

            var start = idx + marker.Length;
            var sepIdx = summary.IndexOf( " | ", start, StringComparison.Ordinal );
            return sepIdx >= 0
                ? summary.Substring( start, sepIdx - start ).Trim()
                : summary.Substring( start ).Trim();
        }

        /// <summary>
        /// Returns the last 4 digits from a masked account number (e.g. "************1234" -> "1234").
        /// Returns empty string if the input is null/empty or has fewer than 4 digits.
        /// </summary>
        private static string ExtractLast4( string masked )
        {
            if ( string.IsNullOrWhiteSpace( masked ) )
                return "";

            var digits = new string( masked.Where( char.IsDigit ).ToArray() );
            return digits.Length >= 4 ? digits.Substring( digits.Length - 4 ) : digits;
        }

        #endregion

        #region Block Actions

        [BlockAction( "GetFilterOptions" )]
        public BlockActionResult GetFilterOptions()
        {
            using ( var rockContext = new RockContext() )
            {
                return ActionOk( GetFilterOptionsInternal( rockContext ) );
            }
        }

        [BlockAction( "GetTransactions" )]
        public BlockActionResult GetTransactions( DonationFilterBag filter )
        {
            using ( var rockContext = new RockContext() )
            {
                return ActionOk( GetTransactionRowsInternal( rockContext, filter ) );
            }
        }

        [BlockAction( "ExportToExcel" )]
        public BlockActionResult ExportToExcel( DonationFilterBag filter )
        {
            if ( !GetAttributeValue( AttributeKey.AllowExport ).AsBoolean() )
                return ActionBadRequest( "Exportación no permitida en este bloque." );

            using ( var rockContext = new RockContext() )
            {
                var rows = GetTransactionRowsInternal( rockContext, filter );

                var dt = new DataTable( "Donaciones" );
                dt.Columns.Add( "Fecha", typeof( string ) );
                dt.Columns.Add( "Persona", typeof( string ) );
                dt.Columns.Add( "NIT", typeof( string ) );
                dt.Columns.Add( "Cuentas", typeof( string ) );
                dt.Columns.Add( "Total", typeof( string ) );
                dt.Columns.Add( "Monto", typeof( decimal ) );
                dt.Columns.Add( "Moneda", typeof( string ) );
                dt.Columns.Add( "Codigo", typeof( string ) );
                dt.Columns.Add( "Tarjeta", typeof( string ) );
                dt.Columns.Add( "Resumen", typeof( string ) );

                foreach ( var r in rows )
                {
                    dt.Rows.Add(
                        r.transactionDateTime,
                        r.personName,
                        r.nits,
                        string.Join( " | ", r.details.Select( d => d.accountName ) ),
                        r.formattedTotal,
                        r.totalAmount,
                        r.currencyLabel,
                        r.transactionCode,
                        string.IsNullOrEmpty( r.cardLast4 ) ? "" : "*" + r.cardLast4,
                        r.summary
                    );
                }

                var ds = new DataSet();
                ds.Tables.Add( dt );

                using ( var excel = ExcelHelper.CreateNewFile( ds, "Donaciones" ) )
                {
                    var bytes = excel.ToByteArray();
                    return ActionOk( new ExportResponseBag
                    {
                        fileName = $"donaciones_{RockDateTime.Now:yyyy-MM-dd_HHmm}.xlsx",
                        base64 = Convert.ToBase64String( bytes )
                    } );
                }
            }
        }

        private List<TransactionRowBag> GetTransactionRowsInternal( RockContext rockContext, DonationFilterBag filter )
        {
            if ( filter == null )
                filter = new DonationFilterBag();

            var maxResults = GetAttributeValue( AttributeKey.MaxResults ).AsIntegerOrNull() ?? 500;
            var contributionTypeId = DefinedValueCache.GetId( Rock.SystemGuid.DefinedValue.TRANSACTION_TYPE_CONTRIBUTION.AsGuid() ) ?? 0;

            var qry = new FinancialTransactionService( rockContext )
                .Queryable()
                .Where( t => t.TransactionTypeValueId == contributionTypeId );

            // Date range
            if ( !string.IsNullOrWhiteSpace( filter.DateFrom ) )
            {
                var dt = filter.DateFrom.AsDateTime();
                if ( dt.HasValue )
                    qry = qry.Where( t => t.TransactionDateTime >= dt.Value );
            }

            if ( !string.IsNullOrWhiteSpace( filter.DateTo ) )
            {
                var dt = filter.DateTo.AsDateTime();
                if ( dt.HasValue )
                {
                    var dateToExclusive = dt.Value.AddDays( 1 );
                    qry = qry.Where( t => t.TransactionDateTime < dateToExclusive );
                }
            }

            // Account filter
            if ( filter.AccountGuids != null && filter.AccountGuids.Any() )
            {
                var accountIds = new FinancialAccountService( rockContext )
                    .Queryable()
                    .Where( a => filter.AccountGuids.Contains( a.Guid ) )
                    .Select( a => a.Id )
                    .ToList();

                if ( accountIds.Any() )
                    qry = qry.Where( t => t.TransactionDetails.Any( d => accountIds.Contains( d.AccountId ) ) );
            }

            // Currency filter: 0 = org default (matches BOTH null ForeignCurrencyCodeValueId AND
            // ForeignCurrencyCodeValueId == org default DefinedValueId)
            if ( filter.CurrencyValueIds != null && filter.CurrencyValueIds.Any() )
            {
                var orgDvId = new RockCurrencyCodeInfo().CurrencyCodeDefinedValueId;
                var specificIds = filter.CurrencyValueIds.Where( id => id > 0 && id != orgDvId ).ToList();
                var includeDefault = filter.CurrencyValueIds.Contains( 0 );

                if ( includeDefault && specificIds.Any() )
                    qry = qry.Where( t => !t.ForeignCurrencyCodeValueId.HasValue
                                          || t.ForeignCurrencyCodeValueId.Value == orgDvId
                                          || specificIds.Contains( t.ForeignCurrencyCodeValueId.Value ) );
                else if ( includeDefault )
                    qry = qry.Where( t => !t.ForeignCurrencyCodeValueId.HasValue
                                          || t.ForeignCurrencyCodeValueId.Value == orgDvId );
                else
                    qry = qry.Where( t => t.ForeignCurrencyCodeValueId.HasValue && specificIds.Contains( t.ForeignCurrencyCodeValueId.Value ) );
            }

            // Person filter
            if ( filter.PersonAliasGuid.HasValue )
            {
                var paGuid = filter.PersonAliasGuid.Value;
                qry = qry.Where( t => t.AuthorizedPersonAlias.Guid == paGuid );
            }

            // NIT filter: search in transaction Summary (stored as "NIT: 12345678")
            if ( !string.IsNullOrWhiteSpace( filter.NitFilter ) )
            {
                var nitTerm = "NIT: " + filter.NitFilter.Trim();
                qry = qry.Where( t => t.Summary != null && t.Summary.Contains( nitTerm ) );
            }

            var rawRows = qry
                .Select( t => new
                {
                    t.Id,
                    t.TransactionDateTime,
                    t.Summary,
                    t.TransactionCode,
                    t.ForeignCurrencyCodeValueId,
                    AccountNumberMasked = t.FinancialPaymentDetail != null ? t.FinancialPaymentDetail.AccountNumberMasked : null,
                    PersonId = ( int? ) t.AuthorizedPersonAlias.PersonId,
                    PersonNickName = t.AuthorizedPersonAlias.Person.NickName,
                    PersonLastName = t.AuthorizedPersonAlias.Person.LastName,
                    PersonAliasGuid = ( Guid? ) t.AuthorizedPersonAlias.Guid,
                } )
                .OrderByDescending( t => t.TransactionDateTime )
                .Take( maxResults )
                .ToList();

            if ( !rawRows.Any() )
                return new List<TransactionRowBag>();

            var txnIds = rawRows.Select( r => r.Id ).ToList();
            var rawDetails = new FinancialTransactionDetailService( rockContext )
                .Queryable()
                .Where( d => txnIds.Contains( d.TransactionId ) )
                .Select( d => new
                {
                    d.TransactionId,
                    AccountName = d.Account.Name,
                    d.AccountId,
                    d.Amount
                } )
                .ToList();

            var detailsMap = rawDetails
                .GroupBy( d => d.TransactionId )
                .ToDictionary( g => g.Key, g => g.ToList() );

            var orgCurrencyInfo = new RockCurrencyCodeInfo();
            var orgCurrencyDvId = orgCurrencyInfo.CurrencyCodeDefinedValueId;
            var orgCurrencyCode = orgCurrencyInfo.CurrencyCode ?? orgCurrencyInfo.Symbol ?? "";

            var currencyLabelCache = rawRows
                .Where( r => r.ForeignCurrencyCodeValueId.HasValue )
                .Select( r => r.ForeignCurrencyCodeValueId.Value )
                .Distinct()
                .ToDictionary(
                    id => id,
                    id => DefinedValueCache.Get( id )?.Value ?? id.ToString()
                );

            return rawRows.Select( r =>
            {
                var details = detailsMap.TryGetValue( r.Id, out var dList ) ? dList : null;
                var totalAmount = details?.Sum( d => d.Amount ) ?? 0m;
                var nitValue = ExtractNitFromSummary( r.Summary );

                // Treat null OR org-default-currency-id as the same bucket (both are org currency)
                var isOrgCurrency = !r.ForeignCurrencyCodeValueId.HasValue || r.ForeignCurrencyCodeValueId.Value == orgCurrencyDvId;
                var currencyLabel = isOrgCurrency
                    ? orgCurrencyCode
                    : ( currencyLabelCache.TryGetValue( r.ForeignCurrencyCodeValueId.Value, out var lbl ) ? lbl : "" );

                return new TransactionRowBag
                {
                    id = r.Id,
                    transactionDateTime = r.TransactionDateTime?.ToShortDateString() ?? "",
                    personName = ( $"{r.PersonNickName} {r.PersonLastName}" ).Trim(),
                    personAliasGuid = r.PersonAliasGuid,
                    nits = nitValue,
                    formattedTotal = totalAmount.FormatAsCurrency( r.ForeignCurrencyCodeValueId ),
                    totalAmount = totalAmount,
                    foreignCurrencyCodeValueId = r.ForeignCurrencyCodeValueId,
                    currencyLabel = currencyLabel,
                    transactionCode = r.TransactionCode ?? "",
                    cardLast4 = ExtractLast4( r.AccountNumberMasked ),
                    summary = r.Summary ?? "",
                    details = details?.Select( d => new TransactionDetailBag
                    {
                        accountName = d.AccountName ?? "",
                        amount = d.Amount,
                        formattedAmount = d.Amount.FormatAsCurrency( r.ForeignCurrencyCodeValueId )
                    } ).ToList() ?? new List<TransactionDetailBag>()
                };
            } ).ToList();
        }

        #endregion

        #region DTOs

        public class InitBag
        {
            public bool allowExport { get; set; }
            public FilterOptionsBag filterOptions { get; set; }
        }

        public class FilterOptionsBag
        {
            public List<ListItemBag> accounts { get; set; }
            public List<ListItemBag> currencies { get; set; }
            public string orgCurrencySymbol { get; set; }
        }

        public class DonationFilterBag
        {
            public string DateFrom { get; set; }
            public string DateTo { get; set; }
            public List<Guid> AccountGuids { get; set; }
            public List<int> CurrencyValueIds { get; set; }
            public Guid? PersonAliasGuid { get; set; }
            public string NitFilter { get; set; }
        }

        public class TransactionRowBag
        {
            public int id { get; set; }
            public string transactionDateTime { get; set; }
            public string personName { get; set; }
            public Guid? personAliasGuid { get; set; }
            public string nits { get; set; }
            public string formattedTotal { get; set; }
            public decimal totalAmount { get; set; }
            public int? foreignCurrencyCodeValueId { get; set; }
            public string currencyLabel { get; set; }
            public string transactionCode { get; set; }
            public string cardLast4 { get; set; }
            public string summary { get; set; }
            public List<TransactionDetailBag> details { get; set; }
        }

        public class TransactionDetailBag
        {
            public string accountName { get; set; }
            public decimal amount { get; set; }
            public string formattedAmount { get; set; }
        }

        public class ExportResponseBag
        {
            public string fileName { get; set; }
            public string base64 { get; set; }
        }

        #endregion
    }
}
