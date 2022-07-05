namespace BotNet

open System
open System.Collections.Generic
open System.Threading.Tasks


[<Struct>] type ChatId = ChatId of string
[<Struct>] type MessageId = MessageId of string


type User = { Id: ChatId; UserName: string; FirstName: string; LastName: string }
type Chat = { Id: ChatId; Title: string }


type ReplyButton = { Text: string; Callback: unit -> ValueTask<IChatState> }

and IChatState =
    abstract GetView : unit -> View seq


and View =
    | EmptyView
    | TypingView of TimeSpan 
    | TextView of string
    | ReplyView of string * ReplyButton list * editable:bool
    | TextHandlerView of (string -> ValueTask<IChatState>)
    | ContactHandlerView of string * (string -> ValueTask<IChatState>)
    
        
    static member Text(txt) =
        if String.IsNullOrEmpty txt then EmptyView
        else TextView (txt)
        
        
    
    static member private ofKeyboard message editable keys = ReplyView(message, Seq.toList keys, editable)
    
    static member Buttons(message, buttons, editable, handler: Func<_, ValueTask<IChatState>>) =
        buttons
        |> Seq.map ^ fun x -> { Text = x.ToString(); Callback = fun () -> handler.Invoke(x) }
        |> View.ofKeyboard message editable
        
    static member Buttons(message, buttons, handler) = View.Buttons(message, buttons, false, handler)
    
    
    static member Buttons(message, editable, keyboard: Dictionary<string, Func<ValueTask<IChatState>>>) =
        keyboard
        |> Seq.map ^ fun x -> { Text = x.Key; Callback = fun () -> x.Value.Invoke() }
        |> View.ofKeyboard message editable
        
    static member Buttons(message: string, keyboard: Dictionary<string, Func<ValueTask<IChatState>>>) = View.Buttons(message, false, keyboard)
        
        
    static member Buttons(lines: string seq, editable, keyboard: Dictionary<string, Func<ValueTask<IChatState>>>) =
        let message = lines
                      |> Seq.filter (not << String.IsNullOrEmpty)
                      |> String.concat "\n"
        
        keyboard
        |> Seq.map ^ fun x -> { Text = x.Key; Callback = fun () -> x.Value.Invoke() }
        |> View.ofKeyboard message editable
        
        
    static member Buttons(lines: string seq, keyboard: Dictionary<string, Func<ValueTask<IChatState>>>) = View.Buttons(lines, false, keyboard)
        
    
        
    static member TextHandler (handler: Func<string, ValueTask<IChatState>>) =
        TextHandlerView handler.Invoke
    
    static member TextHandler (handler: Func<string, IChatState>) =
        TextHandlerView (fun txt -> ValueTask.FromResult(handler.Invoke(txt)))    
    
    static member ContactHandler (text, handler: Func<string, ValueTask<IChatState>>) =
        ContactHandlerView (text, handler.Invoke)
        
    static member ContactHandler (text, handler: Func<string, IChatState>) =
        ContactHandlerView (text, fun txt -> ValueTask.FromResult(handler.Invoke(txt)))
   
    static member Typing delay = TypingView(delay)
    

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
        

    