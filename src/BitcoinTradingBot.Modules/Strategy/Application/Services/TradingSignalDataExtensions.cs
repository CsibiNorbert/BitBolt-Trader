using BitcoinTradingBot.Core;
using BitcoinTradingBot.Core.Models;

namespace BitcoinTradingBot.Modules.Strategy.Application.Services;

/// <summary>
/// Extension methods for TradingSignalData to simplify signal creation
/// </summary>
public static class TradingSignalDataExtensions
{
    /// <summary>
    /// Creates a no-signal instance with reason
    /// </summary>
    public static TradingSignalData NoSignal(Symbol symbol, string reason = "No conditions met")
    {
        var metadata = new Dictionary<string, object>
        {
            ["Reason"] = reason,
            ["SignalType"] = "NoSignal"
        };

        return new TradingSignalData(
            Id: Guid.NewGuid().ToString(),
            Symbol: symbol,
            Signal: TradingSignal.None,
            EntryPrice: Price.Create(0),
            StopLoss: null,
            TakeProfit: null,
            Confidence: 0m,
            SignalTime: DateTime.UtcNow,
            Metadata: metadata
        );
    }

    /// <summary>
    /// Creates a buy signal with specified parameters
    /// </summary>
    public static TradingSignalData BuySignal(
        Symbol symbol,
        Price entryPrice,
        Price? stopLoss = null,
        Price? takeProfit = null,
        decimal confidence = 0.5m,
        Dictionary<string, object>? metadata = null)
    {
        metadata ??= new Dictionary<string, object>();
        metadata["SignalType"] = "Buy";

        return new TradingSignalData(
            Id: Guid.NewGuid().ToString(),
            Symbol: symbol,
            Signal: TradingSignal.Buy,
            EntryPrice: entryPrice,
            StopLoss: stopLoss,
            TakeProfit: takeProfit,
            Confidence: confidence,
            SignalTime: DateTime.UtcNow,
            Metadata: metadata
        );
    }

    /// <summary>
    /// Creates a sell signal with specified parameters
    /// </summary>
    public static TradingSignalData SellSignal(
        Symbol symbol,
        Price entryPrice,
        Price? stopLoss = null,
        Price? takeProfit = null,
        decimal confidence = 0.5m,
        Dictionary<string, object>? metadata = null)
    {
        metadata ??= new Dictionary<string, object>();
        metadata["SignalType"] = "Sell";

        return new TradingSignalData(
            Id: Guid.NewGuid().ToString(),
            Symbol: symbol,
            Signal: TradingSignal.Sell,
            EntryPrice: entryPrice,
            StopLoss: stopLoss,
            TakeProfit: takeProfit,
            Confidence: confidence,
            SignalTime: DateTime.UtcNow,
            Metadata: metadata
        );
    }
} 