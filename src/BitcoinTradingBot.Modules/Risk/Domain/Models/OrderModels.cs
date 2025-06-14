using BitcoinTradingBot.Core;
using BitcoinTradingBot.Core.Models;

namespace BitcoinTradingBot.Modules.Risk.Domain.Models;

/// <summary>
/// Result of order validation
/// </summary>
public record OrderValidationResult
{
    public bool IsValid { get; init; }
    public List<string> ValidationErrors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public RiskLevel RiskLevel { get; init; }
    public decimal RiskScore { get; init; }
    public Dictionary<string, object> ValidationDetails { get; init; } = new();
    public DateTime ValidatedAt { get; init; } = DateTime.UtcNow;

    public static OrderValidationResult Success(RiskLevel riskLevel = RiskLevel.Low, decimal riskScore = 0)
    {
        return new OrderValidationResult
        {
            IsValid = true,
            RiskLevel = riskLevel,
            RiskScore = riskScore
        };
    }

    public static OrderValidationResult Failure(List<string> errors, RiskLevel riskLevel = RiskLevel.High)
    {
        return new OrderValidationResult
        {
            IsValid = false,
            ValidationErrors = errors,
            RiskLevel = riskLevel,
            RiskScore = 100m
        };
    }
}

/// <summary>
/// Slippage calculation result
/// </summary>
public record SlippageCalculation
{
    public decimal MaxAcceptableSlippage { get; init; }
    public decimal ExpectedSlippage { get; init; }
    public decimal WorstCaseSlippage { get; init; }
    public Price RecommendedLimitPrice { get; init; } = Price.Zero();
    public SlippageReason PrimaryReason { get; init; }
    public Dictionary<string, decimal> SlippageFactors { get; init; } = new();
    public DateTime CalculatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Order type recommendation
/// </summary>
public record OrderTypeRecommendation
{
    public OrderType RecommendedType { get; init; }
    public Price? LimitPrice { get; init; }
    public Price? StopPrice { get; init; }
    public TimeInForce TimeInForce { get; init; }
    public string Reasoning { get; init; } = string.Empty;
    public decimal ConfidenceLevel { get; init; }
    public List<OrderTypeAlternative> Alternatives { get; init; } = new();
    public Dictionary<string, object> Parameters { get; init; } = new();
}

/// <summary>
/// Alternative order type suggestion
/// </summary>
public record OrderTypeAlternative
{
    public OrderType Type { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal Score { get; init; }
    public List<string> Pros { get; init; } = new();
    public List<string> Cons { get; init; } = new();
}

/// <summary>
/// Order execution quality analysis
/// </summary>
public record ExecutionQualityResult
{
    public bool IsGoodExecution { get; init; }
    public decimal ActualSlippage { get; init; }
    public decimal SlippageCost { get; init; }
    public Price ExecutionPrice { get; init; } = Price.Zero();
    public TimeSpan ExecutionTime { get; init; }
    public ExecutionQuality Quality { get; init; }
    public List<string> QualityIssues { get; init; } = new();
    public Dictionary<string, object> ExecutionMetrics { get; init; } = new();
    public DateTime AnalyzedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Order execution result from exchange
/// </summary>
public record OrderExecutionResult
{
    public string OrderId { get; init; } = string.Empty;
    public OrderStatus Status { get; init; }
    public Price ExecutedPrice { get; init; } = Price.Zero();
    public Quantity ExecutedQuantity { get; init; } = Quantity.Zero();
    public Quantity RemainingQuantity { get; init; } = Quantity.Zero();
    public Price AveragePrice { get; init; } = Price.Zero();
    public decimal TotalFees { get; init; }
    public DateTime ExecutedAt { get; init; } = DateTime.UtcNow;
    public TimeSpan ExecutionTime { get; init; }
    public List<ExecutionFill> Fills { get; init; } = new();
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Individual fill in order execution
/// </summary>
public record ExecutionFill
{
    public Price FillPrice { get; init; } = Price.Zero();
    public Quantity FillQuantity { get; init; } = Quantity.Zero();
    public decimal FillFee { get; init; }
    public DateTime FillTime { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Order cancellation recommendation
/// </summary>
public record OrderCancellationResult
{
    public bool ShouldCancel { get; init; }
    public CancellationReason Reason { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal UrgencyScore { get; init; } // 0-100
    public TimeSpan RecommendedDelay { get; init; }
    public List<string> Considerations { get; init; } = new();
    public DateTime EvaluatedAt { get; init; } = DateTime.UtcNow;

    public static OrderCancellationResult KeepOrder(string reason = "")
    {
        return new OrderCancellationResult
        {
            ShouldCancel = false,
            Description = reason
        };
    }

    public static OrderCancellationResult CancelOrder(CancellationReason reason, string description, decimal urgency = 50m)
    {
        return new OrderCancellationResult
        {
            ShouldCancel = true,
            Reason = reason,
            Description = description,
            UrgencyScore = urgency
        };
    }
}

/// <summary>
/// Position closure recommendation
/// </summary>
public record PositionClosureResult
{
    public bool ShouldClose { get; init; }
    public ClosureReason Reason { get; init; }
    public decimal PercentageToClose { get; init; } = 100m; // Default to full closure
    public ClosureUrgency Urgency { get; init; }
    public string Description { get; init; } = string.Empty;
    public Price? RecommendedPrice { get; init; }
    public OrderType RecommendedOrderType { get; init; }
    public List<string> RiskFactors { get; init; } = new();
    public DateTime EvaluatedAt { get; init; } = DateTime.UtcNow;

    public static PositionClosureResult KeepOpen(string reason = "")
    {
        return new PositionClosureResult
        {
            ShouldClose = false,
            Description = reason
        };
    }

    public static PositionClosureResult Close(ClosureReason reason, string description, decimal percentage = 100m, ClosureUrgency urgency = ClosureUrgency.Normal)
    {
        return new PositionClosureResult
        {
            ShouldClose = true,
            Reason = reason,
            Description = description,
            PercentageToClose = percentage,
            Urgency = urgency
        };
    }
}

// Enums
public enum OrderUrgency
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

public enum SlippageReason
{
    LowLiquidity,
    HighVolatility,
    LargeOrderSize,
    MarketConditions,
    Spread,
    TimeOfDay
}

public enum ExecutionQuality
{
    Excellent = 0,
    Good = 1,
    Average = 2,
    Poor = 3,
    Terrible = 4
}

public enum CancellationReason
{
    MarketConditionsChanged,
    VolatilityTooHigh,
    LiquidityTooLow,
    RiskLimitsExceeded,
    TechnicalIndicatorsDiverged,
    TimeoutReached,
    EmergencyStop
}

public enum ClosureReason
{
    StopLossHit,
    TakeProfitHit,
    RiskLimitExceeded,
    MarketConditionsChanged,
    TimeStop,
    EmergencyExit,
    VolatilitySpike,
    DrawdownProtection,
    ManualOverride
}

public enum ClosureUrgency
{
    Low = 0,
    Normal = 1,
    High = 2,
    Emergency = 3
} 