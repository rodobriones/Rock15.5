// Plugin.OdooEventSale
// Workflow action que registra una venta de evento (Registration pagada) en
// Odoo via POST /api/event/sell (addon custom_event_sale_api): crea cliente,
// orden, factura FEL certificada en SAT y pago.
//
// Reintentos: el action retorna false sin marcarse completa cuando el error
// es transitorio; el WorkflowType debe ser PERSISTIDO para que el job
// ProcessWorkflows lo reprocese cada ProcessingIntervalSeconds. La referencia
// de idempotencia es el Guid de la FinancialTransaction, estable entre
// reintentos, por lo que Odoo nunca duplica la factura.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using Newtonsoft.Json.Linq;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;
using Rock.Workflow;

namespace Plugin.OdooEventSale
{
    [ActionCategory( "Vida Real" )]
    [Description( "Registra la venta de un evento pagado en Odoo (factura FEL via /api/event/sell)." )]
    [Export( typeof( ActionComponent ) )]
    [ExportMetadata( "ComponentName", "Odoo: Registrar Venta de Evento" )]

    [TextField( "Odoo Base URL",
        Description = "URL base de Odoo, sin slash final. Ej: https://odoo.vidareal.tv",
        IsRequired = true, Order = 0, Key = AttributeKey.OdooBaseUrl )]
    [EncryptedTextField( "Odoo API Key",
        Description = "Clave del header X-API-KEY (Ajustes → Event Sale API en Odoo).",
        IsRequired = true, IsPassword = true, Order = 1, Key = AttributeKey.OdooApiKey )]
    [WorkflowAttribute( "Nit Attribute",
        Description = "Atributo del workflow con el NIT capturado y validado en la pantalla de pago (lo pre-pobla el bloque de inscripción). Vacío → se factura como CF.",
        IsRequired = false, Order = 2, Key = AttributeKey.NitAttribute )]
    [WorkflowAttribute( "WantsInvoice Attribute",
        Description = "Atributo Boolean del workflow ('¿Desea factura?', lo pre-pobla el bloque de inscripción). Apagado o sin mapear → se factura como CF.",
        IsRequired = false, Order = 16, Key = AttributeKey.WantsInvoiceAttribute )]
    [IntegerField( "Max Intentos",
        Description = "Reintentos máximos ante errores transitorios antes de marcar ErrorPermanente.",
        IsRequired = false, DefaultIntegerValue = 5, Order = 3, Key = AttributeKey.MaxAttempts )]
    [IntegerField( "Timeout (segundos)",
        Description = "Timeout HTTP. La certificación FEL ocurre dentro del request: usar 60-90s.",
        IsRequired = false, DefaultIntegerValue = 90, Order = 4, Key = AttributeKey.TimeoutSeconds )]

