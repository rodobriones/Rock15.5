// <copyright>
// Copyright by the Spark Development Network
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
// </copyright>
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;

using Newtonsoft.Json;

using Rock.Attribute;
using Rock.Blocks;
using Rock.Data;
using Rock.Model;
using Rock.Security;
using Rock.Web.Cache;

namespace Rock.Blocks.Eventos
{
    /// <summary>
    /// Catálogo de Preguntas del módulo de Eventos: administración central de las preguntas al
    /// asistente (Person Attributes de la categoría "Preguntas de Eventos") y de las plantillas
    /// (conjuntos nombrados de preguntas listos para aplicar a un tipo de boleto).
    /// </summary>
    /// <remarks>
    /// Las plantillas se guardan como JSON en un System Setting de Rock (sin tablas nuevas):
    /// volumen pequeño, editado por pocos admins. El Event Admin solo SELECCIONA del catálogo
    /// o aplica plantillas; crear/editar/eliminar preguntas vive únicamente aquí.
    /// </remarks>
    [DisplayName( "Question Catalog" )]
    [Category( "Eventos" )]
    [Description( "Catálogo central de preguntas al asistente y plantillas de preguntas para tipos de boleto." )]
    [Rock.SystemGuid.BlockTypeGuid( "b2e4d8f1-2c3e-4f7b-ad12-200000000006" )]
    public class QuestionCatalog : RockBlockType
    {
        private const string TemplatesSettingKey = "com_vidareal_EventQuestionTemplates";

        #region Block Initialization

        public override object GetObsidianBlockInitialization()
        {
            var currentPerson = RequestContext?.CurrentPerson;
            var canEdit = currentPerson != null && CanEdit();

            if ( !canEdit )
            {
                return new { notAuthorized = true };
            }

            using ( var rockContext = new RockContext() )
            {
                return new
                {
                    notAuthorized = false,
                    items = BuildCatalogItems( rockContext ),
                    templates = LoadTemplates()
                };
            }
        }

        #endregion

        #region Block Actions

        /// <summary>Crea una pregunta nueva del catálogo (Person Attribute en la categoría).</summary>
        [BlockAction( "CreateQuestion" )]
        public BlockActionResult CreateQuestion( QuestionEditBag bag )
        {
            if ( !CanEdit() )
            {
                return ActionForbidden( "No tienes permiso para administrar el catálogo." );
            }

            var name = ( bag?.name ?? string.Empty ).Trim();
            if ( name.Length < 3 )
            {
                return ActionBadRequest( "El nombre de la pregunta es obligatorio (mínimo 3 caracteres)." );
            }

            var fieldTypeGuid = MapQuestionType( bag.type );
            if ( fieldTypeGuid == null )
            {
                return ActionBadRequest( "Tipo de pregunta no válido." );
            }

            var isSelect = bag.type == "select" || bag.type == "multiselect";
            var options = CleanOptions( bag.options );
            if ( isSelect && options.Count < 2 )
            {
                return ActionBadRequest( "Una pregunta de selección necesita al menos 2 opciones." );
            }

            using ( var rockContext = new RockContext() )
            {
                var personEntityTypeId = EntityTypeCache.Get( typeof( Rock.Model.Person ) ).Id;
                var fieldTypeId = FieldTypeCache.Get( fieldTypeGuid.Value ).Id;
                var attributeService = new AttributeService( rockContext );

                // Key único: "Evt" + nombre alfanumérico; sufijo numérico si colisiona.
                var baseKey = "Evt" + new string( name.Where( char.IsLetterOrDigit ).ToArray() );
                if ( baseKey.Length > 90 )
                {
                    baseKey = baseKey.Substring( 0, 90 );
                }

                var existingKeys = new HashSet<string>( attributeService.Queryable()
                    .Where( a => a.EntityTypeId == personEntityTypeId && a.Key.StartsWith( baseKey ) )
                    .Select( a => a.Key ), StringComparer.OrdinalIgnoreCase );

                var key = baseKey;
                var suffix = 2;
                while ( existingKeys.Contains( key ) )
                {
                    key = baseKey + suffix++;
                }

                var category = new CategoryService( rockContext ).Get( AttendeeQuestionService.QuestionCategoryGuid );
                if ( category == null )
                {
                    return ActionBadRequest( "La categoría \"Preguntas de Eventos\" no existe todavía. Recicla el sitio para que corra la migración 014." );
                }

                var attribute = new Rock.Model.Attribute
                {
                    EntityTypeId = personEntityTypeId,
                    EntityTypeQualifierColumn = string.Empty,
                    EntityTypeQualifierValue = string.Empty,
                    Name = name,
                    Key = key,
                    Description = ( bag.description ?? string.Empty ).Trim(),
                    FieldTypeId = fieldTypeId,
                    IsActive = true,
                    IsGridColumn = false,
                    IsMultiValue = false,
                    IsRequired = false,
                    Order = AttendeeQuestionService.GetCatalogAttributes().Count
                };

                attribute.Categories.Add( category );

                if ( isSelect )
                {
                    attribute.AttributeQualifiers.Add( new AttributeQualifier { Key = "values", Value = string.Join( ",", options ), IsSystem = false } );
                    attribute.AttributeQualifiers.Add( new AttributeQualifier { Key = "fieldtype", Value = bag.type == "multiselect" ? "cb" : "ddl", IsSystem = false } );
                }

                attributeService.Add( attribute );
                rockContext.SaveChanges();

                return ActionOk( new { items = BuildCatalogItems( rockContext ) } );
            }
        }

