// <copyright>
// ePay Validators (Luhn, Brand Detection, Expiration)
// </copyright>

using System;
using System.Text.RegularExpressions;

namespace Rock.Blocks.QREVENT.EPay
{
    /// <summary>
    /// Validation utilities for ePay card processing
    /// </summary>
    public static class EPayValidators
    {
        /// <summary>
        /// Validate card number using Luhn algorithm
        /// </summary>
        public static bool ValidateLuhn( string cardNumber )
        {
            if ( string.IsNullOrWhiteSpace( cardNumber ) )
            {
                return false;
            }

            var digits = Regex.Replace( cardNumber, @"\D", "" );

            if ( digits.Length < 13 || digits.Length > 19 )
            {
                return false;
            }

            int sum = 0;
            bool shouldDouble = false;

            for ( int i = digits.Length - 1; i >= 0; i-- )
            {
                int digit = int.Parse( digits[i].ToString() );

                if ( shouldDouble )
                {
                    digit *= 2;
                    if ( digit > 9 )
                    {
                        digit -= 9;
                    }
                }

                sum += digit;
                shouldDouble = !shouldDouble;
            }

            return sum % 10 == 0;
        }

        /// <summary>
        /// Detect card brand (Visa, Mastercard, Amex)
        /// </summary>
        public static CardBrand DetectBrand( string cardNumber )
        {
            if ( string.IsNullOrWhiteSpace( cardNumber ) )
            {
                return CardBrand.Unknown;
            }

            var digits = Regex.Replace( cardNumber, @"\D", "" );

            if ( Regex.IsMatch( digits, @"^4\d{0,}$" ) )
            {
                return CardBrand.Visa;
            }

            if ( Regex.IsMatch( digits, @"^(5[1-5]\d{0,}|2(2[2-9]|[3-6]\d|7[01]|720)\d{0,})$" ) )
            {
                return CardBrand.Mastercard;
            }

            if ( Regex.IsMatch( digits, @"^3[47]\d{0,}$" ) )
            {
                return CardBrand.Amex;
            }

            return CardBrand.Unknown;
        }

        /// <summary>
        /// Get expected CVV length for card brand
        /// </summary>
        public static int GetCvvLength( CardBrand brand )
        {
            return brand == CardBrand.Amex ? 4 : 3;
        }

        /// <summary>
        /// Validate card expiration
        /// </summary>
        public static bool ValidateExpiration( int month, int year )
        {
            if ( month < 1 || month > 12 )
            {
                return false;
            }

            // Convertir año a 4 dígitos si es necesario
            var fullYear = year;
            if ( year < 100 )
            {
                fullYear = 2000 + year;
            }

            var currentYear = DateTime.Now.Year;
            var currentMonth = DateTime.Now.Month;

            if ( fullYear < currentYear || fullYear > currentYear + 20 )
            {
                return false;
            }

            if ( fullYear == currentYear && month < currentMonth )
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Get last 4 digits of card number
        /// </summary>
        public static string GetLast4( string cardNumber )
        {
            if ( string.IsNullOrWhiteSpace( cardNumber ) )
            {
                return "";
            }

            var digits = Regex.Replace( cardNumber, @"\D", "" );
            return digits.Length >= 4 ? digits.Substring( digits.Length - 4 ) : digits;
        }
    }

    public enum CardBrand
    {
        Unknown,
        Visa,
        Mastercard,
        Amex
    }
}
