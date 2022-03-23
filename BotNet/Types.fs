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
    
    
    let private findFirst chooser = Seq.choose chooser >> Seq.tryHead 

    let getTextHandler view = view |> findFirst ^ function
        | TextHandler f -> Some f
        | _ -> None
        
        
        
    let getContactHandler view = view |> findFirst ^ function
        | ContactHandler f -> Some f
        | _ -> None
        
         
    let getButtons view =
        view
        |> Seq.choose ^ function
            | ReplyMessage (txt, buttons) -> Some buttons
            | _ -> None
        |> Seq.collect id
        |> Seq.toList
        
    
    
    






            
        