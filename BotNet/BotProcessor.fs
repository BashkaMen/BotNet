namespace BotNet

open System
open System.Threading
open System.Threading.Tasks
open BotNet
open Microsoft.Extensions.DependencyInjection
open Serilog


type ChatUpdate =
    | Text of string
    | Contact of string
    | Callback of string


type IChatStateStore =
    abstract Save : ChatId -> IChatState -> Task
    abstract Get  : ChatId -> Task<IChatState option>
    
type IChatAdapter<'Update> =
    abstract ExtractUpdate : 'Update -> Option<Chat * ChatUpdate> 
    abstract AdaptView : ChatId -> View seq -> Task



module Hook =
    
    type private ChatContext = {
        ServiceProvider: IServiceProvider
        Chat: Chat
    }
    
    let private context = AsyncLocal<ChatContext>()
    
    let setContext serviceProvider chat =
        context.Value <- {
            ServiceProvider = serviceProvider
            Chat = chat
        }
        
    
    [<CompiledName "UseChat">]
    let useChat() = context.Value.Chat
    
    [<CompiledName "Resolve">]
    let resolve<'t> = context.Value.ServiceProvider.GetRequiredService<'t>()


type BotProcessor<'Update>(sp: IServiceProvider,
                  chatAdapter: IChatAdapter<'Update>,
                  store: IChatStateStore) =
    
    let handle (initState: IChatState) (update: 'Update) = task {
        match chatAdapter.ExtractUpdate update with
        | None -> return ()
        | Some (chat, update) ->
            
        Hook.setContext sp chat
        
        let getState chatId = task {
            try
                return! store.Get(chatId) <?!> initState
            with e ->
                Log.Error(e, "Error while get state")
                return initState
        }
        
        let render = chatAdapter.AdaptView chat.Id
        let save = store.Save chat.Id
        let! state = getState chat.Id 


        let runHandler (handler: ('a -> ValueTask<IChatState>) option) (arg: 'a) = task {
            match handler with
            | Some f -> return! f arg
            | None -> return state
        }
        
        let! newState =
            let view = state.GetView()
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
    
    
    member this.Handle(initState: IChatState) (update: 'Update) = task {
        try do! handle initState update
        with e -> Log.Error(e, "Error in bot states")
    }
