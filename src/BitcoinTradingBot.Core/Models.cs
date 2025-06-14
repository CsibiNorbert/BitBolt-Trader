using BitcoinTradingBot.Core;

namespace BitcoinTradingBot.Core.Models;

/// <summary>
/// Represents a market data candle/kline
/// </summary>
public record Candle(
    Symbol Symbol,
    TimeFrame TimeFrame,
    DateTime OpenTime,
    Price Open,
    Price High,
    Price Low,
    Price Close,
    Quantity Volume,
    DateTime CloseTime
)
{
    /// <summary>
    /// Calculates if this candle is bullish (close > open)
    /// </summary>
    public bool IsBullish => Close > Open;
    
    /// <summary>
    /// Calculates if this candle is bearish (close < open)
    /// </summary>
    public bool IsBearish => Close < Open;
    
    /// <summary>
    /// Gets the body size (absolute difference between open and close)
    /// </summary>
    public decimal BodySize => Math.Abs(Close - Open);
    
    /// <summary>
    /// Gets the upper shadow size
    /// </summary>
    public decimal UpperShadow => High - Math.Max(Open, Close);
    
    /// <summary>
    /// Gets the lower shadow size
    /// </summary>
    public decimal LowerShadow => Math.Min(Open, Close) - Low;
}

/// <summary>
/// Represents trading signal data with all relevant information for decision making
/// </summary>
public record TradingSignalData(
    string Id,
    Symbol Symbol,
    TradingSignal Signal,
    Price EntryPrice,
    Price? StopLoss,
    Price? TakeProfit,
    decimal Confidence,
    DateTime SignalTime,
    Dictionary<string, object> Metadata
)
{
    /// <summary>
    /// Calculates risk-reward ratio if both stop loss and take profit are set
    /// </summary>
    public decimal? RiskRewardRatio
    {
        get
        {
            if (StopLoss == null || TakeProfit == null) return null;
            
            var risk = Math.Abs(EntryPrice - StopLoss);
            var reward = Math.Abs(TakeProfit - EntryPrice);
            
            return risk == 0 ? null : reward / risk;
        }
    }
}

/// <summary>
/// Represents position sizing information
/// </summary>
public record PositionSizing(
    Quantity Quantity,
    decimal RiskAmount,
    decimal RiskPercentage,
    decimal NotionalValue,
    string SizingMethod
);

/// <summary>
/// Represents current risk metrics
/// </summary>
public record RiskMetrics(
    decimal CurrentDrawdown,
    decimal MaxDrawdown,
    decimal TotalExposure,
    decimal AvailableCapital,
    decimal VaR95,
    DateTime LastUpdated
);

/// <summary>
/// Represents order information
/// </summary>
public record OrderInfo(
    string OrderId,
    Symbol Symbol,
    OrderSide Side,
    OrderType Type,
    Quantity OriginalQuantity,
    Quantity ExecutedQuantity,
    Price? Price,
    OrderStatus Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt
)
{
    /// <summary>
    /// Gets remaining quantity to be filled
    /// </summary>
    public Quantity RemainingQuantity => OriginalQuantity - ExecutedQuantity;
    
    /// <summary>
    /// Indicates if order is completely filled
    /// </summary>
    public bool IsFilled => Status == OrderStatus.Filled || ExecutedQuantity >= OriginalQuantity;
}

/// <summary>
/// Represents a trading position
/// </summary>
public record Position(
    string PositionId,
    Symbol Symbol,
    OrderSide Side,
    Quantity Size,
    Price EntryPrice,
    Price? StopLoss,
    Price? TakeProfit,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    decimal UnrealizedPnL,
    decimal RealizedPnL
)
{
    /// <summary>
    /// Advanced stop loss levels for risk management
    /// </summary>
    public StopLossLevels? StopLossLevels { get; init; }

    /// <summary>
    /// Indicates if position is currently open
    /// </summary>
    public bool IsOpen => ClosedAt == null;
    
    /// <summary>
    /// Gets total PnL (realized + unrealized)
    /// </summary>
    public decimal TotalPnL => RealizedPnL + UnrealizedPnL;
    
    /// <summary>
    /// Gets the position quantity (alias for Size for compatibility)
    /// </summary>
    public Quantity Quantity => Size;
}

