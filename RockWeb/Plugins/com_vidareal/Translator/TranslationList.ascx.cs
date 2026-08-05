using System;
using System.ComponentModel;
using System.Linq;
using System.Web.UI.WebControls;
using Rock;
using Rock.Data;
using Rock.Web.UI;
using Rock.Web.UI.Controls;
using com.vidareal.Translator;

namespace RockWeb.Plugins.com_vidareal.Translator
{
    /// <summary>
    /// Grid de administracion: ver, editar (correccion manual), excluir y borrar
    /// las traducciones cacheadas en _com_vidareal_Translator_Translation.
    /// Lee/escribe por TranslationStore (SQL crudo).
    /// </summary>
    [DisplayName( "VidaReal Translation List" )]
    [Category( "VidaReal > Translator" )]
    [Description( "Ver y editar las traducciones cacheadas." )]
    public partial class TranslationList : RockBlock
    {
        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );
            gTranslations.Actions.ShowAdd = false;
            gTranslations.IsDeleteEnabled = true;
        }

        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );
            if ( !Page.IsPostBack )
            {
                LoadLanguageFilter();
                ddlStatus.Items.Clear();
                ddlStatus.Items.Add( new ListItem( "Translated", "Translated" ) );
                ddlStatus.Items.Add( new ListItem( "Excluded", "Excluded" ) );
                BindGrid();
            }
        }

        private void LoadLanguageFilter()
        {
            using ( var rockContext = new RockContext() )
            {
                ddlLanguage.Items.Clear();
                ddlLanguage.Items.Add( new ListItem( "Todos", "" ) );
                foreach ( var lang in TranslationStore.GetLanguages( rockContext ) )
                {
                    ddlLanguage.Items.Add( new ListItem( lang, lang ) );
                }
            }
        }

        private void BindGrid()
        {
            using ( var rockContext = new RockContext() )
            {
                gTranslations.DataSource = TranslationStore.GetList(
                    rockContext, ddlLanguage.SelectedValue, tbSearch.Text );
                gTranslations.DataBind();
            }
        }

        protected void gfFilter_ApplyFilterClick( object sender, EventArgs e )
        {
            BindGrid();
        }

        protected void gTranslations_GridRebind( object sender, GridRebindEventArgs e )
        {
            BindGrid();
        }

        protected void gTranslations_RowSelected( object sender, RowEventArgs e )
        {
            using ( var rockContext = new RockContext() )
            {
                var row = TranslationStore.GetById( rockContext, e.RowKeyId );
                if ( row == null )
                {
                    return;
                }

                hfEditId.Value = row.Id.ToString();
                lSource.Text = ( row.SourceText ?? "" ).EncodeHtml();   // Literal -> encode para no inyectar markup
                lLang.Text = ( row.TargetLanguage ?? "" ).EncodeHtml();
                tbTranslated.Text = row.TranslatedText;
                ddlStatus.SetValue( string.IsNullOrEmpty( row.Status ) ? "Translated" : row.Status );
            }

            mdEdit.Show();
        }

        protected void mdEdit_SaveClick( object sender, EventArgs e )
        {
            var id = hfEditId.Value.AsInteger();
            if ( id > 0 )
            {
                using ( var rockContext = new RockContext() )
                {
                    TranslationStore.Update( rockContext, id, tbTranslated.Text, ddlStatus.SelectedValue );
                }
            }

            mdEdit.Hide();
            BindGrid();
            ShowMessage( "Traduccion actualizada. Limpia la cache del navegador para verla." );
        }

        protected void gTranslations_Delete( object sender, RowEventArgs e )
        {
            using ( var rockContext = new RockContext() )
            {
                TranslationStore.Delete( rockContext, e.RowKeyId );
            }
            BindGrid();
        }

        private void ShowMessage( string text )
        {
            nbMessage.NotificationBoxType = NotificationBoxType.Success;
            nbMessage.Text = text;
            nbMessage.Visible = true;
        }
    }
}
