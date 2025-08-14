namespace Mewdeko.Modules.Counting.Common;

/// <summary>
/// Represents different counting patterns/modes.
/// </summary>
public enum CountingPattern
{
    /// <summary>
    /// Normal decimal counting (1, 2, 3, 4...).
    /// </summary>
    Normal = 0,
    
    /// <summary>
    /// Roman numeral counting (I, II, III, IV...).
    /// </summary>
    Roman = 1,
    
    /// <summary>
    /// Binary counting (1, 10, 11, 100...).
    /// </summary>
    Binary = 2,
    
    /// <summary>
    /// Hexadecimal counting (1, 2, 3... 9, A, B, C...).
    /// </summary>
    Hexadecimal = 3,
    
    /// <summary>
    /// Word counting (one, two, three, four...).
    /// </summary>
    Words = 4,
    
    /// <summary>
    /// Ordinal counting (1st, 2nd, 3rd, 4th...).
    /// </summary>
    Ordinal = 5,
    
    /// <summary>
    /// Fibonacci sequence counting (1, 1, 2, 3, 5, 8...).
    /// </summary>
    Fibonacci = 6,
    
    /// <summary>
    /// Prime numbers counting (2, 3, 5, 7, 11...).
    /// </summary>
    Primes = 7,
    
    /// <summary>
    /// Custom counting pattern defined by server.
    /// </summary>
    Custom = 8
}

/// <summary>
/// Represents different types of counting events.
/// </summary>
public enum CountingEventType
{
    /// <summary>
    /// A user successfully counted the correct number.
    /// </summary>
    SuccessfulCount = 0,
    
    /// <summary>
    /// A user submitted an incorrect number.
    /// </summary>
    WrongNumber = 1,
    
    /// <summary>
    /// The count was manually reset by a moderator.
    /// </summary>
    ManualReset = 2,
    
    /// <summary>
    /// The count was automatically reset due to an error.
    /// </summary>
    AutoReset = 3,
    
    /// <summary>
    /// A new counting channel was set up.
    /// </summary>
    ChannelSetup = 4,
    
    /// <summary>
    /// A counting channel was deleted or disabled.
    /// </summary>
    ChannelDeleted = 5,
    
    /// <summary>
    /// A milestone number was reached.
    /// </summary>
    MilestoneReached = 6,
    
    /// <summary>
    /// The maximum configured number was reached.
    /// </summary>
    MaxNumberReached = 7,
    
    /// <summary>
    /// A save point was created for the counting channel.
    /// </summary>
    SaveCreated = 8,
    
    /// <summary>
    /// The count was restored from a save point.
    /// </summary>
    SaveRestored = 9,
    
    /// <summary>
    /// A user was put on timeout for counting violations.
    /// </summary>
    UserTimeout = 10,
    
    /// <summary>
    /// A user was banned from participating in counting.
    /// </summary>
    UserBanned = 11,
    
    /// <summary>
    /// The counting channel configuration was changed.
    /// </summary>
    ConfigChanged = 12
}

/// <summary>
/// Represents different types of counting errors.
/// </summary>
public enum CountingError
{
    /// <summary>
    /// No error occurred.
    /// </summary>
    None = 0,
    
    /// <summary>
    /// Counting is not set up in this channel.
    /// </summary>
    NotSetup = 1,
    
    /// <summary>
    /// Counting configuration was not found for this channel.
    /// </summary>
    ConfigNotFound = 2,
    
    /// <summary>
    /// The submitted text is not a valid number format.
    /// </summary>
    InvalidNumber = 3,
    
    /// <summary>
    /// The submitted number is not the expected next number.
    /// </summary>
    WrongNumber = 4,
    
    /// <summary>
    /// The same user tried to count consecutively when not allowed.
    /// </summary>
    SameUserRepeating = 5,
    
    /// <summary>
    /// The user is currently on cooldown and cannot count yet.
    /// </summary>
    OnCooldown = 6,
    
    /// <summary>
    /// The maximum configured number has been reached.
    /// </summary>
    MaxNumberReached = 7,
    
    /// <summary>
    /// The user is banned from participating in counting.
    /// </summary>
    UserBanned = 8,
    
    /// <summary>
    /// The user doesn't have the required role to participate.
    /// </summary>
    RoleRequired = 9,
    
    /// <summary>
    /// The counting channel is currently inactive.
    /// </summary>
    ChannelInactive = 10
}

/// <summary>
/// Represents different types of milestones.
/// </summary>
public enum MilestoneType
{
    /// <summary>
    /// Custom milestone defined by server administrators.
    /// </summary>
    Custom = 0,
    
    /// <summary>
    /// Milestone at every 100th count.
    /// </summary>
    Hundred = 100,
    
    /// <summary>
    /// Milestone at every 500th count.
    /// </summary>
    FiveHundred = 500,
    
    /// <summary>
    /// Milestone at every 1,000th count.
    /// </summary>
    Thousand = 1000,
    
    /// <summary>
    /// Milestone at every 5,000th count.
    /// </summary>
    FiveThousand = 5000,
    
    /// <summary>
    /// Milestone at every 10,000th count.
    /// </summary>
    TenThousand = 10000,
    
    /// <summary>
    /// Milestone at every 50,000th count.
    /// </summary>
    FiftyThousand = 50000,
    
    /// <summary>
    /// Milestone at every 100,000th count.
    /// </summary>
    HundredThousand = 100000,
    
    /// <summary>
    /// Milestone at every 1,000,000th count.
    /// </summary>
    Million = 1000000
}