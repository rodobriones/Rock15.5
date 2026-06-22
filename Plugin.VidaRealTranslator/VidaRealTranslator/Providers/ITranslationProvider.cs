using System.Collections.Generic;

namespace com.vidareal.Translator.Providers
{
    /// <summary>
    /// Abstraccion de proveedor de IA de traduccion. ponytail: hoy solo existe
    /// AzureOpenAiProvider (el default pedido). La interfaz queda para enchufar
    /// OpenAI/Claude/Gemini cuando se necesiten; agregar una clase + un case en
    /// TranslatorController.GetProvider().
    /// </summary>
    public interface ITranslationProvider
    {
        string Name { get; }

        /// <summary>
        /// Traduce un lote. Devuelve un mapa indice-de-entrada -> traduccion.
        /// Las entradas que el modelo no devuelva simplemente se omiten (el
        /// llamador deja el texto original). Lanza excepcion ante fallo duro.
        /// </summary>
        Dictionary<int, string> TranslateBatch( IList<string> texts, string targetLanguage );
    }
}
