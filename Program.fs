module Environment =
    open System

    type Key = EventPath | AuthToken | LabelToAdd

    [<Literal>]
    let private EVENT_PATH_KEY = "GITHUB_EVENT_PATH"
    [<Literal>]
    let private AUTH_TOKEN_KEY = "REPO_TOKEN"
    [<Literal>]
    let private LABEL_TO_ADD_KEY = "LABEL_TO_ADD"

    let Get key =
        let result = match key with
                     | EventPath -> EVENT_PATH_KEY
                     | AuthToken -> AUTH_TOKEN_KEY
                     | LabelToAdd -> LABEL_TO_ADD_KEY

        let variable = Environment.GetEnvironmentVariable result

        match variable with
        | null -> None
        | _ -> Some variable

module Payload =
    open System.IO
    open FSharp.Data

    type Model = JsonProvider<"./webhook_example.json", RootName="Payload">

    let Open = File.ReadAllText >> Model.Parse

module GitHub =
    open Octokit

    type Client = private Client of GitHubClient

    let Create token =
        let client = GitHubClient(ProductHeaderValue("ActionApi"))
        client.Credentials <- Credentials(token)
        Client client

    let GetIssue (Client client) repoId issueNumber = async {
        return! client.Issue.Get(repoId, issueNumber) |> Async.AwaitTask
    }

    let applyLabel (Client client) (issue : Issue) repoId label =
        let update = issue.ToUpdate()
        update.AddLabel(label)
        client.Issue.Update(repoId, issue.Number, update) |> Async.AwaitTask

type ResultBuilder () =
    member x.Bind(v, f) = Result.bind f v
    member x.Return v = Ok v
    member x.ReturnFrom v = v

let result = ResultBuilder()

type ActionError =
    | MissingEventPathEnvironmentVariable
    | MissingLabelEnvironmentVariable
    | MissingTokenEnvironmentVariable
    | BadCredentials
    | Other of string

let printResult = function
| Ok message -> printfn "%s" message
| Error error -> printfn "%A" error

[<EntryPoint>]
let main argv =
    let res = result {
        let! payloadPath =
            match Environment.Get Environment.Key.EventPath with
            | Some token -> Ok token
            | None -> Error MissingEventPathEnvironmentVariable

        let! token =
            match Environment.Get Environment.Key.AuthToken with
            | Some token -> Ok token
            | None -> Error MissingTokenEnvironmentVariable

        let! labelName =
            match Environment.Get Environment.Key.LabelToAdd with
            | Some token -> Ok token
            | None -> Error MissingLabelEnvironmentVariable

        let payload = Payload.Open payloadPath
        let (repoId, issueNumber) = payload.Repository.Id |> int64, payload.Issue.Number

        let client = GitHub.Create token

        return async {
            let! issue = GitHub.GetIssue client repoId issueNumber
            printfn "Updating issue #%d - %s" issue.Number issue.Title
            let! updatedIssue = GitHub.applyLabel client issue repoId labelName
            return "It worked!"
        } |> Async.RunSynchronously
    }

    printResult res

    0 // return an integer exit code
