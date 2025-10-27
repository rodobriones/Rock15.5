using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rock.ViewModels.Utility;

namespace Rock.ViewModels.Blocks.Finance.BenevolenceRequestDetail
{
    /// <summary>
    /// Represents a BenevolenceRequestDocument associated with a benevolence request.
    /// </summary>
    public class BenevolenceDocumentBag : EntityBagBase
    {
        /// <summary>
        /// Gets or sets the unique identifier for the BenevolenceRequestDocument.
        /// </summary>
        public Guid Guid { get; set; }

        /// <summary>
        /// Gets or sets the file name of the BenevolenceRequestDocument.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this BenevolenceRequestDocument is marked for deletion.
        /// </summary>
        public bool IsMarkedForDeletion { get; set; }
    }
}
