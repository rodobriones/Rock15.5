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
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Rock;
using Rock.Enums.Qr;
using Rock.Model;

namespace Rock.Tests.Model.Qr
{
    /// <summary>
    /// Tests for <see cref="QrCatalogService"/>: the payload that gets encoded and the integrity
    /// rules the database cannot express.
    /// </summary>
    /// <remarks>
    /// Todo lo que se prueba acá es estático y puro: no toca base de datos ni HTTP. Lo que sale de
    /// <see cref="QrCatalogService.BuildStaticPayload"/> se imprime, y un papel impreso no se
    /// corrige con un deploy.
    /// </remarks>
    [TestClass]
    public class QrCatalogServiceTests
    {
        #region BuildStaticPayload - Url

        [DataTestMethod]
        [DataRow( "vidareal.tv", "https://vidareal.tv" )]
        [DataRow( "www.vidareal.tv/qr", "https://www.vidareal.tv/qr" )]
        [DataRow( "  vidareal.tv  ", "https://vidareal.tv" )]
        public void BuildStaticPayload_UrlSinEsquema_AnteponeHttps( string content, string expected )
        {
            var payload = QrCatalogService.BuildStaticPayload( QrStaticContentType.Url, content );

            Assert.AreEqual( expected, payload );
        }

        [DataTestMethod]
        [DataRow( "https://vidareal.tv" )]
        [DataRow( "http://vidareal.tv" )]
        [DataRow( "HTTPS://vidareal.tv" )]
        [DataRow( "Http://vidareal.tv/ruta?a=1" )]
        public void BuildStaticPayload_UrlConEsquema_LaDejaIntacta( string content )
        {
            var payload = QrCatalogService.BuildStaticPayload( QrStaticContentType.Url, content );

            Assert.AreEqual( content, payload );
        }

        #endregion

        #region BuildStaticPayload - Email y Telefono

        [TestMethod]
        public void BuildStaticPayload_Email_AnteponeMailto()
        {
            var payload = QrCatalogService.BuildStaticPayload( QrStaticContentType.Email, "  info@vidareal.tv  " );

            Assert.AreEqual( "mailto:info@vidareal.tv", payload );
        }

        [DataTestMethod]
        [DataRow( "+502 5555 1234", "tel:+50255551234" )]
        [DataRow( "  2222 3333  ", "tel:22223333" )]
        [DataRow( "22223333", "tel:22223333" )]
        public void BuildStaticPayload_Telefono_AnteponeTelYQuitaEspacios( string content, string expected )
        {
            var payload = QrCatalogService.BuildStaticPayload( QrStaticContentType.Telefono, content );

            Assert.AreEqual( expected, payload );
        }

        #endregion

        #region BuildStaticPayload - Wifi

        /// <summary>
        /// El escape del wifi es lo único de este módulo que no se puede corregir después: el
        /// payload viaja dentro del código impreso. En el formato <c>WIFI:</c> los caracteres
        /// <c>; : , \ "</c> son estructurales — terminan un campo. Un <c>;</c> sin escapar en el
        /// SSID «Red;Casa» corta el campo ahí y el teléfono intenta conectarse a la red «Red»,
        /// que es otra red o ninguna. No falla de forma visible: escanea, conecta mal y nadie
        /// entiende por qué. Por eso cada carácter estructural tiene su propia fila.
        /// </summary>
        [DataTestMethod]
        [DataRow( "Red;Casa", "WIFI:T:WPA;S:Red\\;Casa;;" )]
        [DataRow( "Red:Casa", "WIFI:T:WPA;S:Red\\:Casa;;" )]
        [DataRow( "Red,Casa", "WIFI:T:WPA;S:Red\\,Casa;;" )]
        [DataRow( "Red\\Casa", "WIFI:T:WPA;S:Red\\\\Casa;;" )]
        [DataRow( "Red\"Casa", "WIFI:T:WPA;S:Red\\\"Casa;;" )]
        public void BuildStaticPayload_WifiConCaracterEstructuralEnSsid_LoEscapa( string ssid, string expected )
        {
            var content = new QrWifiPayload { Ssid = ssid, Authentication = "WPA" }.ToJson();

            var payload = QrCatalogService.BuildStaticPayload( QrStaticContentType.Wifi, content );

            Assert.AreEqual( expected, payload );
        }

