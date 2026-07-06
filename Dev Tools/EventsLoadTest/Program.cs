// Prueba de carga del módulo Eventos/Boletería (SMOKE #10).
// Ejercita las invariantes de concurrencia DIRECTAMENTE contra los servicios de dominio
// (HoldService / CheckoutService) y la BD real de dev — el mismo código que corre el checkout.
//
//   TEST 1 — SOBREVENTA:     N compradores distintos reservan en paralelo un tipo con cupo C.
//                            Invariante: NUNCA se reserva más que C (applock por TicketType).
//   TEST 2 — OCUPACIÓN FALSA: holds expirados dejan de contar cupo (sin limpieza) y el SP de
//                            limpieza los cancela de verdad.
//   TEST 3 — MUTEX DE COBRO: M confirmaciones simultáneas de la MISMA orden (gratis, sin
//                            pasarela): exactamente un finalize; ni tickets duplicados ni
//                            órdenes varadas en Charging.
//
// Uso:  ef6.exe [--yes] [--keep] [--workers 60] [--capacity 24]
//   --yes      no pide confirmación (imprime servidor/BD y sigue)
//   --keep     no borra el evento de prueba al final (para inspección)
//
// Crea un evento "LOADTEST ..." Privado (no aparece en calendario) con boletos GRATIS
// (no toca pasarela/Odoo/FEL) y lo borra al terminar. Los compradores son PersonAlias
// reales de la BD, pero el correo de entrega se fuerza a un dominio inválido para que
// ningún correo real salga.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Rock;
using Rock.Data;
using Rock.Enums.Eventos;
using Rock.Model;
using Rock.ViewModels.Blocks.Eventos.EventCheckout;

namespace VidaReal.EventsLoadTest
{
    internal static class Program
    {
        private const string SafeDeliveryEmail = "loadtest@example.invalid"; // dominio inválido: ningún correo real sale
        private static string _connectionString;

        private static int Main( string[] args )
        {
            var yes = args.Contains( "--yes" );
            var keep = args.Contains( "--keep" );
            var workers = ArgInt( args, "--workers", 60 );
            var capacity = ArgInt( args, "--capacity", 24 );

            // Todo el grafo de dependencias de Rock.dll se resuelve desde RockWeb\Bin.
            var binPath = FindRockWebBin();
            AppDomain.CurrentDomain.AssemblyResolve += ( s, e ) =>
            {
                var name = new AssemblyName( e.Name ).Name;
                var candidate = Path.Combine( binPath, name + ".dll" );
                return File.Exists( candidate ) ? Assembly.LoadFrom( candidate ) : null;
            };

            return Run( yes, keep, workers, capacity );
        }

