using BotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FSharp.Core;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace BotHost
{
    public record GetNameState(string Name) : IChatState
    {
        public IEnumerable<View> GetView()
        {
            yield return View.Text("What is your name?");
            yield return View.TextHandler(msg => new PrintState(msg));
        }
    }

    public record PrintState(string Text) : IChatState
    {
        public IEnumerable<View> GetView()
        {
            yield return View.Text(Text);
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


    public record ContactState() : IChatState
    {
        public IEnumerable<View> GetView()
        {
            yield return View.ContactHandler("Please share you contact", contact => new PrintState(contact));
        }
    }


    public class Program
    {
        [STAThread]
        public static void Main()
        {
            var services = new ServiceCollection();

            services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(Environment.GetEnvironmentVariable("TG_TOKEN")!));
            
            
            services.AddSingleton<IChatStateStore, InMemoryStore>();
            services.AddTransient<IChatAdapter<Update>, TelegramAdapter.ChatAdapter>();
            services.AddTransient<BotProcessor<Update>>();
            
            var provider = services.BuildServiceProvider();

            var telegram = provider.GetRequiredService<ITelegramBotClient>();
            var processor = provider.GetRequiredService<BotProcessor<Update>>();

            var initState = new ContactState();

            telegram.StartReceiving(
                async (bot, update, token) =>
                {
                    await processor.Handle(initState, update);
                },
                (bot, ex, token) => Task.CompletedTask);

            Console.WriteLine("Bot started");
            Console.ReadLine();
        }
    }
}