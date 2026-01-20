<%@ Control Language="C#" AutoEventWireup="true" CodeFile="EventParticipants.ascx.cs" Inherits="RockWeb.Blocks.QREVENT.EventParticipants" %>

<asp:UpdatePanel ID="upMain" runat="server">
    <ContentTemplate>
        <div class="row">
            <!-- Columna izquierda -->
            <div class="col-md-9">
                <div class="form-group">
                    <label>Selecciona un evento:</label>
                    <asp:DropDownList ID="ddlEventos" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlEventos_SelectedIndexChanged" />
                </div>

                <Rock:Grid ID="gRegistrants" runat="server" DisplayType="Full" AllowPaging="false"  ShowActionRow="true" ShowExportExcelButton="True" ExcelFileName="ParticipantesEvento">
                    <Columns>
                        <Rock:RockBoundField DataField="Nombre" HeaderText="Nombre" />
                        <Rock:RockBoundField DataField="Email" HeaderText="Email" />
                        <Rock:RockBoundField DataField="FechaRegistro" HeaderText="Fecha Registro" DataFormatString="{0:yyyy-MM-dd HH:mm}" />
                        <Rock:RockBoundField DataField="Estado" HeaderText="Estado" />
                        <Rock:RockBoundField DataField="AsistioQR" HeaderText="&iquest;Asisti&oacute;?" />
                        <Rock:RockBoundField DataField="FechaAsistencia" HeaderText="Fecha de Asistencia" DataFormatString="{0:yyyy-MM-dd HH:mm}" />
                    </Columns>
                </Rock:Grid>
            </div>

            <!-- Columna derecha -->
            <div class="col-md-3">
                <div class="card text-white bg-primary mb-3">
                    <div class="card-body">
                        <h5 class="card-title">Total Asistentes</h5>
                        <asp:Literal ID="ltTotalAsistentes" runat="server" Text="0" />
                    </div>
                </div>
            </div>
        </div>
    </ContentTemplate>
</asp:UpdatePanel>
