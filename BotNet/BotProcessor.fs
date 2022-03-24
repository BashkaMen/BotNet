namespace BotNet

open System.Threading
open System.Threading.Tasks
open BotNet



type ChatUpdate =
    | Text of string
    | Contact of string
    | Callback of string


type SaveChatState = SaveChatState of (ChatId -> IChatState -> Task)
type GetChatState = GetChatState of  (ChatId -> Task<IChatState option>)
type ViewAdapter = ViewAdapter of (ChatId -> View seq -> Task)
type UpdateAdapter<'update> = UpdateAdapter of ('update -> Option<Chat * ChatUpdate>)  


module Hook =
    type private ChatContext = {
        Chat: Chat
    }
    
    let private context = AsyncLocal<ChatContext>()
    
    let setContext chat =
        context.Value <- {
            Chat = chat
        }
        
    
    [<CompiledName "UseChat">]
    let useChat() = context.Value.Chat 


module BotProcessor =

    let handleUpdate (SaveChatState save)
                     (GetChatState getState)
                     (ViewAdapter adaptView)
                     (UpdateAdapter mapUpdate)
                     (initState: IChatState)
                     (update: 'Update) = task {
        
        match mapUpdate update with
        | None -> return ()
        | Some (chat, update) ->
            
        Hook.setContext chat
        
        let render = adaptView chat.Id
        let save = save chat.Id
        
        let! state = getState chat.Id <?!> initState 
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
