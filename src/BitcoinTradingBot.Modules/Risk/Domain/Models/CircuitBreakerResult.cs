namespace BitcoinTradingBot.Modules.Risk.Domain.Models;

/// <summary>
/// Result of circuit breaker evaluation
/// </summary>
public record CircuitBreakerResult
{
    /// <summary>
    /// Whether any circuit breakers are triggered
    /// </summary>
    public bool IsTriggered { get; init; }

    /// <summary>
    /// List of triggered circuit breakers
    /// </summary>
    public List<CircuitBreakerTrigger> TriggeredBreakers { get; init; } = new();

    /// <summary>
    /// Severity level of the most critical trigger
    /// </summary>
    public CircuitBreakerSeverity MaxSeverity { get; init; }

    /// <summary>
    /// Recommended actions to take
    /// </summary>
    public List<string> RecommendedActions { get; init; } = new();

    /// <summary>
    /// Time when circuit breakers should be reset (if applicable)
    /// </summary>
    public DateTime? ResetTime { get; init; }

    /// <summary>
    /// Cool-down period in minutes
    /// </summary>
    public int CooldownMinutes { get; init; }

    /// <summary>
    /// Current system state
    /// </summary>
    public SystemState SystemState { get; init; }

    /// <summary>
    /// Additional metadata about the circuit breaker evaluation
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Timestamp when evaluation was performed
    /// </summary>
    public DateTime EvaluatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Create result with no circuit breakers triggered
    /// </summary>
    public static CircuitBreakerResult Normal()
    {
        return new CircuitBreakerResult
        {
            IsTriggered = false,
            SystemState = SystemState.Normal
        };
    }

    /// <summary>
    /// Create result with circuit breakers triggered
    /// </summary>
    public static CircuitBreakerResult Triggered(
        List<CircuitBreakerTrigger> triggers,
        int cooldownMinutes)
    {
        var maxSeverity = triggers.Any() 
            ? triggers.Max(t => t.Severity) 
            : CircuitBreakerSeverity.Low;

        return new CircuitBreakerResult
        {
            IsTriggered = true,
            TriggeredBreakers = triggers,
            MaxSeverity = maxSeverity,
            CooldownMinutes = cooldownMinutes,
            ResetTime = DateTime.UtcNow.AddMinutes(cooldownMinutes),
            SystemState = maxSeverity >= CircuitBreakerSeverity.Critical 
                ? SystemState.Emergency 
                : SystemState.Restricted
        };
    }
}

/// <summary>
/// Individual circuit breaker trigger
/// </summary>
public record CircuitBreakerTrigger
{
    public string Name { get; init; } = string.Empty;
    public CircuitBreakerSeverity Severity { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal TriggerValue { get; init; }
    public decimal ThresholdValue { get; init; }
    public DateTime TriggeredAt { get; init; } = DateTime.UtcNow;
    public Dictionary<string, object> Details { get; init; } = new();
}

/// <summary>
/// Circuit breaker severity levels
/// </summary>
public enum CircuitBreakerSeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3,
    Emergency = 4
}

/// <summary>
/// System operational states
/// </summary>
public enum SystemState
{
    Normal = 0,
    Restricted = 1,
    Emergency = 2,
    Halted = 3
} 