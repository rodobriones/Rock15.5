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

namespace Rock.ViewModels.Blocks.Core.CategoryTreeView
{
    /// <summary>
    /// Initialization bag returned to the frontend
    /// </summary>
    public class CategoryTreeViewBlockAttributesBag
    {
        /// <summary>
        /// Gets or sets the default CSS class to use for icons in the tree view.
        /// </summary>
        public string DefaultIconCSSClass { get; set; }

        /// <summary>
        /// Gets or sets the page reference used to display the details of a selected category or entity.
        /// </summary>
        public string DetailPage { get; set; }

        /// <summary>
        /// Gets or sets the entity type name that this category tree is associated with.
        /// </summary>
        public string EntityType { get; set; }

        /// <summary>
        /// Gets or sets the friendly display name of the associated entity type.
        /// </summary>
        public string EntityTypeFriendlyName { get; set; }

        /// <summary>
        /// Gets or sets the qualifier property used to filter the entity type.
        /// </summary>
        public string EntityTypeQualifierProperty { get; set; }

        /// <summary>
        /// Gets or sets the qualifier value used to filter the entity type.
        /// </summary>
        public string EntityTypeQualifierValue { get; set; }

        /// <summary>
        /// Gets or sets the key of the page parameter used to identify the selected entity.
        /// </summary>
        public string PageParameterKey { get; set; }

        /// <summary>
        /// Gets or sets the page reference used to display search results.
        /// </summary>
        public string SearchResultsPage { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether only categories should be shown in the tree view.
        /// </summary>
        public bool ShowOnlyCategories { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether unnamed entity items should be shown.
        /// </summary>
        public bool ShowUnnamedEntityItems { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the root category for the tree view.
        /// </summary>
        public string RootCategory { get; set; }

        /// <summary>
        /// Gets or sets a comma-delimited list of category identifiers to exclude from the tree view.
        /// </summary>
        public string ExcludeCategories { get; set; }
    }
}