        [DataTestMethod]
        [DataRow( "cla;ve", "WIFI:T:WPA;S:RedVidaReal;P:cla\\;ve;;" )]
        [DataRow( "cla:ve", "WIFI:T:WPA;S:RedVidaReal;P:cla\\:ve;;" )]
        [DataRow( "cla,ve", "WIFI:T:WPA;S:RedVidaReal;P:cla\\,ve;;" )]
        [DataRow( "cla\\ve", "WIFI:T:WPA;S:RedVidaReal;P:cla\\\\ve;;" )]
        [DataRow( "cla\"ve", "WIFI:T:WPA;S:RedVidaReal;P:cla\\\"ve;;" )]
        public void BuildStaticPayload_WifiConCaracterEstructuralEnPassword_LoEscapa( string password, string expected )
        {
            var content = new QrWifiPayload
            {
                Ssid = "RedVidaReal",
                Password = password,
                Authentication = "WPA"
            }.ToJson();

            var payload = QrCatalogService.BuildStaticPayload( QrStaticContentType.Wifi, content );

            Assert.AreEqual( expected, payload );
        }

        [TestMethod]
        public void BuildStaticPayload_WifiConTodosLosCaracteresEstructurales_LosEscapaTodos()
        {
            var content = new QrWifiPayload
            {
                // SSID real: Red;Vida:Real,Guate\Zona"1
                Ssid = "Red;Vida:Real,Guate\\Zona\"1",
                // Password real: c;l:a,v\e"1
                Password = "c;l:a,v\\e\"1",
                Authentication = "WPA"
            }.ToJson();

            var payload = QrCatalogService.BuildStaticPayload( QrStaticContentType.Wifi, content );

            Assert.AreEqual(
                "WIFI:T:WPA;S:Red\\;Vida\\:Real\\,Guate\\\\Zona\\\"1;P:c\\;l\\:a\\,v\\\\e\\\"1;;",
                payload );
        }

        [DataTestMethod]
        [DataRow( "nopass" )]
        [DataRow( "NOPASS" )]
        [DataRow( "NoPass" )]
        public void BuildStaticPayload_WifiNopass_NoIncluyeCampoP( string authentication )
        {
            var content = new QrWifiPayload
            {
                Ssid = "RedVidaReal",
                // Aunque venga una contraseña guardada, una red abierta no lleva campo P.
                Password = "quedo-de-antes",
                Authentication = authentication
            }.ToJson();

            var payload = QrCatalogService.BuildStaticPayload( QrStaticContentType.Wifi, content );

            Assert.AreEqual( "WIFI:T:nopass;S:RedVidaReal;;", payload );
            Assert.IsFalse( payload.Contains( "P:" ) );
        }

        [TestMethod]
        public void BuildStaticPayload_WifiOculta_IncluyeHTrue()
        {
            var content = new QrWifiPayload
            {
                Ssid = "RedVidaReal",
                Password = "clave",
                Authentication = "WPA",
                IsHidden = true
            }.ToJson();

            var payload = QrCatalogService.BuildStaticPayload( QrStaticContentType.Wifi, content );

            Assert.AreEqual( "WIFI:T:WPA;S:RedVidaReal;P:clave;H:true;;", payload );
        }

        [TestMethod]
        public void BuildStaticPayload_WifiVisible_NoIncluyeH()
        {
            var content = new QrWifiPayload
            {
                Ssid = "RedVidaReal",
                Password = "clave",
                Authentication = "WPA",
                IsHidden = false
            }.ToJson();

            var payload = QrCatalogService.BuildStaticPayload( QrStaticContentType.Wifi, content );

            Assert.AreEqual( "WIFI:T:WPA;S:RedVidaReal;P:clave;;", payload );
        }

        [TestMethod]
        public void BuildStaticPayload_WifiSinAutenticacion_AsumeWpa()
        {
            var content = new QrWifiPayload { Ssid = "RedVidaReal", Authentication = null }.ToJson();

            var payload = QrCatalogService.BuildStaticPayload( QrStaticContentType.Wifi, content );

            Assert.AreEqual( "WIFI:T:WPA;S:RedVidaReal;;", payload );
        }

        [TestMethod]
        public void BuildStaticPayload_WifiSinSsid_DevuelveNull()
        {
            var content = new QrWifiPayload { Ssid = null, Password = "clave" }.ToJson();

            var payload = QrCatalogService.BuildStaticPayload( QrStaticContentType.Wifi, content );

            Assert.IsNull( payload );
        }

        [TestMethod]
        public void BuildStaticPayload_WifiConJsonInvalido_DevuelveNull()
        {
            var payload = QrCatalogService.BuildStaticPayload( QrStaticContentType.Wifi, "esto no es json" );

            Assert.IsNull( payload );
        }

