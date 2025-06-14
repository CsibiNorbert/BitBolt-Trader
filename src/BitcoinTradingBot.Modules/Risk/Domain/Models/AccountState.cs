using BitcoinTradingBot.Core;
using BitcoinTradingBot.Core.Models;

namespace BitcoinTradingBot.Modules.Risk.Domain.Models;

/// <summary>
/// Current state of the trading account for risk management
/// </summary>
public record AccountState
{
    /// <summary>
    /// Current account equity (total value)
    /// </summary>
    public required Price TotalEquity { get; init; }

    /// <summary>
    /// Available balance for trading
    /// </summary>
    public required Price AvailableBalance { get; init; }

    /// <summary>
    /// Total value of open positions
    /// </summary>
    public Price PositionValue { get; init; } = Price.Zero();

    /// <summary>
    /// Total unrealized P&L
    /// </summary>
    public Price UnrealizedPnL { get; init; } = Price.Zero();

    /// <summary>
    /// Total realized P&L for the day
    /// </summary>
    public Price DailyRealizedPnL { get; init; } = Price.Zero();

    /// <summary>
    /// Current drawdown from peak equity
    /// </summary>
    public decimal CurrentDrawdown { get; init; }

    /// <summary>
    /// Maximum drawdown reached today
    /// </summary>
    public decimal MaxIntradayDrawdown { get; init; }

    /// <summary>
    /// Peak equity reached (high water mark)
    /// </summary>
    public required Price PeakEquity { get; init; }

    /// <summary>
    /// Number of open positions
    /// </summary>
    public int OpenPositionCount { get; init; }

    /// <summary>
    /// List of current open positions
    /// </summary>
    public List<Position> OpenPositions { get; init; } = new();

    /// <summary>
    /// Total exposure as percentage of equity
    /// </summary>
    public decimal TotalExposurePercentage { get; init; }

    /// <summary>
    /// Number of trades executed today
    /// </summary>
    public int DailyTradeCount { get; init; }

    /// <summary>
    /// Last trade execution time
    /// </summary>
    public DateTime? LastTradeTime { get; init; }

    /// <summary>
    /// Current margin usage (if applicable)
    /// </summary>
    public decimal MarginUsage { get; init; }

    /// <summary>
    /// Account performance metrics
    /// </summary>
    public AccountPerformanceMetrics Performance { get; init; } = new();

    /// <summary>
    /// Risk metrics
    /// </summary>
    public AccountRiskMetrics RiskMetrics { get; init; } = new();

    /// <summary>
    /// Timestamp when state was captured
    /// </summary>
    public DateTime CapturedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Calculate current risk exposure
    /// </summary>
    public decimal CalculateRiskExposure()
    {
        if (TotalEquity.Value <= 0) return 0;
        
        var totalRisk = OpenPositions
            .Where(p => p.StopLoss != null)
            .Sum(p => Math.Abs((p.EntryPrice.Value - p.StopLoss!.Value) * p.Quantity.Value));
        
        return totalRisk / TotalEquity.Value;
    }

    /// <summary>
    /// Check if account is within risk limits
    /// </summary>
    public bool IsWithinRiskLimits(RiskParameters riskParams)
    {
        return CurrentDrawdown <= riskParams.MaxIntradayDrawdown &&
               TotalExposurePercentage <= riskParams.MaxPortfolioExposure &&
               OpenPositionCount <= riskParams.MaxOpenPositions &&
               Math.Abs(DailyRealizedPnL.Value) / TotalEquity.Value <= riskParams.MaxDailyLoss;
    }
}

/// <summary>
/// Account performance metrics
/// </summary>
public record AccountPerformanceMetrics
{
    public decimal WinRate { get; init; }
    public decimal ProfitFactor { get; init; }
    public decimal SharpeRatio { get; init; }
    public decimal MaxDrawdown { get; init; }
    public decimal AverageWin { get; init; }
    public decimal AverageLoss { get; init; }
    public int TotalTrades { get; init; }
    public int WinningTrades { get; init; }
    public int LosingTrades { get; init; }
}

/// <summary>
/// Account risk metrics
/// </summary>
public record AccountRiskMetrics
{
    public decimal VaR95 { get; init; } // Value at Risk 95%
    public decimal ExpectedShortfall { get; init; }
    public decimal VolatilityAnnualized { get; init; }
    public decimal BetaToMarket { get; init; }
    public decimal MaxLeverageUsed { get; init; }
    public decimal ConcentrationRisk { get; init; }
} 