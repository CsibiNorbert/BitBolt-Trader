using BitcoinTradingBot.Core;
using BitcoinTradingBot.Core.Models;

namespace BitcoinTradingBot.Modules.Strategy.Domain.Indicators;

/// <summary>
/// Interface for Keltner Channel calculations with multiple variations
/// </summary>
public interface IKeltnerChannelCalculator
{
    /// <summary>
    /// Calculates Keltner Channel for a given candle series
    /// </summary>
    /// <param name="candles">Historical candles (must be in chronological order)</param>
    /// <param name="emaPeriod">EMA period for middle band (default: 20)</param>
    /// <param name="atrPeriod">ATR period for band calculation (default: 10)</param>
    /// <param name="multiplier">ATR multiplier for band width (default: 2.0)</param>
    /// <returns>Keltner Channel data for the latest candle</returns>
    KeltnerChannel Calculate(
        IReadOnlyList<Candle> candles,
        int emaPeriod = 20,
        int atrPeriod = 10,
        decimal multiplier = 2.0m);

    /// <summary>
    /// Calculates Keltner Channel series for multiple candles
    /// </summary>
    /// <param name="candles">Historical candles</param>
    /// <param name="emaPeriod">EMA period for middle band</param>
    /// <param name="atrPeriod">ATR period for band calculation</param>
    /// <param name="multiplier">ATR multiplier for band width</param>
    /// <returns>Keltner Channel series</returns>
    IReadOnlyList<KeltnerChannel> CalculateSeries(
        IReadOnlyList<Candle> candles,
        int emaPeriod = 20,
        int atrPeriod = 10,
        decimal multiplier = 2.0m);

    /// <summary>
    /// Calculates dynamic multiplier based on market volatility
    /// </summary>
    /// <param name="candles">Recent candles for volatility analysis</param>
    /// <param name="baseMultiplier">Base multiplier (default: 2.0)</param>
    /// <param name="minMultiplier">Minimum multiplier (default: 1.5)</param>
    /// <param name="maxMultiplier">Maximum multiplier (default: 2.5)</param>
    /// <returns>Dynamic multiplier value</returns>
    decimal CalculateDynamicMultiplier(
        IReadOnlyList<Candle> candles,
        decimal baseMultiplier = 2.0m,
        decimal minMultiplier = 1.5m,
        decimal maxMultiplier = 2.5m);

    /// <summary>
    /// Validates calculation accuracy against reference values
    /// </summary>
    /// <param name="calculated">Calculated Keltner Channel</param>
    /// <param name="reference">Reference Keltner Channel (e.g., from TradingView)</param>
    /// <param name="tolerance">Acceptable tolerance (default: 0.01%)</param>
    /// <returns>True if within tolerance</returns>
    bool ValidateAccuracy(KeltnerChannel calculated, KeltnerChannel reference, decimal tolerance = 0.0001m);
} 