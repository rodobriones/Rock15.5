<%@ Control Language="C#" AutoEventWireup="true" CodeFile="CheckInOutView.ascx.cs" Inherits="RockWeb.Blocks.CheckIn.CheckInOutView" %>

<asp:UpdatePanel ID="upContent" runat="server">
    <ContentTemplate>
        <Rock:NotificationBox ID="nbAviso" runat="server" NotificationBoxType="Info" Visible="false" />

        <asp:Panel ID="pnlEntradasSalidas" runat="server">
            <div class="checkin-header">
                <h1>Personas Presentes del Check-in Configuration</h1>
            </div>

            <div class="checkin-body">
                <div class="checkin-scroll-panel">
                    <div class="scroller">
                        <Rock:Grid ID="gCheckIns" runat="server" DisplayType="Full" AllowPaging="false" RowItemText="persona" OnRowCommand="gCheckIns_RowCommand">
                            <Columns>
                                <Rock:RockBoundField DataField="NombreCompleto" HeaderText="Nombre" />
                                <Rock:DateTimeField DataField="HoraEntrada" HeaderText="Hora de Entrada" />
                                <Rock:RockTemplateField HeaderText="Acción">
                                    <ItemTemplate>
                                        <asp:LinkButton ID="btnCheckOut" runat="server" CssClass="btn btn-danger btn-sm" CommandName="CheckOut" CommandArgument='<%# Eval("AttendanceId") %>' Text="Check-Out" />
                                    </ItemTemplate>
                                </Rock:RockTemplateField>
                            </Columns>
                        </Rock:Grid>
                    </div>
                </div>
            </div>
        </asp:Panel>
    </ContentTemplate>
</asp:UpdatePanel>
