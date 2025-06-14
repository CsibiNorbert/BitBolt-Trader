using BitcoinTradingBot.Core;
using BitcoinTradingBot.Core.Interfaces;
using BitcoinTradingBot.Core.Models;
using BitcoinTradingBot.Modules.Strategy.Domain.Models;

namespace BitcoinTradingBot.Modules.Strategy.Domain.Strategies;

/// <summary>
/// Interface for Multi-Timeframe Keltner Channel strategy
/// </summary>
public interface IMultiTimeframeKeltnerStrategy : ITradingStrategy
{
    /// <summary>
    /// Evaluates primary timeframe (4H) conditions
    /// </summary>
    /// <param name="symbol">Trading symbol</param>
    /// <param name="primaryCandles">4H candles</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Primary condition result</returns>
    Task<PrimaryConditionResult> EvaluatePrimaryConditionsAsync(
        Symbol symbol, 
        IReadOnlyList<Candle> primaryCandles, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates entry timeframe (5M) conditions
    /// </summary>
    /// <param name="symbol">Trading symbol</param>
    /// <param name="entryCandles">5M candles</param>
    /// <param name="primaryConditions">Primary conditions from 4H analysis</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Entry condition result</returns>
    Task<EntryConditionResult> EvaluateEntryConditionsAsync(
        Symbol symbol, 
        IReadOnlyList<Candle> entryCandles, 
        PrimaryConditionResult primaryConditions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates confluence across multiple timeframes
    /// </summary>
    /// <param name="symbol">Trading symbol</param>
    /// <param name="primaryConditions">Primary conditions</param>
    /// <param name="entryConditions">Entry conditions</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Confluence validation result</returns>
    Task<ConfluenceResult> ValidateConfluenceAsync(
        Symbol symbol,
        PrimaryConditionResult primaryConditions,
        EntryConditionResult entryConditions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates stop loss and take profit levels
    /// </summary>
    /// <param name="entryPrice">Entry price</param>
    /// <param name="signal">Trading signal direction</param>
    /// <param name="primaryCandles">Primary timeframe candles</param>
    /// <param name="entryCandles">Entry timeframe candles</param>
    /// <returns>Risk management levels</returns>
    Task<RiskManagementLevels> CalculateRiskLevelsAsync(
        Price entryPrice,
        TradingSignal signal,
        IReadOnlyList<Candle> primaryCandles,
        IReadOnlyList<Candle> entryCandles);
} 