        #endregion

        #region BuildStaticPayload - VCard

        [TestMethod]
        public void BuildStaticPayload_VCardSinNombreNiApellido_DevuelveNull()
        {
            var content = new QrVCardPayload
            {
                FirstName = "   ",
                LastName = null,
                Organization = "Iglesia Vida Real",
                Email = "info@vidareal.tv"
            }.ToJson();

            var payload = QrCatalogService.BuildStaticPayload( QrStaticContentType.VCard, content );

            Assert.IsNull( payload );
        }

        [TestMethod]
        public void BuildStaticPayload_VCardCompleta_ProduceVCard30Valida()
        {
            var content = new QrVCardPayload
            {
                FirstName = "Rodolfo",
                LastName = "Briones",
                Organization = "Iglesia Vida Real",
                Title = "Director de TI",
                Phone = "+502 5555 1234",
                Email = "rodolfo@vidareal.tv",
                Website = "https://vidareal.tv"
            }.ToJson();

            var payload = QrCatalogService.BuildStaticPayload( QrStaticContentType.VCard, content );

            var expected = string.Join( "\r\n", new[]
            {
                "BEGIN:VCARD",
                "VERSION:3.0",
                "N:Briones;Rodolfo;;;",
                "FN:Rodolfo Briones",
                "ORG:Iglesia Vida Real",
                "TITLE:Director de TI",
                "TEL;TYPE=CELL:+502 5555 1234",
                "EMAIL:rodolfo@vidareal.tv",
                "URL:https://vidareal.tv",
                "END:VCARD"
            } );

            Assert.AreEqual( expected, payload );
        }

        [TestMethod]
        public void BuildStaticPayload_VCardSoloConApellido_EsValida()
        {
            var content = new QrVCardPayload { LastName = "Briones" }.ToJson();

            var payload = QrCatalogService.BuildStaticPayload( QrStaticContentType.VCard, content );

            var expected = string.Join( "\r\n", new[]
            {
                "BEGIN:VCARD",
                "VERSION:3.0",
                "N:Briones;;;;",
                "FN:Briones",
                "END:VCARD"
            } );

            Assert.AreEqual( expected, payload );
        }

        [TestMethod]
        public void BuildStaticPayload_VCardConJsonInvalido_DevuelveNull()
        {
            var payload = QrCatalogService.BuildStaticPayload( QrStaticContentType.VCard, "esto no es json" );

            Assert.IsNull( payload );
        }

        #endregion

        #region BuildStaticPayload - Texto y contenido vacío

        [DataTestMethod]
        [DataRow( null )]
        [DataRow( "" )]
        [DataRow( "   " )]
        [DataRow( "\t\r\n" )]
        public void BuildStaticPayload_ContenidoVacio_DevuelveNull( string content )
        {
            Assert.IsNull( QrCatalogService.BuildStaticPayload( QrStaticContentType.Url, content ) );
            Assert.IsNull( QrCatalogService.BuildStaticPayload( QrStaticContentType.Texto, content ) );
            Assert.IsNull( QrCatalogService.BuildStaticPayload( QrStaticContentType.Wifi, content ) );
            Assert.IsNull( QrCatalogService.BuildStaticPayload( null, content ) );
        }

        [TestMethod]
        public void BuildStaticPayload_Texto_DevuelveElContenidoRecortado()
        {
            var payload = QrCatalogService.BuildStaticPayload( QrStaticContentType.Texto, "  Dios es bueno  " );

            Assert.AreEqual( "Dios es bueno", payload );
        }

        [TestMethod]
        public void BuildStaticPayload_SinTipo_DevuelveElContenidoRecortado()
        {
            var payload = QrCatalogService.BuildStaticPayload( null, "  contenido  " );

            Assert.AreEqual( "contenido", payload );
        }

        #endregion

        #region ValidateIntegrity

        [TestMethod]
        public void ValidateIntegrity_CodigoNull_DevuelveError()
        {
            Assert.IsNotNull( QrCatalogService.ValidateIntegrity( null ) );
        }

        [DataTestMethod]
        [DataRow( null )]
        [DataRow( "" )]
        [DataRow( "   " )]
        public void ValidateIntegrity_SinNombre_DevuelveError( string name )
        {
            var qrCode = new QrCode
            {
                Name = name,
                QrType = QrCodeType.Dinamico,
                PageShortLinkId = 1
            };

            Assert.IsNotNull( QrCatalogService.ValidateIntegrity( qrCode ) );
        }

