// <copyright>
// Copyright by the Spark Development Network
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
// </copyright>
//
using System;
using System.Threading;
using System.Web.Hosting;

namespace Rock.Model
{
    /// <summary>
    /// Protección del runtime del módulo de eventos contra reciclos del app pool:
    ///
    /// 1) <see cref="EnterCriticalPaymentScope"/> — envuelve la sección crítica del cobro
    ///    (mutex Pending→Charging → Charge() → finalize). Registra un <see cref="IRegisteredObject"/>
    ///    en el hosting de ASP.NET: ante un shutdown GRACIOSO (reciclo programado, idle timeout,
    ///    deploy de DLLs) IIS llama Stop() y aquí se ESPERA a que los cobros en vuelo terminen
    ///    (hasta <see cref="StopDrainSeconds"/>) antes de dejar caer el proceso. Un kill duro
    ///    (crash, StackOverflow, kill -9) no es prevenible: para eso queda la orden en Charging
    ///    y la alerta del job EventsMaintenance.
    ///
    /// 2) <see cref="QueueBackgroundWork"/> — reemplaza a Task.Run para el trabajo post-pago
    ///    (correo con boletos, write-back de respuestas): HostingEnvironment.QueueBackgroundWorkItem
    ///    participa del mismo drenaje de shutdown (el runtime espera a los work items registrados),
    ///    mientras que un Task.Run muere sin log a mitad de un reciclo. Fallback a Task.Run cuando
    ///    no hay hosting ASP.NET (tests, consola, jobs fuera de IIS).
    /// </summary>
    public static class EventsRuntime
    {
        /// <summary>Máximo de segundos que un shutdown gracioso espera a los cobros en vuelo.
        /// Debe caber en el ShutdownTimeLimit de IIS (default 90s).</summary>
        public const int StopDrainSeconds = 60;

        private static readonly object _sync = new object();
        private static int _inFlight;
        private static bool _registered;
        private static bool _stopping;

        /// <summary>
        /// Marca el inicio de una sección crítica de cobro. Usar en un <c>using</c> que cubra
        /// desde el mutex Pending→Charging hasta el commit del finalize. Si el proceso ya está
        /// en shutdown, lanza <see cref="InvalidOperationException"/> para NO iniciar un cobro
        /// que no va a poder terminar (el cliente reintenta contra el proceso nuevo).
        /// </summary>
        public static IDisposable EnterCriticalPaymentScope()
        {
            lock ( _sync )
            {
                if ( _stopping )
                {
                    throw new InvalidOperationException( "El servidor se está reiniciando; no se inicia un cobro nuevo." );
                }

                EnsureRegistered();
                _inFlight++;
            }

            return new PaymentScope();
        }

        private sealed class PaymentScope : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if ( _disposed )
                {
                    return;
                }
                _disposed = true;