    [WorkflowAttribute( "OdooStatus Attribute",
        Description = "Recibe el resultado: Exito | PendienteFEL | PagoManual | SinPago | Reintentando | ErrorPermanente.",
        IsRequired = true, Order = 5, Key = AttributeKey.OdooStatusAttribute )]
    [WorkflowAttribute( "AttemptCount Attribute",
        Description = "Contador de intentos (atributo Text o Integer del workflow).",
        IsRequired = true, Order = 6, Key = AttributeKey.AttemptCountAttribute )]
    [WorkflowAttribute( "FelUuid Attribute", IsRequired = false, Order = 7, Key = AttributeKey.FelUuidAttribute )]
    [WorkflowAttribute( "FelSerie Attribute", IsRequired = false, Order = 8, Key = AttributeKey.FelSerieAttribute )]
    [WorkflowAttribute( "FelNumero Attribute", IsRequired = false, Order = 9, Key = AttributeKey.FelNumeroAttribute )]
    [WorkflowAttribute( "OrderName Attribute", IsRequired = false, Order = 10, Key = AttributeKey.OrderNameAttribute )]
    [WorkflowAttribute( "InvoiceName Attribute", IsRequired = false, Order = 11, Key = AttributeKey.InvoiceNameAttribute )]
    [WorkflowAttribute( "OdooError Attribute", IsRequired = false, Order = 12, Key = AttributeKey.OdooErrorAttribute )]
    [WorkflowAttribute( "RegistrationId Attribute",
        Description = "Opcional. Fallback para resolver la Registration cuando el workflow se lanza manualmente.",
        IsRequired = false, Order = 13, Key = AttributeKey.RegistrationIdAttribute )]
    public class PostEventSaleToOdoo : ActionComponent
    {
        private static class AttributeKey
        {
            public const string OdooBaseUrl = "OdooBaseUrl";
            public const string OdooApiKey = "OdooApiKey";
            public const string NitAttribute = "NitAttribute";
            public const string WantsInvoiceAttribute = "WantsInvoiceAttribute";
            public const string MaxAttempts = "MaxAttempts";
            public const string TimeoutSeconds = "TimeoutSeconds";
            public const string OdooStatusAttribute = "OdooStatusAttribute";
            public const string AttemptCountAttribute = "AttemptCountAttribute";
            public const string FelUuidAttribute = "FelUuidAttribute";
            public const string FelSerieAttribute = "FelSerieAttribute";
            public const string FelNumeroAttribute = "FelNumeroAttribute";
            public const string OrderNameAttribute = "OrderNameAttribute";
            public const string InvoiceNameAttribute = "InvoiceNameAttribute";
            public const string OdooErrorAttribute = "OdooErrorAttribute";
            public const string RegistrationIdAttribute = "RegistrationIdAttribute";
        }

        private static class Status
        {
            public const string Exito = "Exito";
            public const string PendienteFEL = "PendienteFEL";
            public const string PagoManual = "PagoManual";
            public const string SinPago = "SinPago";
            public const string Reintentando = "Reintentando";
            public const string ErrorPermanente = "ErrorPermanente";
        }

        // Timeout por request via CancellationToken; el del cliente queda infinito.
        private static readonly HttpClient _http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };

        public override bool Execute( RockContext rockContext, WorkflowAction action, object entity, out List<string> errorMessages )
        {
            errorMessages = new List<string>();

            try
            {
                return ExecuteInternal( rockContext, action, entity );
            }
            catch ( Exception ex )
            {
                // Nunca lanzar. Las excepciones aquí (BD, atributos, lazy load)
                // ocurren antes o alrededor del POST, donde el reintento
                // idempotente es seguro; respeta el contador de intentos.
                int attempts = GetWorkflowAttribute( action, AttributeKey.AttemptCountAttribute ).AsInteger();
                int maxAttempts = GetAttributeValue( action, AttributeKey.MaxAttempts ).AsIntegerOrNull() ?? 5;
                return Retry( action, attempts, maxAttempts, "Excepción no controlada: " + ex.Message );
            }
        }

