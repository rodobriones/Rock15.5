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

namespace Rock.Enums.Blocks
{
    /// <summary>
    /// Specifies the kind of Obsidian conversion that will take place with this block.
    /// This is temporary until the Obsidian conversion is complete.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <strong>This is an internal API</strong> that supports the Rock
    ///         infrastructure and not subject to the same compatibility standards
    ///         as public APIs. It may be changed or removed without notice in any
    ///         release and should therefore not be directly used in any plug-ins.
    ///     </para>
    /// </remarks>
    public enum MigrationStrategy
    {
        /// <summary>
        /// The block is replacing the webforms version with the Obsidian version.
        /// </summary>
        Chop = 0,

        /// <summary>
        /// The block will replace the webforms version with the Obsidian version,
        /// but the webforms version will remain in place with an new BlockTypeGuid.
        /// </summary>
        Swap = 1,
    }
}
