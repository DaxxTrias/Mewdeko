using System.Text.RegularExpressions;

namespace Mewdeko.Modules.Counting.Common;

/// <summary>
/// Helper class for parsing and formatting different counting patterns.
/// </summary>
public static class CountingPatternHelper
{
    // Roman numeral mappings
    private static readonly Dictionary<char, int> RomanNumeralMap = new()
    {
        {'I', 1}, {'V', 5}, {'X', 10}, {'L', 50},
        {'C', 100}, {'D', 500}, {'M', 1000}
    };

    private static readonly Dictionary<int, string> DecimalToRomanMap = new()
    {
        {1000, "M"}, {900, "CM"}, {500, "D"}, {400, "CD"},
        {100, "C"}, {90, "XC"}, {50, "L"}, {40, "XL"},
        {10, "X"}, {9, "IX"}, {5, "V"}, {4, "IV"}, {1, "I"}
    };

    // Word mappings for numbers
    private static readonly Dictionary<string, long> WordToNumberMap = new(StringComparer.OrdinalIgnoreCase)
    {
        {"zero", 0}, {"one", 1}, {"two", 2}, {"three", 3}, {"four", 4}, {"five", 5},
        {"six", 6}, {"seven", 7}, {"eight", 8}, {"nine", 9}, {"ten", 10},
        {"eleven", 11}, {"twelve", 12}, {"thirteen", 13}, {"fourteen", 14}, {"fifteen", 15},
        {"sixteen", 16}, {"seventeen", 17}, {"eighteen", 18}, {"nineteen", 19}, {"twenty", 20},
        {"thirty", 30}, {"forty", 40}, {"fifty", 50}, {"sixty", 60}, {"seventy", 70},
        {"eighty", 80}, {"ninety", 90}, {"hundred", 100}, {"thousand", 1000}, {"million", 1000000}
    };

    private static readonly string[] NumberWords = {
        "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine",
        "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen"
    };

    private static readonly string[] TensWords = {
        "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"
    };

    /// <summary>
    /// Converts a Roman numeral to decimal.
    /// </summary>
    public static long RomanToDecimal(string roman)
    {
        if (string.IsNullOrEmpty(roman))
            return 0;

        long result = 0;
        int prevValue = 0;

        for (int i = roman.Length - 1; i >= 0; i--)
        {
            if (!RomanNumeralMap.TryGetValue(roman[i], out int value))
                throw new ArgumentException($"Invalid Roman numeral character: {roman[i]}");

            if (value < prevValue)
                result -= value;
            else
                result += value;

            prevValue = value;
        }

        return result;
    }

