<%@ Control Language="C#" AutoEventWireup="true" CodeFile="EventParticipants.ascx.cs" Inherits="RockWeb.Blocks.QREVENT.EventParticipants" %>

<script>
    var _searchTimer;
    function debounceSearch() {
        clearTimeout(_searchTimer);
        _searchTimer = setTimeout(function () {
            var btn = document.getElementById('<%= btnFiltrar.ClientID %>');
            if (btn) btn.click();
        }, 400);
    }
</script>

<asp:UpdatePanel ID="upMain" runat="server">
    <ContentTemplate>

        <!-- Selector de evento -->
        <div class="row margin-b-md">
            <div class="col-md-12">
                <div class="form-group">
                    <label>Selecciona un evento:</label>
                    <asp:DropDownList ID="ddlEventos" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlEventos_SelectedIndexChanged" />
                </div>
            </div>
        </div>

        <asp:Panel ID="pnlContent" runat="server" Visible="false">

            <!-- Filtros -->
            <div class="row margin-b-md">
                <div class="col-md-5">
                    <Rock:RockTextBox ID="tbSearch" runat="server" Placeholder="Buscar por nombre o email..." onkeyup="debounceSearch()" />
                </div>
                <div class="col-md-3">
                    <asp:DropDownList ID="ddlFiltroAsistencia" runat="server" CssClass="form-control">
                        <asp:ListItem Text="Todos los registros" Value="" />
                        <asp:ListItem Text="Solo asistieron" Value="si" />
                        <asp:ListItem Text="No asistieron" Value="no" />
                    </asp:DropDownList>
                </div>
                <div class="col-md-4" style="padding-top:4px">
                    <asp:LinkButton ID="btnFiltrar" runat="server" CssClass="btn btn-primary" OnClick="btnFiltrar_Click">
                        <i class="fa fa-filter"></i> Filtrar
                    </asp:LinkButton>
                    <asp:LinkButton ID="btnLimpiar" runat="server" CssClass="btn btn-default margin-l-sm" OnClick="btnLimpiar_Click">
                        <i class="fa fa-times"></i> Limpiar
                    </asp:LinkButton>
                    &nbsp;<asp:Literal ID="ltResultados" runat="server" />
                </div>
            </div>

            <div class="row">

                <!-- Grid -->
                <div class="col-md-9">
                    <Rock:Grid ID="gRegistrants" runat="server" DisplayType="Full" AllowPaging="true" PageSize="25"
                        ShowActionRow="true" ShowExportExcelButton="True" ExcelFileName="ParticipantesEvento"
                        AutoGenerateColumns="false">
                        <Columns>
                            <Rock:RockTemplateField HeaderText="Nombre" SortExpression="Nombre">
                                <ItemTemplate>
                                    <a href='<%# string.Format("{0}{1}", ResolveRockUrl("~/Person/"), Eval("PersonId")) %>' target="_blank">
                                        <%# Eval("Nombre") %>
                                    </a>
                                </ItemTemplate>
                            </Rock:RockTemplateField>
                            <Rock:RockBoundField DataField="Email" HeaderText="Email" />
                            <Rock:RockBoundField DataField="FechaRegistro" HeaderText="Fecha Registro" DataFormatString="{0:yyyy-MM-dd HH:mm}" />
                            <Rock:RockBoundField DataField="Estado" HeaderText="Estado" />
                            <Rock:RockBoundField DataField="AsistioQR" HeaderText="&iquest;Asisti&oacute;?" />
                            <Rock:RockBoundField DataField="FechaAsistencia" HeaderText="Fecha Asistencia" />
                        </Columns>
                    </Rock:Grid>
                </div>

                <!-- Stats -->
                <div class="col-md-3">
                    <div style="margin-bottom:12px; border-radius:6px; overflow:hidden; background:#E07B39; color:#fff;">
                        <div style="padding:18px 12px; text-align:center;">
                            <div style="font-size:2.4rem; font-weight:700; line-height:1;">
                                <asp:Literal ID="ltTotalRegistrados" runat="server" Text="0" />
                            </div>
                            <div style="font-size:0.85rem; margin-top:4px;">Total Registrados</div>
                        </div>
                    </div>

                    <div style="margin-bottom:12px; border-radius:6px; overflow:hidden; background:#2e9e4f; color:#fff;">
                        <div style="padding:18px 12px; text-align:center;">
                            <div style="font-size:2.4rem; font-weight:700; line-height:1;">
                                <asp:Literal ID="ltTotalAsistentes" runat="server" Text="0" />
                            </div>
                            <div style="font-size:0.85rem; margin-top:4px;">Asistieron</div>
                        </div>
                    </div>

                    <div style="margin-bottom:12px; border-radius:6px; overflow:hidden; background:#d9534f; color:#fff;">
                        <div style="padding:18px 12px; text-align:center;">
                            <div style="font-size:2.4rem; font-weight:700; line-height:1;">
                                <asp:Literal ID="ltNoAsistieron" runat="server" Text="0" />
                            </div>
                            <div style="font-size:0.85rem; margin-top:4px;">No Asistieron</div>
                        </div>
                    </div>

                    <div style="margin-bottom:12px; border-radius:6px; overflow:hidden; background:#31708f; color:#fff;">
                        <div style="padding:18px 12px; text-align:center;">
                            <div style="font-size:2.4rem; font-weight:700; line-height:1;">
                                <asp:Literal ID="ltPorcentaje" runat="server" Text="0%" />
                            </div>
                            <div style="font-size:0.85rem; margin-top:4px;">% Asistencia</div>
                        </div>
                    </div>
                </div>

            </div>
        </asp:Panel>

    </ContentTemplate>
</asp:UpdatePanel>