        private bool ExecuteInternal( RockContext rockContext, WorkflowAction action, object entity )
        {
            int attempts = GetWorkflowAttribute( action, AttributeKey.AttemptCountAttribute ).AsInteger() + 1;
            SetWorkflowAttributeValue( action, AttributeKey.AttemptCountAttribute, attempts.ToString() );
            int maxAttempts = GetAttributeValue( action, AttributeKey.MaxAttempts ).AsIntegerOrNull() ?? 5;

            // ----- Resolver la Registration -----
            // 1er procesamiento: llega como entity. Reintentos del job
            // ProcessWorkflows: entity es null y se usa Workflow.EntityId
            // (Rock lo setea al persistir). Fallback: atributo RegistrationId.
            int? registrationId = ( entity as Registration )?.Id;
            if ( !registrationId.HasValue )
            {
                var workflow = action.Activity?.Workflow;
                int registrationEntityTypeId = EntityTypeCache.Get( typeof( Registration ) ).Id;
                if ( workflow?.EntityId != null && workflow.EntityTypeId == registrationEntityTypeId )
                {
                    registrationId = workflow.EntityId;
                }
            }
            if ( !registrationId.HasValue )
            {
                registrationId = GetWorkflowAttribute( action, AttributeKey.RegistrationIdAttribute ).AsIntegerOrNull();
            }

            var registrationService = new RegistrationService( rockContext );
            var registration = registrationId.HasValue
                ? registrationService.Queryable( "RegistrationInstance,Registrants.Fees,PersonAlias.Person" )
                    .FirstOrDefault( r => r.Id == registrationId.Value )
                : null;

            if ( registration == null )
            {
                action.AddLogEntry( "Odoo: no se pudo resolver la Registration (entity/EntityId/atributo).", true );
                SetStatus( action, Status.ErrorPermanente );
                SetWorkflowAttributeValue( action, AttributeKey.OdooErrorAttribute, "Registration no encontrada." );
                return true;
            }

            // ----- Guard: solo ventas pagadas -----
            var paymentDetails = registrationService.GetPayments( registration.Id )
                .Where( d => d.Transaction != null )
                .ToList();
            decimal charged = paymentDetails.Sum( d => d.Amount );
            decimal feeCoverage = paymentDetails.Sum( d => d.FeeCoverageAmount ?? 0m );
            decimal paidNet = charged - feeCoverage;

            // charged (no paidNet): con descuento del 100% + recargo de cuotas
            // hay un cobro real a la tarjeta aunque paidNet sea 0.
            if ( !paymentDetails.Any() || charged <= 0m )
            {
                // En el primer intento puede ser una carrera con el commit del
                // pago: esperar un ciclo antes de declarar SinPago.
                if ( attempts <= 1 )
                {
                    action.AddLogEntry( "Odoo: aún sin pagos visibles; se reintenta en el próximo ciclo." );
                    SetStatus( action, Status.Reintentando );
                    return false;
                }

                action.AddLogEntry( "Odoo: inscripción sin pago (pay later); no se envía a Odoo." );
                SetStatus( action, Status.SinPago );
                return true;
            }

            // ----- Referencia idempotente: Guid de la primera transacción -----
            var firstTransaction = paymentDetails
                .OrderBy( d => d.Transaction.TransactionDateTime ?? DateTime.MaxValue )
                .ThenBy( d => d.TransactionId )
                .First().Transaction;
            string reference = firstTransaction.Guid.ToString();

            // ----- Líneas con regla de cuadre -----
            // Los registrantes en waitlist no tienen costo: quedan fuera del
            // conteo y de la heurística de precio uniforme.
            var activeRegistrants = ( registration.Registrants ?? Enumerable.Empty<RegistrationRegistrant>() )
                .Where( r => !r.OnWaitList )
                .OrderBy( r => r.Id )
                .ToList();
            int registrantCount = activeRegistrants.Count;
            decimal totalCost = Round2( registration.TotalCost );
            decimal discountedCost = Round2( registration.DiscountedCost );
            decimal discount = Round2( totalCost - discountedCost );
            charged = Round2( charged );
            feeCoverage = Round2( feeCoverage );
            paidNet = Round2( paidNet );

            string instanceName = registration.RegistrationInstance?.Name ?? "Evento";

            // Descripción comercial del evento (atributo de instancia "DescripcionEvento",
            // p.ej. "incluye coffee break y libro"). Se concatena al name de la línea en
            // Odoo → viaja verbatim al <Descripcion> del DTE FEL, por eso se sanitiza
            // (sin HTML ni control chars; SanitizeSatText elimina los \n) y se acota a 500.
            string eventDescription = string.Empty;
            if ( registration.RegistrationInstance != null )
            {
                registration.RegistrationInstance.LoadAttributes( rockContext );
                eventDescription = SanitizeSatText( registration.RegistrationInstance.GetAttributeValue( "DescripcionEvento" ), 500 );
            }

            decimal eventPrice = 0m;
            decimal eventQty = 1m;
            bool includeDiscount = false;
            bool includeSurcharge = false;
            string eventName = instanceName;

            bool fullPayment = Math.Abs( paidNet - discountedCost ) <= 0.01m && registrantCount > 0;
            if ( fullPayment )
            {
                decimal perRegistrant = Round2( totalCost / registrantCount );
                bool uniformCost = activeRegistrants.All( r => Math.Abs( Round2( r.TotalCost ) - perRegistrant ) <= 0.01m )
                    && Math.Abs( Round2( perRegistrant * registrantCount ) - totalCost ) <= 0.01m;

                if ( uniformCost && registrantCount > 1 )
                {
                    eventPrice = perRegistrant;
                    eventQty = registrantCount;
                }
                else
                {
                    eventPrice = totalCost;
                    eventQty = 1;
                }

                includeDiscount = discount > 0.005m;
                includeSurcharge = feeCoverage > 0.005m;

                decimal linesTotal = Round2( eventPrice * eventQty )
                    + ( includeDiscount ? -discount : 0m )
                    + ( includeSurcharge ? feeCoverage : 0m );
                // Odoo exige price > 0 en la línea principal y total > 0.
                if ( eventPrice <= 0m || linesTotal <= 0m || Math.Abs( linesTotal - charged ) > 0.01m )
                {
                    fullPayment = false; // no cuadra: caer al fallback
                }
            }

            if ( !fullPayment )
            {
                // Pago parcial o descuadre: una sola línea por lo cobrado real.
                eventName = "Pago de inscripción - " + instanceName;
                eventPrice = charged;
                eventQty = 1;
                includeDiscount = false;
                includeSurcharge = false;
            }

            // ----- NIT capturado en la pantalla de pago (workflow attributes) -----
            // El bloque de inscripción (RegistrationEntry) valida el NIT contra SAT en
            // la pantalla de pago y pre-pobla 'WantsInvoice' y 'Nit' al lanzar el workflow.
            // Apagado o sin mapear → CF (no se factura con NIT). La action re-valida abajo.
            bool wantsInvoice = GetWorkflowAttribute( action, AttributeKey.WantsInvoiceAttribute ).AsBoolean();
            string nit = wantsInvoice ? GetWorkflowAttribute( action, AttributeKey.NitAttribute ) : string.Empty;

            // FEL exige el NIT sin guiones/espacios: solo alfanumérico, en mayúsculas.
            nit = new string( ( nit ?? string.Empty ).Where( char.IsLetterOrDigit ).ToArray() ).ToUpperInvariant();
            if ( nit.IsNullOrWhiteSpace() || nit.Length > 32 )
            {
                nit = "CF";
            }

            if ( !wantsInvoice )
            {
                action.AddLogEntry( "Odoo: no se solicitó factura; se factura como CF." );
                nit = "CF";
            }

            // Validar el NIT contra la API del certificador (retornarDatosCliente).
            // Si SAT no lo reconoce se factura como CF; si la API no responde se
            // envía normalizado sin validar (el certificador lo valida en el FEL).
            // Nombre y dirección de SAT son la data real: viajan a Odoo para
            // mantener el partner actualizado (el DTE FEL se arma con ellos).
            string satName = null;
            string satAddress = null;
            if ( nit != "CF" )
            {
                var lookup = LookupNit( nit );
                if ( lookup.Status == NitLookupStatus.Valid )
                {
                    satName = lookup.Name;
                    satAddress = lookup.Address;
                    action.AddLogEntry( string.Format( "Odoo: NIT {0} validado en SAT: {1}.", nit, satName ) );
                }
                else if ( lookup.Status == NitLookupStatus.Invalid )
                {
                    action.AddLogEntry( string.Format( "Odoo: NIT {0} no existe en SAT ({1}); se factura como CF.", nit, lookup.Error ), true );
                    nit = "CF";
                }
                else if ( lookup.Status == NitLookupStatus.Unavailable )
                {
                    action.AddLogEntry( string.Format( "Odoo: API de NIT no disponible ({0}); se envía el NIT normalizado sin validar.", lookup.Error ), true );
                }
                else if ( lookup.Status == NitLookupStatus.NotConfigured )
                {
                    // Faltan los Global Attributes OdooNitApiUrl/OdooNitApiBearerToken: no se
                    // puede re-validar. Se envía el NIT normalizado (el certificador lo valida al emitir).
                    action.AddLogEntry( "Odoo: validación de NIT no configurada (faltan Global Attributes OdooNitApiUrl / OdooNitApiBearerToken); se envía el NIT normalizado sin validar.", true );
                }
            }

            // ----- Payload -----
            string buyerName = ( ( registration.FirstName ?? string.Empty ) + " " + ( registration.LastName ?? string.Empty ) ).Trim();
            string phone = registration.PersonAlias?.Person?.PhoneNumbers?
                .OrderByDescending( p => p.IsMessagingEnabled )
                .Select( p => p.Number )
                .FirstOrDefault();

            var payload = new JObject
            {
                ["event_name"] = eventName,
                ["event_description"] = eventDescription,
                ["price"] = eventPrice,
                ["quantity"] = eventQty,
                ["partner"] = new JObject
                {
                    ["name"] = buyerName,
                    ["nit"] = nit,
                    ["email"] = registration.ConfirmationEmail ?? string.Empty,
                    ["phone"] = phone ?? string.Empty
                },
                ["payment"] = new JObject
                {
                    ["method"] = "card",
                    ["reference"] = reference
                }
            };

            if ( satName.IsNotNullOrWhiteSpace() )
            {
                // Datos verificados en SAT: Odoo actualiza el partner con ellos
                // (razón social y dirección reales para el DTE FEL).
                payload["partner"]["sat_name"] = satName;
                if ( satAddress.IsNotNullOrWhiteSpace() )
                {
                    payload["partner"]["sat_address"] = satAddress;
                }
            }

            var lines = new JArray();
            if ( includeDiscount )
            {
                lines.Add( new JObject
                {
                    ["type"] = "discount",
                    // El código de descuento de Rock no se expone en la factura FEL; descripción genérica.
                    ["name"] = "Descuento",
                    ["price"] = -discount,
                    ["quantity"] = 1
                } );
            }
            if ( includeSurcharge )
            {
                lines.Add( new JObject
                {
                    ["type"] = "surcharge",
                    ["name"] = "Recargo por cuotas",
                    ["price"] = feeCoverage,
                    ["quantity"] = 1
                } );
            }
            if ( lines.Count > 0 )
            {
                payload["lines"] = lines;
            }

            // ----- POST -----
            string baseUrl = GetAttributeValue( action, AttributeKey.OdooBaseUrl ).Trim().TrimEnd( '/' );
            string apiKey = Rock.Security.Encryption.DecryptString( GetAttributeValue( action, AttributeKey.OdooApiKey ) ) ?? string.Empty;
            int timeoutSeconds = GetAttributeValue( action, AttributeKey.TimeoutSeconds ).AsIntegerOrNull() ?? 90;

            if ( baseUrl.IsNullOrWhiteSpace() || apiKey.IsNullOrWhiteSpace() )
            {
                SetStatus( action, Status.ErrorPermanente );
                SetWorkflowAttributeValue( action, AttributeKey.OdooErrorAttribute, "Odoo Base URL o API Key sin configurar en la action." );
                return true;
            }

            action.AddLogEntry( string.Format( "Odoo: intento {0}/{1}, POST registración #{2}, ref {3}, total {4:0.00}.",
                attempts, maxAttempts, registration.Id, reference, charged ) );

            int statusCode;
            JObject body;
            try
            {
                using ( var cts = new CancellationTokenSource( TimeSpan.FromSeconds( timeoutSeconds ) ) )
                using ( var request = new HttpRequestMessage( HttpMethod.Post, baseUrl + "/api/event/sell" ) )
                {
                    request.Headers.Add( "X-API-KEY", apiKey );
                    request.Content = new StringContent( payload.ToString(), Encoding.UTF8, "application/json" );

                    using ( var response = _http.SendAsync( request, cts.Token ).GetAwaiter().GetResult() )
                    {
                        statusCode = ( int ) response.StatusCode;
                        string raw = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        body = ParseJson( raw );
                    }
                }
            }
            catch ( Exception ex )
            {
                // Timeout o error de red: la venta pudo haberse completado en
                // Odoo; el reintento con la misma referencia es 100% seguro.
                return Retry( action, attempts, maxAttempts, "Error de red/timeout: " + ex.Message );
            }

            return HandleResponse( action, attempts, maxAttempts, statusCode, body, charged );
        }

