namespace BotNet

open System
open System.Collections.Generic
open System.Threading.Tasks
open BotNet
open MongoDB.Driver
open Newtonsoft.Json

type SaveChatState = SaveChatState of (int64 -> IChatState -> Task)
type GetChatState = GetChatState of  (int64 -> Task<IChatState option>)

module Store =
    
    let inMemory (cache: Dictionary<string, IChatState>) =
        let save = SaveChatState ^ fun chatId state -> task {
            cache[$"{chatId}"] <- state
        }
        
        let getState =  GetChatState ^ fun chatId -> task {
            match cache.TryGetValue($"{chatId}") with
            | true, item -> return Some item
            | _ -> return None
        }
        
        save, getState
        
        
    
    type ChatStateWrapper = {
        ChatId: int64
        TypeName: string
        Payload: string
    }

    let withMongo (db: IMongoDatabase) =
        let inline tableOf name = db.GetCollection(name)
        let chatStates : IMongoCollection<ChatStateWrapper> = tableOf("chat_states")
                
        let save = SaveChatState ^ fun chatId state -> task {
            let opt = FindOneAndReplaceOptions<ChatStateWrapper, ChatStateWrapper>(IsUpsert = true)
            
            let item = {
                ChatId = chatId
                TypeName = state.GetType().FullName
                Payload = JsonConvert.SerializeObject(state)
            }
            let! item = chatStates.FindOneAndReplaceAsync<_>((fun x -> x.ChatId = chatId), item, opt)
            ()
        }
        
        let getState = GetChatState ^ fun chatId -> task {
            let! item = chatStates.Find(fun x -> x.ChatId = chatId).FirstOrDefaultAsync()
            
            return Option.ofObject item
                   |> Option.map ^ fun x -> JsonConvert.DeserializeObject(x.Payload, Type.GetType(x.TypeName)) :?> IChatState
        }
        
        save, getState