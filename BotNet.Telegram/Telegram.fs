namespace BotNet.Telegram

open System
open System.Collections.Concurrent
open Serilog
open Telegram.Bot
open BotNet
open Telegram.Bot.Types.Enums
open Telegram.Bot.Types.ReplyMarkups
open System.Threading.Tasks


type TelegramChatAdapter(client: ITelegramBotClient) =
    let lastMessageId = ConcurrentDictionary<string, int>()
    
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
            let append msgId = lastMessageId.AddOrUpdate(chatId, msgId, (fun key old -> msgId)) |> ignore
            let clear () = lastMessageId.TryRemove(chatId) |> ignore
            let lastMsg() = lastMessageId.TryGetValue(chatId) |> Option.ofTryPattern
            
            let sendTyping (delay: TimeSpan) = logError ^ task {
                do! client.SendChatActionAsync(chatId, ChatAction.Typing)
                do! Task.Delay(delay)
            }
            
            let sendText txt = logError ^ task {
                do! client.SendTextMessageAsync(chatId, txt, replyMarkup=ReplyKeyboardRemove(), parseMode=ParseMode.Html) |> Task.ignore
                clear()
            }
            

            let sendKeyboard txt buttons = logError ^ task {
                let rows = buttons
                           |> Seq.map ^ fun btn -> InlineKeyboardButton.WithCallbackData(btn.Text, $"%i{btnIndex()}")
                           |> Seq.chunkBySize 3
                           |> Seq.map Seq.ofArray
                        
                let reply = InlineKeyboardMarkup(rows)
                
                let replaceOrSend (msgId: int) = task {
                    try return! client.EditMessageTextAsync(chatId, msgId, txt, replyMarkup=reply, parseMode=ParseMode.Html)
                    with e -> return! client.SendTextMessageAsync(chatId, txt, replyMarkup=reply, parseMode=ParseMode.Html)
                }
                    
                match lastMsg() with
                | None ->
                    let! msg = client.SendTextMessageAsync(chatId, txt, replyMarkup=reply, parseMode=ParseMode.Html) 
                    append msg.MessageId
                | Some msgId ->
                    let! msg = replaceOrSend msgId
                    append msg.MessageId
            }
            
            let sendContact txt = logError ^ task {
                let reply = ReplyKeyboardMarkup(KeyboardButton.WithRequestContact("Contact"))
                reply.OneTimeKeyboard <- true
                do! client.SendTextMessageAsync(chatId, txt, replyMarkup=reply, parseMode=ParseMode.Html) |> Task.ignore
            }
            
            for view in views do
                match view with
                | EmptyView -> ()
                | TypingView delay -> do! sendTyping delay
                | TextView txt -> do! sendText txt
                | ReplyView (text, buttons) -> do! sendKeyboard text buttons
                | TextHandlerView f -> ()
                | ContactHandlerView (txt, f) -> do! sendContact txt
        }
            
            
        member this.ExtractUpdate(upd) = 
            let fixStr str = if String.IsNullOrEmpty(str) then "" else str
                
            let inline extract (x: ^a) = {
                Id = (^a : (member Id : int64)x) |> (string >> ChatId)
                FirstName = fixStr (^a : (member FirstName : string)x) 
                LastName = fixStr (^a : (member LastName : string)x) 
                UserName = fixStr (^a : (member Username : string)x) 
            }
            
            match upd.Type with
            | UpdateType.Message ->
                let msg = upd.Message
                let chat = extract msg.Chat
                match msg.Type with
                | MessageType.Text -> Some (chat, Text ^ fixStr msg.Text)
                | MessageType.Contact -> Some (chat, Contact ^ fixStr msg.Contact.PhoneNumber)
                | _ -> None
            
            | UpdateType.CallbackQuery ->
                let callBack = upd.CallbackQuery
                let chat = extract upd.CallbackQuery.From
                Some (chat, Callback ^ fixStr callBack.Data)

            | _ -> None