        /// <summary>
        /// Edita nombre/descripción/opciones/activo de una pregunta. El tipo no cambia (las
        /// respuestas ya guardadas quedarían con un formato incompatible).
        /// </summary>
        [BlockAction( "UpdateQuestion" )]
        public BlockActionResult UpdateQuestion( QuestionEditBag bag )
        {
            if ( !CanEdit() )
            {
                return ActionForbidden( "No tienes permiso para administrar el catálogo." );
            }

            var name = ( bag?.name ?? string.Empty ).Trim();
            if ( bag?.guid == null || bag.guid == Guid.Empty || name.Length < 3 )
            {
                return ActionBadRequest( "Datos inválidos." );
            }

            using ( var rockContext = new RockContext() )
            {
                var attributeService = new AttributeService( rockContext );
                var attribute = attributeService.Get( bag.guid.Value );
                if ( attribute == null || !IsCatalogAttribute( attribute.Guid ) )
                {
                    return ActionNotFound( "Pregunta no encontrada en el catálogo." );
                }

                attribute.Name = name;
                attribute.Description = ( bag.description ?? string.Empty ).Trim();
                attribute.IsActive = bag.isActive;

                var options = CleanOptions( bag.options );
                var valuesQualifier = attribute.AttributeQualifiers.FirstOrDefault( q => q.Key == "values" );
                if ( valuesQualifier != null && options.Count >= 2 )
                {
                    valuesQualifier.Value = string.Join( ",", options );
                }

                rockContext.SaveChanges();

                return ActionOk( new { items = BuildCatalogItems( rockContext ) } );
            }
        }

        /// <summary>
        /// Elimina una pregunta del catálogo. Solo si ningún tipo de boleto la usa: borrarla
        /// elimina también las respuestas guardadas en las personas (cascade de AttributeValue).
        /// </summary>
        [BlockAction( "DeleteQuestion" )]
        public BlockActionResult DeleteQuestion( Guid guid )
        {
            if ( !CanEdit() )
            {
                return ActionForbidden( "No tienes permiso para administrar el catálogo." );
            }

            using ( var rockContext = new RockContext() )
            {
                var usedBy = CountTicketTypeUsage( rockContext, guid );
                if ( usedBy > 0 )
                {
                    return ActionBadRequest( $"No se puede eliminar: {usedBy} tipo(s) de boleto la usan. Quítala de esos boletos primero (o desactívala)." );
                }

                var attributeService = new AttributeService( rockContext );
                var attribute = attributeService.Get( guid );
                if ( attribute == null || !IsCatalogAttribute( attribute.Guid ) )
                {
                    return ActionNotFound( "Pregunta no encontrada en el catálogo." );
                }

                attributeService.Delete( attribute );
                rockContext.SaveChanges();

                return ActionOk( new { items = BuildCatalogItems( rockContext ) } );
            }
        }

