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

using System.Collections.Generic;

using Rock.ViewModels.Utility;

namespace Rock.ViewModels.Blocks.Finance.BenevolenceRequestDetail
{
    /// <summary>
    /// The additional configuration options for the Benevolence Request Detail block.
    /// </summary>
    public class BenevolenceRequestDetailOptionsBag
    {
        /// <summary>
        /// Gets or sets the benevolence type.
        /// </summary>
        public List<ListItemBag> BenevolenceRequestTypes { get; set; }

        /// <summary>
        /// Gets or sets the Rock.Model.DefinedValue representing the Benevolence Request's status.
        /// </summary>
        public List<ListItemBag> RequestStatusValues { get; set; }

        /// <summary>
        /// Gets or sets the Rock.Model.DefinedValue representing the Requester's connection status.
        /// </summary>
        public List<ListItemBag> ConnectionStatusValues { get; set; }

        /// <summary>
        /// Gets or sets the collection of result type values.
        /// </summary>
        public List<ListItemBag> ResultTypeValues { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier for the binary file type associated with benevolence documents.
        /// </summary>
        public System.Guid BenevolenceDocumentBinaryFileTypeGuid { get; set; }
    }
}