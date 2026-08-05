<%@ Control Language="C#" AutoEventWireup="true" CodeFile="TranslatorSettings.ascx.cs" Inherits="RockWeb.Plugins.com_vidareal.Translator.TranslatorSettings" %>

<asp:UpdatePanel ID="upnl" runat="server">
    <ContentTemplate>
        <%-- notranslate: la pagina admin del propio traductor NO se traduce (confundiria la gestion de traducciones). --%>
        <div class="panel panel-block notranslate" data-no-translate="1">
            <div class="panel-heading">
                <h1 class="panel-title"><i class="fa fa-language"></i> VidaReal Translator</h1>
            </div>
            <div class="panel-body">

                <Rock:NotificationBox ID="nbMessage" runat="server" Visible="false" />

                <div class="margin-b-md">
                    <Rock:Toggle ID="tglEnabled" runat="server" Label="Habilitado"
                        OnText="S&iacute;" OffText="No" ActiveButtonCssClass="btn-success"
                        AutoPostBack="true" OnCheckedChanged="tglEnabled_CheckedChanged"
                        Help="Activa o desactiva el traductor en todo el sitio." />
                </div>

                <asp:Literal ID="lStatus" runat="server" />

                <div class="actions margin-t-md">
                    <Rock:BootstrapButton ID="btnPurge" runat="server" CssClass="btn btn-default"
                        Text="Purgar cach&eacute; de traducciones" OnClick="btnPurge_Click"
                        DataLoadingText="Purgando..." />
                </div>

                <p class="text-muted margin-t-md">
                    El idioma destino, el proveedor Azure (endpoint / deployment / API key) y los selectores
                    se editan en <i class="fa fa-cog"></i> <strong>Configuraci&oacute;n del bloque</strong>
                    (engranaje arriba a la derecha de este bloque).
                </p>

            </div>
        </div>
    </ContentTemplate>
</asp:UpdatePanel>
