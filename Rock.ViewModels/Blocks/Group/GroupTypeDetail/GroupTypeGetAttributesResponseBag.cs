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

using Rock.ViewModels.Utility;

namespace Rock.ViewModels.Blocks.Group.GroupTypeDetail
{
    /// <summary>
    /// Response bag containing editable and inherited attributes for an entity type in the GroupTypeDetail block.
    /// </summary>
    public class GroupTypeGetAttributesResponseBag
    {
        /// <summary>
        /// Gets or sets the editable Group attributes scoped to the current group type being edited.
        /// </summary>
        public List<PublicEditableAttributeBag> GroupAttributes { get; set; }

        /// <summary>
        /// Gets or sets the inherited Group attributes from the inherited group type chain (parent/grandparent/etc).
        /// </summary>
        public List<GroupTypeInheritedAttributeBag> InheritedGroupAttributes { get; set; }

        /// <summary>
        /// Gets or sets the editable GroupMember attributes scoped to the current group type being edited.
        /// </summary>
        public List<PublicEditableAttributeBag> GroupMemberAttributes { get; set; }

        /// <summary>
        /// Gets or sets the inherited GroupMember attributes from the inherited group type chain (parent/grandparent/etc).
        /// </summary>
        public List<GroupTypeInheritedAttributeBag> InheritedGroupMemberAttributes { get; set; }

        /// <summary>
        /// Gets or sets the editable GroupType attributes scoped to the current group type being edited.
        /// </summary>
        public List<PublicEditableAttributeBag> GroupTypeAttributes { get; set; }

        /// <summary>
        /// Gets or sets the inherited GroupType attributes from the inherited group type chain (parent/grandparent/etc).
        /// </summary>
        public List<GroupTypeInheritedAttributeBag> InheritedGroupTypeAttributes { get; set; }
    }
}


