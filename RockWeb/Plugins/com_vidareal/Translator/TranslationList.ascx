<%@ Control Language="C#" AutoEventWireup="true" CodeFile="TranslationList.ascx.cs" Inherits="RockWeb.Plugins.com_vidareal.Translator.TranslationList" %>

<asp:UpdatePanel ID="upnl" runat="server">
    <ContentTemplate>

        <Rock:NotificationBox ID="nbMessage" runat="server" Visible="false" />

        <%-- notranslate: la pagina admin del propio traductor NO se traduce (confundiria la gestion de traducciones). --%>
        <div class="panel panel-block notranslate" data-no-translate="1">
            <div class="panel-heading">
                <h1 class="panel-title"><i class="fa fa-language"></i> Traducciones</h1>
            </div>
            <div class="panel-body">

                <Rock:GridFilter ID="gfFilter" runat="server" OnApplyFilterClick="gfFilter_ApplyFilterClick">
                    <Rock:RockDropDownList ID="ddlLanguage" runat="server" Label="Idioma" />
                    <Rock:RockTextBox ID="tbSearch" runat="server" Label="Buscar (original o traduccion)" />
                </Rock:GridFilter>

                <Rock:Grid ID="gTranslations" runat="server" AllowSorting="false" DataKeyNames="Id"
                    OnRowSelected="gTranslations_RowSelected" OnGridRebind="gTranslations_GridRebind"
                    RowItemText="traduccion">
                    <Columns>
                        <Rock:RockBoundField DataField="SourceText" HeaderText="Texto original (ingl&eacute;s)" />
                        <Rock:RockBoundField DataField="TargetLanguage" HeaderText="Idioma" ItemStyle-CssClass="text-center" HeaderStyle-Width="80px" />
                        <Rock:RockBoundField DataField="TranslatedText" HeaderText="Traducci&oacute;n" />
                        <Rock:RockBoundField DataField="Status" HeaderText="Estado" HeaderStyle-Width="110px" />
                        <Rock:RockBoundField DataField="ModifiedDateTime" HeaderText="Modificado" HeaderStyle-Width="150px" />
                        <Rock:DeleteField OnClick="gTranslations_Delete" />
                    </Columns>
                </Rock:Grid>

            </div>
        </div>

        <Rock:ModalDialog ID="mdEdit" runat="server" Title="Editar traduccion" OnSaveClick="mdEdit_SaveClick" ValidationGroup="vgEdit">
            <Content>
                <asp:HiddenField ID="hfEditId" runat="server" />
                <Rock:RockLiteral ID="lSource" runat="server" Label="Texto original" />
                <Rock:RockLiteral ID="lLang" runat="server" Label="Idioma" />
                <Rock:RockTextBox ID="tbTranslated" runat="server" Label="Traduccion" TextMode="MultiLine" Rows="3" ValidationGroup="vgEdit" />
                <Rock:RockDropDownList ID="ddlStatus" runat="server" Label="Estado" ValidationGroup="vgEdit"
                    Help="Translated: se usa la traduccion. Excluded: nunca se traduce (se deja el original)." />
                <p class="text-muted">
                    Tras guardar, limpia la cache del navegador (localStorage 'vrtr:') o haz recarga dura para ver el cambio.
                </p>
            </Content>
        </Rock:ModalDialog>

    </ContentTemplate>
</asp:UpdatePanel>
