using Rock.Plugin;

namespace com.vidareal.Qr.Migrations
{
    /// <summary>
    /// Registra el BlockType Obsidian del generador, crea la pagina interna con su ruta, la
    /// categoria raiz del catalogo, y la seguridad: solo comunicacion y TI.
    ///
    /// La seguridad va en la pagina Y en el bloque a proposito. Quien no esta en el grupo no ve
    /// el menu y tampoco llega por URL directa — el bloque valida por su cuenta (CanEdit), asi
    /// que no depende de que la navegacion lo esconda.
    ///
    /// El Site y el SiteDomain del dominio corto NO se crean aqui: son datos de infraestructura
    /// (dominio, certificado) que no deben viajar en una migracion entre ambientes, porque el
    /// dominio de dev no es el de produccion. Van en el runbook.
    /// </summary>
    [MigrationNumber( 3, "1.16.0" )]
    public class QrPages : Migration
    {
        // --- Contenedores del core ---
        //
        // Estos dos Guid se VERIFICARON contra la base (Rock 18), no se copiaron de otra
        // migracion. La version anterior usaba Guid de Rock 15.5 que en Rock 18 no existen, y el
        // sintoma era feo: "Cannot insert the value NULL into column 'LayoutId'", porque
        // AddPage no encuentra el layout, deja LayoutId nulo y el INSERT revienta.
        //
        // Para re-derivarlos en otro ambiente:
        //   SELECT l.[Guid] FROM [Page] p JOIN [Layout] l ON l.Id = p.LayoutId
        //   WHERE p.InternalName = 'Short Links';                        -> LAYOUT_INTERNAL
        //   SELECT [Guid] FROM [Page] WHERE InternalName = 'Digital Tools';  -> PARENT_INTERNAL
        //
        // Digital Tools es el padre correcto: es donde vive "Short Links", la entidad sobre la
        // que este modulo construye, y es donde la gente lo va a buscar.
        private const string PARENT_INTERNAL = "a6d78c4f-958f-4196-b8ff-527a10f5f047"; // Admin Tools > Digital Tools
        private const string LAYOUT_INTERNAL = "d65f783d-87a9-4cc9-8110-e83466a0eadb"; // Full Width (sitio Rock RMS)

        private const string FT_PAGE_REFERENCE = "BD53F9C9-EBA9-4D3F-82EA-DE5DD34A8108";

        // --- Block types (mismo Guid que [BlockTypeGuid] en C#) ---
        private const string BT_DETAIL = "c7d2e5a0-4b31-4c8e-9d02-b40000000102";
        private const string BT_LIST = "c7d2e5a0-4b31-4c8e-9d02-b40000000103";

        // --- Paginas ---
        private const string PAGE_QR = "c7d2e5a0-4b31-4c8e-9d02-b40000000201";
        private const string ROUTE_QR = "c7d2e5a0-4b31-4c8e-9d02-b40000000202";
        private const string PAGE_QR_DETAIL = "c7d2e5a0-4b31-4c8e-9d02-b40000000203";
        private const string ROUTE_QR_DETAIL = "c7d2e5a0-4b31-4c8e-9d02-b40000000204";

        // --- Bloques colocados ---
        private const string BLOCK_DETAIL = "c7d2e5a0-4b31-4c8e-9d02-b40000000301";
        private const string BLOCK_LIST = "c7d2e5a0-4b31-4c8e-9d02-b40000000302";

        // --- Block setting "Pagina de detalle" del catalogo ---
        private const string ATTR_DETAILPAGE = "c7d2e5a0-4b31-4c8e-9d02-b40000000701";
        private const string ATTR_LISTPAGE = "c7d2e5a0-4b31-4c8e-9d02-b40000000702";

        // --- Entidad y categoria raiz del catalogo ---
        // Mismo Guid que [EntityTypeGuid] en Rock/Model/Qr/QrCode.cs.
        private const string ENTITYTYPE_QRCODE = "c7d2e5a0-4b31-4c8e-9d02-b40000000001";
        private const string CATEGORY_ROOT = "c7d2e5a0-4b31-4c8e-9d02-b40000000401";

        // --- Grupo de seguridad ---
        private const string GROUP_QR = "c7d2e5a0-4b31-4c8e-9d02-b40000000501";

        // --- Auth ---
        private const string AUTH_PAGE_V_ADMINS = "c7d2e5a0-4b31-4c8e-9d02-b40000000601";
        private const string AUTH_PAGE_V_QR = "c7d2e5a0-4b31-4c8e-9d02-b40000000602";
        private const string AUTH_PAGE_V_DENY = "c7d2e5a0-4b31-4c8e-9d02-b40000000603";
        private const string AUTH_PAGE_E_ADMINS = "c7d2e5a0-4b31-4c8e-9d02-b40000000604";
        private const string AUTH_PAGE_E_QR = "c7d2e5a0-4b31-4c8e-9d02-b40000000605";
        private const string AUTH_PAGE_E_DENY = "c7d2e5a0-4b31-4c8e-9d02-b40000000606";