        [TestMethod]
        public void ValidateIntegrity_DinamicoSinEnlaceCorto_DevuelveError()
        {
            var qrCode = new QrCode
            {
                Name = "Volante de bienvenida",
                QrType = QrCodeType.Dinamico,
                PageShortLinkId = null,
                PageShortLink = null
            };

            Assert.IsNotNull( QrCatalogService.ValidateIntegrity( qrCode ) );
        }

        [TestMethod]
        public void ValidateIntegrity_DinamicoConContenidoEstatico_DevuelveError()
        {
            var qrCode = new QrCode
            {
                Name = "Volante de bienvenida",
                QrType = QrCodeType.Dinamico,
                PageShortLinkId = 1,
                StaticContentType = QrStaticContentType.Url,
                StaticContent = "https://vidareal.tv"
            };

            Assert.IsNotNull( QrCatalogService.ValidateIntegrity( qrCode ) );
        }

        [TestMethod]
        public void ValidateIntegrity_EstaticoConEnlaceCorto_DevuelveError()
        {
            var qrCode = new QrCode
            {
                Name = "Wifi del café",
                QrType = QrCodeType.Estatico,
                PageShortLinkId = 1,
                StaticContentType = QrStaticContentType.Url,
                StaticContent = "https://vidareal.tv"
            };

            Assert.IsNotNull( QrCatalogService.ValidateIntegrity( qrCode ) );
        }

        [DataTestMethod]
        [DataRow( null )]
        [DataRow( "" )]
        [DataRow( "   " )]
        public void ValidateIntegrity_EstaticoSinContenido_DevuelveError( string staticContent )
        {
            var qrCode = new QrCode
            {
                Name = "Wifi del café",
                QrType = QrCodeType.Estatico,
                StaticContentType = QrStaticContentType.Wifi,
                StaticContent = staticContent
            };

            Assert.IsNotNull( QrCatalogService.ValidateIntegrity( qrCode ) );
        }

        [TestMethod]
        public void ValidateIntegrity_EstaticoConContenidoQueNoProducePayload_DevuelveError()
        {
            // JSON bien formado pero sin SSID: pasa el "tiene contenido" y aun así no produce un
            // código escaneable. Es exactamente el caso que la base no puede frenar.
            var qrCode = new QrCode
            {
                Name = "Wifi del café",
                QrType = QrCodeType.Estatico,
                StaticContentType = QrStaticContentType.Wifi,
                StaticContent = new QrWifiPayload { Ssid = null, Password = "clave" }.ToJson()
            };

            Assert.IsNotNull( QrCatalogService.ValidateIntegrity( qrCode ) );
        }

        [TestMethod]
        public void ValidateIntegrity_DinamicoValido_DevuelveNull()
        {
            var qrCode = new QrCode
            {
                Name = "Volante de bienvenida",
                QrType = QrCodeType.Dinamico,
                PageShortLinkId = 1,
                StaticContent = null,
                StaticContentType = null
            };

            Assert.IsNull( QrCatalogService.ValidateIntegrity( qrCode ) );
        }

        [TestMethod]
        public void ValidateIntegrity_DinamicoConNavegacionCargadaYSinId_DevuelveNull()
        {
            var qrCode = new QrCode
            {
                Name = "Volante de bienvenida",
                QrType = QrCodeType.Dinamico,
                PageShortLinkId = null,
                PageShortLink = new PageShortLink { Token = "abc1234", SiteId = 1 }
            };

            Assert.IsNull( QrCatalogService.ValidateIntegrity( qrCode ) );
        }

        [TestMethod]
        public void ValidateIntegrity_EstaticoValido_DevuelveNull()
        {
            var qrCode = new QrCode
            {
                Name = "Wifi del café",
                QrType = QrCodeType.Estatico,
                PageShortLinkId = null,
                StaticContentType = QrStaticContentType.Wifi,
                StaticContent = new QrWifiPayload { Ssid = "RedVidaReal", Password = "clave" }.ToJson()
            };

            Assert.IsNull( QrCatalogService.ValidateIntegrity( qrCode ) );
        }

        #endregion

        #region BuildPayload

        [TestMethod]
        public void BuildPayload_CodigoNull_DevuelveNull()
        {
            Assert.IsNull( QrCatalogService.BuildPayload( null ) );
        }

        [TestMethod]
        public void BuildPayload_CodigoEstatico_UsaElPayloadEstatico()
        {
            var qrCode = new QrCode
            {
                Name = "Sitio",
                QrType = QrCodeType.Estatico,
                StaticContentType = QrStaticContentType.Url,
                StaticContent = "vidareal.tv"
            };

            Assert.AreEqual( "https://vidareal.tv", QrCatalogService.BuildPayload( qrCode ) );
        }

