using System.ComponentModel;

using Rock.Blocks;

namespace Rock.Blocks.Layout
{
    [DisplayName( "Footer" )]
    [Category( "Custom" )]
    [Description( "Obsidian footer block." )]
    public class Footer : RockObsidianBlockType
    {
        public override string BlockFileUrl => $"{base.BlockFileUrl}.obs";
    }
}
