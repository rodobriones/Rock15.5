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
//

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Obsidian.UI;
using Rock.Security;
using Rock.SystemGuid;
using Rock.Utility.Enums;
using Rock.ViewModels.Blocks;
using Rock.ViewModels.Blocks.Crm.PersonDuplicateList;
using Rock.Web.Cache;

namespace Rock.Blocks.Crm
{
    /// <summary>
    /// Displays a list of person duplicates.
    /// </summary>

    [DisplayName( "Person Duplicate List" )]
    [Category( "CRM" )]
    [Description( "Displays a list of person duplicates." )]
    [IconCssClass( "ti ti-list" )]
    [SupportedSiteTypes( Model.SiteType.Web )]

    #region Block Attributes

    [DecimalField(
        "Confidence Score High",
        Key = AttributeKey.ConfidenceScoreHigh,
        Description = "The minimum confidence score required to be considered a likely match.",
        IsRequired = true,
        DefaultDecimalValue = 80.00,
        Order = 0 )]

    [DecimalField(
        "Confidence Score Low",
        Key = AttributeKey.ConfidenceScoreLow,
        Description = "The maximum confidence score required to be considered an unlikely match. Values lower than this will not be shown in the grid.",
        IsRequired = true,
        DefaultDecimalValue = 60.00,
        Order = 1 )]

    [BooleanField(
        "Include Inactive",
        Key = AttributeKey.IncludeInactive,
        Description = "Set to true to also include potential matches when both records are inactive.",
        DefaultBooleanValue = false,
        Order = 2 )]

    [BooleanField(
        "Include Businesses",
        Key = AttributeKey.IncludeBusinesses,
        Description = "Set to true to also include potential matches when either record is a Business.",
        DefaultBooleanValue = false,
        Order = 3 )]

    [LinkedPage(
        "Detail Page",
        Description = "The page that will show the person duplicate details.",
        Key = AttributeKey.DetailPage,
        Order = 4 )]

    #endregion Block Attributes

    [Rock.SystemGuid.EntityTypeGuid( "ed3aaaf7-965f-4aef-89e3-961421ce8148" )]
    [Rock.SystemGuid.BlockTypeGuid( "13B356A9-BCA2-4CFC-A698-86A6C9FFD13B" )]
    [CustomizedGrid]
    public class PersonDuplicateList : RockEntityListBlockType<PersonDuplicate>
    {
        #region Fields

        // PersonIds, MatchCount
        Dictionary<int, int> _matchCounts = new Dictionary<int, int>();

        #endregion Fields

        #region Keys

        private static class AttributeKey
        {
            public const string ConfidenceScoreHigh = "ConfidenceScoreHigh";
            public const string ConfidenceScoreLow = "ConfidenceScoreLow";
            public const string IncludeInactive = "IncludeInactive";
            public const string IncludeBusinesses = "IncludeBusinesses";
            public const string DetailPage = "DetailPage";
        }

        private static class NavigationUrlKey
        {
            public const string DetailPage = "DetailPage";
        }

        #endregion Keys

        #region Methods

        #region Initialization Methods

        /// <inheritdoc/>
        public override object GetObsidianBlockInitialization()
        {
            var box = new ListBlockBox<PersonDuplicateListOptionsBag>();
            var builder = GetGridBuilder();

            box.IsAddEnabled = false;
            box.IsDeleteEnabled = false;
            box.ExpectedRowCount = null;
            box.NavigationUrls = GetBoxNavigationUrls();
            box.Options = GetBoxOptions();
            box.GridDefinition = builder.BuildDefinition();

            return box;
        }

        /// <summary>
        /// Gets the box options required for the component to render the list.
        /// </summary>
        /// <returns>The options that provide additional details to the block.</returns>
        private PersonDuplicateListOptionsBag GetBoxOptions()
        {
            var options = new PersonDuplicateListOptionsBag();

            options.ConfidenceScoreHigh = GetAttributeValue( AttributeKey.ConfidenceScoreHigh ).AsInteger();
            options.ConfidenceScoreLow = GetAttributeValue( AttributeKey.ConfidenceScoreLow ).AsInteger();
            options.IncludeInactive = GetAttributeValue( AttributeKey.IncludeInactive ).AsBoolean();
            options.IncludeBusinesses = GetAttributeValue( AttributeKey.IncludeBusinesses ).AsBoolean();
            options.HasMultipleCampuses = CampusCache.All().Count( c => c.IsActive ?? true ) > 1;

            return options;
        }

        /// <summary>
        /// Gets the box navigation URLs required for the page to operate.
        /// </summary>
        /// <returns>A dictionary of key names and URL values.</returns>
        private Dictionary<string, string> GetBoxNavigationUrls()
        {
            return new Dictionary<string, string>
            {
                [NavigationUrlKey.DetailPage] = this.GetLinkedPageUrl( AttributeKey.DetailPage, "PersonDuplicateId", "((Key))" )
            };
        }

        /// <inheritdoc/>
        protected override IQueryable<PersonDuplicate> GetListQueryable(RockContext rockContext)
        {
            var recordStatusInactiveId = DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_INACTIVE.AsGuid() ).Id;
            var recordTypeBusinessId = DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.PERSON_RECORD_TYPE_BUSINESS.AsGuid() ).Id;

            var query =  ( IQueryable<PersonDuplicate> ) Rock.Reflection.GetQueryableForEntityType( typeof( PersonDuplicate ), RockContext );

