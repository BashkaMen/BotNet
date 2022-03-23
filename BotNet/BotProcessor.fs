namespace BotNet

open System.Threading.Tasks
open BotNet
open Telegram.Bot
open Telegram.Bot.Types
open Telegram.Bot.Types.Enums
open Telegram.Bot.Types.ReplyMarkups

[<Struct>] type ChatId = ChatId of string


type ChatUpdate =
    | Text of string
    | Contact of string
    | Callback of string


type SaveChatState = SaveChatState of (ChatId -> IChatState -> Task)
type GetChatState = GetChatState of  (ChatId -> Task<IChatState option>)
type ViewAdapter = ViewAdapter of (ChatId -> View seq -> Task)
type UpdateAdapter<'update> = UpdateAdapter of ('update -> Option<ChatId * ChatUpdate>)  



module BotProcessor =

    let handleUpdate (SaveChatState save)
                     (GetChatState getState)
                     (ViewAdapter adaptView)
                     (UpdateAdapter mapUpdate)
                     (initState: IChatState)
                     (update: 'Update) = task {
        
        match mapUpdate update with
        | None -> return ()
        | Some (chatId, update) -> 
        
        let render = adaptView chatId
        let save = save chatId
        
        let! state = getState chatId <?!> initState 
        let view = state.GetView()

        
        let runHandler (handler: ('a -> ValueTask<IChatState>) option) (arg: 'a) = task {
            match handler with
            | Some f -> return! f arg
            | None -> return state
        }
        
        let! newState = 
            match update with
            | Text txt when txt = "/start" -> Task.FromResult initState
            | Text txt -> runHandler (View.getTextHandler view) txt
            | Contact txt -> runHandler (View.getContactHandler view) txt 
            | Callback query ->
                let findButton index = view |> View.getButtons |> Seq.tryItem index
                let callBack = query |> Int32.tryParse |> Option.bind findButton |> Option.map ^ fun x -> x.Callback
                runHandler callBack ()
        
        do! render ^ newState.GetView()
        do! save newState
    }



module TelegramAdapter =
    let view (client: ITelegramBotClient) = ViewAdapter ^ fun (ChatId chatId) views ->
        let rec go buttonOffset views : Task = task {
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
        
    
    let update = UpdateAdapter ^ fun (upd: Update) ->
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