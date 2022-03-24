namespace BotNet

open System
open System.Collections.Generic
open System.Threading.Tasks

[<Struct>] type ChatId = ChatId of string
type Chat = { Id: ChatId; UserName: string; FirstName: string; LastName: string } 
type ReplyButton = { Text: string; Callback: unit -> ValueTask<IChatState> }

and IChatState =
    abstract GetView : unit -> View seq


and View =
    | EmptyView
    | TypingView of TimeSpan 
    | TextView of string
    | ReplyView of string * ReplyButton list
    | TextHandlerView of (string -> ValueTask<IChatState>)
    | ContactHandlerView of string * (string -> ValueTask<IChatState>)
    
    static member Text txt =
        Option.ofObj txt
        |> Option.map TextView
        <?> EmptyView
        
        
    static member Text(txt, predicate : Func<string, bool>) =
        match predicate.Invoke(txt) with
        | false -> EmptyView
        | true -> View.Text txt
        
    static member Buttons(message, keyboard: Dictionary<string, Func<ValueTask<IChatState>>>) =
        keyboard
        |> Seq.map ^ fun x -> { Text = x.Key; Callback = fun () -> x.Value.Invoke() }
        |> Seq.toList
        |> fun keyboard -> ReplyView(message, keyboard)
        
    static member TextHandler (handler: Func<string, ValueTask<IChatState>>) =
        TextHandlerView handler.Invoke
    
    static member TextHandler (handler: Func<string, IChatState>) =
        TextHandlerView (fun txt -> ValueTask.FromResult(handler.Invoke(txt)))    
    
    static member ContactHandler (text, handler: Func<string, ValueTask<IChatState>>) =
        ContactHandlerView (text, handler.Invoke)
        
    static member ContactHandler (text, handler: Func<string, IChatState>) =
        ContactHandlerView (text, fun txt -> ValueTask.FromResult(handler.Invoke(txt)))
   

module View =
    let text txt = TextView txt  
    let buttons message keyboard = ReplyView(message, keyboard)
    let textHandler handler = TextHandlerView handler
    
    
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
            | ReplyView (txt, buttons) -> Some buttons
            | _ -> None
        |> Seq.collect id
        |> Seq.toList
        
    
    
    






            
        