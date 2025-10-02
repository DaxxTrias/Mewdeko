﻿namespace Mewdeko.Common;

/// <summary>
///     Represents a smart number that can be implicitly converted to and from ulong and uint.
/// </summary>
public readonly struct ShmartNumber : IEquatable<ShmartNumber>
{
    /// <summary>
    ///     Gets the value of the ShmartNumber.
    /// </summary>
    public ulong Value { get; }

    /// <summary>
    ///     Gets the input string of the ShmartNumber.
    /// </summary>
    public string Input { get; }

    /// <summary>
    ///     Initializes a new instance of the ShmartNumber struct with the specified value and input string.
    /// </summary>
    /// <param name="val">The value of the ShmartNumber.</param>
    /// <param name="input">The input string of the ShmartNumber.</param>
    public ShmartNumber(ulong val, string? input = null)
    {
        Value = val;
        Input = input ?? string.Empty;
    }

    /// <summary>
    ///     Implicitly converts an unsigned long integer to a ShmartNumber.
    /// </summary>
    /// <param name="num">The num identifier.</param>
    public static implicit operator ShmartNumber(ulong num)
    {
        return new ShmartNumber(num);
    }

    /// <summary>
    ///     Implicitly converts a ShmartNumber to an unsigned long integer.
    /// </summary>
    /// <param name="num">The num parameter.</param>
    public static implicit operator ulong(ShmartNumber num)
    {
        return num.Value;
    }

    /// <summary>
    ///     Implicitly converts an unsigned integer to a ShmartNumber.
    /// </summary>
    /// <param name="num">The num parameter.</param>
    public static implicit operator ShmartNumber(uint num)
    {
        return new ShmartNumber(num);
    }

    /// <summary>
    ///     Returns a string that represents the current ShmartNumber.
    /// </summary>
    public override string ToString()
    {
        return Value.ToString();
    }

    /// <summary>
    ///     Determines whether the specified object is equal to the current ShmartNumber.
    /// </summary>
    /// <param name="obj">The obj parameter.</param>
    public override bool Equals(object? obj)
    {
        return obj is ShmartNumber sn && Equals(sn);
    }

    /// <summary>
    ///     Indicates whether the current ShmartNumber is equal to another ShmartNumber.
    /// </summary>
    /// <param name="other">The other parameter.</param>
    public bool Equals(ShmartNumber other)
    {
        return other.Value == Value;
    }

    /// <summary>
    ///     Returns the hash code for the current ShmartNumber.
    /// </summary>
    public override int GetHashCode()
    {
        return Value.GetHashCode() ^ Input.GetHashCode(StringComparison.InvariantCulture);
    }

    /// <summary>
    ///     Determines whether two ShmartNumber objects are equal.
    /// </summary>
    public static bool operator ==(ShmartNumber left, ShmartNumber right)
    {
        return left.Equals(right);
    }

    /// <summary>
    ///     Determines whether two ShmartNumber objects are not equal.
    /// </summary>
    public static bool operator !=(ShmartNumber left, ShmartNumber right)
    {
        return !(left == right);
    }
}