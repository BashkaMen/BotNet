namespace BotNet

open System
open System.Threading
open System.Threading.Tasks
open BotNet
open Microsoft.Extensions.DependencyInjection
open Serilog

type CallBackArgs = { Id: string; Data: string }

type ChatUpdate =
    | Text of string
    | Contact of string
    | Callback of CallBackArgs


type IChatStateStore =
    abstract Save : ChatId -> IChatState -> Task
    abstract Get  : ChatId -> Task<IChatState option>


type IChatAdapter<'Update> =
    abstract ExtractUpdate : 'Update -> Option<Chat * User * ChatUpdate> 
    abstract AdaptView : ChatId -> View seq -> Task
    abstract ResetChat : ChatId -> Task  



module Hook =
    
    type private ChatContext = {
        ServiceProvider: IServiceProvider
        Chat: Chat
        Sender: User
    }
    
    let private context = AsyncLocal<ChatContext>()
    
    let setContext serviceProvider chat sender =
        context.Value <- {
            ServiceProvider = serviceProvider
            Chat = chat
            Sender = sender
        }
        
    
    [<CompiledName "UseChat">]
    let useChat() = context.Value.Chat
    
    [<CompiledName "Resolve">]
    let resolve<'t> = context.Value.ServiceProvider.GetRequiredService<'t>()
    
    [<CompiledName "UseSender">]
    let useSender() = context.Value.Sender



type BotProcessor<'Update>(sp: IServiceProvider,
                  chatAdapter: IChatAdapter<'Update>,
                  store: IChatStateStore) =
    
    let handle (initState: IChatState) (errorState: exn -> Task<IChatState>) (update: 'Update) = task {
        match chatAdapter.ExtractUpdate update with
        | None -> return ()
        | Some (chat, user, update) ->
            
        Log.Information("Received update {Update} on {Chat}", update, chat)
            
        Hook.setContext sp chat user
        
        let getState chatId = task {
            try
                let! state = store.Get(chatId)
                match state with
                | Some state -> return (false, state)
                | None -> return (true, initState)
            with e ->
                let! state = errorState e
                return (true, state)
        }
        
        let render = chatAdapter.AdaptView chat.Id
        let save = store.Save chat.Id
        let! isInit, state = getState chat.Id 

        let runHandler (handler: ('a -> Task<IChatState>) option) (arg: 'a) = task {
            match handler with
            | Some f -> return! f arg
            | None -> return state
        }
        
        let! newState = task {
            try
                let view = state.GetView()
                match update with
                | Text txt -> return! runHandler (View.getTextHandler view) txt
                | Contact txt -> return! runHandler (View.getContactHandler view) txt 
                | Callback callBackArgs ->
                    let findButton index = view |> View.getButtons |> Seq.tryItem index
                    let callBack = callBackArgs.Data |> Int32.tryParse |> Option.bind findButton |> Option.map ^ fun x -> x.Callback
                    return! runHandler callBack ()
                    
            with e -> return! errorState e
        }
            
        
        if not isInit && state = newState then
            Log.Information("State no changed {ChatId}", chat.Id)
            return ()
        else
            Log.Information("Changed state {ChatId} {FromState} -> {ToState}", chat.Id, state, newState)
            do! render ^ newState.GetView()
            do! save newState
    }
    
    
    member this.Handle (initState: IChatState) (errorHandler: Func<exn, Task<IChatState>>) (update: 'Update) = task {
        try do! handle initState errorHandler.Invoke update
        with e -> Log.Error(e, "Error in bot states")
    }
    
    
    member this.SetState (chatId) (state: IChatState) = task {
        do! chatAdapter.ResetChat chatId
        do! chatAdapter.AdaptView chatId (state.GetView())
        do! store.Save chatId state
    }
