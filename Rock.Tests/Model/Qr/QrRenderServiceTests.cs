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
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Xml.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Rock.Enums.Qr;
using Rock.Model;

namespace Rock.Tests.Model.Qr
{
    /// <summary>
    /// Tests for <see cref="QrRenderService"/>: el guardarraíl de diseño, la cultura al renderizar
    /// y los topes que evitan que un valor absurdo tumbe el worker.
    /// </summary>
    [TestClass]
    public class QrRenderServiceTests
    {
        private const string TestPayload = "https://vidareal.tv/qr-prueba";

        private static QrDesign BlackOnWhite( int logoSizePercent = 0 )
        {
            return new QrDesign
            {
                ForegroundColor = "#000000",
                BackgroundColor = "#FFFFFF",
                LogoSizePercent = logoSizePercent
            };
        }

        #region ValidateDesign

        [TestMethod]
        public void ValidateDesign_NegroSobreBlanco_DevuelveNull()
        {
            Assert.IsNull( QrRenderService.ValidateDesign( BlackOnWhite() ) );
        }

        [TestMethod]
        public void ValidateDesign_ColorInstitucionalOscuroSobreBlanco_DevuelveNull()
        {
            var design = new QrDesign
            {
                ForegroundColor = "#1A3A6B",
                BackgroundColor = "#FFFFFF",
                LogoSizePercent = 15
            };

            Assert.IsNull( QrRenderService.ValidateDesign( design ) );
        }

        [TestMethod]
        public void ValidateDesign_ColoresEnBlanco_UsaLosPorDefectoYEsValido()
        {
            var design = new QrDesign { ForegroundColor = null, BackgroundColor = "   " };

            Assert.IsNull( QrRenderService.ValidateDesign( design ) );
        }

        [DataTestMethod]
        // #999999 sobre blanco da 2.85:1, y #CCCCCC da 1.61:1. Ambos por debajo del mínimo de 3:1.
        [DataRow( "#999999", "#FFFFFF" )]
        [DataRow( "#CCCCCC", "#FFFFFF" )]
        public void ValidateDesign_ContrastePorDebajoDelMinimo_DevuelveMensaje( string foreground, string background )
        {
            var design = new QrDesign { ForegroundColor = foreground, BackgroundColor = background };

            var result = QrRenderService.ValidateDesign( design );

            Assert.IsNotNull( result );
            Assert.IsTrue( result.Contains( "contraste" ) );
        }

        [DataTestMethod]
        // Contraste de sobra (21:1 y 11.3:1) pero el módulo es MÁS CLARO que el fondo: invertido.
        [DataRow( "#FFFFFF", "#000000" )]
        [DataRow( "#FFFFFF", "#1A3A6B" )]
        public void ValidateDesign_CodigoInvertidoConContrasteSuficiente_DevuelveMensaje( string foreground, string background )
        {
            var design = new QrDesign { ForegroundColor = foreground, BackgroundColor = background };

            // El contraste alcanza: lo que se rechaza es la inversión, no el contraste.
            Assert.IsTrue( QrRenderService.GetContrastRatio( foreground, background ) >= QrDesign.MinContrastRatio );

            var result = QrRenderService.ValidateDesign( design );

            Assert.IsNotNull( result );
            Assert.IsTrue( result.Contains( "más oscuro" ) );
        }

