namespace BotNet.Telegram

open System
open System.Collections.Concurrent
open RateLimiter
open Serilog
open Telegram.Bot
open BotNet
open Telegram.Bot.Types.Enums
open Telegram.Bot.Types.ReplyMarkups
open System.Threading.Tasks


type TelegramChatAdapter(client: ITelegramBotClient) =
    let limiter = ConcurrentDictionary<string, TimeLimiter>()
    let lastMessageId = ConcurrentDictionary<string, int>()
    
    let globalLimiter = TimeLimiter.GetFromMaxCountByInterval(30, TimeSpan.FromSeconds(1))
    let limiter (chatId) = limiter.GetOrAdd(chatId, fun _ -> TimeLimiter.GetFromMaxCountByInterval(1, TimeSpan.FromSeconds(1)))
    
    let waitLimit chatId = task {
        do! globalLimiter.Enqueue(fun () -> ())
        do! (limiter chatId).Enqueue(fun () -> ())
    }
    
    interface IChatAdapter<Telegram.Bot.Types.Update> with
        member this.AdaptView(ChatId chatId) (views) = task {
            let btnIndex =
                let mutable buttonOffset = -1
                fun () -> 
                    buttonOffset <- buttonOffset + 1
                    buttonOffset
            
            
            let logError (tValue: Task) = task {
                try
                    do! tValue
                with e -> Log.Error(e, "Error while send telegram request")
            }
            let append (msg: Types.Message) =
                let msgId = msg.MessageId
                ignore ^ lastMessageId.AddOrUpdate(chatId, msgId, (fun key old -> msgId))
                
            let clear () = lastMessageId.TryRemove(chatId) |> ignore
            let getEditMsg() = lastMessageId.TryGetValue(chatId) |> Option.ofTryPattern
                    
            
            let sendTyping (delay: TimeSpan) = logError ^ task {
                do! waitLimit chatId
                do! client.SendChatActionAsync(chatId, ChatAction.Typing)
                do! Task.Delay(delay)
            }
            
            let mkKeyboard buttons =
                let rows = buttons
                           |> Seq.map ^ fun btn -> InlineKeyboardButton.WithCallbackData(btn.Text, $"%i{btnIndex()}")
                           |> Seq.chunkBySize 3
                           |> Seq.map Seq.ofArray
                           |> Seq.toArray
                        
                InlineKeyboardMarkup(rows)
            
            
            let sendMessage txt buttons = logError ^ task {
                do! waitLimit chatId
                let! msg = client.SendTextMessageAsync(chatId, txt, replyMarkup=mkKeyboard buttons, parseMode=ParseMode.Html)
                append msg
            }
            
            let editMessage msgId txt buttons = logError ^ task {
                do! waitLimit chatId
                let! msg = client.EditMessageTextAsync(Types.ChatId(chatId), msgId, txt, parseMode=ParseMode.Html, replyMarkup=mkKeyboard buttons)
                append msg
            }

            
            let editOrSendMessage txt reply = logError ^ task {
                match getEditMsg() with
                | Some msgId ->
                    try
                        do! editMessage msgId txt reply
                    with e -> Log.Error(e, "Error while edit message")
                | None -> do! sendMessage txt reply
            }
            
            let sendContact txt = logError ^ task {
                let reply = ReplyKeyboardMarkup(KeyboardButton.WithRequestContact("Contact"))
                reply.OneTimeKeyboard <- true
                do! waitLimit chatId
                let! msg = client.SendTextMessageAsync(chatId, txt, replyMarkup=reply, parseMode=ParseMode.Html)
                append msg
            }
            
            
            for view in views do
                match view with
                | EmptyView -> ()
                | TypingView delay -> do! sendTyping delay
                | TextView (txt) -> do! sendMessage txt []
                | ReplyView (text, buttons, false) -> do! sendMessage text buttons
                | ReplyView (text, buttons, true) -> do! editOrSendMessage text buttons
                | TextHandlerView f -> ()
                | ContactHandlerView (txt, f) -> do! sendContact txt
        }
            
            
        member this.ExtractUpdate(upd) = 
            let fixStr str = if String.IsNullOrEmpty(str) then "" else str
                
            let fromUser (x: Telegram.Bot.Types.User) = {
                Id = x.Id |> (string >> UserId)
                FirstName = fixStr x.FirstName
                LastName = fixStr x.LastName
                UserName = fixStr x.Username
            }
            
            match upd.Type with
            | UpdateType.Message ->
                let msg = upd.Message
                let chat = { Id = ChatId (msg.Chat.Id.ToString()); Title = msg.Chat.Title } 
                let user = fromUser msg.From
                match msg.Type with
                | MessageType.Text -> Some (chat, user, Text ^ fixStr msg.Text)
                | MessageType.Contact -> Some (chat, user, Contact ^ fixStr msg.Contact.PhoneNumber)
                | _ -> None
            
            | UpdateType.CallbackQuery ->
                let callBack = upd.CallbackQuery
                let chat = { Id = ChatId (callBack.Message.Chat.Id.ToString()); Title = callBack.Message.Chat.Title }
                let user = fromUser callBack.From
                Some (chat, user, Callback ^ { Id = callBack.Id; Data = fixStr callBack.Data })

            | _ -> None

        
        member this.ResetChat(ChatId chatId) = task {
            ignore ^ lastMessageId.TryRemove(chatId)
        }
            



