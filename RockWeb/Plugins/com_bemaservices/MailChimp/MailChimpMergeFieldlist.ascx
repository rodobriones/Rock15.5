<%@ Control Language="C#" AutoEventWireup="true" CodeFile="MailChimpMergeFieldlist.ascx.cs" Inherits="com.bemaservices.MailChimp.MailChimpMergeFieldlist" %>

<asp:UpdatePanel ID="upnlSettings" runat="server">
    <ContentTemplate>

        <asp:Panel ID="pnlContent" runat="server" CssClass="panel panel-block">
            <asp:HiddenField ID="hfDefinedTypeId" runat="server" />

            <div class="panel-heading">
                <h1 class="panel-title">
                    <i class="fa fa-envelope"></i>
                    Merge Fields
                </h1>
            </div>
            <div class="panel-body">

                <asp:Panel ID="pnlList" runat="server" Visible="false">

                    <asp:Panel ID="pnlValues" runat="server">
                        <Rock:ModalAlert ID="mdGridWarningValues" runat="server" />

                        <div class="grid grid-panel">
                            <Rock:Grid ID="gDefinedValues" runat="server" AllowPaging="true" EmptyDataText="No MailChimp Merge Fields Found for this Audience" DisplayType="Full" OnRowSelected="gDefinedValues_Edit" AllowSorting="False" TooltipField="Id">
                                <Columns>
                                    <Rock:ReorderField />
                                    <Rock:RockBoundField DataField="Value" HeaderText="Tag" />
                                    <Rock:RockBoundField DataField="Description" HeaderText="Name" />
                                </Columns>
                            </Rock:Grid>
                        </div>

                    </asp:Panel>

                </asp:Panel>

            </div>

            <Rock:ModalDialog ID="modalValue" runat="server" Title="Merge Field" ValidationGroup="Value" >
                <Content>

                <asp:HiddenField ID="hfDefinedValueId" runat="server" />
                <asp:ValidationSummary ID="valSummaryValue" runat="server" HeaderText="Please correct the following:" CssClass="alert alert-validation" ValidationGroup="Value" />
                <legend>
                    <asp:Literal ID="lActionTitleDefinedValue" runat="server" />
                </legend>
                <fieldset>
                    <Rock:DataTextBox ID="tbValueName" runat="server" SourceTypeName="Rock.Model.DefinedValue, Rock" PropertyName="Value" ValidationGroup="Value" Label="Value"/>
                    <Rock:DataTextBox ID="tbValueDescription" runat="server" SourceTypeName="Rock.Model.DefinedValue, Rock" PropertyName="Description" TextMode="MultiLine" Rows="3" ValidationGroup="Value" ValidateRequestMode="Disabled"/>
                    <asp:CheckBox ID="cbValueActive" runat="server" Text="Active" />
                    <div class="attributes">
                        <Rock:AttributeValuesContainer ID="avcDefinedValueAttributes" runat="server" />
                    </div>
                </fieldset>

                </Content>
            </Rock:ModalDialog>

        </asp:Panel>

    </ContentTemplate>
</asp:UpdatePanel>
