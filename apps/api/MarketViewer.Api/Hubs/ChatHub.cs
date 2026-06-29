using MarketViewer.Infrastructure.Services;
using Microsoft.AspNetCore.SignalR;

namespace MarketViewer.Api.Hubs
{
    public class ChatHub : Hub
    {
        //private readonly MarketDataCache _scannerCache;

        public ChatHub(/*MarketDataCache scannerCache*/)
        {
            //_scannerCache = scannerCache;
        }

        public Task SendMessage(int count)
        {
            count++;
            return Clients.All.SendAsync("ReceiveMessage", count);
        }
    }
}