        /// <summary>
        /// Un logo por encima del <see cref="QrDesign.MaxLogoSizePercent"/> tiene que devolver un
        /// mensaje: pasado ese punto ni el nivel H de corrección de error alcanza.
        /// </summary>
        /// <remarks>
        /// <strong>BUG — este test falla hoy.</strong> <c>ValidateDesign</c> arranca con
        /// <c>design = design.WithDefaults()</c>, y <c>WithDefaults</c> ya acota
        /// <c>LogoSizePercent</c> a <see cref="QrDesign.MaxLogoSizePercent"/>. Para cuando se
        /// evalúa <c>design.LogoSizePercent &gt; MaxLogoSizePercent</c> el valor nunca puede
        /// superarlo, así que esa rama es código muerto y un diseño con 50 % de logo se reporta
        /// como válido. No es un fallo de seguridad —<c>RenderSvg</c> también llama a
        /// <c>WithDefaults</c>, así que dibuja 20 %— pero el usuario pide 50 %, no recibe ningún
        /// aviso y obtiene otra cosa. El arreglo es medir el valor <em>original</em> antes de
        /// acotarlo. No se acomodó el test para que pase.
        /// </remarks>
        [TestMethod]
        public void ValidateDesign_LogoPorEncimaDelMaximo_DevuelveMensaje()
        {
            var design = new QrDesign
            {
                ForegroundColor = "#000000",
                BackgroundColor = "#FFFFFF",
                LogoSizePercent = 50
            };

            var result = QrRenderService.ValidateDesign( design );

            Assert.IsNotNull(
                result,
                "Un logo del 50 % debería rechazarse; WithDefaults lo acota antes de la comprobación y la vuelve inalcanzable." );
        }

        [TestMethod]
        public void ValidateDesign_LogoEnElMaximo_DevuelveNull()
        {
            Assert.IsNull( QrRenderService.ValidateDesign( BlackOnWhite( QrDesign.MaxLogoSizePercent ) ) );
        }

        [DataTestMethod]
        [DataRow( "#GG0000", "#FFFFFF" )]
        [DataRow( "#12345", "#FFFFFF" )]
        [DataRow( "azul", "#FFFFFF" )]
        [DataRow( "#0000001", "#FFFFFF" )]
        public void ValidateDesign_ColorDeModuloInvalido_DevuelveMensaje( string foreground, string background )
        {
            var design = new QrDesign { ForegroundColor = foreground, BackgroundColor = background };

            var result = QrRenderService.ValidateDesign( design );

            Assert.IsNotNull( result );
            Assert.IsTrue( result.Contains( "color del módulo" ) );
        }

        [DataTestMethod]
        [DataRow( "#000000", "#ZZZZZZ" )]
        [DataRow( "#000000", "blanco" )]
        public void ValidateDesign_ColorDeFondoInvalido_DevuelveMensaje( string foreground, string background )
        {
            var design = new QrDesign { ForegroundColor = foreground, BackgroundColor = background };

            var result = QrRenderService.ValidateDesign( design );

            Assert.IsNotNull( result );
            Assert.IsTrue( result.Contains( "color de fondo" ) );
        }

        [TestMethod]
        public void ValidateDesign_HexDeTresDigitos_EsValido()
        {
            var design = new QrDesign { ForegroundColor = "#000", BackgroundColor = "#FFF" };

            Assert.IsNull( QrRenderService.ValidateDesign( design ) );
        }

        [TestMethod]
        public void ValidateDesign_DisenoNull_DevuelveMensaje()
        {
            Assert.IsNotNull( QrRenderService.ValidateDesign( null ) );
        }

        [TestMethod]
        public void ValidateDesign_MismoColorEnModuloYFondo_DevuelveMensaje()
        {
            var design = new QrDesign { ForegroundColor = "#123456", BackgroundColor = "#123456" };

            Assert.IsNotNull( QrRenderService.ValidateDesign( design ) );
        }

        #endregion

        #region GetContrastRatio

        [TestMethod]
        public void GetContrastRatio_NegroYBlanco_DevuelveVeintiuno()
        {
            Assert.AreEqual( 21.0, QrRenderService.GetContrastRatio( "#000000", "#FFFFFF" ), 0.001 );
        }

        [TestMethod]
        public void GetContrastRatio_BlancoYNegro_EsSimetrico()
        {
            Assert.AreEqual( 21.0, QrRenderService.GetContrastRatio( "#FFFFFF", "#000000" ), 0.001 );
        }