        #endregion

        #region GetCurrentDestination / GetShortLinkUrl

        [TestMethod]
        public void GetCurrentDestination_CodigoEstatico_DevuelveNull()
        {
            var qrCode = new QrCode
            {
                Name = "Sitio",
                QrType = QrCodeType.Estatico,
                StaticContentType = QrStaticContentType.Url,
                StaticContent = "vidareal.tv"
            };

            Assert.IsNull( QrCatalogService.GetCurrentDestination( qrCode ) );
        }

        [TestMethod]
        public void GetShortLinkUrl_CodigoEstatico_DevuelveNull()
        {
            var qrCode = new QrCode
            {
                Name = "Sitio",
                QrType = QrCodeType.Estatico,
                StaticContentType = QrStaticContentType.Url,
                StaticContent = "vidareal.tv"
            };

            Assert.IsNull( QrCatalogService.GetShortLinkUrl( qrCode ) );
        }

        [TestMethod]
        public void GetShortLinkUrl_CodigoNull_DevuelveNull()
        {
            Assert.IsNull( QrCatalogService.GetShortLinkUrl( null ) );
        }

        #endregion

        #region Retire

        [TestMethod]
        public void Retire_CodigoActivo_LoDesactivaYEstampaLaFecha()
        {
            var qrCode = new QrCode
            {
                Name = "Volante viejo",
                QrType = QrCodeType.Estatico,
                StaticContentType = QrStaticContentType.Url,
                StaticContent = "vidareal.tv",
                IsActive = true
            };

            QrCatalogService.Retire( qrCode );

            Assert.IsFalse( qrCode.IsActive );
            Assert.IsNotNull( qrCode.RetiredDateTime );
        }

        [TestMethod]
        public void Retire_CodigoYaRetirado_NoPisaLaFechaOriginal()
        {
            var qrCode = new QrCode
            {
                Name = "Volante viejo",
                QrType = QrCodeType.Estatico,
                IsActive = false,
                RetiredDateTime = null
            };

            QrCatalogService.Retire( qrCode );

            Assert.IsFalse( qrCode.IsActive );
            Assert.IsNull( qrCode.RetiredDateTime );
        }

        #endregion

        #region NormalizeDestination

        /// <summary>
        /// El bug que motivó este método: un destino sin esquema se guardaba crudo y el redirector
        /// emitía `Location: /google.com`, que el navegador resuelve DENTRO del dominio corto.
        /// El QR se veía perfecto y llevaba a una página inexistente del propio sitio.
        /// </summary>
        [DataTestMethod]
        [DataRow( "google.com", "https://google.com" )]
        [DataRow( "  google.com  ", "https://google.com" )]
        [DataRow( "vidareal.tv/eventos", "https://vidareal.tv/eventos" )]
        [DataRow( "www.vidareal.tv", "https://www.vidareal.tv" )]
        public void NormalizeDestination_SinEsquema_AnteponeHttps( string entrada, string esperado )
        {
            Assert.AreEqual( esperado, QrCatalogService.NormalizeDestination( entrada ) );
        }

        [DataTestMethod]
        [DataRow( "https://google.com" )]
        [DataRow( "http://google.com" )]
        [DataRow( "HTTPS://GOOGLE.COM" )]
        [DataRow( "mailto:info@vidareal.tv" )]
        [DataRow( "tel:+50255551234" )]
        public void NormalizeDestination_ConEsquema_NoLoToca( string entrada )
        {
            Assert.AreEqual( entrada, QrCatalogService.NormalizeDestination( entrada ) );
        }

        /// <summary>
        /// Una ruta relativa dentro del sitio es un destino legítimo y deliberado, así que la
        /// barra inicial se respeta en vez de convertirla en https://.
        /// </summary>
        [DataTestMethod]
        [DataRow( "/eventos" )]
        [DataRow( "/page/123" )]
        public void NormalizeDestination_RutaRelativa_LaRespeta( string entrada )
        {
            Assert.AreEqual( entrada, QrCatalogService.NormalizeDestination( entrada ) );
        }

        [DataTestMethod]
        [DataRow( null )]
        [DataRow( "" )]
        [DataRow( "   " )]
        public void NormalizeDestination_Vacio_LoDevuelveIgual( string entrada )
        {
            Assert.AreEqual( entrada, QrCatalogService.NormalizeDestination( entrada ) );
        }

        #endregion

    }
}
