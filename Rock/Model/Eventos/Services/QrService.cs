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
using System.IO;

using QRCoder;

using Rock.Data;
using Rock.Web.Cache;

namespace Rock.Model
{
    /// <summary>
    /// Domain service that generates unique ticket codes, renders QR PNG images
    /// without any dependency on System.Drawing, and persists them as <see cref="BinaryFile"/> records.
    /// </summary>
    /// <remarks>
    /// Skeleton for Frente 3. Real generation logic for QR codes is implemented here;
    /// the higher-level orchestration (associating the resulting binary file with a ticket,
    /// applying branding, etc.) is filled in by later phases.
    /// </remarks>
    public class QrService
    {
        /// <summary>
        /// Guid of the dedicated, view-secured <see cref="BinaryFileType"/> for ticket QR images
        /// (created by migration 007). Storing QRs under this type — instead of DEFAULT — makes
        /// <c>GetFile.ashx?guid=…</c> return 403 to anonymous requests, so the QR (which is the entry
        /// credential) is not downloadable by GUID without auth. The QR is delivered as an email
        /// attachment and shown in-app as a base64 data URI (<see cref="GenerateQrDataUri"/>), never
        /// via a public URL.
        /// </summary>
        public const string TicketQrBinaryFileTypeGuid = "6e8e9f2a-3b1c-4d7e-9a05-700000000001";

        /// <summary>
        /// Generates a short, URL-safe unique code suitable for storing in <see cref="Ticket.UniqueCode"/>.
        /// </summary>
        /// <returns>A 12-character uppercase hex code derived from a new <see cref="Guid"/>.</returns>
        public string GenerateUniqueCode()
        {
            // First 12 hex chars of a GUID (48 bits of entropy). Uniqueness at the DB level is
            // additionally enforced by the UNIQUE index IX_Ticket_UniqueCode; callers should retry
            // on a unique-constraint violation.
            return Guid.NewGuid().ToString( "N" ).Substring( 0, 12 ).ToUpperInvariant();
        }

        /// <summary>
        /// Renders the supplied code as a QR PNG byte array using QRCoder's <see cref="PngByteQRCode"/>,
        /// which produces a PNG with no <c>System.Drawing</c> dependency (safe under .NET / IIS without GDI+).
        /// </summary>
        /// <param name="code">The code to encode in the QR image.</param>
        /// <param name="pixelsPerModule">The size, in pixels, of each QR module.</param>
        /// <returns>The PNG image as a byte array.</returns>
        public byte[] GenerateQrPng( string code, int pixelsPerModule = 10 )
        {
            if ( string.IsNullOrWhiteSpace( code ) )
            {
                throw new ArgumentException( "Code is required.", nameof( code ) );
            }

            using ( var generator = new QRCodeGenerator() )
            {
                var data = generator.CreateQrCode( code, QRCodeGenerator.ECCLevel.Q );
                var pngQrCode = new PngByteQRCode( data );
                return pngQrCode.GetGraphic( pixelsPerModule );
            }
        }

        /// <summary>
        /// Renders the code as a QR PNG and returns it as a <c>data:image/png;base64,…</c> URI, suitable
        /// for embedding directly in an <c>&lt;img src&gt;</c> (confirmation screen, My Tickets). Because the QR
        /// is a deterministic function of the code, this needs no stored file and no authenticated URL —
        /// it sidesteps the view-secured <see cref="BinaryFileType"/> entirely.
        /// </summary>
        public string GenerateQrDataUri( string code, int pixelsPerModule = 6 )
        {
            if ( string.IsNullOrWhiteSpace( code ) )
            {
                return null;
            }

            return "data:image/png;base64," + Convert.ToBase64String( GenerateQrPng( code, pixelsPerModule ) );
        }

        /// <summary>
        /// Persists a QR PNG byte array as a non-secured <see cref="BinaryFile"/> and returns its identifier.
        /// </summary>
        /// <param name="pngBytes">The PNG bytes to store.</param>
        /// <param name="rockContext">The <see cref="RockContext"/> to use.</param>
        /// <param name="fileName">Optional file name; a default is generated when omitted.</param>
        /// <returns>The identifier of the saved <see cref="BinaryFile"/>.</returns>
        public int SaveQrToBinaryFile( byte[] pngBytes, RockContext rockContext, string fileName = null )
        {
            if ( pngBytes == null || pngBytes.Length == 0 )
            {
                throw new ArgumentException( "PNG bytes are required.", nameof( pngBytes ) );
            }

            if ( rockContext == null )
            {
                throw new ArgumentNullException( nameof( rockContext ) );
            }

            // Dedicated, view-secured binary file type for ticket QRs (RequiresViewSecurity = true):
            // GetFile.ashx returns 403 to anonymous requests, closing the "QR downloadable by GUID
            // without auth" leak. The QR reaches the buyer as an email attachment / in-app base64,
            // never via a public URL. Fallback to DEFAULT if migration 007 hasn't run yet.
            var binaryFileType = BinaryFileTypeCache.Get( TicketQrBinaryFileTypeGuid.AsGuid() )
                ?? BinaryFileTypeCache.Get( Rock.SystemGuid.BinaryFiletype.DEFAULT.AsGuid() );

            var binaryFile = new BinaryFile
            {
                IsTemporary = false,
                BinaryFileTypeId = binaryFileType?.Id,
                MimeType = "image/png",
                FileName = string.IsNullOrWhiteSpace( fileName ) ? $"ticket-qr-{Guid.NewGuid():N}.png" : fileName,
                FileSize = pngBytes.Length,
                ContentStream = new MemoryStream( pngBytes )
            };

            var binaryFileService = new BinaryFileService( rockContext );
            binaryFileService.Add( binaryFile );
            rockContext.SaveChanges();

            return binaryFile.Id;
        }

        /// <summary>
        /// Ensures the supplied ticket has a non-empty <see cref="Ticket.UniqueCode"/> and a generated
        /// QR PNG persisted in <see cref="Ticket.QrImageBinaryFileId"/>. Missing pieces are created and
        /// the ticket entity is updated in place. The caller is responsible for calling
        /// <see cref="RockContext.SaveChanges()"/>.
        /// </summary>
        /// <param name="ticket">The ticket to back-fill. Must already be tracked by <paramref name="rockContext"/>.</param>
        /// <param name="rockContext">The <see cref="RockContext"/> to use.</param>
        /// <returns><c>true</c> if the ticket was modified (code and/or QR generated); otherwise <c>false</c>.</returns>
        public bool EnsureTicketCodeAndQr( Ticket ticket, RockContext rockContext )
        {
            if ( ticket == null )
            {
                throw new ArgumentNullException( nameof( ticket ) );
            }

            if ( rockContext == null )
            {
                throw new ArgumentNullException( nameof( rockContext ) );
            }

            var modified = false;

            if ( string.IsNullOrWhiteSpace( ticket.UniqueCode ) )
            {
                ticket.UniqueCode = GenerateUniqueCode();
                modified = true;
            }

            if ( !ticket.QrImageBinaryFileId.HasValue )
            {
                var pngBytes = GenerateQrPng( ticket.UniqueCode );
                ticket.QrImageBinaryFileId = SaveQrToBinaryFile( pngBytes, rockContext, $"ticket-qr-{ticket.UniqueCode}.png" );
                modified = true;
            }

            return modified;
        }
    }
}
