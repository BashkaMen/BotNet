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
