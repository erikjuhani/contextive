module Contextive.LanguageServer.Hover

open OmniSharp.Extensions.LanguageServer.Protocol
open OmniSharp.Extensions.LanguageServer.Protocol.Models
open OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities
open Contextive.Core

module private Filtering =
    let private termEqualsToken
        (term: Definitions.Term)
        (tokenAndCandidateTerms: CandidateTerms.TokenAndCandidateTerms)
        =
        fst tokenAndCandidateTerms |> Definitions.Term.equals term

    let private termInCandidates
        (term: Definitions.Term)
        (tokenAndCandidateTerms: CandidateTerms.TokenAndCandidateTerms)
        =
        (snd tokenAndCandidateTerms) |> Seq.exists (Definitions.Term.equals term)

    let private removeLessRelevantTerms
        (tokenAndCandidateTerms: CandidateTerms.TokenAndCandidateTerms seq)
        (terms: Definitions.Term seq)
        =
        let exactTerms =
            tokenAndCandidateTerms
            |> Seq.allPairs terms
            |> Seq.filter (fun (t, tokenAndCandidates) -> termEqualsToken t tokenAndCandidates)

        let relevantTerms =
            exactTerms
            |> Seq.filter (fun (t, tokenAndCandidates) ->
                exactTerms
                |> Seq.except (seq { (t, tokenAndCandidates) })
                |> Seq.exists (fun (_, w) -> termInCandidates t w)
                |> not)
            |> Seq.map fst

        match relevantTerms with
        | Seq.Empty -> terms
        | _ -> relevantTerms

    let findMatchingTerms (tokenAndCandidateTerms: CandidateTerms.TokenAndCandidateTerms seq) =
        Seq.filter (fun t ->
            let candidateMatchesTerm = termEqualsToken t
            tokenAndCandidateTerms |> Seq.exists candidateMatchesTerm)

    let termFilterForCandidateTerms tokenAndCandidateTerms =
        Seq.map (fun (c: Definitions.Context) ->
            Definitions.Context.withTerms
                (c.Terms
                 |> findMatchingTerms tokenAndCandidateTerms
                 |> removeLessRelevantTerms tokenAndCandidateTerms)
                c)

module private TextDocument =

    let getTokenAtPosition (p: HoverParams) (tokenFinder: TextDocument.TokenFinder) =
        match p.TextDocument with
        | null -> None
        | document -> tokenFinder (document.Uri) p.Position

module private Lsp =
    let markupContent content =
        MarkedStringsOrMarkupContent(markupContent = MarkupContent(Kind = MarkupKind.Markdown, Value = content))

    let noHoverResult = null

let private hoverResult (contexts: Definitions.FindResult) =
    match Rendering.renderContexts contexts with
    | None -> Lsp.noHoverResult
    | Some(c) -> Hover(Contents = (c |> Lsp.markupContent))

let private hoverContentForToken
    (uri: string)
    (termFinder: Definitions.Finder)
    (tokensAndCandidateTerms: CandidateTerms.TokenAndCandidateTerms seq)
    =
    async {
        let! findResult = termFinder uri (Filtering.termFilterForCandidateTerms tokensAndCandidateTerms)

        return
            if Seq.isEmpty findResult then
                Lsp.noHoverResult
            else
                hoverResult findResult
    }

let handler
    (termFinder: Definitions.Finder)
    (tokenFinder: TextDocument.TokenFinder)
    (p: HoverParams)
    (_: HoverCapability)
    _
    =
    async {
        return!
            match TextDocument.getTokenAtPosition p tokenFinder with
            | None -> async { return Lsp.noHoverResult }
            | tokenAtPosition ->
                tokenAtPosition
                |> CandidateTerms.tokenToTokenAndCandidateTerms
                |> hoverContentForToken (p.TextDocument.Uri.ToString()) termFinder
    }
    |> Async.StartAsTask

let private registrationOptionsProvider (hc: HoverCapability) (cc: ClientCapabilities) = HoverRegistrationOptions()

let registrationOptions =
    RegistrationOptionsDelegate<HoverRegistrationOptions, HoverCapability>(registrationOptionsProvider)
