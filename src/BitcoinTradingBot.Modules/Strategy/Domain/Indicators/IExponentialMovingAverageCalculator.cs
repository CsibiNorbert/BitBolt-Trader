using BitcoinTradingBot.Core;
using BitcoinTradingBot.Core.Models;

namespace BitcoinTradingBot.Modules.Strategy.Domain.Indicators;

/// <summary>
/// Interface for Exponential Moving Average calculations
/// </summary>
public interface IExponentialMovingAverageCalculator
{
    /// <summary>
    /// Calculates EMA for the latest candle in the series
    /// </summary>
    /// <param name="candles">Historical candles (must be in chronological order)</param>
    /// <param name="period">Period for EMA calculation</param>
    /// <returns>EMA value for the latest candle</returns>
    ExponentialMovingAverage Calculate(IReadOnlyList<Candle> candles, int period);

    /// <summary>
    /// Calculates EMA series for multiple candles
    /// </summary>
    /// <param name="candles">Historical candles</param>
    /// <param name="period">Period for EMA calculation</param>
    /// <returns>EMA series</returns>
    IReadOnlyList<ExponentialMovingAverage> CalculateSeries(IReadOnlyList<Candle> candles, int period);

    /// <summary>
    /// Updates EMA with new candle using rolling calculation
    /// </summary>
    /// <param name="previousEma">Previous EMA value</param>
    /// <param name="newPrice">New price (typically close price)</param>
    /// <param name="period">Period for EMA calculation</param>
    /// <returns>Updated EMA value</returns>
    ExponentialMovingAverage UpdateRolling(ExponentialMovingAverage previousEma, Price newPrice, int period);

    /// <summary>
    /// Calculates smoothing factor (alpha) for given period
    /// </summary>
    /// <param name="period">Period for EMA calculation</param>
    /// <returns>Smoothing factor</returns>
    decimal CalculateSmoothingFactor(int period);

    /// <summary>
    /// Determines if EMA slope is bullish or bearish
    /// </summary>
    /// <param name="emaValues">Recent EMA values (at least 2 values)</param>
    /// <returns>Positive for bullish, negative for bearish, zero for flat</returns>
    decimal CalculateSlope(IReadOnlyList<ExponentialMovingAverage> emaValues);
} 