        [DataTestMethod]
        [DataRow( "#000000" )]
        [DataRow( "#FFFFFF" )]
        [DataRow( "#123456" )]
        public void GetContrastRatio_MismoColor_DevuelveUno( string hex )
        {
            Assert.AreEqual( 1.0, QrRenderService.GetContrastRatio( hex, hex ), 0.001 );
        }

        [DataTestMethod]
        [DataRow( "#GGGGGG", "#FFFFFF" )]
        [DataRow( "#000000", "no-es-color" )]
        [DataRow( null, "#FFFFFF" )]
        public void GetContrastRatio_ColorInvalido_DevuelveCero( string hexA, string hexB )
        {
            Assert.AreEqual( 0.0, QrRenderService.GetContrastRatio( hexA, hexB ), 0.001 );
        }

        [TestMethod]
        public void GetContrastRatio_HexConYSinNumeral_DaElMismoResultado()
        {
            var conNumeral = QrRenderService.GetContrastRatio( "#000000", "#FFFFFF" );
            var sinNumeral = QrRenderService.GetContrastRatio( "000000", "FFFFFF" );

            Assert.AreEqual( conNumeral, sinNumeral, 0.001 );
        }

        #endregion

        #region GetEccLevel

        [TestMethod]
        public void GetEccLevel_SinLogo_DevuelveQ()
        {
            Assert.AreEqual( QrEccLevel.Q, QrRenderService.GetEccLevel( BlackOnWhite( 0 ) ) );
        }

        [TestMethod]
        public void GetEccLevel_DisenoNull_DevuelveQ()
        {
            Assert.AreEqual( QrEccLevel.Q, QrRenderService.GetEccLevel( null ) );
        }

        [DataTestMethod]
        [DataRow( 1 )]
        [DataRow( 10 )]
        [DataRow( 20 )]
        public void GetEccLevel_ConLogo_DevuelveH( int logoSizePercent )
        {
            Assert.AreEqual( QrEccLevel.H, QrRenderService.GetEccLevel( BlackOnWhite( logoSizePercent ) ) );
        }

        #endregion

        #region RenderSvg y cultura

        /// <summary>
        /// El SVG lleva coordenadas con decimales (el parche y la imagen del logo caen en medios
        /// módulos). Bajo es-GT el separador decimal es la coma, y una coma dentro de un atributo
        /// numérico de SVG lo rompe sin avisar: el archivo abre, el visor ignora el atributo y el
        /// logo aparece en la esquina o no aparece. Por eso el servicio formatea con
        /// InvariantCulture, y esto lo convierte en garantía en vez de convención.
        /// </summary>
        [TestMethod]
        public void RenderSvg_BajoCulturaEsGt_NoEmiteComaDecimalEnAtributosNumericos()
        {
            var originalCulture = Thread.CurrentThread.CurrentCulture;
            var originalUiCulture = Thread.CurrentThread.CurrentUICulture;

            try
            {
                var esGt = CultureInfo.GetCultureInfo( "es-GT" );
                Thread.CurrentThread.CurrentCulture = esGt;
                Thread.CurrentThread.CurrentUICulture = esGt;

                var svg = QrRenderService.RenderSvg(
                    TestPayload,
                    BlackOnWhite( 20 ),
                    10,
                    7.55,
                    new byte[] { 1, 2, 3, 4, 5 },
                    "image/png" );

                // 1. Tiene que ser XML bien formado: si una coma se coló, esto no lo detecta, pero
                //    cualquier otro destrozo del marcado sí.
                var doc = XDocument.Parse( svg );

                // 2. El logo tiene que estar.
                var images = doc.Root
                    .DescendantsAndSelf()
                    .Where( e => e.Name.LocalName == "image" )
                    .ToList();

                Assert.AreEqual( 1, images.Count, "El SVG debería traer exactamente un <image> con el logo." );
                Assert.IsTrue( images[0].Attributes().Any( a => a.Value.StartsWith( "data:image/png;base64," ) ) );

                // 3. Ningún atributo numérico con coma decimal.
                var numericNames = new[] { "x", "y", "width", "height" };
                var numericValues = doc.Root
                    .DescendantsAndSelf()
                    .SelectMany( e => e.Attributes() )
                    .Where( a => numericNames.Contains( a.Name.LocalName ) )
                    .Select( a => a.Value )
                    .ToList();

                Assert.IsTrue( numericValues.Count > 0, "No se encontraron atributos numéricos en el SVG." );

                // Si no hubiera ni un decimal, el test no estaría probando nada.
                Assert.IsTrue(
                    numericValues.Any( v => v.Contains( "." ) ),
                    "El SVG no trae ningún valor decimal, así que la prueba de cultura sería vacua." );

                foreach ( var value in numericValues )
                {
                    Assert.IsFalse(
                        value.Contains( "," ),
                        $"Atributo numérico con coma decimal: '{value}'. Bajo es-GT esto rompe el SVG." );
                }
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = originalCulture;
                Thread.CurrentThread.CurrentUICulture = originalUiCulture;
            }
        }

