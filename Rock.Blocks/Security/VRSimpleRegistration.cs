// <copyright>
// Copyright by the Spark Development Network
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Security.Authentication;
using Rock.Security.Authentication.Passwordless;
using Rock.Utility;
using Rock.Web.Cache;

namespace Rock.Blocks.Security
{
    /// <summary>
    /// Registro simplificado VidaReal post-validacion passwordless.
    /// </summary>
    [DisplayName( "VR Simple Registration" )]
    [Category( "VidaReal > Security" )]
    [Description( "Registro simplificado post-validacion passwordless." )]
    [SupportedSiteTypes( Model.SiteType.Web )]

    [LinkedPage(
        "Return Page",
        Key = AttributeKey.ReturnPage,
        Description = "Pagina a la que redirigir tras el registro. Si vacio, usa returnurl del query string.",
        IsRequired = false,
        Order = 0 )]

    [LinkedPage(
        "Login Page",
        Key = AttributeKey.LoginPage,
        Description = "Pagina de inicio de sesion (usada si el state esta expirado o ya existe la cuenta).",
        IsRequired = false,
        Order = 1 )]

    [Rock.SystemGuid.EntityTypeGuid( "E0AE2775-BFB2-4F28-A7C3-9FC968C42A86" )]
    [Rock.SystemGuid.BlockTypeGuid( "61C805E0-F228-4DCA-9934-3F12FEC67C7D" )]
    public class VRSimpleRegistration : RockBlockType
    {
        #region Keys

        private static class AttributeKey
        {
            public const string ReturnPage = "ReturnPage";
            public const string LoginPage = "LoginPage";
        }

        private static class PageParameterKey
        {
            public const string State = "State";
            public const string ReturnUrl = "returnurl";
        }

        #endregion

        #region View Models

        public class VRSimpleRegistrationInitBox
        {
            public bool IsBlocked { get; set; }
            public string Email { get; set; }
            public string PhoneNumber { get; set; }
            public string PhoneCountryCode { get; set; }
            public List<string> CountryCodes { get; set; } = new List<string>();
            public string DefaultCountryCode { get; set; }
            public string State { get; set; }
            public bool IsCampusPickerShown { get; set; }
            public List<VRCampusItem> Campuses { get; set; } = new List<VRCampusItem>();
            public string LoginPageUrl { get; set; }
            public string ReturnUrl { get; set; }
        }

        public class VRCampusItem
        {
            public string Name { get; set; } = "";
            public string Guid { get; set; } = "";
        }

        public class VRRegisterRequestBag
        {
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public int Gender { get; set; }
            public string Email { get; set; }
            public string PhoneNumber { get; set; }
            public string PhoneCountryCode { get; set; }
            public string CampusGuid { get; set; }
            public string State { get; set; }
            public string ReturnUrl { get; set; }
        }

        public class VRRegisterResponseBag
        {
            public bool IsSuccess { get; set; }
            public string RedirectUrl { get; set; }
            public string ErrorMessage { get; set; }
        }

        #endregion

        #region Methods

        public override object GetObsidianBlockInitialization()
        {
            var loginPageUrl = this.GetLinkedPageUrl( AttributeKey.LoginPage ) ?? "/Login";
            var stateString = PageParameter( PageParameterKey.State );

            PasswordlessAuthenticationState state = null;
            if ( stateString.IsNotNullOrWhiteSpace() )
            {
                state = PasswordlessAuthentication.GetDecryptedAuthenticationState( stateString );
            }

            // Block direct access: require a valid, not-yet-used passwordless session.
            if ( state == null )
            {
                return new VRSimpleRegistrationInitBox { IsBlocked = true, LoginPageUrl = loginPageUrl };
            }

            using ( var rockContext = new RockContext() )
            {
                var remoteAuthService = new RemoteAuthenticationSessionService( rockContext );
                var remoteSession = remoteAuthService.VerifyRemoteAuthenticationSession(
                    state.UniqueIdentifier, state.Code, state.CodeIssueDate, state.CodeLifetime );

                if ( remoteSession == null )
                {
                    return new VRSimpleRegistrationInitBox { IsBlocked = true, LoginPageUrl = loginPageUrl };
                }

                var countryCodes = new List<string> { "+502", "+503", "+1" };
                var defaultCode = "+502";

                var box = new VRSimpleRegistrationInitBox
                {
                    Email = state.Email,
                    PhoneNumber = state.PhoneNumber,
                    PhoneCountryCode = state.PhoneNumber.IsNotNullOrWhiteSpace() ? defaultCode : null,
                    CountryCodes = countryCodes,
                    DefaultCountryCode = defaultCode,
                    State = stateString,
                    IsCampusPickerShown = true,
                    LoginPageUrl = loginPageUrl,
                    ReturnUrl = GetSafeDecodedUrl( PageParameter( PageParameterKey.ReturnUrl ) )
                };

                box.Campuses = new CampusService( rockContext )
                    .Queryable()
                    .Where( c => c.IsActive == true )
                    .OrderBy( c => c.Name )
                    .Select( c => new VRCampusItem { Name = c.Name, Guid = c.Guid.ToString() } )
                    .ToList();

                return box;
            }
        }