        // Separado de Main para que el JIT no exija resolver Rock.dll antes de instalar el AssemblyResolve.
        private static int Run( bool yes, bool keep, int workers, int capacity )
        {
            _connectionString = ConfigurationManager.ConnectionStrings["RockContext"]?.ConnectionString;
            if ( string.IsNullOrWhiteSpace( _connectionString ) )
            {
                Console.WriteLine( "❌ No hay connection string 'RockContext' (¿se copió web.ConnectionStrings.config junto al exe?)." );
                return 1;
            }

            // Con cientos de workers el cuello sería el propio harness, no Rock: el ThreadPool
            // inyecta ~1 hilo/seg pasado el mínimo, y el pool de SQL trae Max Pool Size=100.
            ThreadPool.SetMinThreads( workers + 50, workers + 50 );
            var poolBuilder = new SqlConnectionStringBuilder( _connectionString );
            if ( poolBuilder.MaxPoolSize < workers + 50 )
            {
                poolBuilder.MaxPoolSize = workers + 50;
                _connectionString = poolBuilder.ConnectionString;
            }

            var csb = new SqlConnectionStringBuilder( _connectionString );
            Console.WriteLine( $"Servidor: {csb.DataSource}   BD: {csb.InitialCatalog}" );
            Console.WriteLine( $"Workers: {workers}   Cupo de prueba: {capacity}" );
            if ( !yes )
            {
                Console.Write( "¿Correr la prueba de carga contra esta BD? (escribe SI): " );
                if ( Console.ReadLine()?.Trim().ToUpperInvariant() != "SI" )
                {
                    Console.WriteLine( "Abortado." );
                    return 1;
                }
            }

            int eventId = 0, ticketTypeId = 0;
            var failures = new List<string>();

            try
            {
                Console.WriteLine( "\nInicializando Rock (primer RockContext arma el modelo EF, tarda unos segundos)..." );
                var sw = Stopwatch.StartNew();
                List<int> buyerAliasIds;
                using ( var ctx = NewContext() )
                {
                    // Compradores = primary alias de personas CON email (el write-back post-pago
                    // guarda el DeliveryEmail al perfil solo si el perfil NO tiene correo — nunca
                    // debemos "regalarle" el correo de prueba a una persona real sin email).
                    buyerAliasIds = new PersonAliasService( ctx ).Queryable()
                        .Where( pa => pa.AliasPersonId == pa.PersonId
                            && pa.Person.Email != null && pa.Person.Email != "" )
                        .OrderBy( pa => pa.Id )
                        .Select( pa => pa.Id )
                        .Take( workers + 10 )
                        .ToList();
                }
                Console.WriteLine( $"Rock listo en {sw.ElapsedMilliseconds:n0} ms. Compradores disponibles: {buyerAliasIds.Count}" );

                if ( buyerAliasIds.Count < workers )
                {
                    Console.WriteLine( $"⚠ Solo hay {buyerAliasIds.Count} personas con email; se reduce workers a ese número." );
                    workers = buyerAliasIds.Count;
                }

                ( eventId, ticketTypeId ) = CreateTestEvent( capacity );
                Console.WriteLine( $"Evento de prueba creado: EventId={eventId}, TicketTypeId={ticketTypeId} (Privado, gratis, cupo {capacity})." );

                Test1Sobreventa( eventId, ticketTypeId, capacity, workers, buyerAliasIds, failures );
                Test2OcupacionFalsa( eventId, ticketTypeId, capacity, buyerAliasIds, failures );
                Test3MutexDeCobro( eventId, ticketTypeId, buyerAliasIds, failures );
            }
            catch ( Exception ex )
            {
                failures.Add( "Excepción no controlada: " + ex );
            }
            finally
            {
                if ( eventId > 0 && !keep )
                {
                    Cleanup( eventId );
                    Console.WriteLine( $"\nLimpieza: evento {eventId} y sus órdenes/tickets borrados." );
                }
                else if ( eventId > 0 )
                {
                    Console.WriteLine( $"\n--keep: se conserva el evento {eventId} para inspección." );
                }
            }

            Console.WriteLine( new string( '=', 64 ) );
            if ( failures.Count == 0 )
            {
                Console.WriteLine( "✅ TODAS las invariantes se cumplieron (sin sobreventa, sin ocupación falsa, sin doble finalize)." );
                return 0;
            }

            Console.WriteLine( $"❌ {failures.Count} FALLO(S):" );
            foreach ( var f in failures )
            {
                Console.WriteLine( "  - " + f );
            }
            return 2;
        }