        [TestMethod]
        public void RenderSvg_SinLogo_NoInyectaImagen()
        {
            var svg = QrRenderService.RenderSvg( TestPayload, BlackOnWhite( 0 ), 10, 5.0 );

            Assert.IsFalse( svg.Contains( "<image" ) );
            XDocument.Parse( svg );
        }

        [TestMethod]
        public void RenderSvg_ConLogoPeroSinBytes_NoInyectaImagen()
        {
            var svg = QrRenderService.RenderSvg( TestPayload, BlackOnWhite( 20 ), 10, 5.0, null, "image/png" );

            Assert.IsFalse( svg.Contains( "<image" ) );
        }

        [TestMethod]
        public void RenderSvg_SinTamanoDeImpresion_NoEstampaMilimetros()
        {
            var svg = QrRenderService.RenderSvg( TestPayload, BlackOnWhite(), 10, 0 );

            Assert.IsFalse( svg.Contains( "mm\"" ) );
        }

        [TestMethod]
        public void RenderSvg_PayloadVacio_LanzaArgumentException()
        {
            try
            {
                QrRenderService.RenderSvg( "   ", BlackOnWhite() );
                Assert.Fail( "Se esperaba ArgumentException para un payload vacío." );
            }
            catch ( ArgumentException )
            {
                // Esperado.
            }
        }

        #endregion

        #region Topes: MaxPrintSideCm y MaxPixelsPerModule

        [TestMethod]
        public void RenderSvg_LadoDeImpresionDisparatado_SeAcotaAlMaximo()
        {
            // 5000 cm es casi siempre milímetros escritos como centímetros. Se baja al tope en vez
            // de estamparse tal cual y romper la impresión en silencio.
            var svg = QrRenderService.RenderSvg( TestPayload, BlackOnWhite(), 10, 5000 );

            var expectedMm = ( QrRenderService.MaxPrintSideCm * 10.0 ).ToString( "0.##", CultureInfo.InvariantCulture );

            Assert.IsTrue( svg.Contains( $"width=\"{expectedMm}mm\"" ), $"No se encontró width=\"{expectedMm}mm\"." );
            Assert.IsTrue( svg.Contains( $"height=\"{expectedMm}mm\"" ) );
        }

        [TestMethod]
        public void RenderSvg_LadoDeImpresionPorDebajoDelMinimo_SeSubeAlMinimo()
        {
            var svg = QrRenderService.RenderSvg( TestPayload, BlackOnWhite(), 10, 0.5 );

            var expectedMm = ( QrRenderService.MinPrintSideCm * 10.0 ).ToString( "0.##", CultureInfo.InvariantCulture );

            Assert.IsTrue( svg.Contains( $"width=\"{expectedMm}mm\"" ), $"No se encontró width=\"{expectedMm}mm\"." );
        }