        private const string GUID_ADMINS = Rock.SystemGuid.Group.GROUP_ADMINISTRATORS;
        private const string ENTITY_PAGE = "Rock.Model.Page";

        public override void Up()
        {
            // 0) Fallar temprano y claro si el ambiente no tiene el layout o la pagina padre.
            //    Sin esto, AddPage deja LayoutId nulo y el error que llega al log es
            //    "Cannot insert the value NULL into column 'LayoutId'", que no dice nada sobre
            //    cual Guid falta ni donde buscarlo.
            Sql( $@"
IF NOT EXISTS ( SELECT 1 FROM [Layout] WHERE [Guid] = '{LAYOUT_INTERNAL}' )
    THROW 51000, 'QrPages: no existe el Layout {LAYOUT_INTERNAL}. Buscá el correcto con: SELECT l.[Guid] FROM [Page] p JOIN [Layout] l ON l.Id = p.LayoutId WHERE p.InternalName = ''Short Links''.', 1;

IF NOT EXISTS ( SELECT 1 FROM [Page] WHERE [Guid] = '{PARENT_INTERNAL}' )
    THROW 51001, 'QrPages: no existe la pagina padre {PARENT_INTERNAL}. Buscá la correcta con: SELECT [Guid] FROM [Page] WHERE InternalName = ''Digital Tools''.', 1;
" );

            // 1) BlockType Obsidian (idempotente; el scan de Rock tambien lo encuentra, pero
            //    garantizarlo aqui hace la migracion independiente del orden de arranque).
            RockMigrationHelper.AddOrUpdateEntityBlockType(
                "QR Code Detail",
                "Crea y edita un codigo QR institucional, dinamico o estatico.",
                "Rock.Blocks.Qr.QrCodeDetail",
                "Vida Real > QR",
                BT_DETAIL );

            RockMigrationHelper.AddOrUpdateEntityBlockType(
                "QR Code List",
                "Catalogo de codigos QR institucionales: que existen, a donde apuntan y cuanto se usan.",
                "Rock.Blocks.Qr.QrCodeList",
                "Vida Real > QR",
                BT_LIST );

            // 2) Paginas internas + rutas amigables. El catalogo es la pagina padre porque es la
            //    entrega de gobernanza; el editor cuelga de el.
            RockMigrationHelper.AddPage( true, PARENT_INTERNAL, LAYOUT_INTERNAL,
                "Codigos QR", "Catalogo de codigos QR institucionales.",
                PAGE_QR, "ti ti-qrcode" );

            RockMigrationHelper.AddPage( true, PAGE_QR, LAYOUT_INTERNAL,
                "Generador de QR", "Crear y editar un codigo QR: diseno, destino y descargas.",
                PAGE_QR_DETAIL, "ti ti-pencil" );

            RockMigrationHelper.AddOrUpdatePageRoute( PAGE_QR, "qr", ROUTE_QR );
            RockMigrationHelper.AddOrUpdatePageRoute( PAGE_QR_DETAIL, "qr/codigo/{QrCodeId}", ROUTE_QR_DETAIL );

            // 3) Colocar los bloques.
            RockMigrationHelper.AddBlock( true, PAGE_QR, "", BT_LIST,
                "QR Code List", "Main", "", "", 0, BLOCK_LIST );
            RockMigrationHelper.AddBlock( true, PAGE_QR_DETAIL, "", BT_DETAIL,
                "QR Code Detail", "Main", "", "", 0, BLOCK_DETAIL );

            // 3b) Cablear el catalogo con el editor para que "Nuevo codigo" y los enlaces de cada
            //     fila funcionen sin configuracion manual.
            RockMigrationHelper.AddBlockTypeAttribute( BT_LIST, FT_PAGE_REFERENCE,
                "Página de detalle", "DetailPage", "",
                "La pagina con el bloque QR Code Detail.", 0, "", ATTR_DETAILPAGE );
            RockMigrationHelper.AddBlockAttributeValue( BLOCK_LIST, ATTR_DETAILPAGE, PAGE_QR_DETAIL );

            // Y el editor de vuelta con el catalogo, para el enlace "Volver".
            RockMigrationHelper.AddBlockTypeAttribute( BT_DETAIL, FT_PAGE_REFERENCE,
                "Página del catálogo", "ListPage", "",
                "La pagina con el bloque QR Code List, para el enlace de volver.", 0, "", ATTR_LISTPAGE );
            RockMigrationHelper.AddBlockAttributeValue( BLOCK_DETAIL, ATTR_LISTPAGE, PAGE_QR );

            // 4) Grupo de seguridad de comunicacion y TI.
            //    Se crea vacio: a quien entra lo decide la gente, no una migracion.
            Sql( $@"
DECLARE @SecurityGroupTypeId INT = ( SELECT TOP 1 [Id] FROM [GroupType] WHERE [Guid] = '{Rock.SystemGuid.GroupType.GROUPTYPE_SECURITY_ROLE}' );

IF NOT EXISTS ( SELECT 1 FROM [Group] WHERE [Guid] = '{GROUP_QR}' )
BEGIN
    INSERT INTO [Group] ( [IsSystem], [GroupTypeId], [Name], [Description], [IsSecurityRole], [IsActive], [Order], [Guid] )
    VALUES ( 0, @SecurityGroupTypeId, 'Generador de QR',
        'Comunicacion y TI: quienes pueden crear y editar codigos QR institucionales.', 1, 1, 0, '{GROUP_QR}' );
END
" );

            // 5) Categoria raiz del catalogo, sobre la entidad QrCode.
            //    Se garantiza el EntityType primero. Antes esto era un INSERT con
            //    "IF @EntityTypeId IS NOT NULL": si la migracion corria antes de que Rock
            //    escaneara la entidad, la categoria NO se creaba — y como las migraciones corren
            //    una sola vez, no se creaba nunca y el filtro del catalogo quedaba vacio para
            //    siempre. UpdateEntityType lo hace determinista.
            RockMigrationHelper.UpdateEntityType( "Rock.Model.QrCode", ENTITYTYPE_QRCODE, true, true );

            RockMigrationHelper.UpdateCategory( ENTITYTYPE_QRCODE, "Códigos QR", "ti ti-qrcode",
                "Categoría raíz del catálogo de QR institucionales.", CATEGORY_ROOT );

            // 6) Seguridad de la pagina: administradores y el grupo de QR ven y editan; el resto no.
            //    El orden importa: los Allow van primero y el Deny a todos cierra al final.
            RockMigrationHelper.AddSecurityAuthForPage( PAGE_QR, 0, "View", true, GUID_ADMINS,
                ( int ) Rock.Model.SpecialRole.None, AUTH_PAGE_V_ADMINS );
            RockMigrationHelper.AddSecurityAuthForPage( PAGE_QR, 1, "View", true, GROUP_QR,
                ( int ) Rock.Model.SpecialRole.None, AUTH_PAGE_V_QR );
            RockMigrationHelper.AddSecurityAuthForPage( PAGE_QR, 2, "View", false, null,
                ( int ) Rock.Model.SpecialRole.AllUsers, AUTH_PAGE_V_DENY );

            RockMigrationHelper.AddSecurityAuthForPage( PAGE_QR, 0, "Edit", true, GUID_ADMINS,
                ( int ) Rock.Model.SpecialRole.None, AUTH_PAGE_E_ADMINS );
            RockMigrationHelper.AddSecurityAuthForPage( PAGE_QR, 1, "Edit", true, GROUP_QR,
                ( int ) Rock.Model.SpecialRole.None, AUTH_PAGE_E_QR );
            RockMigrationHelper.AddSecurityAuthForPage( PAGE_QR, 2, "Edit", false, null,
                ( int ) Rock.Model.SpecialRole.AllUsers, AUTH_PAGE_E_DENY );
        }

        public override void Down()
        {
            RockMigrationHelper.DeleteSecurityAuth( AUTH_PAGE_E_DENY );
            RockMigrationHelper.DeleteSecurityAuth( AUTH_PAGE_E_QR );
            RockMigrationHelper.DeleteSecurityAuth( AUTH_PAGE_E_ADMINS );
            RockMigrationHelper.DeleteSecurityAuth( AUTH_PAGE_V_DENY );
            RockMigrationHelper.DeleteSecurityAuth( AUTH_PAGE_V_QR );
            RockMigrationHelper.DeleteSecurityAuth( AUTH_PAGE_V_ADMINS );

            Sql( $"DELETE FROM [Category] WHERE [Guid] = '{CATEGORY_ROOT}';" );
            Sql( $"DELETE FROM [Group] WHERE [Guid] = '{GROUP_QR}';" );

            RockMigrationHelper.DeleteAttribute( ATTR_DETAILPAGE );
            RockMigrationHelper.DeleteAttribute( ATTR_LISTPAGE );
            RockMigrationHelper.DeleteBlock( BLOCK_DETAIL );
            RockMigrationHelper.DeleteBlock( BLOCK_LIST );

            // La hija primero: borrar la padre con hijas deja paginas huerfanas.
            RockMigrationHelper.DeletePage( PAGE_QR_DETAIL );
            RockMigrationHelper.DeletePage( PAGE_QR );
        }
    }
}
