using Alpaca.Client.Models;

namespace Alpaca.Client.Interfaces;

public interface IAlpacaTradingClient
{
    Task<AlpacaClock> GetClock();
    Task<List<AlpacaCalendarDay>> GetCalendar(DateOnly start, DateOnly end);
    Task<AlpacaAccount> GetAccount();
    Task<AlpacaOrder> SubmitOrder(AlpacaOrderRequest request);
    Task<AlpacaOrder> GetOrder(string orderId);
    Task<CancelOrderResult> CancelOrder(string orderId);
    Task<List<AlpacaPosition>> GetPositions();
}

/// <summary>Outcome of a cancel request; the distinctions drive backstop handling.</summary>
public enum CancelOrderResult
{
    /// <summary>Cancel accepted (the order may still take a moment to reach a terminal state).</summary>
    Canceled,

    /// <summary>422 — the order is already in a terminal state, most likely filled.</summary>
    NotCancelable,

    /// <summary>404 — Alpaca does not know the order.</summary>
    NotFound,

    /// <summary>Transport or server error; the order's state is unknown.</summary>
    Failed
}