        // ------------------------------------------------------------------
        // TEST 1 — Sobreventa: N compradores distintos, en paralelo, cupo C.
        // ------------------------------------------------------------------
        private static void Test1Sobreventa( int eventId, int ticketTypeId, int capacity, int workers, List<int> buyers, List<string> failures )
        {
            Console.WriteLine( $"\n── TEST 1: SOBREVENTA — {workers} compradores paralelos vs cupo {capacity} ──" );

            var results = new ConcurrentBag<(bool ok, int qty, long ms, string error)>();
            var start = new ManualResetEventSlim( false );

            var tasks = Enumerable.Range( 0, workers ).Select( i => Task.Run( () =>
            {
                var qty = ( i % 2 ) + 1; // mezcla de reservas de 1 y de 2
                var buyerAliasId = buyers[i];
                start.Wait();
                var sw = Stopwatch.StartNew();
                try
                {
                    using ( var ctx = NewContext() )
                    {
                        var ev = new EventService( ctx ).Get( eventId );
                        var bag = HoldBag( eventId, ticketTypeId, qty );
                        var result = HoldService.BuildPendingOrder( ctx, ev, bag, buyerAliasId, Guid.NewGuid(), TicketStatus.Held, snapshotAnswers: false );
                        results.Add( (result.Error == null, qty, sw.ElapsedMilliseconds, result.Error) );
                    }
                }
                catch ( Exception ex )
                {
                    results.Add( (false, qty, sw.ElapsedMilliseconds, "EXCEPCIÓN: " + ex.Message) );
                }
            } ) ).ToArray();

            var total = Stopwatch.StartNew();
            start.Set(); // todos arrancan a la vez
            Task.WaitAll( tasks );
            total.Stop();

            var okList = results.Where( r => r.ok ).ToList();
            var held = okList.Sum( r => r.qty );
            var times = results.Select( r => r.ms ).OrderBy( m => m ).ToList();
            var exceptions = results.Where( r => !r.ok && r.error.StartsWith( "EXCEPCIÓN" ) ).ToList();

            int consuming = CountConsuming( ticketTypeId );

            Console.WriteLine( $"  Reservas exitosas: {okList.Count} órdenes / {held} tickets. Rechazadas: {results.Count( r => !r.ok )}." );
            Console.WriteLine( $"  Cupo consumido según BD (criterio del checkout): {consuming} / {capacity}" );
            Console.WriteLine( $"  Tiempos por reserva: p50={Pct( times, 50 )} ms, p95={Pct( times, 95 )} ms, max={times.Last()} ms. Total: {total.ElapsedMilliseconds:n0} ms." );
            foreach ( var g in results.Where( r => !r.ok ).GroupBy( r => r.error ).OrderByDescending( g => g.Count() ) )
            {
                Console.WriteLine( $"    rechazo ×{g.Count()}: {Trunc( g.Key, 90 )}" );
            }

            if ( consuming > capacity )
            {
                failures.Add( $"TEST 1: SOBREVENTA — cupo {capacity} pero hay {consuming} tickets consumiendo cupo." );
            }
            if ( held != consuming )
            {
                failures.Add( $"TEST 1: lo reportado a los compradores ({held}) no cuadra con la BD ({consuming})." );
            }
            if ( consuming < capacity )
            {
                // No es corrupción, pero con demanda >> cupo el tipo debería quedar lleno.
                Console.WriteLine( $"  ⚠ El cupo no quedó lleno ({consuming}/{capacity}) — revisar rechazos de arriba (¿timeouts del applock?)." );
            }
            if ( exceptions.Any() )
            {
                failures.Add( $"TEST 1: {exceptions.Count} excepciones no controladas (deberían ser rechazos limpios): {Trunc( exceptions.First().error, 120 )}" );
            }
        }

