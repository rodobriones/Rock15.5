using Rock.Data;

namespace Rock.Model
{
    public partial class Contact
    {
        internal class SaveHook : EntitySaveHook<Contact>
        {
            protected override void PostSave()
            {
                var contactTemporaryPhotoId = Entity.PhotoId;
                if ( contactTemporaryPhotoId.HasValue )
                {
                    using ( var rockContext = new RockContext() )
                    {
                        var binaryFileService = new BinaryFileService( rockContext );
                        var binaryFile = binaryFileService.Get( contactTemporaryPhotoId.Value );
                        if ( binaryFile != null && binaryFile.IsTemporary )
                        {
                            binaryFile.IsTemporary = false;
                            rockContext.SaveChanges();
                        }
                    }
                }

                base.PostSave();
            }
        }
    }
}
