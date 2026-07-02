using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Crea la tabla de cache de traducciones + indice unico.
    /// Patron: Rock.Plugin.Migration + [MigrationNumber] (igual que Rock.Checkr).
    /// La configuracion vive como block attributes del bloque de settings
    /// (migracion 002), NO como Global Attributes.
    /// </summary>
    [MigrationNumber( 1, "18.0" )]
    public class TranslatorSetup : Migration
    {
        private const string TableName = "_com_vidareal_Translator_Translation";

        public override void Up()
        {
            AddTable( TableName, t => new
            {
                Id = t.Int( identity: true, nullable: false ),
                Guid = t.Guid( nullable: false, defaultValueSql: "newid()" ),
                SourceHash = t.String( maxLength: 64, nullable: false ),
                SourceText = t.String( nullable: false ),                 // nvarchar(max)
                TargetLanguage = t.String( maxLength: 10, nullable: false ),
                TranslatedText = t.String(),                              // nvarchar(max)
                Provider = t.String( maxLength: 50 ),
                Status = t.String( maxLength: 20, nullable: false ),
                UsageCount = t.Int( nullable: false, defaultValue: 0 ),
                CreatedDateTime = t.DateTime( nullable: false, defaultValueSql: "getdate()" ),
                ModifiedDateTime = t.DateTime( nullable: false, defaultValueSql: "getdate()" )
            } );

            AddPrimaryKey( TableName, "Id" );
            AddIndex( TableName, new[] { "SourceHash", "TargetLanguage" }, unique: true, name: "IX_SourceHash_TargetLanguage" );
        }

        public override void Down()
        {
            DropTable( TableName );
        }
    }
}
