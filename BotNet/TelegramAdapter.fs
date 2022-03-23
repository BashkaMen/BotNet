module BotNet.TelegramAdapter

open BotNet
open Telegram.Bot
open Telegram.Bot.Types.Enums
open Telegram.Bot.Types.ReplyMarkups


let view (client: ITelegramBotClient) = ViewAdapter ^ fun (ChatId chatId) views ->
    let rec go buttonOffset views = task {
        match views with
        | [] -> return ()
        | view::xs ->
            let next offset = go (buttonOffset + offset) xs 
            match view with
            | TextMessage txt ->
                do! client.SendTextMessageAsync(chatId, txt, replyMarkup=ReplyKeyboardRemove()) |> Task.ignore
                return! next 0
                
            | ReplyMessage (text, buttons) ->
                let rows = buttons
                              |> Seq.mapi ^ fun index btn ->
                                  InlineKeyboardButton.WithCallbackData(btn.Text, $"{buttonOffset + index}")
                              |> Seq.chunkBySize 3
                              |> Seq.map Seq.ofArray
                    
                let reply = InlineKeyboardMarkup(rows);
                do! client.SendTextMessageAsync(chatId, text, replyMarkup=reply) |> Task.ignore
                return! next buttons.Length
            
            | TextHandler f -> return! next 0
            | ContactHandler f -> return! next 0
    }
    
    go 0 (List.ofSeq views)
    

let update = UpdateAdapter ^ fun (upd: Telegram.Bot.Types.Update) ->
    match upd.Type with
    | UpdateType.Message ->
        let msg = upd.Message
        let chatId = ChatId ^ msg.Chat.Id.ToString()
        match msg.Type with
        | MessageType.Text -> Some (chatId, Text msg.Text)
        | MessageType.Contact -> Some (chatId, Contact msg.Contact.PhoneNumber)
        | _ -> None
    
    | UpdateType.CallbackQuery ->
        let callBack = upd.CallbackQuery
        let chatId = ChatId ^ callBack.From.Id.ToString()
        Some (chatId, Callback callBack.Data)

    | _ -> None 

