using System;
using System.Threading.Tasks;

using Rock.Model;
using Rock.RealTime;

namespace Rock.Blocks.QREVENT
{
    /// <summary>
    /// Métodos que el servidor puede invocar en los clientes suscritos al topic.
    /// El nombre del método llega al cliente en camelCase: <c>reservationCheckedIn</c>.
    /// </summary>
    public interface ISundayServiceClient
    {
        /// <summary>La reserva del canal acaba de recibir check-in en la puerta.</summary>
        Task ReservationCheckedIn( ReservationCheckedInBag bag );
    }

    /// <summary>
    /// Carga del aviso de check-in. Propiedades en camelCase a propósito, como el
    /// resto de bags de QREVENT, para que el .obs las lea tal cual.
    /// </summary>
    public class ReservationCheckedInBag
    {
        public string reservationCode { get; set; }
        public string name { get; set; }
        public string scheduleName { get; set; }
        public int quantity { get; set; }
        public string checkedInAtIso { get; set; }
        /// <summary>Segundos que la tarjeta debe seguir visible en el telefono.</summary>
        public int visibleForSeconds { get; set; }
    }

    /// <summary>
    /// Topic RealTime (SignalR) del servicio dominical. Vive en Rock.Blocks y no en
    /// Rock.dll: <c>Reflection.GetPluginAssemblies()</c> escanea todos los DLL de Bin,
    /// así que se descubre solo y el despliegue sigue siendo DLL + bundle.
    ///
    /// Un canal por reserva. El cliente NUNCA elige canal: se une a través del
    /// BlockAction <c>SubscribeToReservation</c> de <see cref="SundayServiceRegistration"/>,
    /// que verifica que la persona conectada sea dueña de la reserva.
    ///
    /// Patrón copiado de <c>CheckInTopic</c> + <c>CheckInKiosk.SubscribeToRealTime</c>.
    /// </summary>
    [RealTimeTopic( TopicIdentifier )]
    public sealed class SundayServiceTopic : Topic<ISundayServiceClient>
    {
        /// <summary>El mismo string que usa el cliente en <c>getTopic(...)</c>.</summary>
        public const string TopicIdentifier = "Rock.Blocks.QREVENT.SundayServiceTopic";

        /// <summary>
        /// Minutos que la tarjeta «¡Bienvenido!» permanece visible tras el check-in.
        /// Decision de negocio (2026-09-07): 10 minutos, luego desaparece sola.
        /// La usa tambien SundayServiceRegistration para filtrar recargas.
        /// </summary>
        public const int WelcomeVisibleMinutes = 10;

        /// <summary>Canal de una reserva concreta.</summary>
        public static string GetReservationChannel( string reservationCode )
        {
            return "sundayservice:reservation:" + ( reservationCode ?? string.Empty ).Trim().ToUpperInvariant();
        }

        /// <summary>
        /// Avisa al teléfono de la persona que su reserva acaba de recibir check-in.
        /// Fire-and-forget, igual que <c>CheckInDirector.SendRefreshKioskConfiguration</c>:
        /// nunca retrasa la respuesta al escáner y nunca la hace fallar. Si el motor
        /// RealTime no está disponible, se registra la excepción y se sigue.
        /// </summary>
        public static void NotifyReservationCheckedIn( string reservationCode, string name, string scheduleName, int quantity )
        {
            if ( string.IsNullOrWhiteSpace( reservationCode ) )
            {
                return;
            }

            var bag = new ReservationCheckedInBag
            {
                reservationCode = reservationCode.Trim().ToUpperInvariant(),
                name = name ?? string.Empty,
                scheduleName = scheduleName ?? string.Empty,
                quantity = quantity,
                checkedInAtIso = RockDateTime.Now.ToString( "yyyy-MM-ddTHH:mm:ss" ),
                visibleForSeconds = WelcomeVisibleMinutes * 60
            };

            var channel = GetReservationChannel( reservationCode );

            Task.Run( async () =>
            {
                try
                {
                    await RealTimeHelper.GetTopicContext<ISundayServiceClient>()
                        .Clients
                        .Channel( channel )
                        .ReservationCheckedIn( bag );
                }
                catch ( Exception ex )
                {
                    ExceptionLogService.LogException( ex );
                }
            } );
        }
    }
}
