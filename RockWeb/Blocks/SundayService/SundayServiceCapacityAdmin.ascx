<%@ Control Language="C#" AutoEventWireup="true" CodeFile="SundayServiceCapacityAdmin.ascx.cs"
    Inherits="RockWeb.Blocks.QREVENT.SundayServiceCapacityAdmin" %>

<asp:UpdatePanel ID="upMain" runat="server">
    <ContentTemplate>

        <asp:Literal ID="ltMsg" runat="server" />

        <div class="panel panel-block">
            <div class="panel-heading">
                <h1 class="panel-title">Cupos Servicios Dominicales</h1>
            </div>

            <div class="panel-body">

                <div class="row">
                    <div class="col-md-4">
                        <label>Campus</label>
                        <Rock:RockDropDownList ID="ddlCampus" runat="server"
                            AutoPostBack="true" OnSelectedIndexChanged="ddlCampus_SelectedIndexChanged" />

                    </div>

                    <div class="col-md-4">
                        <label>Fecha inicio</label>
<Rock:DatePicker ID="dpStart" runat="server"
    AutoPostBack="true" OnTextChanged="dpDates_TextChanged" />
                        </div>

                    <div class="col-md-4">
                        <label>Fecha fin</label>
<Rock:DatePicker ID="dpEnd" runat="server"
    AutoPostBack="true" OnTextChanged="dpDates_TextChanged" />
                        </div>
                </div>

                <hr />

                <div class="row">
                    <div class="col-md-12">
                        <label>Capacidad por horario (Schedules existentes)</label>
                        <div class="text-muted">
                            Esto aplica una plantilla completa (Domingos del rango x Schedules seleccionados). Opcional: desactiva slots extra que no estén en la plantilla.
                        </div>
                    </div>
                </div>

                <asp:Repeater ID="rptSchedules" runat="server">
                    <HeaderTemplate>
                        <div class="table-responsive">
                            <table class="table table-striped">
                                <thead>
                                    <tr>
                                        <th style="width: 60px;">Usar</th>
                                        <th>Schedule</th>
                                        <th style="width: 160px;">Capacity</th>
                                    </tr>
                                </thead>
                                <tbody>
                    </HeaderTemplate>

                    <ItemTemplate>
                        <tr>
                            <td>
                                <asp:CheckBox ID="cbUse" runat="server" Checked="true" />
                                <asp:HiddenField ID="hfScheduleId" runat="server" Value='<%# Eval("Id") %>' />
                            </td>
                            <td>
                                <%# Eval("Name") %>
                            </td>
                            <td>
                                <Rock:RockTextBox ID="tbCapacity" runat="server" Text='<%# Eval("DefaultCapacity") %>' />
                            </td>
                        </tr>
                    </ItemTemplate>

                    <FooterTemplate>
                                </tbody>
                            </table>
                        </div>
                    </FooterTemplate>
                </asp:Repeater>

                <div class="row">
                    <div class="col-md-6">
                        <Rock:RockCheckBox ID="cbUpdateExisting" runat="server"
                            Text="Actualizar capacity si el slot ya existe" Checked="true" />

                        <Rock:RockCheckBox ID="cbDeactivateMissing" runat="server"
                            Text="Reemplazar secuencia: desactivar slots NO incluidos en los schedules seleccionados (solo si no tienen reservados/holds)" />
                    </div>

                    <div class="col-md-6 text-right">
                        <Rock:BootstrapButton ID="btnGenerate" runat="server" CssClass="btn btn-primary"
                            Text="Aplicar plantilla al rango" OnClick="btnGenerate_Click" />
                    </div>
                </div>

            </div>
        </div>

        <Rock:Grid ID="gSlots" runat="server" DisplayType="Full" AllowPaging="true" PageSize="50">
            <Columns>
                <Rock:RockBoundField DataField="OccurrenceDate" HeaderText="Domingo" DataFormatString="{0:yyyy-MM-dd}" />
                <Rock:RockBoundField DataField="CampusName" HeaderText="Campus" />
                <Rock:RockBoundField DataField="ScheduleName" HeaderText="Schedule" />
                <Rock:RockBoundField DataField="Capacity" HeaderText="Capacity" />
                <Rock:RockBoundField DataField="ReservedCount" HeaderText="Reservados" />
                <Rock:RockBoundField DataField="HoldCount" HeaderText="Holds" />
                <Rock:RockBoundField DataField="Available" HeaderText="Disponible" />
                <Rock:RockBoundField DataField="IsActive" HeaderText="Activo" />
            </Columns>
        </Rock:Grid>

    </ContentTemplate>
</asp:UpdatePanel>