/// <summary>
/// Represents performance metrics over a period
/// </summary>
public record PerformanceMetrics(
    decimal TotalReturn,
    decimal TotalReturnPercentage,
    decimal WinRate,
    decimal AverageWin,
    decimal AverageLoss,
    decimal AverageWinLossRatio,
    decimal ProfitFactor,
    decimal SharpeRatio,
    decimal SortinoRatio,
    decimal MaxDrawdown,
    int TotalTrades,
    int WinningTrades,
    int LosingTrades,
    DateTime PeriodStart,
    DateTime PeriodEnd
);

/// <summary>
/// Represents Keltner Channel values
/// </summary>
public record KeltnerChannel(
    Price MiddleBand,
    Price UpperBand,
    Price LowerBand,
    decimal Atr,
    DateTime Timestamp
)
{
    /// <summary>
    /// Gets the channel width
    /// </summary>
    public decimal Width => UpperBand - LowerBand;
    
    /// <summary>
    /// Calculates position of price within the channel (0 = lower band, 0.5 = middle, 1 = upper band)
    /// </summary>
    public decimal GetPositionInChannel(Price price)
    {
        if (Width == 0) return 0.5m;
        return (price - LowerBand) / Width;
    }
}

/// <summary>
/// Represents Exponential Moving Average values
/// </summary>
public record ExponentialMovingAverage(
    Price Value,
    int Period,
    DateTime Timestamp
) : ITimestampedValue;

/// <summary>
/// Represents Average True Range values
/// </summary>
public record AverageTrueRange(
    decimal Value,
    int Period,
    DateTime Timestamp
) : ITimestampedValue;

/// <summary>
/// Represents trading configuration settings
/// </summary>
public record TradingConfiguration(
    Symbol TradingSymbol,
    decimal MaxRiskPerTrade,
    decimal MaxTotalExposure,
    int KeltnerChannelPeriod,
    decimal KeltnerChannelMultiplier,
    int EmaPeriod,
    int AtrPeriod,
    TimeFrame PrimaryTimeFrame,
    TimeFrame EntryTimeFrame,
    bool IsBacktestMode,
    bool IsEnabled
);

/// <summary>
/// Represents a position sizing request with all required parameters
/// </summary>
public record PositionSizeRequest(
    Symbol Symbol,
    Price EntryPrice,
    Price StopLoss,
    Price? TakeProfit,
    decimal AccountBalance,
    decimal RiskPercentage,
    decimal? WinRate,
    decimal? AverageWinLossRatio,
    decimal? CurrentDrawdown,
    decimal? VolatilityMultiplier,
    Dictionary<string, object>? AdditionalData,
    PerformanceMetrics? HistoricalPerformance = null,
    decimal? CurrentExposure = null,
    decimal? ExchangeMinimumOrderSize = null
)
{
    /// <summary>
    /// Calculates the risk per share/unit
    /// </summary>
    public decimal RiskPerUnit => Math.Abs(EntryPrice - StopLoss);
    
    /// <summary>
    /// Calculates the reward per share/unit if take profit is set
    /// </summary>
    public decimal? RewardPerUnit => TakeProfit is not null ? Math.Abs(TakeProfit.Value - EntryPrice) : null;
    
    /// <summary>
    /// Calculates the risk-reward ratio
    /// </summary>
    public decimal? RiskRewardRatio => RewardPerUnit.HasValue && RiskPerUnit > 0 ? RewardPerUnit.Value / RiskPerUnit : null;
}

