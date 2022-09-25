namespace BotNet

open System
open System.Collections.Generic
open System.Threading.Tasks


[<Struct>] type ChatId = ChatId of string
[<Struct>] type UserId = UserId of string
[<Struct>] type MessageId = MessageId of string

type StringButton<'T>(item: 'T, toString: Func<'T, string>) =
    member this.Value = item
    override this.ToString() = toString.Invoke item

type User = { Id: UserId; UserName: string; FirstName: string; LastName: string }
type Chat = { Id: ChatId; Title: string }


type ReplyButton = { Text: string; Callback: unit -> Task<IChatState> }

and IChatState =
    abstract GetView : unit -> View seq


and View =
    | EmptyView
    | TypingView of TimeSpan 
    | TextView of string
    | ReplyView of string * ReplyButton list * editable:bool
    | TextHandlerView of (string -> Task<IChatState>)
    | ContactHandlerView of string * (string -> Task<IChatState>)
    //| CallBackAnswer of string
    
        
    static member Text(txt) =
        if String.IsNullOrEmpty txt then EmptyView
        else TextView (txt)
        
        
    
    static member private ofKeyboard message editable keys = ReplyView(message, Seq.toList keys, editable)
    
    static member Buttons(message, buttons, editable, handler: Func<_, Task<IChatState>>) =
        buttons
        |> Seq.map ^ fun x -> { Text = x.ToString(); Callback = fun () -> handler.Invoke(x) }
        |> View.ofKeyboard message editable
        
        
    static member Buttons(message, buttons, handler) = View.Buttons(message, buttons, false, handler)
    
    static member Buttons(message, editable, keyboard: Dictionary<string, Func<Task<IChatState>>>) =
        keyboard
        |> Seq.map ^ fun x -> { Text = x.Key; Callback = fun () -> x.Value.Invoke() }
        |> View.ofKeyboard message editable
        
    static member Buttons(message: string, keyboard: Dictionary<string, Func<Task<IChatState>>>) = View.Buttons(message, false, keyboard)
        
        
    static member Buttons(lines: string seq, editable, keyboard: Dictionary<string, Func<Task<IChatState>>>) =
        let message = lines
                      |> Seq.filter (not << String.IsNullOrEmpty)
                      |> String.concat "\n"
        
        keyboard
        |> Seq.map ^ fun x -> { Text = x.Key; Callback = fun () -> x.Value.Invoke() }
        |> View.ofKeyboard message editable
        
        
    static member Buttons(lines: string seq, keyboard: Dictionary<string, Func<Task<IChatState>>>) = View.Buttons(lines, false, keyboard)
        
    
        
    static member TextHandler (handler: Func<string, Task<IChatState>>) =
        TextHandlerView handler.Invoke
    
    static member TextHandler (handler: Func<string, IChatState>) =
        TextHandlerView (fun txt -> Task.FromResult(handler.Invoke(txt)))    
    
    static member ContactHandler (text, handler: Func<string, Task<IChatState>>) =
        ContactHandlerView (text, handler.Invoke)
        
    static member ContactHandler (text, handler: Func<string, IChatState>) =
        ContactHandlerView (text, fun txt -> Task.FromResult(handler.Invoke(txt)))
   
    static member Typing delay = TypingView(delay)
    
//    static member PopupAnswer txt =
//        if String.IsNullOrEmpty(txt)
//        then EmptyView
//        else CallBackAnswer(txt)
    

module View =
    
    let private findFirst chooser = Seq.choose chooser >> Seq.tryHead 

    let getTextHandler view = view |> findFirst ^ function
        | TextHandlerView f -> Some f
        | _ -> None
        
        
        
    let getContactHandler view = view |> findFirst ^ function
        | ContactHandlerView (txt, f) -> Some f
        | _ -> None
        
         
    let getButtons view =
        view
        |> Seq.choose ^ function
            | ReplyView (txt, buttons, editable) -> Some buttons
            | _ -> None
        |> Seq.collect id
        |> Seq.toList
        
    
//    let getCallbackAnswer views =
//        views
//        |> findFirst ^ function
//            | CallBackAnswer txt -> Some txt
//            | _ -> None
//    
//    type Counter = { Count: int }
//    
//    let change delta = chatAction { // chat action (side effect + transition): 'Env -> 'State -> ValueTask<'State> 
//        let! logger = resolve<Logger>() // resolve services
//        let! state = useState()
//        
//        logger.Info("Change counter state")
//        do! Task.Delay(1000)
//        
//        return { state with Count = state.Count + delta }
//    }
//    
//    let counter = chatView { // pure function
//        let! chat = useChat() 
//        let! sender = userSender() // Activity sender
//        let! state = useState() // model state
//        
//        
//        do! render $"Counter {state.Count}" [
//            "Incr", change 1
//            "Decr", change -1
//        ]
//    }
//    
//    