        [TestMethod]
        public void RenderPng_PixelesPorModuloDisparatado_SeAcotaAlMaximo()
        {
            var acotado = QrRenderService.RenderPng( TestPayload, BlackOnWhite(), int.MaxValue );
            var enElTope = QrRenderService.RenderPng( TestPayload, BlackOnWhite(), QrRenderService.MaxPixelsPerModule );

            CollectionAssert.AreEqual( enElTope, acotado, "Un valor absurdo tiene que producir exactamente el PNG del tope." );

            // Cota de cordura: el lado es (módulos × px/módulo) y la memoria crece al cuadrado.
            // Un PNG del tope para este payload ronda los 9 KB; 1 MB deja margen de sobra y detecta
            // que el tope dejó de aplicarse.
            Assert.IsTrue( acotado.Length < 1024 * 1024, $"El PNG acotado pesa {acotado.Length} bytes." );
        }

        [DataTestMethod]
        [DataRow( 0 )]
        [DataRow( -1 )]
        [DataRow( int.MinValue )]
        public void RenderPng_PixelesPorModuloNoPositivo_SeSubeAUno( int pixelsPerModule )
        {
            var acotado = QrRenderService.RenderPng( TestPayload, BlackOnWhite(), pixelsPerModule );
            var enUno = QrRenderService.RenderPng( TestPayload, BlackOnWhite(), 1 );

            CollectionAssert.AreEqual( enUno, acotado );
        }

        [TestMethod]
        public void RenderPng_PayloadVacio_LanzaArgumentException()
        {
            try
            {
                QrRenderService.RenderPng( null, BlackOnWhite() );
                Assert.Fail( "Se esperaba ArgumentException para un payload vacío." );
            }
            catch ( ArgumentException )
            {
                // Esperado.
            }
        }

        [TestMethod]
        public void RenderPng_DisenoNull_UsaLosColoresPorDefecto()
        {
            var conNull = QrRenderService.RenderPng( TestPayload, null, 4 );
            var conDefecto = QrRenderService.RenderPng( TestPayload, BlackOnWhite(), 4 );

            CollectionAssert.AreEqual( conDefecto, conNull );
        }

        #endregion

        #region RenderPngDataUri

        [TestMethod]
        public void RenderPngDataUri_PayloadValido_DevuelveDataUriDePng()
        {
            var dataUri = QrRenderService.RenderPngDataUri( TestPayload, BlackOnWhite(), 4 );

            Assert.IsNotNull( dataUri );
            Assert.IsTrue( dataUri.StartsWith( "data:image/png;base64," ) );

            var base64 = dataUri.Substring( "data:image/png;base64,".Length );

            CollectionAssert.AreEqual(
                QrRenderService.RenderPng( TestPayload, BlackOnWhite(), 4 ),
                Convert.FromBase64String( base64 ) );
        }

        [DataTestMethod]
        [DataRow( null )]
        [DataRow( "" )]
        [DataRow( "   " )]
        public void RenderPngDataUri_PayloadVacio_DevuelveNull( string payload )
        {
            Assert.IsNull( QrRenderService.RenderPngDataUri( payload, BlackOnWhite() ) );
        }

        #endregion

        #region GetRecommendedSideCm

        [DataTestMethod]
        [DataRow( 300.0, 30.0 )]
        [DataRow( 100.0, 10.0 )]
        [DataRow( 1000.0, 100.0 )]
        public void GetRecommendedSideCm_DistanciaNormal_EsLaDecimaParte( double distanceCm, double expected )
        {
            Assert.AreEqual( expected, QrRenderService.GetRecommendedSideCm( distanceCm ), 0.001 );
        }

        [DataTestMethod]
        [DataRow( 0.0 )]
        [DataRow( 5.0 )]
        [DataRow( 19.9 )]
        public void GetRecommendedSideCm_DistanciaCorta_NuncaBajaDelMinimo( double distanceCm )
        {
            Assert.AreEqual( QrRenderService.MinPrintSideCm, QrRenderService.GetRecommendedSideCm( distanceCm ), 0.001 );
        }

        #endregion
    }
}