        private bool HandleResponse( WorkflowAction action, int attempts, int maxAttempts, int statusCode, JObject body, decimal charged )
        {
            string step = body?.Value<string>( "step" ) ?? string.Empty;
            string error = body?.Value<string>( "error" ) ?? string.Empty;
            bool success = body?.Value<bool?>( "success" ) == true;

            if ( statusCode == 200 && success )
            {
                SaveSaleData( action, body );
                bool alreadyProcessed = body.Value<bool?>( "already_processed" ) == true;
                string felUuid = body.Value<string>( "fel_uuid" );

                if ( alreadyProcessed && body.Value<string>( "order_state" ) == "cancel" )
                {
                    // La venta original fue anulada en Odoo: requiere revisión manual.
                    action.AddLogEntry( "Odoo: la venta original de esta referencia está cancelada en Odoo.", true );
                    SetStatus( action, Status.ErrorPermanente );
                    SetWorkflowAttributeValue( action, AttributeKey.OdooErrorAttribute, "La venta original fue cancelada en Odoo; facturar/revisar manualmente." );
                    return true;
                }

                if ( felUuid.IsNullOrWhiteSpace() )
                {
                    // Venta/factura creadas pero FEL no certificó: seguimiento
                    // manual en Odoo; NO reintentar (devolvería already_processed).
                    string felError = body.Value<string>( "fel_error" ) ?? "FEL sin certificar.";
                    action.AddLogEntry( "Odoo: venta creada pero FEL pendiente: " + felError, true );
                    SetStatus( action, Status.PendienteFEL );
                    SetWorkflowAttributeValue( action, AttributeKey.OdooErrorAttribute, felError );
                    return true;
                }

                string paymentState = body.Value<string>( "payment_state" );
                if ( alreadyProcessed && ( paymentState == "not_paid" || paymentState == "partial" ) )
                {
                    // El POST original falló en el paso de pago y este reintento
                    // devolvió la venta certificada pero sin pago registrado.
                    action.AddLogEntry( "Odoo: factura certificada sin pago registrado (payment_state=" + paymentState + ").", true );
                    SetStatus( action, Status.PagoManual );
                    SetWorkflowAttributeValue( action, AttributeKey.OdooErrorAttribute, "Factura certificada pero sin pago registrado en Odoo (payment_state=" + paymentState + ")." );
                    return true;
                }

                decimal? invoicedTotal = body.Value<decimal?>( "total" );
                if ( invoicedTotal.HasValue && Math.Abs( invoicedTotal.Value - charged ) > 0.02m )
                {
                    // Probable impuesto mal configurado en los productos de Odoo
                    // (IVA no incluido en precio). No bloquea, pero queda rastro.
                    action.AddLogEntry( string.Format( "Odoo: ADVERTENCIA: total facturado {0:0.00} difiere de lo cobrado {1:0.00}.", invoicedTotal.Value, charged ), true );
                }

                action.AddLogEntry( string.Format( "Odoo: venta registrada. Factura {0}, FEL {1}{2}.",
                    body.Value<string>( "invoice_name" ), felUuid,
                    alreadyProcessed ? " (already_processed)" : string.Empty ) );
                SetStatus( action, Status.Exito );
                return true;
            }

            if ( statusCode == 400 && step == "payment" )
            {
                // Factura certificada pero el pago no se registró en Odoo:
                // guardar los datos FEL del body y alertar a contabilidad.
                SaveSaleData( action, body );
                action.AddLogEntry( "Odoo: factura certificada pero pago no registrado: " + error, true );
                SetStatus( action, Status.PagoManual );
                SetWorkflowAttributeValue( action, AttributeKey.OdooErrorAttribute, error );
                return true;
            }

            if ( statusCode == 401 || ( statusCode == 400 && step == "validation" ) )
            {
                action.AddLogEntry( string.Format( "Odoo: error permanente ({0} {1}): {2}", statusCode, step, error ), true );
                SetStatus( action, Status.ErrorPermanente );
                SetWorkflowAttributeValue( action, AttributeKey.OdooErrorAttribute, error );
                return true;
            }

            if ( statusCode == 409 )
            {
                // Referencia duplicada detectada por la BD: el siguiente intento
                // recibe 200 already_processed con los datos FEL completos.
                return Retry( action, attempts, maxAttempts, "409 referencia duplicada; próximo intento devuelve la venta original." );
            }

            // 400/500 en order/confirm/invoice o 500 genérico: nada quedó en
            // Odoo, reintento seguro.
            return Retry( action, attempts, maxAttempts, string.Format( "HTTP {0} step={1}: {2}", statusCode, step, error ) );
        }