        /// <summary>Crea o actualiza una plantilla de preguntas.</summary>
        [BlockAction( "SaveTemplate" )]
        public BlockActionResult SaveTemplate( TemplateEditBag bag )
        {
            if ( !CanEdit() )
            {
                return ActionForbidden( "No tienes permiso para administrar el catálogo." );
            }

            var name = ( bag?.name ?? string.Empty ).Trim();
            if ( name.Length < 3 )
            {
                return ActionBadRequest( "El nombre de la plantilla es obligatorio (mínimo 3 caracteres)." );
            }

            // Misma normalización que SaveTicketType: solo básicos conocidos y atributos existentes.
            var entries = AttendeeQuestionService.ParseConfig( bag.questionsJson );
            var basicKeys = new HashSet<string> { "phone", "email", "birthDate", "gender" };
            var catalogGuids = new HashSet<Guid>( AttendeeQuestionService.GetCatalogAttributes().Select( a => a.Guid ) );
            var clean = entries.Where( q =>
                    ( q.Kind == "basic" && basicKeys.Contains( q.Key ) )
                    || ( q.Kind == "attr" && q.AttributeGuid.HasValue && catalogGuids.Contains( q.AttributeGuid.Value ) ) )
                .ToList();

            if ( !clean.Any() )
            {
                return ActionBadRequest( "La plantilla necesita al menos una pregunta." );
            }

            var templates = LoadTemplates();
            var existing = bag.guid.HasValue ? templates.FirstOrDefault( t => t.guid == bag.guid.Value ) : null;

            if ( existing == null )
            {
                templates.Add( new TemplateBag { guid = Guid.NewGuid(), name = name, questionsJson = JsonConvert.SerializeObject( clean ) } );
            }
            else
            {
                existing.name = name;
                existing.questionsJson = JsonConvert.SerializeObject( clean );
            }

            SaveTemplates( templates );
            return ActionOk( new { templates } );
        }

        /// <summary>Elimina una plantilla (los boletos que ya la aplicaron conservan sus preguntas).</summary>
        [BlockAction( "DeleteTemplate" )]
        public BlockActionResult DeleteTemplate( Guid guid )
        {
            if ( !CanEdit() )
            {
                return ActionForbidden( "No tienes permiso para administrar el catálogo." );
            }

            var templates = LoadTemplates().Where( t => t.guid != guid ).ToList();
            SaveTemplates( templates );
            return ActionOk( new { templates } );
        }

        #endregion

        #region Private Helpers

        private bool CanEdit()
        {
            var currentPerson = RequestContext?.CurrentPerson;
            return currentPerson != null
                && BlockCache != null
                && ( BlockCache.IsAuthorized( Authorization.EDIT, currentPerson )
                    || BlockCache.IsAuthorized( Authorization.ADMINISTRATE, currentPerson ) );
        }

        private static bool IsCatalogAttribute( Guid attributeGuid )
        {
            var attribute = AttributeCache.Get( attributeGuid );
            return attribute != null && attribute.Categories.Any( c => c.Guid == AttendeeQuestionService.QuestionCategoryGuid );
        }

        private static List<string> CleanOptions( List<string> options )
        {
            return ( options ?? new List<string>() )
                .Select( o => ( o ?? string.Empty ).Trim() )
                .Where( o => o.Length > 0 )
                .Distinct()
                .ToList();
        }

        /// <summary>Cuántos tipos de boleto referencian el atributo en su QuestionsJson.</summary>
        private static int CountTicketTypeUsage( RockContext rockContext, Guid attributeGuid )
        {
            var configs = new TicketTypeService( rockContext ).Queryable()
                .AsNoTracking()
                .Where( tt => tt.QuestionsJson != null )
                .Select( tt => tt.QuestionsJson )
                .ToList();

            return configs.Count( json => AttendeeQuestionService.ParseConfig( json )
                .Any( q => q.Kind == "attr" && q.AttributeGuid == attributeGuid ) );
        }

        private static List<CatalogItemBag> BuildCatalogItems( RockContext rockContext )
        {
            var usageByGuid = new Dictionary<Guid, int>();
            foreach ( var json in new TicketTypeService( rockContext ).Queryable()
                .AsNoTracking()
                .Where( tt => tt.QuestionsJson != null )
                .Select( tt => tt.QuestionsJson )
                .ToList() )
            {
                foreach ( var q in AttendeeQuestionService.ParseConfig( json ).Where( q => q.Kind == "attr" && q.AttributeGuid.HasValue ) )
                {
                    usageByGuid[q.AttributeGuid.Value] = usageByGuid.TryGetValue( q.AttributeGuid.Value, out var n ) ? n + 1 : 1;
                }
            }

            var personEntityTypeId = EntityTypeCache.Get( typeof( Rock.Model.Person ) ).Id;

            // Incluye inactivas (a diferencia de GetCatalogAttributes): aquí se administran.
            return AttributeCache.GetByEntityType( personEntityTypeId )
                .Where( a => a != null && a.Categories.Any( c => c.Guid == AttendeeQuestionService.QuestionCategoryGuid ) )
                .OrderBy( a => a.Order )
                .ThenBy( a => a.Name )
                .Select( a => new CatalogItemBag
                {
                    guid = a.Guid,
                    name = a.Name,
                    description = a.Description,
                    typeLabel = FriendlyFieldTypeLabel( a.FieldTypeId ),
                    isActive = a.IsActive,
                    isSelect = HasValuesQualifier( a ),
                    options = HasValuesQualifier( a ) ? ( a.QualifierValues.TryGetValue( "values", out var v ) ? v?.Value : null ) : null,
                    usedByCount = usageByGuid.TryGetValue( a.Guid, out var used ) ? used : 0
                } )
                .ToList();
        }

