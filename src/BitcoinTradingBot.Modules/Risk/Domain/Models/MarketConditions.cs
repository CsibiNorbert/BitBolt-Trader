using BitcoinTradingBot.Core;
using BitcoinTradingBot.Core.Models;
using BitcoinTradingBot.Modules.Strategy.Domain.Indicators;

namespace BitcoinTradingBot.Modules.Risk.Domain.Models;

/// <summary>
/// Current market conditions for risk assessment
/// </summary>
public record MarketConditions
{
    /// <summary>
    /// Current price of the asset
    /// </summary>
    public required Price CurrentPrice { get; init; }

    /// <summary>
    /// Current volatility (e.g., ATR-based)
    /// </summary>
    public decimal Volatility { get; init; }

    /// <summary>
    /// Market liquidity indicator (0-100)
    /// </summary>
    public decimal Liquidity { get; init; }

    /// <summary>
    /// Bid-ask spread percentage
    /// </summary>
    public decimal BidAskSpread { get; init; }

    /// <summary>
    /// Recent trading volume (last 24h)
    /// </summary>
    public decimal Volume24h { get; init; }

    /// <summary>
    /// Volume-weighted average price
    /// </summary>
    public Price VWAP { get; init; } = Price.Zero();

    /// <summary>
    /// Current market trend direction
    /// </summary>
    public MarketTrend Trend { get; init; }

    /// <summary>
    /// Market sentiment indicator (-100 to +100)
    /// </summary>
    public decimal Sentiment { get; init; }

    /// <summary>
    /// Fear and greed index (0-100)
    /// </summary>
    public decimal FearGreedIndex { get; init; }

    /// <summary>
    /// Volatility regime classification
    /// </summary>
    public VolatilityRegime VolatilityRegime { get; init; }

    /// <summary>
    /// Market session (Asian, European, US)
    /// </summary>
    public MarketSession Session { get; init; }

    /// <summary>
    /// Time until next major economic event (minutes)
    /// </summary>
    public int? NextEventMinutes { get; init; }

    /// <summary>
    /// Support levels
    /// </summary>
    public List<Price> SupportLevels { get; init; } = new();

    /// <summary>
    /// Resistance levels
    /// </summary>
    public List<Price> ResistanceLevels { get; init; } = new();

    /// <summary>
    /// Key technical indicators
    /// </summary>
    public Dictionary<string, decimal> TechnicalIndicators { get; init; } = new();

    /// <summary>
    /// Market anomalies detected
    /// </summary>
    public List<MarketAnomaly> Anomalies { get; init; } = new();

    /// <summary>
    /// Timestamp when conditions were assessed
    /// </summary>
    public DateTime AssessedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Calculate overall market risk score
    /// </summary>
    public decimal CalculateMarketRiskScore()
    {
        var riskScore = 0m;
        
        // Volatility risk (0-40 points)
        riskScore += VolatilityRegime switch
        {
            VolatilityRegime.Low => 5m,
            VolatilityRegime.Normal => 15m,
            VolatilityRegime.High => 30m,
            VolatilityRegime.Extreme => 40m,
            _ => 20m
        };
        
        // Liquidity risk (0-20 points)
        riskScore += Liquidity switch
        {
            >= 80 => 2m,
            >= 60 => 5m,
            >= 40 => 10m,
            >= 20 => 15m,
            _ => 20m
        };
        
        // Spread risk (0-20 points)
        riskScore += BidAskSpread switch
        {
            <= 0.001m => 2m,
            <= 0.005m => 5m,
            <= 0.01m => 10m,
            <= 0.02m => 15m,
            _ => 20m
        };
        
        // Event risk (0-20 points)
        if (NextEventMinutes.HasValue && NextEventMinutes < 60)
            riskScore += 20m;
        else if (NextEventMinutes.HasValue && NextEventMinutes < 240)
            riskScore += 10m;
        
        return Math.Min(riskScore, 100m);
    }

    /// <summary>
    /// Check if conditions are suitable for trading
    /// </summary>
    public bool IsSuitableForTrading()
    {
        return Liquidity >= 40 &&
               BidAskSpread <= 0.02m &&
               VolatilityRegime != VolatilityRegime.Extreme &&
               !Anomalies.Any(a => a.Severity >= AnomalySeverity.High);
    }
}

/// <summary>
/// Market trend direction
/// </summary>
public enum MarketTrend
{
    StrongBearish = -2,
    Bearish = -1,
    Sideways = 0,
    Bullish = 1,
    StrongBullish = 2
}

/// <summary>
/// Market trading sessions
/// </summary>
public enum MarketSession
{
    Asian,
    European,
    US,
    Overlap
}

/// <summary>
/// Market anomaly detection
/// </summary>
public record MarketAnomaly
{
    public string Type { get; init; } = string.Empty;
    public AnomalySeverity Severity { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal Impact { get; init; }
    public DateTime DetectedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Anomaly severity levels
/// </summary>
public enum AnomalySeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
} 