        private bool Retry( WorkflowAction action, int attempts, int maxAttempts, string reason )
        {
            SetWorkflowAttributeValue( action, AttributeKey.OdooErrorAttribute, reason );

            if ( attempts >= maxAttempts )
            {
                action.AddLogEntry( string.Format( "Odoo: agotados {0} intentos. Último error: {1}", attempts, reason ), true );
                SetStatus( action, Status.ErrorPermanente );
                return true;
            }

            action.AddLogEntry( string.Format( "Odoo: intento {0}/{1} falló ({2}); se reintenta en el próximo ciclo.", attempts, maxAttempts, reason ) );
            SetStatus( action, Status.Reintentando );

            // El retry exige workflow persistido; forzarlo aquí evita perder la
            // venta si el WorkflowType no quedó marcado Persisted (mismo patrón
            // que la action core PersistWorkflow).
            var workflow = action.Activity?.Workflow;
            if ( workflow != null )
            {
                workflow.IsPersisted = true;
            }

            // false sin marcar complete: el workflow persistido queda activo y
            // ProcessWorkflows lo reprocesa cada ProcessingIntervalSeconds.
            return false;
        }

        private void SaveSaleData( WorkflowAction action, JObject body )
        {
            SetWorkflowAttributeValue( action, AttributeKey.FelUuidAttribute, body.Value<string>( "fel_uuid" ) ?? string.Empty );
            SetWorkflowAttributeValue( action, AttributeKey.FelSerieAttribute, body.Value<string>( "fel_serie" ) ?? string.Empty );
            SetWorkflowAttributeValue( action, AttributeKey.FelNumeroAttribute, body.Value<string>( "fel_numero" ) ?? string.Empty );
            SetWorkflowAttributeValue( action, AttributeKey.OrderNameAttribute, body.Value<string>( "order_name" ) ?? string.Empty );
            SetWorkflowAttributeValue( action, AttributeKey.InvoiceNameAttribute, body.Value<string>( "invoice_name" ) ?? string.Empty );
        }

