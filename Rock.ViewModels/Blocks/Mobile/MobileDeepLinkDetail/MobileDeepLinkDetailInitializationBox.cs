using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rock.ViewModels.Blocks.Mobile.MobileDeepLinkDetail
{

    /// <summary>
    /// Represents the initialization data for the Mobile Deep Link Detail block.
    /// </summary>
    /// <seealso cref="BlockBox" />
    public class MobileDeepLinkDetailInitializationBox : BlockBox
    {
        /// <summary>
        /// Gets or sets the entity.
        /// </summary>
        /// <value>The entity.</value>
        public MobileDeepLinkDetailBag Bag { get; set; }

        /// <summary>
        /// Gets or sets the options.
        /// </summary>
        /// <value>The options.</value>
        public MobileDeepLinkDetailOptionsBag Options { get; set; } = new MobileDeepLinkDetailOptionsBag();

        /// <summary>
        /// Gets or sets a value indicating whether this instance is editable.
        /// </summary>
        /// <value><c>true</c> if this instance is editable; otherwise, <c>false</c>.</value>
        public bool IsEditable { get; set; } = false;
    }
}