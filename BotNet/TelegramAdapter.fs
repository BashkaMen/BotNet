module BotNet.TelegramAdapter

open System
open BotNet
open Telegram.Bot
open Telegram.Bot.Types.Enums
open Telegram.Bot.Types.ReplyMarkups


let view (client: ITelegramBotClient) = ViewAdapter ^ fun (ChatId chatId) views -> task {
    let btnIndex =
        let mutable buttonOffset = -1
        fun () -> 
            buttonOffset <- buttonOffset + 1
            buttonOffset
    
    for view in views do
        match view with
        | EmptyView -> ()
        | TextView txt -> do! client.SendTextMessageAsync(chatId, txt, replyMarkup=ReplyKeyboardRemove()) |> Task.ignore
        | ReplyView (text, buttons) ->
            let rows = buttons
                          |> Seq.map ^ fun btn -> InlineKeyboardButton.WithCallbackData(btn.Text, $"%i{btnIndex()}")
                          |> Seq.chunkBySize 3
                          |> Seq.map Seq.ofArray
                
            let reply = InlineKeyboardMarkup(rows);
            do! client.SendTextMessageAsync(chatId, text, replyMarkup=reply) |> Task.ignore
        
        | TextHandlerView f -> ()
        | ContactHandlerView (txt, f) ->
            let reply = ReplyKeyboardMarkup(KeyboardButton.WithRequestContact("Contact"))
            reply.OneTimeKeyboard <- true
            do! client.SendTextMessageAsync(chatId, txt, replyMarkup=reply) |> Task.ignore
}


let update = UpdateAdapter ^ fun (upd: Telegram.Bot.Types.Update) ->
    let inline ( <|> ) str def =
        if String.IsNullOrEmpty(str)
        then def
        else str
        
    let inline extract (x: ^a) = {
        Id = (^a : (member Id : int64)x) |> (string >> ChatId)
        FirstName = (^a : (member FirstName : string)x) <|> ""
        LastName = (^a : (member LastName : string)x) <|> ""
        UserName = (^a : (member Username : string)x) <|> ""
    }
    
    match upd.Type with
    | UpdateType.Message ->
        let msg = upd.Message
        let chat = extract msg.Chat
        match msg.Type with
        | MessageType.Text -> Some (chat, Text msg.Text)
        | MessageType.Contact -> Some (chat, Contact msg.Contact.PhoneNumber)
        | _ -> None
    
    | UpdateType.CallbackQuery ->
        let callBack = upd.CallbackQuery
        let chat = extract upd.CallbackQuery.From
        Some (chat, Callback callBack.Data)

    | _ -> None

