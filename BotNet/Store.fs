namespace BotNet

open System.Collections.Generic
open BotNet

type InMemoryStore() =
    let cache = Dictionary()
    
    interface IChatStateStore with
        member this.Get(chatId) = task {
            match cache.TryGetValue(chatId) with
            | true, item -> return Some item
            | _ -> return None
        }
        
        member this.Save(chatId) (state) = task {
            cache[chatId] <- state
        }
    
