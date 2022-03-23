using BotNet;
using Telegram.Bot;

namespace BotHost
{
    public record EchoState(string? LastMessage = null) : IChatState
    {
        public IEnumerable<View> GetView()
        {
            yield return View.Text(LastMessage ?? "Hello");
            yield return View.TextHook(msg => this with { LastMessage = msg });
        }
    }


    public record CounterState(int Counter = 0, int Step = 1, string? Reply = null) : IChatState
    {
        public IEnumerable<View> GetView()
        {
            if (Reply != null) yield return View.Text(Reply);
            yield return View.Buttons($"Counter: {Counter}",
                new()
                {   
                    { $"Increment by {Step}", () => ChangeCounter(Step) },
                    { $"Decrement by {Step}", () => ChangeCounter(-Step) },
                    { $"Step = 1", () => SetStep(1) },
                    { $"Step = 5", () => SetStep(5) },
                    { $"Step = 10", () => SetStep(10) },
                }
            );

            yield return View.TextHook(msg => this with { Reply = "Please, use buttons" });
        }

        private async ValueTask<IChatState> ChangeCounter(int delta)
            => this with { Counter = Counter + delta, Reply = $"Counter changed: {delta}" };

        private async ValueTask<IChatState> SetStep(int step)
            => this with { Step = step, Reply = $"Step changed to: {step}"  };
    }
    
    
    public class Program
    {
        [STAThread]
        public static void Main()
        {
            var telegram = new TelegramBotClient(Environment.GetEnvironmentVariable("TG_TOKEN"));
            
            var (save, getState) = Store.inMemory(new Dictionary<ChatId, IChatState>());
            var viewAdapter = TelegramAdapter.view(telegram);
            var updateAdapter = TelegramAdapter.update;
            var initState = new CounterState();

            telegram.StartReceiving(
                async (bot, update, token) =>
                {
                    await BotProcessor.handleUpdate(save, getState, viewAdapter, updateAdapter, initState, update);
                },
                async (bot, ex, token) => { });

            Console.WriteLine("Bot started");
            Console.ReadLine();
        }
    }
}

