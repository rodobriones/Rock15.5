// <copyright>
// ePay Response Code Mappings
// </copyright>

using System.Collections.Generic;

namespace Rock.Blocks.QREVENT.EPay
{
    /// <summary>
    /// ePay response code messages (Guatemala)
    /// </summary>
    public static class EPayResponseCodes
    {
        private static readonly Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            { "00", "Transacción aprobada" },
            { "01", "Refiérase al emisor de la tarjeta" },
            { "02", "Refiérase al emisor de la tarjeta" },
            { "05", "Transacción no aceptada por el banco emisor" },
            { "13", "Monto inválido" },
            { "19", "Transacción no realizada, intente de nuevo" },
            { "31", "Tarjeta no soportada por switch" },
            { "35", "Transacción ya ha sido anulada" },
            { "36", "Transacción a anular no existe" },
            { "37", "Transacción de anulación reversada" },
            { "38", "Transacción de anulación con error" },
            { "41", "Tarjeta extraviada" },
            { "43", "Tarjeta robada" },
            { "51", "No tiene fondos disponibles" },
            { "57", "Transacción no permitida" },
            { "58", "Transacción no permitida en la terminal" },
            { "65", "Límite de actividad excedido" },
            { "89", "Terminal inválida" },
            { "91", "Emisor no disponible" },
            { "93", "Transacción no puede completarse, valide configuración con el adquirente" },
            { "94", "Transacción duplicada" },
            { "96", "Error del sistema, intente más tarde" }
        };

        public static string GetMessage( string code )
        {
            if ( string.IsNullOrWhiteSpace( code ) )
            {
                return "Error del sistema, intente más tarde";
            }

            return Messages.TryGetValue( code, out string message )
                ? message
                : "Error del sistema, intente más tarde";
        }

        public static bool IsApproved( string code )
        {
            return code == "00";
        }
    }
}
