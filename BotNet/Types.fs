namespace BotNet

open System
open System.Collections.Generic
open System.Threading.Tasks



type ReplyButton = { Text: string; Callback: unit -> ValueTask<IChatState> }

and IChatState =
    abstract GetView : unit -> View seq


and View =
    | TextMessage of string
    | ReplyMessage of string * ReplyButton list
    | TextHandler of (string -> ValueTask<IChatState>)
    | ContactHandler of (string -> ValueTask<IChatState>)
    
    static member Text txt = TextMessage txt
    static member Buttons(message, keyboard: Dictionary<string, Func<ValueTask<IChatState>>>) =
        keyboard
        |> Seq.map ^ fun x -> { Text = x.Key; Callback = fun () -> x.Value.Invoke() }
        |> Seq.toList
        |> fun keyboard -> ReplyMessage(message, keyboard)
        
    static member TextHook (handler: Func<string, ValueTask<IChatState>>) =
        TextHandler handler.Invoke
        
    static member TextHook (handler: Func<string, IChatState>) =
        TextHandler (fun txt -> ValueTask.FromResult(handler.Invoke(txt)))
        
    static member ContactHook (handler: Func<string, ValueTask<IChatState>>) =
        ContactHandler handler.Invoke
        
    static member ContactHook (handler: Func<string, IChatState>) =
        ContactHandler (fun txt -> ValueTask.FromResult(handler.Invoke(txt)))
   
    

module View =
    let text txt = TextMessage txt  
    let buttons message keyboard = ReplyMessage(message, keyboard)
    let textHandler handler = TextHandler handler
    

    let getTextHandler view =
        view
        |> Seq.choose ^ function
            | TextHandler f -> Some f
            | _ -> None
        |> Seq.tryHead
         
    let getButtons view =
        view
        |> Seq.choose ^ function
            | ReplyMessage (txt, buttons) -> Some buttons
            | _ -> None
        |> Seq.collect id
        |> Seq.toArray
        
    
    let getContactHandler view =
        view
        |> Seq.choose ^ function
            | ContactHandler f -> Some f
            | _ -> None
        |> Seq.tryHead
    






            
        