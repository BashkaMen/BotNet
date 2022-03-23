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
    | TextReceiver of (string -> ValueTask<IChatState>)
    static member Text txt = TextMessage txt
    static member Buttons(message, keyboard: Dictionary<string, Func<ValueTask<IChatState>>>) =
        keyboard
        |> Seq.map ^ fun x -> { Text = x.Key; Callback = fun () -> x.Value.Invoke() }
        |> Seq.toList
        |> fun keyboard -> ReplyMessage(message, keyboard)
        
    static member TextHandler (handler: Func<string, ValueTask<IChatState>>) =
        TextReceiver handler.Invoke
        
    static member TextHandler (handler: Func<string, IChatState>) =
        TextReceiver (fun txt -> ValueTask.FromResult(handler.Invoke(txt)))
    

module View =
    let text txt = TextMessage txt  
    let buttons message keyboard = ReplyMessage(message, keyboard)
    let textHandler handler = TextReceiver handler
    

    let getTextHandler views =
        views
        |> Seq.choose ^ function
            | TextReceiver f -> Some f
            | _ -> None
        |> Seq.tryHead
         
    let getButtons views =
        views
        |> Seq.choose ^ function
            | ReplyMessage (txt, buttons) -> Some buttons
            | _ -> None
        |> Seq.collect id
        |> Seq.toArray
        
    let render (renderer: View -> Task) (views: View seq) : Task = task {
        for view in views do
            do! renderer view
    } 
    






            
        