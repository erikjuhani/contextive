module Contextly.VsCodeExtension.Tests.Main

open Fable.Mocha
open Fable.Core

// Import mocha explicitly.  Fable.Mocha assumes running via the mocha CLI which imports mocha _implicitly_
[<Import("*", from="mocha")>]
let Mocha: obj = jsNative

Fable.Mocha.Mocha.runTests <| testList "All" [
    Extension.tests
    Completion.tests
    Initialize.tests
] |> ignore