/// <summary>
/// Represents a trade result for backtesting and analysis
/// </summary>
public record TradeResult(
    string TradeId,
    Symbol Symbol,
    OrderSide Side,
    Price EntryPrice,
    Price ExitPrice,
    Quantity Quantity,
    DateTime EntryTime,
    DateTime ExitTime,
    decimal PnL,
    decimal PnLPercentage,
    decimal Fee,
    string ExitReason,
    Dictionary<string, object>? Metadata
)
{
    /// <summary>
    /// Indicates if the trade was profitable
    /// </summary>
    public bool IsWinner => PnL > 0;
    
    /// <summary>
    /// Gets the trade duration
    /// </summary>
    public TimeSpan Duration => ExitTime - EntryTime;
    
    /// <summary>
    /// Gets the gross PnL (before fees)
    /// </summary>
    public decimal GrossPnL => PnL + Fee;
    
    /// <summary>
    /// Gets the profit/loss (alias for PnL for compatibility)
    /// </summary>
    public decimal ProfitLoss => PnL;
}

/// <summary>
/// Represents an order for execution
/// </summary>
public record Order(
    string OrderId,
    Symbol Symbol,
    OrderSide Side,
    OrderType Type,
    Quantity Quantity,
    Price? Price,
    Price? StopPrice,
    TimeInForce TimeInForce,
    OrderStatus Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? ClientOrderId
)
{
    /// <summary>
    /// Gets the order ID (alias for OrderId for compatibility)
    /// </summary>
    public string Id => OrderId;
    
    /// <summary>
    /// Indicates if the order is active (can be filled)
    /// </summary>
    public bool IsActive => Status == OrderStatus.New || Status == OrderStatus.PartiallyFilled;
    
    /// <summary>
    /// Indicates if the order is a market order
    /// </summary>
    public bool IsMarketOrder => Type == OrderType.Market;
}

/// <summary>
/// Represents Keltner Channel calculation settings
/// </summary>
public record KeltnerChannelSettings(
    int EmaPeriod,
    int AtrPeriod,
    decimal Multiplier,
    decimal? DynamicMultiplierMin = null,
    decimal? DynamicMultiplierMax = null,
    bool UseDynamicMultiplier = false
)
{
    /// <summary>
    /// Default Keltner Channel settings (20 EMA, 10 ATR, 2.0 multiplier)
    /// </summary>
    public static KeltnerChannelSettings Default => new(20, 10, 2.0m);
    
    /// <summary>
    /// Conservative Keltner Channel settings (50 EMA, 20 ATR, 1.5 multiplier)
    /// </summary>
    public static KeltnerChannelSettings Conservative => new(50, 20, 1.5m);
    
    /// <summary>
    /// Aggressive Keltner Channel settings (14 EMA, 10 ATR, 2.5 multiplier)
    /// </summary>
    public static KeltnerChannelSettings Aggressive => new(14, 10, 2.5m);
}

/// <summary>
/// Represents a single Keltner Channel value at a specific point in time
/// </summary>
public record KeltnerChannelValue(
    DateTime Timestamp,
    Price MiddleBand,
    Price UpperBand,
    Price LowerBand,
    decimal Atr,
    decimal Multiplier,
    BandTouchType BandTouch = BandTouchType.None
) : ITimestampedValue
{
    /// <summary>
    /// Gets the channel width
    /// </summary>
    public decimal Width => UpperBand - LowerBand;
    
    /// <summary>
    /// Calculates position of price within the channel (0 = lower band, 0.5 = middle, 1 = upper band)
    /// </summary>
    public decimal GetPositionInChannel(Price price)
    {
        if (Width == 0) return 0.5m;
        return (price - LowerBand) / Width;
    }
    
    /// <summary>
    /// Determines the band touch type for a given price
    /// </summary>
    public BandTouchType DetermineBandTouch(Price price, decimal touchThreshold = 0.01m)
    {
        var position = GetPositionInChannel(price);
        
        if (position >= (1.0m - touchThreshold))
        {
            return price > UpperBand ? BandTouchType.UpperBreak : BandTouchType.UpperTouch;
        }
        
        if (position <= touchThreshold)
        {
            return price < LowerBand ? BandTouchType.LowerBreak : BandTouchType.LowerTouch;
        }
        
        if (Math.Abs(position - 0.5m) <= touchThreshold)
        {
            return BandTouchType.MiddleTouch;
        }
        
        return BandTouchType.None;
    }
}