            // Filter down to non-ignored, non-confirmed duplicates where the main and duplicate Person IDs are not the same IDs
            query = query.Where( personDuplicate => !personDuplicate.IsConfirmedAsNotDuplicate )
                .Where( personDuplicate => !personDuplicate.IgnoreUntilScoreChanges )
                .Where( personDuplicate => personDuplicate.PersonAlias.PersonId != personDuplicate.DuplicatePersonAlias.PersonId );

            // Include inactive records if block property is set
            if ( GetAttributeValue( AttributeKey.IncludeInactive ).AsBoolean() == false )
            {
                query = query.Where( personDuplicate =>
                    !(
                        personDuplicate.PersonAlias.Person.RecordStatusValueId == recordStatusInactiveId
                        && personDuplicate.DuplicatePersonAlias.Person.RecordStatusValueId == recordStatusInactiveId
                    )
                );
            }

            // Include business records if block property is set
            if ( GetAttributeValue( AttributeKey.IncludeBusinesses ).AsBoolean() == false )
            {
                query = query.Where( personDuplicate =>
                    !(
                        personDuplicate.PersonAlias.Person.RecordTypeValueId == recordTypeBusinessId
                        || personDuplicate.DuplicatePersonAlias.Person.RecordTypeValueId == recordTypeBusinessId
                    )
                );
            }

            // Only show confidence scores that at least meet the Low requirement
            double? confidenceScoreLow = GetAttributeValue( AttributeKey.ConfidenceScoreLow ).AsDoubleOrNull();
            if ( confidenceScoreLow.HasValue )
            {
                query = query.Where( a => a.ConfidenceScore > confidenceScoreLow );
            }

            // Add 1 to the value for key personDuplicate.PersonAlias.PersonId for each duplicate found
            _matchCounts = query
                .GroupBy(personDuplicate => personDuplicate.PersonAlias.PersonId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Count()
                );

            // Select the record with the highest confidence score for each Person
            query = query
                .GroupBy(personDuplicate => personDuplicate.PersonAlias.PersonId)
                .Select(group => group.OrderByDescending(personDuplicate => personDuplicate.ConfidenceScore)
                .ThenBy( personDuplicate => personDuplicate.Id )
                .FirstOrDefault());

            return query;
        }

        /// <inheritdoc/>
        protected override IQueryable<PersonDuplicate> GetOrderedListQueryable( IQueryable<PersonDuplicate> query, RockContext rockContext )
        {
            if ( typeof( IOrdered ).IsAssignableFrom( typeof( PersonDuplicate ) ) )
            {
                return query.OrderBy( nameof( IOrdered.Order ) )
                    .ThenBy( personDuplicate => personDuplicate.Id );
            }
            else
            {
                return query = query.OrderByDescending( personDuplicate => personDuplicate.ConfidenceScore )
                    .ThenBy( personDuplicate => personDuplicate.PersonAlias.Person.LastName )
                    .ThenBy( personDuplicate => personDuplicate.PersonAlias.Person.FirstName );
            }
        }

        /// <inheritdoc/>
        protected override GridBuilder<PersonDuplicate> GetGridBuilder()
        {
            return new GridBuilder<PersonDuplicate>()
                .WithBlock( this )
                .AddTextField( "idKey", a => a.PersonAlias.Person.IdKey )
                .AddField( "confidenceScore", a => a.ConfidenceScore.HasValue ? a.ConfidenceScore.Value : 0 )
                .AddTextField( "accountProtectionProfile", a => a.PersonAlias.Person.AccountProtectionProfile.ConvertToString() )
                .AddTextField( "firstName", a => a.PersonAlias.Person.FirstName )
                .AddTextField( "lastName", a => a.PersonAlias.Person.LastName )
                .AddTextField( "suffix", a => a.PersonAlias.Person.SuffixValue == null ? "" : a.PersonAlias.Person.SuffixValue.ToString() )
                .AddTextField( "matchCount", a => _matchCounts[a.PersonAlias.PersonId].ToString() )
                .AddTextField( "modified", a => a.ModifiedDateTime.ToString() )
                .AddTextField( "createdBy", a => a.CreatedByPersonName )
                .AddTextField( "campus", a => a.PersonAlias.Person.PrimaryCampus.ToString() )
                .AddField( "isSecurityDisabled", a => !a.IsAuthorized( Authorization.ADMINISTRATE, RequestContext.CurrentPerson ) )
                .AddAttributeFields( GetGridAttributes() );
        }

        #endregion Initialization Methods

        #region Helper Methods

        /// <summary>
        /// Gets the account protection profile column HTML.
        /// </summary>
        /// <param name="accountProtectionProfile">The account protection profile.</param>
        /// <returns></returns>
        public string GetAccountProtectionProfileColumnHtml( AccountProtectionProfile accountProtectionProfile )
        {
            var cssMap = new Dictionary<AccountProtectionProfile, string>
            {
                { AccountProtectionProfile.Extreme, "danger" },
                { AccountProtectionProfile.High, "primary" },
                { AccountProtectionProfile.Medium, "warning" },
                { AccountProtectionProfile.Low, "success" }
            };

            var css = $"label label-{cssMap[accountProtectionProfile]}";


            return $"<span class='{css}'>{accountProtectionProfile.ConvertToString()}</span>";
        }

        #endregion Helper Methods

        #endregion Methods

        #region Block Actions

        #endregion
    }
}
