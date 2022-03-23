namespace BotNet

open System.Threading.Tasks
open Telegram.Bot
open Telegram.Bot.Types
open Telegram.Bot.Types.Enums
open Telegram.Bot.Types.ReplyMarkups


module BotProcessor =
    
    let renderTelegram (client: ITelegramBotClient) (chatId: int64) view : Task =
        match view with
        | TextMessage txt -> client.SendTextMessageAsync(chatId, txt, replyMarkup=ReplyKeyboardRemove()) 
        | ReplyMessage (text, buttons) ->
            let buttons = buttons
                          |> Seq.mapi ^ fun index btn ->
                              InlineKeyboardButton.WithCallbackData(btn.Text, $"{index}")
                          |> Seq.chunkBySize 3
                          |> Seq.map Seq.ofArray
                
            let reply = InlineKeyboardMarkup(buttons);
            client.SendTextMessageAsync(chatId, text, replyMarkup=reply)
        
        | TextReceiver f -> Task.CompletedTask
    
    
    let handleUpdate (client: ITelegramBotClient)
                     (SaveChatState save)
                     (GetChatState getState)
                     (initState: IChatState)
                     (update: Update) = task {
        
        let chatId = match update.Type with
                     | UpdateType.Message -> Some update.Message.Chat.Id
                     | UpdateType.CallbackQuery -> Some update.CallbackQuery.From.Id
                     | _ -> None
                     
        match chatId with
        | None -> return ()
        | Some chatId -> 

        
        let render = View.render (renderTelegram client chatId)
        let! state = getState chatId <?!> initState 
        let views = state.GetView()
        let buttons = View.getButtons views
        
        
        let button = 
            let findButton index = buttons |> Seq.tryItem index    
            Option.ofObj update.CallbackQuery
            |> Option.bind ^ fun x -> Option.ofObj x.Data
            |> Option.bind Int32.tryParse
            |> Option.bind ^ findButton
        
        let textHandler = View.getTextHandler views
        
        
        match update.Type, button, textHandler with
        | UpdateType.CallbackQuery, Some btn, _ ->
            let! state = btn.Callback()
            do! render ^ state.GetView()
            do! save chatId state
        
        | UpdateType.Message, _, Some handler when update.Message.Type = MessageType.Text ->
            match update.Message.Text with
            | "/start" ->
                do! render ^ initState.GetView()
                do! save chatId initState
            | _ ->
                let! state = handler update.Message.Text
                do! render ^ state.GetView()
                do! save chatId state
        | _ ->
            do! render views
    }
          

