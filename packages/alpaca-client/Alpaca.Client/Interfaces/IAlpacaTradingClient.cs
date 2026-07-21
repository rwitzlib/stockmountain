using Alpaca.Client.Models;

namespace Alpaca.Client.Interfaces;

public interface IAlpacaTradingClient
{
    Task<AlpacaClock> GetClock();
    Task<List<AlpacaCalendarDay>> GetCalendar(DateOnly start, DateOnly end);
}