        [BlockAction]
        public BlockActionResult Register( VRRegisterRequestBag bag )
        {
            if ( bag == null )
            {
                return ActionBadRequest( "Solicitud invalida." );
            }

            using ( var rockContext = new RockContext() )
            {
                var state = PasswordlessAuthentication.GetDecryptedAuthenticationState( bag.State ?? "" );
                if ( state == null )
                {
                    return ActionBadRequest( "Sesion expirada. Por favor inicia el proceso de nuevo." );
                }

                var remoteAuthService = new RemoteAuthenticationSessionService( rockContext );
                var remoteSession = remoteAuthService.VerifyRemoteAuthenticationSession(
                    state.UniqueIdentifier, state.Code, state.CodeIssueDate, state.CodeLifetime );
                if ( remoteSession == null )
                {
                    return ActionBadRequest( "La sesion de autenticacion ya no es valida." );
                }

                var email = state.Email.IsNotNullOrWhiteSpace() ? state.Email : ( bag.Email ?? "" ).Trim();
                var phone = state.PhoneNumber.IsNotNullOrWhiteSpace() ? state.PhoneNumber : ( bag.PhoneNumber ?? "" ).Trim();
                var cCode = ( bag.PhoneCountryCode ?? "502" ).Replace( "+", "" );

                var person = new Person
                {
                    FirstName = bag.FirstName?.Trim(),
                    LastName = bag.LastName?.Trim(),
                    Email = email,
                    IsEmailActive = email.IsNotNullOrWhiteSpace(),
                    EmailPreference = EmailPreference.EmailAllowed,
                    Gender = (Gender) bag.Gender,
                    RecordTypeValueId = DefinedValueCache.GetId(
                        Rock.SystemGuid.DefinedValue.PERSON_RECORD_TYPE_PERSON.AsGuid() ),
                    ConnectionStatusValueId = DefinedValueCache.GetId(
                        Rock.SystemGuid.DefinedValue.PERSON_CONNECTION_STATUS_PARTICIPANT.AsGuid() ),
                    RecordStatusValueId = DefinedValueCache.GetId(
                        Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_ACTIVE.AsGuid() )
                };

                int? campusId = null;
                if ( bag.CampusGuid.IsNotNullOrWhiteSpace() )
                {
                    campusId = CampusCache.Get( bag.CampusGuid.AsGuid() )?.Id;
                }

                PersonService.SaveNewPerson( person, rockContext, campusId, true );

                if ( phone.IsNotNullOrWhiteSpace() )
                {
                    var mobileType = DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_MOBILE.AsGuid() );
                    if ( mobileType != null )
                    {
                        person.UpdatePhoneNumber( mobileType.Id, cCode, phone, true, false, rockContext );
                    }
                }

                var uniqueIdentifier = state.UniqueIdentifier.IsNotNullOrWhiteSpace() ? state.UniqueIdentifier : email;
                UserLoginService.Create(
                    rockContext,
                    person,
                    AuthenticationServiceType.External,
                    EntityTypeCache.Get( typeof( PasswordlessAuthentication ) ).Id,
                    PasswordlessAuthentication.GetUsername( uniqueIdentifier ),
                    null,
                    isConfirmed: true );

                rockContext.SaveChanges();

                if ( person.PrimaryAliasId.HasValue )
                {
                    remoteAuthService.CompleteRemoteAuthenticationSession( remoteSession, person.PrimaryAliasId.Value );
                    rockContext.SaveChanges();
                }

                var returnUrl = GetSafeDecodedUrl( bag.ReturnUrl );
                if ( returnUrl.IsNullOrWhiteSpace() )
                {
                    returnUrl = GetSafeDecodedUrl( PageParameter( PageParameterKey.ReturnUrl ) );
                }
                if ( returnUrl.IsNullOrWhiteSpace() )
                {
                    returnUrl = this.GetLinkedPageUrl( AttributeKey.ReturnPage );
                }
                if ( returnUrl.IsNullOrWhiteSpace() )
                {
                    returnUrl = "/";
                }

                return ActionOk( new VRRegisterResponseBag
                {
                    IsSuccess = true,
                    RedirectUrl = returnUrl
                } );
            }
        }

        private string GetSafeDecodedUrl( string url )
        {
            if ( url.IsNullOrWhiteSpace() )
            {
                return url;
            }

            var decodedUrl = url.GetFullyUrlDecodedValue();

            if ( decodedUrl.Replace( "https://", string.Empty )
                .Replace( "http://", string.Empty )
                .RedirectUrlContainsXss() )
            {
                return null;
            }

            return decodedUrl;
        }

        #endregion
    }
}