                lock ( _sync )
                {
                    _inFlight--;
                    Monitor.PulseAll( _sync );
                }
            }
        }

        private static void EnsureRegistered()
        {
            // Solo aplica dentro de IIS/ASP.NET; en tests o consola no hay hosting que registrar.
            if ( _registered || !HostingEnvironment.IsHosted )
            {
                return;
            }

            HostingEnvironment.RegisterObject( new ShutdownGuard() );
            _registered = true;
        }

        private sealed class ShutdownGuard : IRegisteredObject
        {
            public void Stop( bool immediate )
            {
                var deadline = DateTime.UtcNow.AddSeconds( immediate ? 2 : StopDrainSeconds );

                lock ( _sync )
                {
                    _stopping = true;
                    while ( _inFlight > 0 && DateTime.UtcNow < deadline )
                    {
                        Monitor.Wait( _sync, TimeSpan.FromSeconds( 1 ) );
                    }

                    if ( _inFlight > 0 )
                    {
                        // Se agotó la gracia con cobros aún en vuelo: quedarán en Charging y el job
                        // EventsMaintenance los reconciliará (o alertará si no hay transacción).
                        ExceptionLogService.LogException( new Exception(
                            $"[EventsRuntime] Shutdown con {_inFlight} cobro(s) en vuelo tras {( immediate ? 2 : StopDrainSeconds )}s de gracia. Las órdenes quedarán en Charging para reconciliación del job." ) );
                    }
                }

                HostingEnvironment.UnregisterObject( this );
            }
        }

        /// <summary>
        /// Carriles de trabajo en segundo plano. Cada carril acota cuántos trabajos corren A LA VEZ
        /// (los demás esperan turno sin bloquear hilos): una venta masiva encola cientos de correos
        /// con PDF (Chromium) y POSTs a Odoo, y sin la cota tumbarían el servidor o saturarían Odoo.
        /// </summary>
        public enum WorkLane
        {
            /// <summary>Sin cota (trabajo barato de BD, p. ej. write-back de respuestas).</summary>
            Default,

            /// <summary>Correo de boletos + generación de PDF (Chromium): máx. 2 simultáneos.</summary>
            EmailPdf,

            /// <summary>POST de venta/FEL a Odoo: máx. 3 simultáneos.</summary>
            Odoo
        }

        // ponytail: cotas fijas; volverlas configurables cuando alguien las necesite distintas.
        private static readonly SemaphoreSlim _emailPdfGate = new SemaphoreSlim( 2, 2 );
        private static readonly SemaphoreSlim _odooGate = new SemaphoreSlim( 3, 3 );

        private static SemaphoreSlim GetGate( WorkLane lane )
        {
            switch ( lane )
            {
                case WorkLane.EmailPdf:
                    return _emailPdfGate;
                case WorkLane.Odoo:
                    return _odooGate;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Encola trabajo en segundo plano que sobrevive a un reciclo gracioso (el runtime de
        /// ASP.NET drena los work items antes de tirar el proceso). El carril acota la concurrencia
        /// (la espera de turno es async: no consume hilos). Excepciones quedan en el log.
        /// OJO: la cola vive en memoria — lo que un kill duro alcance a matar lo recoge el barrido
        /// del job EventsMaintenance (FEL por OdooStatus, correos por EmailSentCount).
        /// </summary>
        /// <param name="name">Nombre corto para el log de errores (p. ej. "TicketEmail").</param>
        /// <param name="work">El trabajo; recibe el CancellationToken del shutdown.</param>
        /// <param name="lane">Carril de concurrencia (default: sin cota).</param>
        public static void QueueBackgroundWork( string name, Action<CancellationToken> work, WorkLane lane = WorkLane.Default )
        {
            async System.Threading.Tasks.Task SafeRunAsync( CancellationToken ct )
            {
                var gate = GetGate( lane );
                var acquired = false;
                try
                {
                    if ( gate != null )
                    {
                        await gate.WaitAsync( ct );
                        acquired = true;
                    }

                    work( ct );
                }
                catch ( OperationCanceledException )
                {
                    // Shutdown mientras esperaba turno: el barrido del job lo recogerá.
                }
                catch ( Exception ex )
                {
                    ExceptionLogService.LogException( new Exception( $"[EventsRuntime] Trabajo en segundo plano '{name}' falló.", ex ) );
                }
                finally
                {
                    if ( acquired )
                    {
                        gate.Release();
                    }
                }
            }

            if ( HostingEnvironment.IsHosted )
            {
                try
                {
                    HostingEnvironment.QueueBackgroundWorkItem( ( Func<CancellationToken, System.Threading.Tasks.Task> ) SafeRunAsync );
                    return;
                }
                catch ( InvalidOperationException )
                {
                    // Shutdown ya iniciado: caer al fallback (mejor intentar que perder el trabajo).
                }
            }

            System.Threading.Tasks.Task.Run( () => SafeRunAsync( CancellationToken.None ) );
        }
    }
}
