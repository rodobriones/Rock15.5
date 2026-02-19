using System.ComponentModel;

using Rock.Blocks;

namespace Rock.Blocks.Layout
{
    [DisplayName( "Header" )]
    [Category( "Custom" )]
    [Description( "Obsidian header block." )]
    public class Header : RockObsidianBlockType
    {
        public override string BlockFileUrl => $"{base.BlockFileUrl}.obs";
    }
}