/// <summary>
/// Represents market data for risk calculations
/// </summary>
public record MarketData(
    Symbol Symbol,
    Price CurrentPrice,
    decimal Volatility,
    Quantity Volume24h,
    decimal PriceChange24h,
    decimal PriceChangePercentage24h,
    DateTime Timestamp
)
{
    /// <summary>
    /// Indicates if the market is in an uptrend (positive 24h change)
    /// </summary>
    public bool IsUptrend => PriceChange24h > 0;
    
    /// <summary>
    /// Gets the absolute price change percentage
    /// </summary>
    public decimal AbsPriceChangePercentage => Math.Abs(PriceChangePercentage24h);
}

/// <summary>
/// Interface for timestamped values
/// </summary>
public interface ITimestampedValue
{
    DateTime Timestamp { get; }
}

/// <summary>
/// Band touch detection enumeration
/// </summary>
public enum BandTouchType
{
    None = 0,
    UpperTouch = 1,
    UpperBreak = 2,
    LowerTouch = 3,
    LowerBreak = 4,
    MiddleTouch = 5
}

/// <summary>
/// Time in force enumeration for orders
/// </summary>
public enum TimeInForce
{
    GoodTillCanceled = 1,
    ImmediateOrCancel = 2,
    FillOrKill = 3,
    PostOnly = 4
}

/// <summary>
/// Stop loss level calculations for a position
/// </summary>
public record StopLossLevels
{
    /// <summary>
    /// Initial stop loss level (hard stop)
    /// </summary>
    public required Price InitialStopLoss { get; init; }

    /// <summary>
    /// Current trailing stop level
    /// </summary>
    public Price? TrailingStop { get; init; }

    /// <summary>
    /// Breakeven level where stop moves to entry
    /// </summary>
    public Price BreakevenLevel { get; init; } = Price.Zero();

    /// <summary>
    /// Emergency stop level (extreme conditions)
    /// </summary>
    public Price? EmergencyStop { get; init; }

    /// <summary>
    /// Time-based stop (maximum hold time)
    /// </summary>
    public DateTime? TimeStop { get; init; }

    /// <summary>
    /// Volatility-adjusted stop level
    /// </summary>
    public Price? VolatilityStop { get; init; }

    /// <summary>
    /// Technical stop based on support/resistance
    /// </summary>
    public Price? TechnicalStop { get; init; }

    /// <summary>
    /// Profit taking levels
    /// </summary>
    public List<ProfitTarget> ProfitTargets { get; init; } = new();

    /// <summary>
    /// Stop loss reasoning and calculation details
    /// </summary>
    public StopLossCalculationDetails CalculationDetails { get; init; } = new();

    /// <summary>
    /// Whether trailing stop is active
    /// </summary>
    public bool IsTrailingActive { get; init; }

    /// <summary>
    /// Trailing stop distance in percentage
    /// </summary>
    public decimal TrailingDistance { get; init; }

    /// <summary>
    /// Minimum profit before trailing activates
    /// </summary>
    public decimal TrailingActivationThreshold { get; init; }

    /// <summary>
    /// Current risk per share/unit
    /// </summary>
    public Price RiskPerUnit { get; init; } = Price.Zero();

    /// <summary>
    /// Maximum acceptable risk for the position
    /// </summary>
    public Price MaxAcceptableRisk { get; init; } = Price.Zero();

    /// <summary>
    /// Risk-reward ratio at different levels
    /// </summary>
    public Dictionary<string, decimal> RiskRewardRatios { get; init; } = new();