        private void SetStatus( WorkflowAction action, string status )
        {
            SetWorkflowAttributeValue( action, AttributeKey.OdooStatusAttribute, status );
        }

        private enum NitLookupStatus
        {
            Valid,
            Invalid,
            Unavailable,
            NotConfigured
        }

        private class NitLookupResult
        {
            public NitLookupStatus Status;
            public string Name;
            public string Address;
            public string Error;
        }

        /// <summary>
        /// Valida el NIT contra la API del certificador FEL (Megaprint iFacere,
        /// método retornarDatosCliente) y devuelve la razón social registrada
        /// en SAT. Mismo contrato que usa el bloque de donaciones.
        /// </summary>
        private NitLookupResult LookupNit( string cleanNit )
        {
            // La config de la API de NIT vive en Global Attributes (compartida con el
            // bloque de inscripción que valida el NIT en la pantalla de pago).
            string apiUrl = GlobalAttributesCache.Value( "OdooNitApiUrl" );
            string rawToken = GlobalAttributesCache.Value( "OdooNitApiBearerToken" );
            string apiToken = Rock.Security.Encryption.DecryptString( rawToken );
            if ( apiToken.IsNullOrWhiteSpace() )
            {
                // Fallback si el Global Attribute se guardó como texto plano en vez de encriptado.
                apiToken = rawToken;
            }

            if ( apiUrl.IsNullOrWhiteSpace() || apiToken.IsNullOrWhiteSpace() )
            {
                return new NitLookupResult { Status = NitLookupStatus.NotConfigured };
            }

            if ( !Uri.TryCreate( apiUrl.Trim(), UriKind.Absolute, out var apiUri ) || apiUri.Scheme != Uri.UriSchemeHttps )
            {
                return new NitLookupResult { Status = NitLookupStatus.Unavailable, Error = "NIT API URL inválida (debe ser https)." };
            }

            string requestXml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
                + "<RetornaDatosClienteRequest>\n"
                + "  <nit>" + System.Security.SecurityElement.Escape( cleanNit ) + "</nit>\n"
                + "</RetornaDatosClienteRequest>";

            try
            {
                using ( var cts = new CancellationTokenSource( TimeSpan.FromSeconds( 15 ) ) )
                using ( var request = new HttpRequestMessage( HttpMethod.Post, apiUri ) )
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue( "Bearer", apiToken );
                    request.Content = new StringContent( requestXml, Encoding.UTF8, "application/xml" );

                    using ( var response = _http.SendAsync( request, cts.Token ).GetAwaiter().GetResult() )
                    {
                        string raw = response.Content.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty;

                        if ( !response.IsSuccessStatusCode )
                        {
                            return new NitLookupResult { Status = NitLookupStatus.Unavailable, Error = "HTTP " + ( int ) response.StatusCode };
                        }

                        // La API responde 200 también para NIT inexistente: sin
                        // <nombre> (o tipo_respuesta=1) se trata como inválido.
                        var matchName = Regex.Match( raw, @"<nombre>(.*?)</nombre>", RegexOptions.IgnoreCase | RegexOptions.Singleline );
                        string name = matchName.Success ? SanitizeSatText( matchName.Groups[1].Value, 120 ) : string.Empty;

                        if ( name.IsNullOrWhiteSpace() )
                        {
                            var matchError = Regex.Match( raw, @"<listado_errores>(.*?)</listado_errores>", RegexOptions.IgnoreCase | RegexOptions.Singleline );
                            string error = matchError.Success ? SanitizeSatText( matchError.Groups[1].Value, 200 ) : "sin nombre en la respuesta";
                            return new NitLookupResult { Status = NitLookupStatus.Invalid, Error = error };
                        }

                        var matchAddress = Regex.Match( raw, @"<direccion>(.*?)</direccion>", RegexOptions.IgnoreCase | RegexOptions.Singleline );
                        string address = matchAddress.Success ? SanitizeSatText( matchAddress.Groups[1].Value, 200 ) : string.Empty;

                        return new NitLookupResult { Status = NitLookupStatus.Valid, Name = name, Address = address };
                    }
                }
            }
            catch ( Exception ex )
            {
                return new NitLookupResult { Status = NitLookupStatus.Unavailable, Error = ex.Message };
            }
        }

        private static string SanitizeSatText( string value, int maxLength )
        {
            string clean = Regex.Replace( value ?? string.Empty, "<[^>]*>", string.Empty );
            clean = System.Net.WebUtility.HtmlDecode( clean ).Trim();
            clean = new string( clean.Where( c => !char.IsControl( c ) ).ToArray() );
            return clean.Length > maxLength ? clean.Substring( 0, maxLength ) : clean;
        }

        private string GetWorkflowAttribute( WorkflowAction action, string actionAttributeKey )
        {
            var attrGuid = GetAttributeValue( action, actionAttributeKey ).AsGuidOrNull();
            if ( attrGuid.HasValue )
            {
                return action.GetWorkflowAttributeValue( attrGuid.Value ) ?? string.Empty;
            }
            return string.Empty;
        }

        private static JObject ParseJson( string raw )
        {
            if ( raw.IsNullOrWhiteSpace() )
            {
                return new JObject();
            }
            try
            {
                return JObject.Parse( raw );
            }
            catch
            {
                return new JObject { ["error"] = raw.Truncate( 500 ) };
            }
        }

        private static decimal Round2( decimal value )
        {
            return Math.Round( value, 2, MidpointRounding.AwayFromZero );
        }
    }
}
