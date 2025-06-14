namespace BitcoinTradingBot.Modules.Risk.Domain.Models;

/// <summary>
/// Result of risk validation for a trading signal
/// </summary>
public record RiskValidationResult
{
    /// <summary>
    /// Whether the trade passes all risk checks
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Overall risk score (0-100, lower is better)
    /// </summary>
    public decimal RiskScore { get; init; }

    /// <summary>
    /// Individual risk check results
    /// </summary>
    public Dictionary<string, RiskCheckResult> RiskChecks { get; init; } = new();

    /// <summary>
    /// Reasons why validation failed (if any)
    /// </summary>
    public List<string> ValidationFailures { get; init; } = new();

    /// <summary>
    /// Risk level assessment
    /// </summary>
    public RiskLevel RiskLevel { get; init; }

    /// <summary>
    /// Recommended actions based on risk assessment
    /// </summary>
    public List<string> RecommendedActions { get; init; } = new();

    /// <summary>
    /// Maximum recommended position size
    /// </summary>
    public decimal MaxRecommendedPositionSize { get; init; }

    /// <summary>
    /// Confidence level in the risk assessment (0-100)
    /// </summary>
    public decimal ConfidenceLevel { get; init; }

    /// <summary>
    /// Validation metadata and calculations
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Timestamp when validation was performed
    /// </summary>
    public DateTime ValidatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Create successful validation result
    /// </summary>
    public static RiskValidationResult Success(
        decimal riskScore,
        RiskLevel riskLevel,
        decimal maxRecommendedPositionSize,
        decimal confidenceLevel = 95m)
    {
        return new RiskValidationResult
        {
            IsValid = true,
            RiskScore = riskScore,
            RiskLevel = riskLevel,
            MaxRecommendedPositionSize = maxRecommendedPositionSize,
            ConfidenceLevel = confidenceLevel
        };
    }

    /// <summary>
    /// Create failed validation result
    /// </summary>
    public static RiskValidationResult Failure(
        List<string> failures,
        RiskLevel riskLevel = RiskLevel.High,
        decimal riskScore = 100m)
    {
        return new RiskValidationResult
        {
            IsValid = false,
            RiskScore = riskScore,
            RiskLevel = riskLevel,
            ValidationFailures = failures,
            MaxRecommendedPositionSize = 0m
        };
    }
}

/// <summary>
/// Individual risk check result
/// </summary>
public record RiskCheckResult
{
    public bool Passed { get; init; }
    public string CheckName { get; init; } = string.Empty;
    public decimal Score { get; init; }
    public string? Message { get; init; }
    public Dictionary<string, object> Details { get; init; } = new();
}

/// <summary>
/// Risk level enumeration
/// </summary>
public enum RiskLevel
{
    VeryLow = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    VeryHigh = 4,
    Extreme = 5
} 