    /// <summary>
    /// When the levels were calculated
    /// </summary>
    public DateTime CalculatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Get the most restrictive (closest) stop loss level
    /// </summary>
    public Price GetEffectiveStopLoss(Price currentPrice, bool isLong)
    {
        var stops = new List<Price> { InitialStopLoss };
        
        if (TrailingStop != null) stops.Add(TrailingStop.Value);
        if (EmergencyStop != null) stops.Add(EmergencyStop.Value);
        if (VolatilityStop != null) stops.Add(VolatilityStop.Value);
        if (TechnicalStop != null) stops.Add(TechnicalStop.Value);

        // Ensure we have at least one stop (InitialStopLoss is required)
        if (stops.Count == 0) return InitialStopLoss;

        if (isLong)
        {
            // For long positions, use the highest stop (least restrictive but safest)
            return stops.Max() ?? InitialStopLoss;
        }
        else
        {
            // For short positions, use the lowest stop
            return stops.Min() ?? InitialStopLoss;
        }
    }

    /// <summary>
    /// Update trailing stop based on current price
    /// </summary>
    public StopLossLevels UpdateTrailingStop(Price currentPrice, Price entryPrice, bool isLong)
    {
        if (!IsTrailingActive) return this;

        var profitPercentage = isLong 
            ? (currentPrice.Value - entryPrice.Value) / entryPrice.Value
            : (entryPrice.Value - currentPrice.Value) / entryPrice.Value;

        // Only activate trailing if we're profitable enough
        if (profitPercentage < TrailingActivationThreshold) return this;

        Price newTrailingStop;
        if (isLong)
        {
            // For long positions, trail below current price
            newTrailingStop = Price.Create(currentPrice.Value * (1 - TrailingDistance));
            
            // Only move trailing stop up, never down
            if (TrailingStop == null || newTrailingStop.Value > TrailingStop.Value)
            {
                return this with { TrailingStop = newTrailingStop };
            }
        }
        else
        {
            // For short positions, trail above current price
            newTrailingStop = Price.Create(currentPrice.Value * (1 + TrailingDistance));
            
            // Only move trailing stop down, never up
            if (TrailingStop == null || newTrailingStop.Value < TrailingStop.Value)
            {
                return this with { TrailingStop = newTrailingStop };
            }
        }

        return this;
    }
}

/// <summary>
/// Profit target level
/// </summary>
public record ProfitTarget
{
    public required Price TargetPrice { get; init; }
    public decimal PercentageToClose { get; init; } // e.g., 50% of position
    public decimal RiskRewardRatio { get; init; }
    public string Description { get; init; } = string.Empty;
    public bool IsHit { get; init; }
    public DateTime? HitAt { get; init; }
}

/// <summary>
/// Details about stop loss calculations
/// </summary>
public record StopLossCalculationDetails
{
    public string Method { get; init; } = string.Empty; // e.g., "ATR", "Support/Resistance", "Percentage"
    public decimal CalculationInput { get; init; } // ATR value, percentage, etc.
    public string Reasoning { get; init; } = string.Empty;
    public decimal ConfidenceLevel { get; init; } // 0-100
    public Dictionary<string, object> Parameters { get; init; } = new();
}

/// <summary>
/// Represents a completed trade
/// </summary>
public record Trade(
    string TradeId,
    Symbol Symbol,
    OrderSide Side,
    Price EntryPrice,
    Price ExitPrice,
    Quantity Quantity,
    DateTime EntryTime,
    DateTime ExitTime,
    decimal PnL,
    decimal PnLPercentage,
    decimal Fee,
    string ExitReason
)
{
    /// <summary>
    /// Indicates if the trade was profitable
    /// </summary>
    public bool IsWinner => PnL > 0;
    
    /// <summary>
    /// Gets the trade duration
    /// </summary>
    public TimeSpan Duration => ExitTime - EntryTime;
    
    /// <summary>
    /// Gets the gross PnL (before fees)
    /// </summary>
    public decimal GrossPnL => PnL + Fee;
}

/// <summary>
/// Represents market data snapshot
/// </summary>
public record MarketDataSnapshot(
    Symbol Symbol,
    Price Price,
    decimal Volume,
    decimal Change24h,
    decimal ChangePercent24h,
    decimal High24h,
    decimal Low24h,
    DateTime Timestamp
);

/// <summary>
/// Represents performance metric
/// </summary>
public record PerformanceMetric(
    string Name,
    decimal Value,
    string Unit,
    string Description,
    DateTime Timestamp
); 