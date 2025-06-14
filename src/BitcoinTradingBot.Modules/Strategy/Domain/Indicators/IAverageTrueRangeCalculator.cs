using BitcoinTradingBot.Core;
using BitcoinTradingBot.Core.Models;

namespace BitcoinTradingBot.Modules.Strategy.Domain.Indicators;

/// <summary>
/// Interface for Average True Range calculations with True Range normalization
/// </summary>
public interface IAverageTrueRangeCalculator
{
    /// <summary>
    /// Calculates ATR for the latest candle in the series
    /// </summary>
    /// <param name="candles">Historical candles (must be in chronological order, minimum 2 candles)</param>
    /// <param name="period">Period for ATR calculation (default: 14)</param>
    /// <returns>ATR value for the latest candle</returns>
    AverageTrueRange Calculate(IReadOnlyList<Candle> candles, int period = 14);

    /// <summary>
    /// Calculates ATR series for multiple candles
    /// </summary>
    /// <param name="candles">Historical candles</param>
    /// <param name="period">Period for ATR calculation</param>
    /// <returns>ATR series</returns>
    IReadOnlyList<AverageTrueRange> CalculateSeries(IReadOnlyList<Candle> candles, int period = 14);

    /// <summary>
    /// Calculates True Range for a single candle
    /// </summary>
    /// <param name="currentCandle">Current candle</param>
    /// <param name="previousCandle">Previous candle</param>
    /// <returns>True Range value</returns>
    decimal CalculateTrueRange(Candle currentCandle, Candle previousCandle);

    /// <summary>
    /// Updates ATR with new candle using rolling calculation
    /// </summary>
    /// <param name="previousAtr">Previous ATR value</param>
    /// <param name="newTrueRange">New True Range value</param>
    /// <param name="period">Period for ATR calculation</param>
    /// <returns>Updated ATR value</returns>
    AverageTrueRange UpdateRolling(AverageTrueRange previousAtr, decimal newTrueRange, int period);

    /// <summary>
    /// Calculates normalized ATR (ATR / Price) for volatility comparison
    /// </summary>
    /// <param name="atr">ATR value</param>
    /// <param name="price">Current price (typically close price)</param>
    /// <returns>Normalized ATR as percentage</returns>
    decimal CalculateNormalizedAtr(AverageTrueRange atr, Price price);

    /// <summary>
    /// Determines current volatility regime
    /// </summary>
    /// <param name="currentAtr">Current ATR value</param>
    /// <param name="historicalAtr">Historical ATR values for comparison</param>
    /// <returns>Low, Normal, High, or Extreme volatility</returns>
    VolatilityRegime DetermineVolatilityRegime(AverageTrueRange currentAtr, IReadOnlyList<AverageTrueRange> historicalAtr);

    /// <summary>
    /// Checks if current volatility is within normal trading range
    /// </summary>
    /// <param name="currentAtr">Current ATR value</param>
    /// <param name="historicalAtr">Historical ATR for baseline</param>
    /// <param name="threshold">Threshold multiplier (default: 2.0)</param>
    /// <returns>True if within normal range</returns>
    bool IsVolatilityNormal(AverageTrueRange currentAtr, IReadOnlyList<AverageTrueRange> historicalAtr, decimal threshold = 2.0m);
}

/// <summary>
/// Volatility regime enumeration
/// </summary>
public enum VolatilityRegime
{
    Low = 1,
    Normal = 2,
    High = 3,
    Extreme = 4
} 