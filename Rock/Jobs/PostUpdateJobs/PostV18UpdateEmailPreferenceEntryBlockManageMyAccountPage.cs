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
using System.ComponentModel;

using Rock.Data;
using Rock.Model;

namespace Rock.Jobs
{
    /// <summary>
    /// Run once job for v18.0 to update the Manage My Account Page block setting for the recently-chopped Email Preference Entry block.
    /// </summary>
    [DisplayName( "Rock Update Helper v18.0 - Update Email Preference Entry Block Manage My Account Page" )]
    [Description( "This job will update the Manage My Account Page block setting for the recently-chopped Email Preference Entry block." )]

    public class PostV18UpdateEmailPreferenceEntryBlockManageMyAccountPage : RockJob
    {
        /// <inheritdoc />
        public override void Execute()
        {
            using ( var rockContext = new RockContext() )
            {
                var jobMigration = new JobMigration( rockContext );
                var migrationHelper = new MigrationHelper( jobMigration );

                var emailPreferenceEntryBlockGuid = jobMigration.SqlScalar( $@"
DECLARE @EmailPreferencePageId INT = (SELECT TOP 1 [Id] FROM [Page] WHERE [Guid] = '{Rock.SystemGuid.Page.EMAIL_PREFERENCE}');
DECLARE @EmailPreferenceEntryBlockTypeId INT = (SELECT TOP 1 [Id] FROM [BlockType] WHERE [Guid] = 'FF88B243-4580-4BE0-97B1-BA9895C3FB8F');

SELECT TOP 1 [Guid]
FROM [Block]
WHERE [PageId] = @EmailPreferencePageId
    AND [BlockTypeId] = @EmailPreferenceEntryBlockTypeId;" )
                    .ToStringSafe()
                    .AsGuidOrNull();

                // Only perform the update if the block instance was found.
                if ( emailPreferenceEntryBlockGuid.HasValue )
                {
                    // Overwrite the block setting to link to the "My Account" page.
                    migrationHelper.AddBlockAttributeValue(
                       skipIfAlreadyExists: false,
                       blockGuid: emailPreferenceEntryBlockGuid.Value.ToString(),
                       attributeGuid: "0AB5FAA0-0C6C-449C-A0C1-5D3189B066D6",
                       value: "c0854f84-2e8b-479c-a3fb-6b47be89b795,ec68edc9-a8a5-4937-b78b-45f9bc278ee1"
                   );
                }
            }

            DeleteJob();
        }

        /// <summary>
        /// Deletes the job.
        /// </summary>
        private void DeleteJob()
        {
            using ( var rockContext = new RockContext() )
            {
                var jobService = new ServiceJobService( rockContext );
                var job = jobService.Get( GetJobId() );

                if ( job != null )
                {
                    jobService.Delete( job );
                    rockContext.SaveChanges();
                }
            }
        }
    }
}
