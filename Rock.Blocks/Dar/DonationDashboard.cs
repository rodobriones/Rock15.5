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
            var currencies = new List<ListItemBag>
            {
                new ListItemBag { Value = "0", Text = $"Org. ({orgCurrencyInfo.Symbol})" }
            };

            foreach ( var id in usedCurrencyIds )
            {
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

        private int? GetNitAttributeId()
        {
            var nitAttributeKey = GetAttributeValue( AttributeKey.PersonNitAttributeKey );
            if ( string.IsNullOrWhiteSpace( nitAttributeKey ) )
                return null;

            var personEntityTypeId = EntityTypeCache.GetId<Person>();
            return AttributeCache.All()
                .Where( a => a.EntityTypeId == personEntityTypeId && a.Key == nitAttributeKey )
                .Select( a => ( int? ) a.Id )
                .FirstOrDefault();
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
            if ( filter == null )
                filter = new DonationFilterBag();

            using ( var rockContext = new RockContext() )
            {
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

                // Currency filter: 0 = org default (null ForeignCurrencyCodeValueId)
                if ( filter.CurrencyValueIds != null && filter.CurrencyValueIds.Any() )
                {
                    var specificIds = filter.CurrencyValueIds.Where( id => id > 0 ).ToList();
                    var includeDefault = filter.CurrencyValueIds.Contains( 0 );

                    if ( includeDefault && specificIds.Any() )
                        qry = qry.Where( t => !t.ForeignCurrencyCodeValueId.HasValue || specificIds.Contains( t.ForeignCurrencyCodeValueId.Value ) );
                    else if ( includeDefault )
                        qry = qry.Where( t => !t.ForeignCurrencyCodeValueId.HasValue );
                    else
                        qry = qry.Where( t => t.ForeignCurrencyCodeValueId.HasValue && specificIds.Contains( t.ForeignCurrencyCodeValueId.Value ) );
                }

                // Person filter
                if ( filter.PersonAliasGuid.HasValue )
                {
                    var paGuid = filter.PersonAliasGuid.Value;
                    qry = qry.Where( t => t.AuthorizedPersonAlias.Guid == paGuid );
                }

                // NIT filter: resolve person IDs first, then filter transactions
                var nitAttributeId = GetNitAttributeId();
                if ( !string.IsNullOrWhiteSpace( filter.NitFilter ) && nitAttributeId.HasValue )
                {
                    var nitTerm = filter.NitFilter.Trim();
                    var attrId = nitAttributeId.Value;

                    var matchingPersonIds = new AttributeValueService( rockContext )
                        .Queryable()
                        .Where( av => av.AttributeId == attrId && av.Value.Contains( nitTerm ) )
                        .Select( av => av.EntityId.Value );

                    var nitAliasIds = new PersonAliasService( rockContext )
                        .Queryable()
                        .Where( pa => matchingPersonIds.Contains( pa.PersonId ) )
                        .Select( pa => pa.Id );

                    qry = qry.Where( t => t.AuthorizedPersonAliasId.HasValue && nitAliasIds.Contains( t.AuthorizedPersonAliasId.Value ) );
                }

                // Project to flat anonymous DTO (keeps SQL-side)
                var rawRows = qry
                    .Select( t => new
                    {
                        t.Id,
                        t.TransactionDateTime,
                        t.Summary,
                        t.TransactionCode,
                        t.ForeignCurrencyCodeValueId,
                        PersonId = ( int? ) t.AuthorizedPersonAlias.PersonId,
                        PersonNickName = t.AuthorizedPersonAlias.Person.NickName,
                        PersonLastName = t.AuthorizedPersonAlias.Person.LastName,
                        PersonAliasGuid = ( Guid? ) t.AuthorizedPersonAlias.Guid,
                    } )
                    .OrderByDescending( t => t.TransactionDateTime )
                    .Take( maxResults )
                    .ToList();

                if ( !rawRows.Any() )
                    return ActionOk( new List<TransactionRowBag>() );

                // Load transaction details in a single query
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

                // Load NITs for all persons in result
                var personIds = rawRows
                    .Where( r => r.PersonId.HasValue )
                    .Select( r => r.PersonId.Value )
                    .Distinct()
                    .ToList();

                var nitMap = new Dictionary<int, string>();
                if ( nitAttributeId.HasValue && personIds.Any() )
                {
                    var attrId = nitAttributeId.Value;
                    nitMap = new AttributeValueService( rockContext )
                        .Queryable()
                        .Where( av => av.AttributeId == attrId && av.EntityId.HasValue && personIds.Contains( av.EntityId.Value ) )
                        .ToDictionary( av => av.EntityId.Value, av => av.Value );
                }

                // Currency labels cache
                var orgSymbol = new RockCurrencyCodeInfo().Symbol ?? "";
                var currencyLabelCache = rawRows
                    .Where( r => r.ForeignCurrencyCodeValueId.HasValue )
                    .Select( r => r.ForeignCurrencyCodeValueId.Value )
                    .Distinct()
                    .ToDictionary(
                        id => id,
                        id => DefinedValueCache.Get( id )?.Value ?? id.ToString()
                    );

                // Build result bags
                var result = rawRows.Select( r =>
                {
                    var details = detailsMap.TryGetValue( r.Id, out var dList ) ? dList : null;
                    var totalAmount = details?.Sum( d => d.Amount ) ?? 0m;
                    string nitValue = "";
                    if ( r.PersonId.HasValue && nitMap.TryGetValue( r.PersonId.Value, out var n ) )
                        nitValue = n;
                    var currencyLabel = r.ForeignCurrencyCodeValueId.HasValue
                        ? ( currencyLabelCache.TryGetValue( r.ForeignCurrencyCodeValueId.Value, out var lbl ) ? lbl : "" )
                        : orgSymbol;

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
                        summary = r.Summary ?? "",
                        details = details?.Select( d => new TransactionDetailBag
                        {
                            accountName = d.AccountName ?? "",
                            amount = d.Amount,
                            formattedAmount = d.Amount.FormatAsCurrency( r.ForeignCurrencyCodeValueId )
                        } ).ToList() ?? new List<TransactionDetailBag>()
                    };
                } ).ToList();

                return ActionOk( result );
            }
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
            public string summary { get; set; }
            public List<TransactionDetailBag> details { get; set; }
        }

        public class TransactionDetailBag
        {
            public string accountName { get; set; }
            public decimal amount { get; set; }
            public string formattedAmount { get; set; }
        }

        #endregion
    }
}