        // ---------------------------------------------------------------------------------
        // TEST 2 — Ocupación falsa: holds expirados no cuentan cupo y la limpieza los cancela.
        // ---------------------------------------------------------------------------------
        private static void Test2OcupacionFalsa( int eventId, int ticketTypeId, int capacity, List<int> buyers, List<string> failures )
        {
            Console.WriteLine( $"\n── TEST 2: OCUPACIÓN FALSA — expiración de holds y limpieza ──" );

            // Todos los holds del TEST 1 se retro-datan 20 min (la ventana es de 10).
            Sql( $@"UPDATE o SET CreatedDateTime = DATEADD(minute, -20, CreatedDateTime)
                    FROM [_com_vidareal_Events_Order] o WHERE o.EventId = {eventId} AND o.Status = 0" );

            int consuming = CountConsuming( ticketTypeId );
            Console.WriteLine( $"  Tras expirar todos los holds: cupo consumido = {consuming} (esperado 0, sin correr limpieza)." );
            if ( consuming != 0 )
            {
                failures.Add( $"TEST 2: holds expirados siguen consumiendo cupo ({consuming}) — ocupación falsa." );
            }

            // Con el cupo liberado por pura fecha, una reserva grande debe pasar.
            // Tope del producto: MaxTicketsPerLine (100) por línea — se respeta aquí.
            var bigQty = Math.Min( capacity, HoldService.MaxTicketsPerLine );
            using ( var ctx = NewContext() )
            {
                var ev = new EventService( ctx ).Get( eventId );
                var result = HoldService.BuildPendingOrder( ctx, ev, HoldBag( eventId, ticketTypeId, bigQty ), buyers[0], Guid.NewGuid(), TicketStatus.Held, snapshotAnswers: false );
                if ( result.Error != null )
                {
                    failures.Add( $"TEST 2: con todos los holds expirados, una reserva de {bigQty} fue rechazada: {result.Error}" );
                }
                else
                {
                    Console.WriteLine( $"  Reserva de {bigQty} sobre holds expirados: OK (orden {result.Order.Id})." );
                }
            }

            // El SP del job debe cancelar los expirados sin tocar el hold vigente.
            try
            {
                // Firma real: (@HoldMinutes INT = 15, @Now DATETIME) — nombrado, no posicional.
                Sql( "EXEC [sp_VidaRealEventsCleanupExpiredHolds] @HoldMinutes = 15, @Now = @Now", new SqlParameter( "@Now", DateTime.Now ) );
                var zombies = SqlScalar( $@"SELECT COUNT(*) FROM [_com_vidareal_Events_Order]
                                            WHERE EventId = {eventId} AND Status = 0
                                              AND CreatedDateTime < DATEADD(minute, -10, GETDATE())" );
                var vigentes = CountConsuming( ticketTypeId );
                Console.WriteLine( $"  Tras sp_VidaRealEventsCleanupExpiredHolds: holds zombis = {zombies} (esperado 0), vigentes = {vigentes} (esperado {bigQty})." );
                if ( zombies != 0 )
                {
                    failures.Add( $"TEST 2: el SP de limpieza dejó {zombies} holds expirados en Pending." );
                }
                if ( vigentes != bigQty )
                {
                    failures.Add( $"TEST 2: la limpieza tocó el hold VIGENTE (quedaron {vigentes}/{bigQty})." );
                }
            }
            catch ( Exception ex )
            {
                failures.Add( "TEST 2: error ejecutando el SP de limpieza: " + ex.Message );
            }

            // Libera el hold gigante para dejar el tipo limpio para el TEST 3.
            using ( var ctx = NewContext() )
            {
                HoldService.ReleaseBuyerHolds( ctx, eventId, buyers[0], Guid.NewGuid() );
            }
            if ( CountConsuming( ticketTypeId ) != 0 )
            {
                failures.Add( "TEST 2: ReleaseBuyerHolds no liberó el cupo del comprador." );
            }
        }

        // -----------------------------------------------------------------------------
        // TEST 3 — Mutex de cobro: M confirmaciones paralelas de la MISMA orden (gratis).
        // -----------------------------------------------------------------------------
        private static void Test3MutexDeCobro( int eventId, int ticketTypeId, List<int> buyers, List<string> failures )
        {
            const int attempts = 12;
            const int qty = 2;
            Console.WriteLine( $"\n── TEST 3: MUTEX DE COBRO — {attempts} confirmaciones paralelas de la misma orden ──" );

            var paymentReference = Guid.NewGuid();
            int orderId;
            using ( var ctx = NewContext() )
            {
                var ev = new EventService( ctx ).Get( eventId );
                var result = HoldService.BuildPendingOrder( ctx, ev, HoldBag( eventId, ticketTypeId, qty ), buyers[1], paymentReference, TicketStatus.Held, snapshotAnswers: false );
                if ( result.Error != null )
                {
                    failures.Add( "TEST 3: no se pudo crear el hold base: " + result.Error );
                    return;
                }
                orderId = result.Order.Id;
            }

            var outcomes = new ConcurrentBag<string>();
            var start = new ManualResetEventSlim( false );
            var tasks = Enumerable.Range( 0, attempts ).Select( i => Task.Run( () =>
            {
                start.Wait();
                try
                {
                    using ( var ctx = NewContext() )
                    {
                        // Mismo camino que dos requests simultáneos de ProcessCheckout con el
                        // mismo PaymentReference: preparar y confirmar. Gratis ⇒ sin token.
                        var order = new OrderService( ctx ).Get( orderId );
                        var bag = HoldBag( eventId, ticketTypeId, qty );
                        bag.DeliveryEmail = SafeDeliveryEmail;

                        var prepError = CheckoutService.PrepareHeldOrderForCharge( ctx, order, bag );
                        if ( prepError != null )
                        {
                            outcomes.Add( "prep: " + prepError );
                            return;
                        }

                        var charge = CheckoutService.ChargeAndFinalizeOrder( ctx, order, bag );
                        outcomes.Add( charge.Success ? "SUCCESS" : "rechazo: " + Trunc( charge.Error, 70 ) );
                    }
                }
                catch ( Exception ex )
                {
                    outcomes.Add( "EXCEPCIÓN: " + ex.Message );
                }
            } ) ).ToArray();

            start.Set();
            Task.WaitAll( tasks );

            foreach ( var g in outcomes.GroupBy( o => o ).OrderByDescending( g => g.Count() ) )
            {
                Console.WriteLine( $"  ×{g.Count()}: {g.Key}" );
            }

            // Las invariantes reales están en la BD, no en cuántos reportaron "éxito"
            // (el perdedor que relee Paid devuelve confirmación idempotente — eso es correcto).
            var status = SqlScalar( $"SELECT Status FROM [_com_vidareal_Events_Order] WHERE Id = {orderId}" );
            var validTickets = SqlScalar( $"SELECT COUNT(*) FROM [_com_vidareal_Events_Ticket] WHERE OrderId = {orderId} AND Status = 0" );
            var totalTickets = SqlScalar( $"SELECT COUNT(*) FROM [_com_vidareal_Events_Ticket] WHERE OrderId = {orderId}" );
            var charging = SqlScalar( $"SELECT COUNT(*) FROM [_com_vidareal_Events_Order] WHERE EventId = {eventId} AND Status = 5" );

            Console.WriteLine( $"  BD: orden {orderId} Status={status} (1=Paid), tickets Valid={validTickets}/{totalTickets} (esperado {qty}/{qty}), órdenes Charging={charging} (esperado 0)." );

            if ( status != 1 )
            {
                failures.Add( $"TEST 3: la orden terminó en Status={status}, esperado Paid(1)." );
            }
            if ( validTickets != qty || totalTickets != qty )
            {
                failures.Add( $"TEST 3: tickets Valid={validTickets}/{totalTickets}, esperado exactamente {qty} — duplicación o pérdida." );
            }
            if ( charging != 0 )
            {
                failures.Add( $"TEST 3: quedaron {charging} órdenes varadas en Charging." );
            }
            if ( outcomes.Any( o => o.StartsWith( "EXCEPCIÓN" ) ) )
            {
                failures.Add( "TEST 3: hubo excepciones no controladas: " + outcomes.First( o => o.StartsWith( "EXCEPCIÓN" ) ) );
            }
        }

        // ------------------------------------------------------------------ helpers

        private static RockContext NewContext() => new RockContext( _connectionString );

        private static ProcessCheckoutRequestBag HoldBag( int eventId, int ticketTypeId, int qty )
        {
            return new ProcessCheckoutRequestBag
            {
                PaymentReference = Guid.NewGuid(),
                Lines = new List<CheckoutLineBag> { new CheckoutLineBag { TicketTypeId = ticketTypeId, Quantity = qty } }
            };
        }

        private static (int eventId, int ticketTypeId) CreateTestEvent( int capacity )
        {
            using ( var ctx = NewContext() )
            {
                var ev = new Event
                {
                    Name = "LOADTEST " + DateTime.Now.ToString( "yyyyMMdd-HHmmss" ),
                    Description = "Evento sintético de prueba de carga — se borra al terminar.",
                    StartDateTime = DateTime.Now.AddDays( 7 ),
                    EndDateTime = DateTime.Now.AddDays( 7 ).AddHours( 2 ),
                    Status = EventStatus.Published,
                    Visibility = EventVisibility.Private // jamás listado en el calendario público
                };
                new EventService( ctx ).Add( ev );
                ctx.SaveChanges();

                var tt = new TicketType
                {
                    EventId = ev.Id,
                    Name = "Load",
                    Price = 0m, // gratis: sin pasarela, sin Odoo, sin FEL
                    Capacity = capacity,
                    IsActive = true,
                    SortOrder = 0
                };
                new TicketTypeService( ctx ).Add( tt );
                ctx.SaveChanges();

                return (ev.Id, tt.Id);
            }
        }

        private static int CountConsuming( int ticketTypeId )
        {
            using ( var ctx = NewContext() )
            {
                return HoldService.CountSoldTickets( new TicketService( ctx ), ticketTypeId );
            }
        }

        private static void Cleanup( int eventId )
        {
            Sql( $@"
DELETE cl FROM [_com_vidareal_Events_CheckinLog] cl
    JOIN [_com_vidareal_Events_Ticket] t ON t.Id = cl.TicketId
    JOIN [_com_vidareal_Events_Order] o ON o.Id = t.OrderId WHERE o.EventId = {eventId};
DELETE t FROM [_com_vidareal_Events_Ticket] t
    JOIN [_com_vidareal_Events_Order] o ON o.Id = t.OrderId WHERE o.EventId = {eventId};
DELETE FROM [_com_vidareal_Events_Order] WHERE EventId = {eventId};
DELETE FROM [_com_vidareal_Events_PromoCode] WHERE EventId = {eventId};
DELETE FROM [_com_vidareal_Events_EventStaff] WHERE EventId = {eventId};
DELETE FROM [_com_vidareal_Events_TicketType] WHERE EventId = {eventId};
DELETE FROM [_com_vidareal_Events_Event] WHERE Id = {eventId};" );
        }

        private static void Sql( string sql, params SqlParameter[] parameters )
        {
            using ( var conn = new SqlConnection( _connectionString ) )
            using ( var cmd = new SqlCommand( sql, conn ) )
            {
                cmd.CommandTimeout = 60;
                cmd.Parameters.AddRange( parameters );
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        private static int SqlScalar( string sql )
        {
            using ( var conn = new SqlConnection( _connectionString ) )
            using ( var cmd = new SqlCommand( sql, conn ) )
            {
                conn.Open();
                return Convert.ToInt32( cmd.ExecuteScalar() );
            }
        }

        private static string FindRockWebBin()
        {
            var dir = new DirectoryInfo( AppDomain.CurrentDomain.BaseDirectory );
            while ( dir != null )
            {
                var candidate = Path.Combine( dir.FullName, "RockWeb", "Bin" );
                if ( Directory.Exists( candidate ) )
                {
                    return candidate;
                }
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException( "No se encontró RockWeb\\Bin subiendo desde " + AppDomain.CurrentDomain.BaseDirectory );
        }

        private static int ArgInt( string[] args, string name, int fallback )
        {
            var i = Array.IndexOf( args, name );
            return i >= 0 && i + 1 < args.Length && int.TryParse( args[i + 1], out var v ) ? v : fallback;
        }

        private static long Pct( List<long> sorted, int pct )
        {
            if ( sorted.Count == 0 )
            {
                return 0;
            }
            var idx = Math.Min( sorted.Count - 1, ( int ) Math.Ceiling( pct / 100.0 * sorted.Count ) - 1 );
            return sorted[Math.Max( 0, idx )];
        }

        private static string Trunc( string s, int len ) => s != null && s.Length > len ? s.Substring( 0, len ) + "…" : s;
    }
}
