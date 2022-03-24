using BotNet;
using Telegram.Bot;

namespace BotHost
{
    public record TestState() : IChatState
    {
        public IEnumerable<View> GetView()
        {
            yield return View.Text("What is your name?");
            yield return View.TextHandler(msg => new GreetState(msg));
        }
    }

    public record GreetState(string Name) : IChatState
    {
        public IEnumerable<View> GetView()
        {
            yield return View.Text($"Hello {Name}!");
        }
    }

    public record CounterState(int Counter = 0, int Step = 1, string? Reply = null) : IChatState
    {
        public IEnumerable<View> GetView()
        {
            yield return View.Text(Reply); 
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
            
            yield return View.TextHandler(msg => this with { Reply = "Please use buttons" });
        }

        private async ValueTask<IChatState> ChangeCounter(int delta)
            => this with { Counter = Counter + delta, Reply = $"Counter changed: {delta}" };

        private async ValueTask<IChatState> SetStep(int step)
            => this with { Step = step, Reply = $"Step changed to: {step}" };
    }

    public record ManyKeyboards(string? Log = null) : IChatState
    {
        public IEnumerable<View> GetView()
        {
            yield return View.Text(Log);
            yield return View.Buttons("First", 
                new()
                {
                    { "Button 1", async () => this with { Log = "First:Button 1" }},
                    { "Button 2", async () => this with { Log = "First:Button 2" }},
                }
            );
            
            yield return View.Buttons("Second", 
                new()
                {
                    { "Button 1", async () => this with { Log = "Second:Button 1" }},
                    { "Button 2", async () => this with { Log = "Second:Button 2" }},
                }
            );
        }
    }

    public record ChatHookState() : IChatState
    {
        public IEnumerable<View> GetView()
        {
            var chat = Hook.UseChat();

            yield return View.Typing(TimeSpan.FromSeconds(1));
            yield return View.Text($"Hello {chat.UserName}");
        }
    }


    public class Program
    {
        [STAThread]
        public static void Main()
        {
            var telegram = new TelegramBotClient(Environment.GetEnvironmentVariable("TG_TOKEN")!);

            var (saveState, getState) = Store.inMemory(new Dictionary<ChatId, IChatState>());
            var viewAdapter = TelegramAdapter.view(telegram);
            var updateAdapter = TelegramAdapter.update;
            var initState = new ChatHookState();

            telegram.StartReceiving(
                async (bot, update, token) =>
                {
                    await BotProcessor.handleUpdate(saveState, getState, viewAdapter, updateAdapter, initState, update);
                },
                (bot, ex, token) => Task.CompletedTask);

            Console.WriteLine("Bot started");
            Console.ReadLine();
        }
    }
}