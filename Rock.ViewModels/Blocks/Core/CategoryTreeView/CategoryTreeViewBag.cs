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
    public class CategoryTreeViewBag
    {
        /// <summary>
        /// Gets or sets the tree list containing the category tree structure.
        /// </summary>
        public CategoryTreeViewTreeListBag TreeList { get; set; }

        /// <summary>
        /// Gets or sets the error message to be displayed to the user, if any.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the category tree is editable by the current user.
        /// </summary>
        public bool IsEditable { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the category type is activated.
        /// </summary>
        public bool isActivatedType { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the user is hiding inactive items in the category tree.
        /// </summary>
        public bool isUserHidingInactiveItems { get; set; }
    }
}