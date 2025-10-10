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
using System;

using Rock.ViewModels.Utility;
using Rock.ViewModels.Blocks.Finance.BenevolenceRequestList;

namespace Rock.ViewModels.Blocks.Finance.BenevolenceRequestDetail
{
    /// <summary>
    /// The item details for the Benevolence Request Detail block.
    /// </summary>
    public class BenevolenceRequestBag : EntityBagBase
    {
        /// <summary>
        /// Gets or sets a collection of BenevolenceResults
        /// </summary>
        public List<ListItemBag> BenevolenceResults { get; set; }

        /// <summary>
        /// Gets or sets the benevolence type.
        /// </summary>
        public ListItemBag BenevolenceType { get; set; }

        /// <summary>
        /// Gets or sets the benevolence type identifier.
        /// </summary>
        public int BenevolenceTypeId { get; set; }

        /// <summary>
        /// Gets or sets the Rock.Model.Campus that this Benevolence Request is associated with.
        /// </summary>
        public ListItemBag Campus { get; set; }

        /// <summary>
        /// Gets or sets the campus identifier.
        /// </summary>
        public int? CampusId { get; set; }

        /// <summary>
        /// Gets or sets the case worker Rock.Model.PersonAlias.
        /// </summary>
        public ListItemBag CaseWorkerPersonAlias { get; set; }

        /// <summary>
        /// Gets or sets the PersonAliasId of the Rock.Model.PersonAlias who is the case worker for this request.
        /// </summary>
        public int? CaseWorkerPersonAliasId { get; set; }

        /// <summary>
        /// Gets or sets the Cell Phone Number of the person who requested benevolence.
        /// </summary>
        public string CellPhoneNumber { get; set; }

        /// <summary>
        /// Gets or sets the Rock.Model.DefinedValue representing the Requester's connection status.
        /// </summary>
        public ListItemBag ConnectionStatusValue { get; set; }

        /// <summary>
        /// Gets or sets the Id of the Defined Value Rock.Model.DefinedValue representing the connection status of the Requester.
        /// </summary>
        public int? ConnectionStatusValueId { get; set; }

        /// <summary>
        /// Gets or sets the documents.
        /// </summary>
        public List<ListItemBag> Documents { get; set; }

        /// <summary>
        /// Gets or sets the email address of the person requesting benevolence.
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// Gets or sets the First Name of the person that this benevolence request is about. This property is required.
        /// </summary>
        public string FirstName { get; set; }

        /// <summary>
        /// Gets or sets the GovernmentId of the person who requested benevolence.
        /// </summary>
        public string GovernmentId { get; set; }

        /// <summary>
        /// Gets or sets the Home Phone Number of the person who requested benevolence.
        /// </summary>
        public string HomePhoneNumber { get; set; }

        /// <summary>
        /// Gets or sets the Last Name of the person that this benevolence request is about. This property is required.
        /// </summary>
        public string LastName { get; set; }

        /// <summary>
        /// Gets or sets the Rock.Model.Location that is associated with this Benevolence Request.
        /// </summary>
        public ListItemBag Location { get; set; }

        /// <summary>
        /// Gets or sets the Id of the Rock.Model.Location that is associated with this BenevolenceRequest.
        /// </summary>
        public int? LocationId { get; set; }

        /// <summary>
        /// Gets or sets the provided next steps.
        /// </summary>
        public string ProvidedNextSteps { get; set; }

        /// <summary>
        /// Gets the request date key.
        /// </summary>
        public int RequestDateKey { get; set; }

        /// <summary>
        /// Gets or sets the date that this benevolence request was entered.
        /// </summary>
        public DateTime RequestDateTime { get; set; }

        /// <summary>
        /// Gets or sets the requested by Rock.Model.PersonAlias.
        /// </summary>
        public ListItemBag RequestedByPersonAlias { get; set; }

        /// <summary>
        /// Gets or sets the PersonAliasId of the Rock.Model.PersonAlias who is submitting the BenevolenceRequest
        /// </summary>
        public int? RequestedByPersonAliasId { get; set; }

        /// <summary>
        /// Gets or sets the Rock.Model.DefinedValue representing the Benevolence Request's status.
        /// </summary>
        public ListItemBag RequestStatusValue { get; set; }

        /// <summary>
        /// Gets or sets the Id of the Defined Value Rock.Model.DefinedValue representing the status of the Benevolence Request.
        /// </summary>
        public int? RequestStatusValueId { get; set; }

        /// <summary>
        /// Gets or sets the text/content of the request.
        /// </summary>
        public string RequestText { get; set; }

        /// <summary>
        /// Gets or sets the summary of the request result.
        /// </summary>
        public string ResultSummary { get; set; }

        /// <summary>
        /// Gets or sets the collection of benevolence results.
        /// </summary>
        public List<BenevolenceResultBag> Results { get; set; }

        /// <summary>
        /// Gets or sets the Work Phone Number of the person who requested benevolence.
        /// </summary>
        public string WorkPhoneNumber { get; set; }
    }
}
