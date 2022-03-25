namespace BotNet.Telegram

open System
open System.Collections.Concurrent
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
                    
            match lastMessageId.TryGetValue(chatId) with
            | true, msgId ->
                do! client.DeleteMessageAsync(chatId, msgId)
                ignore ^ lastMessageId.TryRemove(chatId)
            | _ -> ()
            
            
            for view in views do
                match view with
                | EmptyView -> ()
                | TypingView delay ->
                    do! client.SendChatActionAsync(chatId, ChatAction.Typing)
                    do! Task.Delay(delay)
                    
                | TextView txt -> do! client.SendTextMessageAsync(chatId, txt, replyMarkup=ReplyKeyboardRemove(), parseMode=ParseMode.Markdown) |> Task.ignore
                | ReplyView (text, buttons) ->
                    let rows = buttons
                                  |> Seq.map ^ fun btn -> InlineKeyboardButton.WithCallbackData(btn.Text, $"%i{btnIndex()}")
                                  |> Seq.chunkBySize 3
                                  |> Seq.map Seq.ofArray
                        
                    let reply = InlineKeyboardMarkup(rows);
                    let! msg = client.SendTextMessageAsync(chatId, text, replyMarkup=reply, parseMode=ParseMode.Markdown)
                    ignore ^ lastMessageId.AddOrUpdate(chatId, msg.MessageId, fun key old -> msg.MessageId)
                    
                
                | TextHandlerView f -> ()
                | ContactHandlerView (txt, f) ->
                    let reply = ReplyKeyboardMarkup(KeyboardButton.WithRequestContact("Contact"))
                    reply.OneTimeKeyboard <- true
                    do! client.SendTextMessageAsync(chatId, txt, replyMarkup=reply, parseMode=ParseMode.Markdown) |> Task.ignore
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



