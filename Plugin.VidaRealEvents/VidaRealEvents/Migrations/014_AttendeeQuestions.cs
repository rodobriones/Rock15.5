using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Preguntas al asistente por tipo de boleto:
    /// - <c>TicketType.QuestionsJson</c>: configuración de preguntas del tipo de boleto
    ///   (básicos del perfil + atributos de persona del catálogo, con flag required).
    /// - <c>Ticket.AnswersJson</c>: snapshot de las respuestas capturadas en ESA compra
    ///   (la persona puede actualizar sus valores después; el ticket conserva lo respondido).
    /// - Categoría "Preguntas de Eventos" para Person Attributes: el catálogo maestro de
    ///   preguntas vive como atributos de persona bajo esta categoría (las respuestas quedan
    ///   amarradas a la persona vía AttributeValue → prefill automático en eventos futuros).
    /// Idempotente.
    /// </summary>
    // Paso de la migración consolidada (017_ProductionSetup la ejecuta en orden).
    // SIN [MigrationNumber]: ya no corre por sí sola.
    public class AttendeeQuestions : Migration
    {
        /// <summary>
        /// Guid fijo de la categoría de atributos "Preguntas de Eventos" (rango 35xx = categorías).
        /// Instalaciones que corrieron la 014 original (guid 30xx, colisionaba con la página
        /// "Eventos") se corrigen en la migración 016.
        /// </summary>
        public const string QuestionCategoryGuid = "b2e4d8f1-2c3e-4f7b-ad12-350000000001";

        public override void Up()
        {
            Sql( @"
IF COL_LENGTH('dbo._com_vidareal_Events_TicketType','QuestionsJson') IS NULL
    ALTER TABLE [dbo].[_com_vidareal_Events_TicketType] ADD [QuestionsJson] NVARCHAR(MAX) NULL;

IF COL_LENGTH('dbo._com_vidareal_Events_Ticket','AnswersJson') IS NULL
    ALTER TABLE [dbo].[_com_vidareal_Events_Ticket] ADD [AnswersJson] NVARCHAR(MAX) NULL;" );

            // Categoría para atributos de Persona (EntityType=Attribute, calificada a Person).
            Sql( $@"
DECLARE @AttributeEntityTypeId INT = (SELECT TOP 1 [Id] FROM [EntityType] WHERE [Name] = 'Rock.Model.Attribute');
DECLARE @PersonEntityTypeId INT = (SELECT TOP 1 [Id] FROM [EntityType] WHERE [Name] = 'Rock.Model.Person');

IF NOT EXISTS (SELECT 1 FROM [Category] WHERE [Guid] = '{QuestionCategoryGuid}')
BEGIN
    INSERT INTO [Category] ( [IsSystem], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue], [Name], [Description], [IconCssClass], [Order], [Guid] )
    VALUES ( 0, @AttributeEntityTypeId, 'EntityTypeId', CAST(@PersonEntityTypeId AS NVARCHAR(10)), 'Preguntas de Eventos',
             'Catálogo de preguntas al asistente del módulo de Eventos. Las respuestas quedan en el perfil de la persona y se pre-llenan en eventos futuros.',
             'ti ti-help-circle', 0, '{QuestionCategoryGuid}' );
END" );
        }

        public override void Down()
        {
            Sql( $@"
IF COL_LENGTH('dbo._com_vidareal_Events_TicketType','QuestionsJson') IS NOT NULL
    ALTER TABLE [dbo].[_com_vidareal_Events_TicketType] DROP COLUMN [QuestionsJson];
IF COL_LENGTH('dbo._com_vidareal_Events_Ticket','AnswersJson') IS NOT NULL
    ALTER TABLE [dbo].[_com_vidareal_Events_Ticket] DROP COLUMN [AnswersJson];
DELETE FROM [Category] WHERE [Guid] = '{QuestionCategoryGuid}';" );
        }
    }
}