    /// <summary>
    /// Converts a decimal number to Roman numeral.
    /// </summary>
    public static string DecimalToRoman(long number)
    {
        if (number <= 0 || number > 3999)
            throw new ArgumentException("Number must be between 1 and 3999 for Roman numerals");

        var result = new System.Text.StringBuilder();
        
        foreach (var kvp in DecimalToRomanMap)
        {
            while (number >= kvp.Key)
            {
                result.Append(kvp.Value);
                number -= kvp.Key;
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Converts written words to a number.
    /// </summary>
    public static long WordsToNumber(string words)
    {
        if (string.IsNullOrEmpty(words))
            return 0;

        // Simple implementation for basic numbers
        words = words.ToLower().Trim();
        
        if (WordToNumberMap.TryGetValue(words, out long simpleNumber))
            return simpleNumber;

        // Handle compound numbers like "twenty-one", "thirty-five", etc.
        var parts = words.Split(new[] {' ', '-'}, StringSplitOptions.RemoveEmptyEntries);
        long total = 0;
        long current = 0;

        foreach (string part in parts)
        {
            if (WordToNumberMap.TryGetValue(part, out long value))
            {
                if (value == 100)
                {
                    current = current == 0 ? 100 : current * 100;
                }
                else if (value == 1000)
                {
                    total += (current == 0 ? 1 : current) * 1000;
                    current = 0;
                }
                else if (value == 1000000)
                {
                    total += (current == 0 ? 1 : current) * 1000000;
                    current = 0;
                }
                else
                {
                    current += value;
                }
            }
        }

        return total + current;
    }

    /// <summary>
    /// Converts a number to written words.
    /// </summary>
    public static string NumberToWords(long number)
    {
        if (number == 0)
            return "zero";

        if (number < 0)
            return "negative " + NumberToWords(Math.Abs(number));

        string words = "";

        if (number / 1000000 > 0)
        {
            words += NumberToWords(number / 1000000) + " million ";
            number %= 1000000;
        }

        if (number / 1000 > 0)
        {
            words += NumberToWords(number / 1000) + " thousand ";
            number %= 1000;
        }

        if (number / 100 > 0)
        {
            words += NumberToWords(number / 100) + " hundred ";
            number %= 100;
        }

        if (number > 0)
        {
            if (words != "")
                words += "and ";

            if (number < 20)
                words += NumberWords[number];
            else
            {
                words += TensWords[number / 10];
                if (number % 10 > 0)
                    words += "-" + NumberWords[number % 10];
            }
        }

        return words.Trim();
    }

    /// <summary>
    /// Converts a number to ordinal format (1st, 2nd, 3rd, etc.).
    /// </summary>
    public static string NumberToOrdinal(long number)
    {
        if (number <= 0)
            return number.ToString();

        switch (number % 100)
        {
            case 11:
            case 12:
            case 13:
                return number + "th";
        }

        switch (number % 10)
        {
            case 1:
                return number + "st";
            case 2:
                return number + "nd";
            case 3:
                return number + "rd";
            default:
                return number + "th";
        }
    }

    /// <summary>
    /// Checks if a number is in the Fibonacci sequence.
    /// </summary>
    public static bool IsFibonacci(long number)
    {
        if (number < 0) return false;
        if (number == 0 || number == 1) return true;

        long a = 0, b = 1;
        while (b < number)
        {
            long temp = a + b;
            a = b;
            b = temp;
        }

        return b == number;
    }

    /// <summary>
    /// Gets the next Fibonacci number after the given number.
    /// </summary>
    public static long GetNextFibonacci(long number)
    {
        if (number < 0) return 0;
        if (number == 0) return 1;

        long a = 0, b = 1;
        while (b <= number)
        {
            long temp = a + b;
            a = b;
            b = temp;
        }

        return b;
    }

    /// <summary>
    /// Checks if a number is prime.
    /// </summary>
    public static bool IsPrime(long number)
    {
        if (number <= 1) return false;
        if (number == 2) return true;
        if (number % 2 == 0) return false;

        long sqrt = (long)Math.Sqrt(number);
        for (long i = 3; i <= sqrt; i += 2)
        {
            if (number % i == 0)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Gets the next prime number after the given number.
    /// </summary>
    public static long GetNextPrime(long number)
    {
        if (number < 2) return 2;
        
        long candidate = number + 1;
        while (!IsPrime(candidate))
            candidate++;
            
        return candidate;
    }

    /// <summary>
    /// Formats a number according to the specified pattern.
    /// </summary>
    public static string FormatNumber(long number, CountingPattern pattern, int numberBase = 10)
    {
        return pattern switch
        {
            CountingPattern.Normal when numberBase == 10 => number.ToString(),
            CountingPattern.Normal => Convert.ToString(number, numberBase),
            CountingPattern.Roman => DecimalToRoman(number),
            CountingPattern.Binary => Convert.ToString(number, 2),
            CountingPattern.Hexadecimal => Convert.ToString(number, 16).ToUpper(),
            CountingPattern.Words => NumberToWords(number),
            CountingPattern.Ordinal => NumberToOrdinal(number),
            CountingPattern.Fibonacci => IsFibonacci(number) ? number.ToString() : GetNextFibonacci(number - 1).ToString(),
            CountingPattern.Primes => IsPrime(number) ? number.ToString() : GetNextPrime(number - 1).ToString(),
            _ => number.ToString()
        };
    }

    /// <summary>
    /// Validates if a text matches the expected pattern format.
    /// </summary>
    public static bool ValidatePattern(string text, CountingPattern pattern)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return pattern switch
        {
            CountingPattern.Normal => Regex.IsMatch(text.Trim(), @"^\d+$"),
            CountingPattern.Roman => Regex.IsMatch(text.Trim(), @"^[IVXLCDM]+$", RegexOptions.IgnoreCase),
            CountingPattern.Binary => Regex.IsMatch(text.Trim(), @"^[01]+$"),
            CountingPattern.Hexadecimal => Regex.IsMatch(text.Trim(), @"^[0-9A-Fa-f]+$"),
            CountingPattern.Words => Regex.IsMatch(text.Trim(), @"^[a-zA-Z\s\-]+$"),
            CountingPattern.Ordinal => Regex.IsMatch(text.Trim(), @"^\d+(st|nd|rd|th)$", RegexOptions.IgnoreCase),
            _ => false
        };
    }
}