        private static bool HasValuesQualifier( AttributeCache attribute )
        {
            return attribute.QualifierValues != null && attribute.QualifierValues.ContainsKey( "values" );
        }

        private static Guid? MapQuestionType( string type )
        {
            switch ( type )
            {
                case "text":
                    return Rock.SystemGuid.FieldType.TEXT.AsGuid();
                case "memo":
                    return Rock.SystemGuid.FieldType.MEMO.AsGuid();
                case "select":
                    return Rock.SystemGuid.FieldType.SINGLE_SELECT.AsGuid();
                case "multiselect":
                    return Rock.SystemGuid.FieldType.MULTI_SELECT.AsGuid();
                case "number":
                    return Rock.SystemGuid.FieldType.INTEGER.AsGuid();
                case "date":
                    return Rock.SystemGuid.FieldType.DATE.AsGuid();
                case "boolean":
                    return Rock.SystemGuid.FieldType.BOOLEAN.AsGuid();
                default:
                    return null;
            }
        }

        internal static string FriendlyFieldTypeLabel( int fieldTypeId )
        {
            var ft = FieldTypeCache.Get( fieldTypeId );
            switch ( ft?.Guid.ToString().ToUpperInvariant() )
            {
                case Rock.SystemGuid.FieldType.TEXT:
                    return "Texto";
                case Rock.SystemGuid.FieldType.MEMO:
                    return "Párrafo";
                case Rock.SystemGuid.FieldType.SINGLE_SELECT:
                    return "Selección";
                case Rock.SystemGuid.FieldType.MULTI_SELECT:
                    return "Selección múltiple";
                case Rock.SystemGuid.FieldType.INTEGER:
                    return "Número";
                case Rock.SystemGuid.FieldType.DATE:
                    return "Fecha";
                case Rock.SystemGuid.FieldType.BOOLEAN:
                    return "Sí / No";
                default:
                    return ft?.Name ?? "—";
            }
        }

        // ---- Plantillas (System Settings; sin tablas) ----

        internal static List<TemplateBag> LoadTemplates()
        {
            var json = Rock.Web.SystemSettings.GetValue( TemplatesSettingKey );
            if ( string.IsNullOrWhiteSpace( json ) )
            {
                return new List<TemplateBag>();
            }

            try
            {
                return JsonConvert.DeserializeObject<List<TemplateBag>>( json ) ?? new List<TemplateBag>();
            }
            catch
            {
                return new List<TemplateBag>();
            }
        }

        private static void SaveTemplates( List<TemplateBag> templates )
        {
            Rock.Web.SystemSettings.SetValue( TemplatesSettingKey, JsonConvert.SerializeObject( templates ) );
        }

        #endregion

        #region View Models

        public class CatalogItemBag
        {
            public Guid guid { get; set; }
            public string name { get; set; }
            public string description { get; set; }
            public string typeLabel { get; set; }
            public bool isActive { get; set; }
            public bool isSelect { get; set; }
            /// <summary>Opciones actuales (coma-separadas) cuando es de selección.</summary>
            public string options { get; set; }
            public int usedByCount { get; set; }
        }

        public class QuestionEditBag
        {
            public Guid? guid { get; set; }
            public string name { get; set; }
            public string description { get; set; }
            /// <summary>text | memo | select | multiselect | number | date | boolean (solo al crear).</summary>
            public string type { get; set; }
            public List<string> options { get; set; }
            public bool isActive { get; set; } = true;
        }

        public class TemplateBag
        {
            public Guid guid { get; set; }
            public string name { get; set; }
            /// <summary>Mismo formato que TicketType.QuestionsJson.</summary>
            public string questionsJson { get; set; }
        }

        public class TemplateEditBag
        {
            public Guid? guid { get; set; }
            public string name { get; set; }
            public string questionsJson { get; set; }
        }

        #endregion
    }
}
