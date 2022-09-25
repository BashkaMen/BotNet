namespace BotNet

open System
open BotNet
open System.Threading.Tasks
open FSharp.Control.TaskBuilder
open FSharpx



//
//module ChatBot = 
//
//    type ChatActionDsl<'State> =
//        | Pure of 'State
//        | GetState of ('State -> ChatActionDsl<'State>)
//        | ResolveService of (obj -> ChatActionDsl<'State>)
//    
//    
//    module ChatActionDsl =
//        let rec bind f x =
//            match x with
//            | Pure x -> f x
//            | GetState next -> GetState (next >> bind f)
//            | ResolveService next -> ResolveService (next >> bind f)
//                
//                
//        let getState() = GetState Pure
//        let inline resolve<'TService, 'TState>() = ResolveService (box >> Pure)
//        
//    
//        
//    type ChatActionDslBuilder() =
//        member this.Zero() = Pure ()
//        member this.Return x = Pure x
//        member this.Bind(x, f) = ChatActionDsl.bind f x
//    
//    
//    
//    let chatAction = ChatActionDslBuilder()
//    
//    
//    open ChatActionDsl
//    type Store =
//        abstract Save : unit -> Task
//        
//    type Counter = { Count: int } 
//    
//    let change delta = chatAction {
//        let! state = getState()
//        let! store = resolve<Store>()
//        
//        return { state with Count = state.Count + delta }
//    }
//
//    
//    type ChatButtons<'T> =
//        | InlineButtons of {| Text: string; Action: ChatActionDsl<'T> |}
//        | KeyboardButtons of {| Text: string; Action: ChatActionDsl<'T> |}
//        
//    //type InlineBtn<'T> = { Text: string; Action: ChatActionDsl<'T> }
//
//    type ChatViewDsl<'T> =
//        | UseChat of (Chat -> ChatViewDsl<'T>)
//        | UseSender of (User -> ChatViewDsl<'T>)
//        | UseState of ('T -> ChatViewDsl<'T>)
//       // | Render of (string * InlineBtn<'T> list)
//        
